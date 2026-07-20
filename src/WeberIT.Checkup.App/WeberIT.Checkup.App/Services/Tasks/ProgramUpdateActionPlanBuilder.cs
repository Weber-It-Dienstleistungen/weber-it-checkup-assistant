using System.Text;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Tasks;

public sealed class ProgramUpdateActionPlanBuilder :
    IProgramUpdateActionPlanBuilder
{
    private const string SupportedTaskCode =
        "task.program-updates.available";

    private const string SupportedActionCode =
        "action.program-updates.selected-upgrades";

    public CheckupTaskActionPlan Build(
        CheckupTask task,
        ProgramUpdateInformation programUpdateInformation,
        IReadOnlyCollection<ProgramUpdateActionSelection>
            selections)
    {
        ArgumentNullException.ThrowIfNull(
            task);

        ArgumentNullException.ThrowIfNull(
            programUpdateInformation);

        ArgumentNullException.ThrowIfNull(
            selections);

        ValidateTask(
            task);

        ValidateAnalysis(
            programUpdateInformation);

        var selectedUpdates =
            ResolveSelectedUpdates(
                programUpdateInformation,
                selections);

        var actionDefinition =
            CheckupTaskActionCatalog.GetDefinition(
                task.TaskCode);

        if (!actionDefinition.IsExecutable
            || !string.Equals(
                actionDefinition.ActionCode,
                SupportedActionCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Für diese Aufgabe ist keine kontrollierte "
                + "WinGet-Aktion freigegeben.");
        }

        var commands =
            selectedUpdates
                .Select(
                    CreateCommandPreview)
                .ToList();

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
                        selectedUpdates),

                ExpectedEffect =
                    BuildExpectedEffect(
                        selectedUpdates),

                RiskDescription =
                    actionDefinition.RiskDescription,

                RiskLevel =
                    actionDefinition.RiskLevel,

                RequiresAdministrator =
                    false,

                MayRequireRestart =
                    actionDefinition.MayRequireRestart,

                Commands =
                    commands
            };

        plan.Validate();

        return plan;
    }

    private static void ValidateTask(
        CheckupTask task)
    {
        if (!string.Equals(
                task.TaskCode,
                SupportedTaskCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Die ausgewählte Aufgabe ist keine "
                + "Programmupdateaufgabe.");
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
        ProgramUpdateInformation information)
    {
        if (information.IsWingetAvailable
            != true)
        {
            throw new InvalidOperationException(
                "WinGet war beim zugehörigen Scan "
                + "nicht verfügbar.");
        }

        if (!information.IsAnalysisPerformed)
        {
            throw new InvalidOperationException(
                "Im zugehörigen Checkup wurde keine "
                + "WinGet-Analyse durchgeführt.");
        }

        if (!information.IsAnalysisSuccessful)
        {
            throw new InvalidOperationException(
                "Die WinGet-Analyse des zugehörigen "
                + "Checkups war nicht erfolgreich.");
        }

        if (information.AvailableUpdates.Count == 0)
        {
            throw new InvalidOperationException(
                "Im zugehörigen Checkup sind keine "
                + "auswählbaren Programmupdates enthalten.");
        }
    }

    private static List<AvailableProgramUpdate>
        ResolveSelectedUpdates(
            ProgramUpdateInformation information,
            IReadOnlyCollection<ProgramUpdateActionSelection>
                selections)
    {
        if (selections.Count == 0)
        {
            throw new InvalidOperationException(
                "Es wurde kein Programmupdate ausgewählt.");
        }

        var normalizedSelections =
            selections
                .Select(
                    selection =>
                        NormalizeSelection(
                            selection))
                .ToList();

        var duplicateSelection =
            normalizedSelections
                .GroupBy(
                    selection =>
                        BuildIdentityKey(
                            selection.PackageId,
                            selection.Source),
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(
                    group =>
                        group.Count() > 1);

        if (duplicateSelection is not null)
        {
            throw new InvalidOperationException(
                "Mindestens ein Programmupdate wurde "
                + "mehrfach ausgewählt.");
        }

        var selectedUpdates =
            new List<AvailableProgramUpdate>();

        foreach (var selection in normalizedSelections)
        {
            var matchingUpdates =
                information.AvailableUpdates
                    .Where(
                        update =>
                            string.Equals(
                                update.PackageId,
                                selection.PackageId,
                                StringComparison.OrdinalIgnoreCase)
                            && string.Equals(
                                update.Source,
                                selection.Source,
                                StringComparison.OrdinalIgnoreCase))
                    .ToList();

            if (matchingUpdates.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Das ausgewählte Paket "
                    + $"\"{selection.PackageId}\" ist im "
                    + "zugehörigen Checkup nicht enthalten.");
            }

            if (matchingUpdates.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Das Paket \"{selection.PackageId}\" "
                    + "ist im zugehörigen Checkup nicht "
                    + "eindeutig enthalten.");
            }

            var matchingUpdate =
                matchingUpdates[0];

            ValidateStoredUpdate(
                matchingUpdate);

            selectedUpdates.Add(
                matchingUpdate);
        }

        return selectedUpdates
            .OrderBy(
                update =>
                    update.Name,
                StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(
                update =>
                    update.PackageId,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ProgramUpdateActionSelection
        NormalizeSelection(
            ProgramUpdateActionSelection selection)
    {
        ArgumentNullException.ThrowIfNull(
            selection);

        var packageId =
            selection.PackageId?.Trim()
            ?? string.Empty;

        var source =
            selection.Source?.Trim()
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(
                packageId))
        {
            throw new InvalidOperationException(
                "Eine Paketauswahl enthält keine "
                + "gültige Paket-ID.");
        }

        if (string.IsNullOrWhiteSpace(
                source))
        {
            throw new InvalidOperationException(
                "Eine Paketauswahl enthält keine "
                + "gültige WinGet-Quelle.");
        }

        if (!string.Equals(
                source,
                "winget",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Die Paketquelle \"{source}\" ist für "
                + "diese Aktion nicht freigegeben.");
        }

        return new ProgramUpdateActionSelection
        {
            PackageId =
                packageId,

            Source =
                source
        };
    }

    private static void ValidateStoredUpdate(
        AvailableProgramUpdate update)
    {
        if (string.IsNullOrWhiteSpace(
                update.PackageId))
        {
            throw new InvalidOperationException(
                "Ein gespeichertes Programmupdate besitzt "
                + "keine eindeutige Paket-ID.");
        }

        if (string.IsNullOrWhiteSpace(
                update.Name))
        {
            throw new InvalidOperationException(
                $"Für das Paket \"{update.PackageId}\" "
                + "ist kein Anzeigename gespeichert.");
        }

        if (string.IsNullOrWhiteSpace(
                update.AvailableVersion))
        {
            throw new InvalidOperationException(
                $"Für das Paket \"{update.PackageId}\" "
                + "ist keine Zielversion gespeichert.");
        }

        if (!string.Equals(
                update.Source,
                "winget",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Die gespeicherte Quelle des Pakets "
                + $"\"{update.PackageId}\" ist nicht freigegeben.");
        }
    }

    private static CheckupTaskActionCommandPreview
        CreateCommandPreview(
            AvailableProgramUpdate update)
    {
        return new CheckupTaskActionCommandPreview
        {
            FileName =
                "winget.exe",

            Arguments =
                new List<string>
                {
                    "upgrade",
                    "--id",
                    update.PackageId,
                    "--version",
                    update.AvailableVersion,
                    "--source",
                    update.Source,
                    "--exact",
                    "--disable-interactivity",
                    "--accept-source-agreements",
                    "--accept-package-agreements"
                },

            RequiresAdministrator =
                false
        };
    }

    private static string BuildTargetDescription(
        IReadOnlyCollection<AvailableProgramUpdate> updates)
    {
        var builder =
            new StringBuilder();

        builder.Append(
            updates.Count == 1
                ? "Ein ausgewähltes Programmupdate:"
                : $"{updates.Count} ausgewählte Programmupdates:");

        foreach (var update in updates)
        {
            builder.AppendLine();
            builder.Append("• ");
            builder.Append(update.Name);
            builder.Append(" (");
            builder.Append(update.PackageId);
            builder.Append(')');

            if (!string.IsNullOrWhiteSpace(
                    update.InstalledVersion))
            {
                builder.Append(": ");
                builder.Append(update.InstalledVersion);
            }

            builder.Append(" → ");
            builder.Append(update.AvailableVersion);
        }

        return builder.ToString();
    }

    private static string BuildExpectedEffect(
        IReadOnlyCollection<AvailableProgramUpdate> updates)
    {
        return updates.Count == 1
            ? "WinGet soll ausschließlich das ausgewählte "
              + "Paket auf die im Checkup erkannte verfügbare "
              + "Version aktualisieren."
            : "WinGet soll ausschließlich die einzeln "
              + "ausgewählten Pakete auf die im Checkup "
              + "erkannten verfügbaren Versionen aktualisieren.";
    }

    private static string BuildIdentityKey(
        string packageId,
        string source)
    {
        return
            packageId.Trim()
            + "\u001F"
            + source.Trim();
    }
}