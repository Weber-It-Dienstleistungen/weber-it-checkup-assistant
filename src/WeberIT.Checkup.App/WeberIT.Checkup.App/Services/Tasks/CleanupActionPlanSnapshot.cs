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
               && IsExecutableCategory(
                   plan.CleanupCategories[0])
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
                "Jede sichere Bereinigungskategorie wird "
                + "einzeln bestätigt und ausgeführt. Der "
                + "dargestellte Mehrfachplan dient daher "
                + "ausschließlich als Vorschau.";
        }

        var category =
            plan.CleanupCategories[0];

        if (!IsExecutableCategory(
                category))
        {
            return
                "Die Kategorie „"
                + category.Title
                + "“ kann bereits geprüft werden, ist jedoch "
                + "noch nicht zur automatischen Ausführung "
                + "freigegeben.";
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

        if (category.Category
            == CleanupCategoryType.BrowserCache)
        {
            return
                "Der Browsercache kann nach vollständiger "
                + "Prüfung und ausdrücklicher Bestätigung "
                + "kontrolliert bereinigt werden. Microsoft "
                + "Edge, Google Chrome und Mozilla Firefox "
                + "müssen vor dem Start vollständig beendet "
                + "sein. Das Tool beendet keine Prozesse selbst.";
        }

        return
            "Dieser Plan enthält genau eine sicher freigegebene "
            + "Bereinigungskategorie und kann nach vollständiger "
            + "Prüfung und ausdrücklicher Bestätigung "
            + "kontrolliert ausgeführt werden.";
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
                "Für eine kontrollierte Ausführung muss genau "
                + "eine Bereinigungskategorie ausgewählt werden.");
        }

        var category =
            plan.CleanupCategories.Single();

        if (!IsExecutableCategory(
                category))
        {
            throw new InvalidOperationException(
                "Die ausgewählte Bereinigungskategorie ist noch "
                + "nicht zur automatischen Ausführung freigegeben.");
        }

        if (plan.RequiresAdministrator)
        {
            throw new InvalidOperationException(
                "Der freigegebene Bereinigungsplan darf keine "
                + "Administratorrechte anfordern.");
        }

        if (plan.MayRequireRestart)
        {
            throw new InvalidOperationException(
                "Der freigegebene Bereinigungsplan darf keinen "
                + "Neustart vorsehen.");
        }
    }

    private static bool IsExecutableCategory(
        CleanupActionCategory category)
    {
        return category.Category
            is CleanupCategoryType.UserTemporaryFiles
            or CleanupCategoryType.DirectXShaderCache
            or CleanupCategoryType.ThumbnailCache
            or CleanupCategoryType.BrowserCache;
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