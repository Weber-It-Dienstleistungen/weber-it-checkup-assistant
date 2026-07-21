using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

internal static class CleanupActionPlanSnapshot
{
    private const string RestoreCapacityTaskCode =
        "task.storage.restore-system-volume-capacity";

    private const string ControlledCleanupTaskCode =
        "task.storage.controlled-cleanup";

    private const string SupportedActionCode =
        "action.cleanup.selected-safe-categories";

    private const CleanupCategoryType ExecutableCategory =
        CleanupCategoryType.UserTemporaryFiles;

    public static CheckupTaskActionPlan CreatePreviewCopy(
        CheckupTaskActionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(
            plan);

        ValidateCleanupPlan(
            plan);

        var copy =
            new CheckupTaskActionPlan
            {
                Id =
                    plan.Id,

                TaskId =
                    plan.TaskId,

                TaskCode =
                    plan.TaskCode,

                ActionCode =
                    plan.ActionCode,

                ActionTitle =
                    plan.ActionTitle,

                TargetDescription =
                    plan.TargetDescription,

                ExpectedEffect =
                    plan.ExpectedEffect,

                RiskDescription =
                    plan.RiskDescription,

                RiskLevel =
                    plan.RiskLevel,

                RequiresAdministrator =
                    plan.RequiresAdministrator,

                MayRequireRestart =
                    plan.MayRequireRestart,

                CreatedAt =
                    plan.CreatedAt,

                Commands =
                    new List<
                        CheckupTaskActionCommandPreview>(),

                CleanupCategories =
                    plan.CleanupCategories
                        .Select(
                            CloneCategory)
                        .ToList()
            };

        ValidateCleanupPlan(
            copy);

        return copy;
    }

    public static CheckupTaskActionPlan CreateExecutableCopy(
        CheckupTaskActionPlan plan)
    {
        var copy =
            CreatePreviewCopy(
                plan);

        ValidateExecutablePlan(
            copy);

        return copy;
    }

    public static bool CanExecute(
        CheckupTaskActionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(
            plan);

        return plan.CleanupCategories.Count == 1
               && plan.CleanupCategories[0].Category
                   == ExecutableCategory
               && !plan.RequiresAdministrator
               && !plan.MayRequireRestart;
    }

    public static string GetExecutionAvailabilityText(
        CheckupTaskActionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(
            plan);

        if (plan.CleanupCategories.Count != 1)
        {
            return
                "In dieser Ausbaustufe kann nur eine einzelne "
                + "Bereinigungskategorie ausgeführt werden. "
                + "Der dargestellte Mehrfachplan dient daher "
                + "ausschließlich als Vorschau.";
        }

        var category =
            plan.CleanupCategories[0];

        if (category.Category
            != ExecutableCategory)
        {
            return
                "Die Kategorie „"
                + category.Title
                + "“ kann bereits vollständig geprüft werden, "
                + "ist in dieser Ausbaustufe jedoch noch nicht "
                + "zur automatischen Ausführung freigegeben.";
        }

        if (plan.RequiresAdministrator)
        {
            return
                "Dieser Bereinigungsplan benötigt "
                + "Administratorrechte und ist in dieser "
                + "Ausbaustufe noch nicht ausführbar.";
        }

        if (plan.MayRequireRestart)
        {
            return
                "Dieser Bereinigungsplan kann einen Neustart "
                + "erfordern und ist in dieser Ausbaustufe "
                + "noch nicht ausführbar.";
        }

        return
            "Dieser Plan enthält genau die freigegebene "
            + "Kategorie Benutzertemporärdateien und kann "
            + "nach ausdrücklicher Bestätigung kontrolliert "
            + "ausgeführt werden.";
    }

    private static void ValidateCleanupPlan(
        CheckupTaskActionPlan plan)
    {
        plan.Validate();

        if (!string.Equals(
                plan.TaskCode,
                RestoreCapacityTaskCode,
                StringComparison.Ordinal)
            && !string.Equals(
                plan.TaskCode,
                ControlledCleanupTaskCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Der Aktionsplan gehört nicht zu einer "
                + "freigegebenen Bereinigungsaufgabe.");
        }

        if (!string.Equals(
                plan.ActionCode,
                SupportedActionCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Der Aktionscode gehört nicht zu einer "
                + "freigegebenen Bereinigungsaktion.");
        }

        if (plan.HasCommands)
        {
            throw new InvalidOperationException(
                "Ein Bereinigungsplan darf keine externen "
                + "Befehle enthalten.");
        }

        if (!plan.HasCleanupCategories)
        {
            throw new InvalidOperationException(
                "Der Bereinigungsplan enthält keine "
                + "validierte Bereinigungskategorie.");
        }

        foreach (var category
                 in plan.CleanupCategories)
        {
            category.Validate();
        }
    }

    private static void ValidateExecutablePlan(
        CheckupTaskActionPlan plan)
    {
        if (plan.CleanupCategories.Count != 1)
        {
            throw new InvalidOperationException(
                "In dieser Ausbaustufe darf genau eine "
                + "Bereinigungskategorie ausgeführt werden.");
        }

        var category =
            plan.CleanupCategories.Single();

        if (category.Category
            != ExecutableCategory)
        {
            throw new InvalidOperationException(
                "In dieser Ausbaustufe können ausschließlich "
                + "Benutzertemporärdateien bereinigt werden.");
        }

        if (plan.RequiresAdministrator)
        {
            throw new InvalidOperationException(
                "Der freigegebene Benutzer-Temp-Plan darf "
                + "keine Administratorrechte anfordern.");
        }

        if (plan.MayRequireRestart)
        {
            throw new InvalidOperationException(
                "Der freigegebene Benutzer-Temp-Plan darf "
                + "keinen Neustart vorsehen.");
        }
    }

    private static CleanupActionCategory CloneCategory(
        CleanupActionCategory category)
    {
        ArgumentNullException.ThrowIfNull(
            category);

        category.Validate();

        return new CleanupActionCategory
        {
            Category =
                category.Category,

            Classification =
                category.Classification,

            MeasurementStatus =
                category.MeasurementStatus,

            Title =
                category.Title,

            TargetAreaDescription =
                category.TargetAreaDescription,

            MeasuredSizeBytes =
                category.MeasuredSizeBytes,

            MeasuredFileCount =
                category.MeasuredFileCount
        };
    }
}