using System.Text;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Tasks;

public sealed class CleanupActionPlanBuilder :
    ICleanupActionPlanBuilder
{
    private const string RestoreCapacityTaskCode =
        "task.storage.restore-system-volume-capacity";

    private const string ControlledCleanupTaskCode =
        "task.storage.controlled-cleanup";

    private const string SupportedActionCode =
        "action.cleanup.selected-safe-categories";

    public IReadOnlyList<CleanupActionCategory>
        GetSelectableCategories(
            CleanupPotentialInformation cleanupInformation)
    {
        ArgumentNullException.ThrowIfNull(
            cleanupInformation);

        ValidateAnalysis(
            cleanupInformation);

        return cleanupInformation
            .Categories
            .OfType<CleanupCategoryResult>()
            .Where(
                IsSelectableStoredCategory)
            .GroupBy(
                category =>
                    category.Category)
            .Where(
                group =>
                    group.Count() == 1)
            .Select(
                group =>
                    CreatePlanCategory(
                        group.Single()))
            .OrderBy(
                category =>
                    category.Title,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public CheckupTaskActionPlan Build(
        CheckupTask task,
        CleanupPotentialInformation cleanupInformation,
        IReadOnlyCollection<CleanupActionSelection>
            selections)
    {
        ArgumentNullException.ThrowIfNull(
            task);

        ArgumentNullException.ThrowIfNull(
            cleanupInformation);

        ArgumentNullException.ThrowIfNull(
            selections);

        ValidateTask(
            task);

        var selectableCategories =
            GetSelectableCategories(
                cleanupInformation);

        var selectedCategories =
            ResolveSelectedCategories(
                selectableCategories,
                selections);

        var actionDefinition =
            CheckupTaskActionCatalog.GetDefinition(
                task.TaskCode);

        ValidateActionDefinition(
            actionDefinition);

        var plan =
            new CheckupTaskActionPlan
            {
                TaskId =
                    task.Id,

                TaskCode =
                    task.TaskCode,

                ActionCode =
                    actionDefinition.ActionCode,

                ActionTitle =
                    actionDefinition.ActionTitle,

                TargetDescription =
                    BuildTargetDescription(
                        cleanupInformation.TargetVolume,
                        selectedCategories),

                ExpectedEffect =
                    BuildExpectedEffect(
                        selectedCategories),

                RiskDescription =
                    actionDefinition.RiskDescription,

                RiskLevel =
                    actionDefinition.RiskLevel,

                RequiresAdministrator =
                    selectedCategories.Any(
                        RequiresAdministrator),

                MayRequireRestart =
                    actionDefinition.MayRequireRestart,

                CleanupCategories =
                    selectedCategories
            };

        plan.Validate();

        return plan;
    }

    private static void ValidateTask(
        CheckupTask task)
    {
        if (!string.Equals(
                task.TaskCode,
                RestoreCapacityTaskCode,
                StringComparison.Ordinal)
            && !string.Equals(
                task.TaskCode,
                ControlledCleanupTaskCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Die ausgewählte Aufgabe ist keine "
                + "freigegebene Bereinigungsaufgabe.");
        }

        if (task.Status
            != CheckupTaskStatus.Open)
        {
            throw new InvalidOperationException(
                "Eine technische Aktion darf nur für "
                + "eine offene Aufgabe vorbereitet werden.");
        }
    }

    private static void ValidateAnalysis(
        CleanupPotentialInformation information)
    {
        if (!information.AnalysisDate.HasValue)
        {
            throw new InvalidOperationException(
                "Im zugehörigen Checkup ist kein "
                + "Bereinigungsanalysezeitpunkt gespeichert.");
        }

        if (information.AnalysisStatus
            == CleanupMeasurementStatus.NotAnalyzed)
        {
            throw new InvalidOperationException(
                "Im zugehörigen Checkup wurde keine "
                + "Bereinigungsanalyse durchgeführt.");
        }

        if (string.IsNullOrWhiteSpace(
                information.TargetVolume))
        {
            throw new InvalidOperationException(
                "Das bei der Bereinigungsanalyse "
                + "untersuchte Systemvolume ist nicht gespeichert.");
        }

        if (information.Categories is null
            || information.Categories.Count == 0)
        {
            throw new InvalidOperationException(
                "Im zugehörigen Checkup sind keine "
                + "gespeicherten Bereinigungskategorien enthalten.");
        }
    }

    private static bool IsSelectableStoredCategory(
        CleanupCategoryResult category)
    {
        return Enum.IsDefined(
                   typeof(CleanupCategoryType),
                   category.Category)
               && CleanupActionCategory.IsSupportedCategory(
                   category.Category)
               && category.Classification
                   == CleanupCategoryClassification.SafePotential
               && HasSelectableMeasurement(
                   category)
               && category.SizeBytes.HasValue
               && category.FileCount.HasValue
               && category.FileCount.Value >= 0;
    }

    private static bool HasSelectableMeasurement(
        CleanupCategoryResult category)
    {
        if (category.MeasurementStatus
            == CleanupMeasurementStatus.Measured)
        {
            return true;
        }

        return category.Category
                   == CleanupCategoryType.UserTemporaryFiles
               && category.MeasurementStatus
                   == CleanupMeasurementStatus.PartiallyMeasured;
    }

    private static List<CleanupActionCategory>
        ResolveSelectedCategories(
            IReadOnlyList<CleanupActionCategory>
                selectableCategories,
            IReadOnlyCollection<CleanupActionSelection>
                selections)
    {
        if (selections.Count == 0)
        {
            throw new InvalidOperationException(
                "Es wurde keine Bereinigungskategorie "
                + "ausgewählt.");
        }

        var selectedCategoryTypes =
            selections
                .Select(
                    NormalizeSelection)
                .ToList();

        var duplicateSelection =
            selectedCategoryTypes
                .GroupBy(
                    category =>
                        category)
                .FirstOrDefault(
                    group =>
                        group.Count() > 1);

        if (duplicateSelection is not null)
        {
            throw new InvalidOperationException(
                "Mindestens eine Bereinigungskategorie "
                + "wurde mehrfach ausgewählt.");
        }

        var selectedCategories =
            new List<CleanupActionCategory>();

        foreach (var selectedCategoryType
                 in selectedCategoryTypes)
        {
            var matchingCategory =
                selectableCategories
                    .SingleOrDefault(
                        category =>
                            category.Category
                            == selectedCategoryType);

            if (matchingCategory is null)
            {
                throw new InvalidOperationException(
                    $"Die Kategorie "
                    + $"„{GetCategoryTitle(selectedCategoryType)}“ "
                    + "ist im zugehörigen Checkup nicht als "
                    + "sichere und ausreichend gemessene "
                    + "Kategorie eindeutig freigegeben.");
            }

            matchingCategory.Validate();

            selectedCategories.Add(
                matchingCategory);
        }

        return selectedCategories
            .OrderBy(
                category =>
                    category.Title,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static CleanupCategoryType NormalizeSelection(
        CleanupActionSelection selection)
    {
        ArgumentNullException.ThrowIfNull(
            selection);

        if (!selection.Category.HasValue)
        {
            throw new InvalidOperationException(
                "Eine Bereinigungsauswahl enthält "
                + "keinen eindeutigen Kategoriecode.");
        }

        var category =
            selection.Category.Value;

        if (!Enum.IsDefined(
                typeof(CleanupCategoryType),
                category))
        {
            throw new InvalidOperationException(
                "Eine Bereinigungsauswahl enthält "
                + "eine unbekannte Kategorie.");
        }

        if (!CleanupActionCategory.IsSupportedCategory(
                category))
        {
            throw new InvalidOperationException(
                $"Die Kategorie "
                + $"„{GetCategoryTitle(category)}“ "
                + "ist für eine automatische "
                + "Bereinigung nicht freigegeben.");
        }

        return category;
    }

    private static CleanupActionCategory CreatePlanCategory(
        CleanupCategoryResult storedCategory)
    {
        if (!storedCategory.SizeBytes.HasValue
            || !storedCategory.FileCount.HasValue)
        {
            throw new InvalidOperationException(
                "Für eine Bereinigungskategorie fehlt "
                + "das gespeicherte Messergebnis.");
        }

        var category =
            new CleanupActionCategory
            {
                Category =
                    storedCategory.Category,

                Classification =
                    storedCategory.Classification,

                MeasurementStatus =
                    storedCategory.MeasurementStatus,

                Title =
                    GetCategoryTitle(
                        storedCategory.Category),

                TargetAreaDescription =
                    GetTargetAreaDescription(
                        storedCategory.Category),

                MeasuredSizeBytes =
                    storedCategory.SizeBytes.Value,

                MeasuredFileCount =
                    storedCategory.FileCount.Value
            };

        category.Validate();

        return category;
    }

    private static void ValidateActionDefinition(
        CheckupTaskActionDefinition definition)
    {
        if (!definition.IsExecutable
            || !string.Equals(
                definition.ActionCode,
                SupportedActionCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Für diese Aufgabe ist keine kontrollierte "
                + "Bereinigungsaktion freigegeben.");
        }
    }

    private static bool RequiresAdministrator(
        CleanupActionCategory category)
    {
        if (!category.Category.HasValue)
        {
            throw new InvalidOperationException(
                "Die Rechteanforderung einer unbekannten "
                + "Bereinigungskategorie kann nicht "
                + "sicher bestimmt werden.");
        }

        return category.Category.Value switch
        {
            CleanupCategoryType.UserTemporaryFiles =>
                false,

            CleanupCategoryType.WindowsTemporaryFiles =>
                true,

            CleanupCategoryType.DirectXShaderCache =>
                false,

            CleanupCategoryType.ThumbnailCache =>
                false,

            CleanupCategoryType.BrowserCache =>
                false,

            _ =>
                throw new InvalidOperationException(
                    "Für die Bereinigungskategorie ist "
                    + "keine eindeutige Rechteanforderung "
                    + "definiert.")
        };
    }

    private static string BuildTargetDescription(
        string targetVolume,
        IReadOnlyCollection<CleanupActionCategory>
            categories)
    {
        var builder =
            new StringBuilder();

        builder.Append(
            categories.Count == 1
                ? "Eine ausgewählte und sicher freigegebene "
                  + "Bereinigungskategorie"
                : $"{categories.Count} ausgewählte und sicher "
                  + "freigegebene Bereinigungskategorien");

        builder.Append(
            " auf dem im Checkup gespeicherten Systemvolume ");

        builder.Append(
            targetVolume.Trim());

        builder.Append(':');

        foreach (var category in categories)
        {
            builder.AppendLine();
            builder.Append("• ");
            builder.Append(category.Title);
            builder.Append(" – ");
            builder.Append(category.MeasurementSummaryText);
            builder.AppendLine();
            builder.Append("  Zielbereich: ");
            builder.Append(category.TargetAreaDescription);
        }

        return builder.ToString();
    }

    private static string BuildExpectedEffect(
        IReadOnlyCollection<CleanupActionCategory>
            categories)
    {
        var expectedEffect =
            categories.Count == 1
                ? "Vorgesehen ist ausschließlich die kontrollierte "
                  + "Bereinigung der einzeln ausgewählten Kategorie. "
                  + "Andere Kategorien und nicht freigegebene Bereiche "
                  + "sind nicht Bestandteil des Plans."
                : "Vorgesehen ist ausschließlich die getrennte und "
                  + "kontrollierte Bereinigung der einzeln ausgewählten "
                  + "Kategorien. Andere Kategorien und nicht "
                  + "freigegebene Bereiche sind nicht Bestandteil "
                  + "des Plans.";

        if (!categories.Any(
                category =>
                    category.MeasurementStatus
                    == CleanupMeasurementStatus.PartiallyMeasured))
        {
            return expectedEffect;
        }

        return expectedEffect
               + " Die gespeicherte Messung der "
               + "Benutzertemporärdateien ist unvollständig. "
               + "Die angezeigte Größe und Dateianzahl sind daher "
               + "Mindestwerte. Gesperrte oder nicht zugängliche "
               + "Einträge werden nicht gelöscht und im technischen "
               + "Ergebnis berücksichtigt.";
    }

    private static string GetCategoryTitle(
        CleanupCategoryType category)
    {
        return category switch
        {
            CleanupCategoryType.UserTemporaryFiles =>
                "Benutzertemporärdateien",

            CleanupCategoryType.WindowsTemporaryFiles =>
                "Windows-Temp",

            CleanupCategoryType.DirectXShaderCache =>
                "DirectX-Shadercache",

            CleanupCategoryType.ThumbnailCache =>
                "Vorschaubildcache",

            CleanupCategoryType.BrowserCache =>
                "Browsercache",

            _ =>
                "Nicht freigegebene Kategorie"
        };
    }

    private static string GetTargetAreaDescription(
        CleanupCategoryType category)
    {
        return category switch
        {
            CleanupCategoryType.UserTemporaryFiles =>
                "Inhalte des Temp-Ordners des aktuell "
                + "angemeldeten Benutzers einschließlich "
                + "seiner Unterordner. Der Temp-Stammordner "
                + "selbst bleibt bestehen.",

            CleanupCategoryType.WindowsTemporaryFiles =>
                "Inhalte des Windows-Temp-Ordners "
                + "einschließlich seiner Unterordner. "
                + "Der Windows-Temp-Stammordner selbst "
                + "bleibt bestehen.",

            CleanupCategoryType.DirectXShaderCache =>
                "Inhalte des DirectX-Shadercacheordners "
                + "D3DSCache des aktuell angemeldeten "
                + "Benutzers. Der Cacheordner selbst "
                + "bleibt bestehen.",

            CleanupCategoryType.ThumbnailCache =>
                "Ausschließlich Dateien mit dem Muster "
                + "thumbcache_*.db im Windows-Explorer-"
                + "Cacheordner des aktuell angemeldeten "
                + "Benutzers. Unterordner sind nicht umfasst.",

            CleanupCategoryType.BrowserCache =>
                "Ausschließlich bekannte Cache-, Code-Cache-, "
                + "GPU-Cache-, Shader-Cache-, cache2- und "
                + "startupCache-Bereiche unterstützter Profile "
                + "von Microsoft Edge, Google Chrome und "
                + "Mozilla Firefox. Verlauf, Cookies, "
                + "Anmeldedaten und sonstige Browserinhalte "
                + "sind nicht umfasst.",

            _ =>
                throw new InvalidOperationException(
                    "Für die Bereinigungskategorie ist "
                    + "kein freigegebener Zielbereich definiert.")
        };
    }
}