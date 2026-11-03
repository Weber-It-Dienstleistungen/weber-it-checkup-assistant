
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.30a
///
/// Erstellt geprüfte Kopien kontrollierter
/// Bereinigungspläne.
///
/// Die Windows-Temp-Bereinigung bleibt
/// bis zur ausdrücklichen Produktivfreigabe
/// ausschließlich als Vorschau verfügbar.
///
/// Keine Änderung an der Bereinigungs-Engine.
/// Keine produktiven Löschoperationen.
/// </summary>
internal static class CleanupActionPlanSnapshot
{
    private const string RestoreCapacityTaskCode =
        "task.storage.restore-system-volume-capacity";

    private const string ControlledCleanupTaskCode =
        "task.storage.controlled-cleanup";

    private const string SupportedActionCode =
        "action.cleanup.selected-safe-categories";

    // =============================================
    // VORSCHAU ERSTELLEN
    // =============================================

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

    // =============================================
    // AUSFÜHRBARE KOPIE ERSTELLEN
    // =============================================

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

    // =============================================
    // AUSFÜHRBARKEIT PRÜFEN
    // =============================================

    public static bool CanExecute(
        CheckupTaskActionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(
            plan);

        if (plan.CleanupCategories.Count != 1)
        {
            return false;
        }

        var category =
            plan.CleanupCategories[0];

        /*
         * Windows-Temp wird derzeit bewusst
         * nicht als ausführbar eingestuft.
         *
         * Die neue Handle-Engine existiert,
         * die produktive Sicherheitsfreigabe
         * ist aber noch nicht erteilt.
         *
         * Deshalb darf die Planvorschau
         * keinen ausführbaren Start anbieten.
         */
        return IsExecutableCategory(
                   category)
               && HasMatchingAdministratorRequirement(
                   plan,
                   category)
               && !plan.MayRequireRestart;
    }

    // =============================================
    // AUSFÜHRBARKEIT BESCHREIBEN
    // =============================================

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

        // =========================================
        // WINDOWS-TEMP: SICHERHEITSSPERRE
        // =========================================

        /*
         * Die Vorschau bleibt erlaubt.
         *
         * Die produktive Ausführung wird
         * dagegen erst nach Abschluss der
         * Sicherheitsprüfung freigegeben.
         *
         * Eine zusätzliche UAC-Bestätigung
         * beim Aktionsstart wird ausdrücklich
         * nicht behauptet:
         *
         * Das aktuelle App-Manifest fordert
         * Administratorrechte bereits beim
         * Programmstart an.
         */
        if (category.Category
            == CleanupCategoryType.WindowsTemporaryFiles)
        {
            return
                "Die Windows-Temp-Bereinigung befindet "
                + "sich noch in der Sicherheitsprüfung. "
                + "Der Bereinigungsplan kann eingesehen "
                + "werden, die technische Ausführung "
                + "ist derzeit jedoch gesperrt. "
                + "Es werden keine Dateien gelöscht. "
                + "Die Anwendung fordert gemäß ihrem "
                + "aktuellen Manifest Administratorrechte "
                + "bereits beim Programmstart an.";
        }

        if (!IsExecutableCategory(
                category))
        {
            return
                "Die Kategorie „"
                + category.Title
                + "“ kann bereits geprüft werden, ist jedoch "
                + "noch nicht zur technischen Ausführung "
                + "freigegeben.";
        }

        if (!HasMatchingAdministratorRequirement(
                plan,
                category))
        {
            return
                "Die Rechteanforderung des Bereinigungsplans "
                + "stimmt nicht mit der ausgewählten Kategorie "
                + "überein. Der Plan ist nicht ausführbar.";
        }

        if (plan.MayRequireRestart)
        {
            return
                "Dieser Bereinigungsplan kann einen Neustart "
                + "erfordern und ist in dieser Ausbaustufe "
                + "nicht ausführbar.";
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

    // =============================================
    // BEREINIGUNGSPLAN VALIDIEREN
    // =============================================

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

    // =============================================
    // AUSFÜHRBAREN PLAN VALIDIEREN
    // =============================================

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
                + "nicht zur technischen Ausführung freigegeben.");
        }

        if (!HasMatchingAdministratorRequirement(
                plan,
                category))
        {
            throw new InvalidOperationException(
                "Die Rechteanforderung des bestätigten Plans "
                + "stimmt nicht mit der ausgewählten "
                + "Bereinigungskategorie überein.");
        }

        if (plan.MayRequireRestart)
        {
            throw new InvalidOperationException(
                "Der freigegebene Bereinigungsplan darf keinen "
                + "Neustart vorsehen.");
        }
    }

    // =============================================
    // TECHNISCH AUSFÜHRBARE KATEGORIEN
    // =============================================

    private static bool IsExecutableCategory(
        CleanupActionCategory category)
    {
        /*
         * WindowsTemporaryFiles ist hier
         * bewusst nicht enthalten.
         *
         * Die Kategorie bleibt analysierbar
         * und in der Planvorschau sichtbar.
         *
         * Erst mit der späteren ausdrücklichen
         * Freigabe darf diese Sperre zusammen
         * mit der Executor-Freigabesperre
         * überprüft und geändert werden.
         */
        return category.Category
            is CleanupCategoryType.UserTemporaryFiles
            or CleanupCategoryType.DirectXShaderCache
            or CleanupCategoryType.ThumbnailCache
            or CleanupCategoryType.BrowserCache;
    }

    // =============================================
    // RECHTEANFORDERUNG VERGLEICHEN
    // =============================================

    private static bool HasMatchingAdministratorRequirement(
        CheckupTaskActionPlan plan,
        CleanupActionCategory category)
    {
        var expectedAdministratorRequirement =
            category.Category
            == CleanupCategoryType.WindowsTemporaryFiles;

        return plan.RequiresAdministrator
               == expectedAdministratorRequirement;
    }

    // =============================================
    // KATEGORIE SICHER KOPIEREN
    // =============================================

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
