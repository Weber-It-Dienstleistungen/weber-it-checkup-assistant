
using System.Globalization;
using System.Text;
using System.Windows.Data;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Updates;

namespace WeberIT.Checkup.App.Infrastructure.Converters;

/// <summary>
/// Erstellt die aktuelle, ausschließlich lesende
/// Beschreibung einer Programmupdate-Aufgabe.
///
/// Der originale Aufgabentext bleibt erhalten.
///
/// Die Anzeige kombiniert:
///
/// - Den gespeicherten WinGet-Scan
/// - Die protokollierten Einzelaktionen
/// - Den daraus berechneten Paketstatus
///
/// Ein erfolgreich ausgeführter WinGet-Befehl
/// wird ausdrücklich nicht mit einer durch
/// Nachscan bestätigten Installation verwechselt.
///
/// Dieser Konverter verändert keine Daten.
/// </summary>
public sealed class ProgramUpdateTaskDescriptionConverter :
    IMultiValueConverter
{
    private const string ProgramUpdateTaskCode =
        "task.program-updates.available";

    private const int MaximumDisplayedPackagesPerGroup = 6;

    public object Convert(
        object[] values,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (values.Length == 0
            || values[0] is not CheckupTask task)
        {
            return string.Empty;
        }

        /*
         * Für sämtliche anderen Aufgaben bleibt
         * das bisherige Anzeigeverhalten bestehen.
         */
        if (!string.Equals(
                task.TaskCode,
                ProgramUpdateTaskCode,
                StringComparison.Ordinal))
        {
            return task.Description;
        }

        if (values.Length < 2
            || values[1] is not CheckupSession session
            || !session.TaskList.Tasks.Any(
                existingTask =>
                    existingTask.Id == task.Id))
        {
            return task.Description;
        }

        /*
         * Der dritte Binding-Wert wird später
         * ActionResultCount sein.
         *
         * Er sorgt dafür, dass WPF die Anzeige
         * nach einer gespeicherten technischen
         * Aktion erneut berechnet.
         *
         * Sein Zahlenwert wird nicht als
         * fachliche Entscheidungsgrundlage
         * verwendet.
         */
        var information =
            session.ProgramUpdateInformation;

        if (!IsAnalysisReliable(information))
        {
            return BuildUncertainDescription(
                task.Description);
        }

        IReadOnlyList<ProgramUpdatePackageWorkStatus> statuses;

        try
        {
            statuses =
                ProgramUpdatePackageWorkStatusService
                    .Evaluate(
                        information,
                        session.TaskList);
        }
        catch
        {
            /*
             * Ein Fehler bei der zusätzlichen
             * Darstellung darf niemals die
             * Aufgabenkarte oder den gesamten
             * Checkup unbenutzbar machen.
             */
            return BuildUncertainDescription(
                task.Description);
        }

        if (statuses.Any(status =>
                status.State ==
                ProgramUpdatePackageWorkState.NotAssessable))
        {
            return BuildUncertainDescription(
                task.Description);
        }

        return BuildCurrentDescription(
            information,
            statuses);
    }

    private static bool IsAnalysisReliable(
        ProgramUpdateInformation information)
    {
        return information.IsWingetAvailable == true
               && information.IsAnalysisPerformed
               && information.IsAnalysisSuccessful
               && !information.IsResultTruncated
               && information.AnalysisDate.HasValue
               && information.AvailableUpdateCount
                   == information.AvailableUpdates.Count;
    }

    private static string BuildCurrentDescription(
        ProgramUpdateInformation information,
        IReadOnlyList<ProgramUpdatePackageWorkStatus> statuses)
    {
        var pending =
            statuses
                .Where(status =>
                    status.CanOfferInstallation)
                .OrderBy(status =>
                    status.Update.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        var awaitingVerification =
            statuses
                .Where(status =>
                    status.IsAwaitingVerification)
                .OrderBy(status =>
                    status.Update.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        var needingReview =
            statuses
                .Where(status =>
                    status.NeedsManualReview)
                .OrderBy(status =>
                    status.Update.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        var builder =
            new StringBuilder();

        if (pending.Count > 0)
        {
            AppendGroup(
                builder,
                "NOCH ZU BEARBEITEN",
                pending,
                DescribePendingPackage);
        }
        else
        {
            builder.AppendLine(
                "Noch zur Installation angeboten: keine.");
        }

        if (awaitingVerification.Count > 0)
        {
            AppendGroup(
                builder,
                "TECHNISCH AUSGEFÜHRT – KONTROLLE AUSSTEHEND",
                awaitingVerification,
                DescribeAwaitingVerification);
        }

        if (needingReview.Count > 0)
        {
            AppendGroup(
                builder,
                "MANUELLE KONTROLLE ERFORDERLICH",
                needingReview,
                DescribePackageNeedingReview);
        }

        if (information.AnalysisDate.HasValue)
        {
            builder.AppendLine();

            builder.Append(
                "Grundlage: WinGet-Analyse vom ");

            builder.Append(
                information.AnalysisDate.Value.ToString(
                    "dd.MM.yyyy HH:mm",
                    CultureInfo.GetCultureInfo("de-DE")));

            builder.AppendLine(" Uhr.");
        }

        builder.Append(
            "Die ursprünglichen Scanergebnisse "
            + "bleiben dokumentiert. Technisch "
            + "ausgeführte Updates benötigen "
            + "eine erneute Kontrolle.");

        return builder
            .ToString()
            .Trim();
    }

    private static void AppendGroup(
        StringBuilder builder,
        string heading,
        IReadOnlyList<ProgramUpdatePackageWorkStatus> statuses,
        Func<ProgramUpdatePackageWorkStatus, string>
            formatPackage)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(heading);
        builder.Append(" (");
        builder.Append(statuses.Count);
        builder.AppendLine("):");

        foreach (var status in statuses.Take(
                     MaximumDisplayedPackagesPerGroup))
        {
            builder.Append("• ");
            builder.AppendLine(
                formatPackage(status));
        }

        var remainingCount =
            statuses.Count
            - MaximumDisplayedPackagesPerGroup;

        if (remainingCount > 0)
        {
            builder.Append("• sowie ");
            builder.Append(remainingCount);

            builder.AppendLine(
                remainingCount == 1
                    ? " weiteres Paket"
                    : " weitere Pakete");
        }
    }

    private static string DescribePendingPackage(
        ProgramUpdatePackageWorkStatus status)
    {
        var text =
            BuildPackageVersionText(status.Update);

        return status.State switch
        {
            ProgramUpdatePackageWorkState.Failed =>
                text
                + " – letzter Versuch fehlgeschlagen"
                + FormatExitCode(status.LastExitCode),

            ProgramUpdatePackageWorkState.NotStarted =>
                text
                + " – bisheriger Versuch nicht gestartet",

            _ =>
                text
        };
    }

    private static string DescribeAwaitingVerification(
        ProgramUpdatePackageWorkStatus status)
    {
        return GetPackageName(status.Update)
               + " – WinGet erfolgreich, "
               + "Nachkontrolle ausstehend";
    }

    private static string DescribePackageNeedingReview(
        ProgramUpdatePackageWorkStatus status)
    {
        return GetPackageName(status.Update)
               + " – "
               + status.StatusText;
    }

    private static string BuildPackageVersionText(
        AvailableProgramUpdate update)
    {
        var name =
            GetPackageName(update);

        var installed =
            NormalizeVersion(update.InstalledVersion);

        var available =
            NormalizeVersion(update.AvailableVersion);

        if (installed.Length == 0
            || available.Length == 0)
        {
            return name;
        }

        return name
               + ": "
               + installed
               + " → "
               + available;
    }

    private static string GetPackageName(
        AvailableProgramUpdate update)
    {
        if (!string.IsNullOrWhiteSpace(
                update.Name))
        {
            return update.Name.Trim();
        }

        return string.IsNullOrWhiteSpace(
                update.PackageId)
            ? "Unbekanntes Paket"
            : update.PackageId.Trim();
    }

    private static string NormalizeVersion(
        string? version)
    {
        return version?.Trim()
               ?? string.Empty;
    }

    private static string FormatExitCode(
        int? exitCode)
    {
        return exitCode.HasValue
            ? " (Fehlercode "
              + exitCode.Value.ToString(
                  CultureInfo.InvariantCulture)
              + ")"
            : string.Empty;
    }

    private static string BuildUncertainDescription(
        string? originalDescription)
    {
        var builder =
            new StringBuilder();

        if (!string.IsNullOrWhiteSpace(
                originalDescription))
        {
            builder.AppendLine(
                originalDescription.Trim());

            builder.AppendLine();
        }

        builder.Append(
            "Hinweis: Der aktuelle Bearbeitungsstand "
            + "der einzelnen Programmupdates konnte "
            + "nicht zuverlässig ermittelt werden. "
            + "Bitte die WinGet-Analyse und die "
            + "technischen Aktionsprotokolle prüfen.");

        return builder.ToString();
    }

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException(
            "Die aktuelle WinGet-Arbeitsbeschreibung "
            + "ist ausschließlich lesbar.");
    }
}
