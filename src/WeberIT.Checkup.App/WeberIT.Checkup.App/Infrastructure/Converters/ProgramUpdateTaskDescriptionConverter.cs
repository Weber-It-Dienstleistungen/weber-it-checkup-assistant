
using System.Globalization;
using System.Text;
using System.Windows.Data;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Updates;

namespace WeberIT.Checkup.App.Infrastructure.Converters;

/// <summary>
/// Erstellt eine ausschließlich lesende
/// Beschreibung der Programmupdate-Aufgabe.
///
/// Berücksichtigt:
///
/// - den unveränderten Eingangsbefund
/// - technische Einzelaktionen
/// - zuverlässige WinGet-Zwischenprüfungen
/// - den aktuellen Paketarbeitsstatus
///
/// Der ursprüngliche Aufgabentext, der
/// Eingangsscan und der Systemscore
/// werden niemals verändert.
/// </summary>
public sealed class ProgramUpdateTaskDescriptionConverter :
    IMultiValueConverter
{
    private const string ProgramUpdateTaskCode =
        "task.program-updates.available";

    private const int MaximumDisplayedPackagesPerGroup = 6;

    private static readonly CultureInfo GermanCulture =
        CultureInfo.GetCultureInfo("de-DE");

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

        // Andere Aufgabentypen bleiben unverändert.
        if (!string.Equals(
                task.TaskCode,
                ProgramUpdateTaskCode,
                StringComparison.Ordinal))
        {
            return task.Description;
        }

        if (values.Length < 2
            || values[1] is not CheckupSession session
            || session.TaskList is null
            || !session.TaskList.Tasks.Any(
                existingTask =>
                    existingTask.Id == task.Id))
        {
            return task.Description;
        }

        var originalInformation =
            session.ProgramUpdateInformation;

        if (!IsAnalysisReliable(originalInformation))
        {
            return BuildUncertainDescription(
                task.Description);
        }

        /*
         * Ohne Zwischenprüfung bleibt das
         * bisherige Verhalten vollständig
         * erhalten.
         */
        var visit =
            session.CurrentCustomerCheckupVisit;

        var latestIntermediateScan =
            visit?.ProgramUpdateIntermediateScans
                .Where(scan => scan is not null)
                .OrderByDescending(
                    scan => scan.RecordedAt)
                .FirstOrDefault();

        if (latestIntermediateScan is null)
        {
            return BuildOriginalDescription(
                originalInformation,
                session.TaskList,
                task.Description);
        }

        /*
         * Eine fehlgeschlagene letzte Prüfung
         * darf nicht durch einen älteren
         * erfolgreichen Befund verdeckt werden.
         */
        if (!latestIntermediateScan.IsReliable)
        {
            return BuildUnreliableIntermediateDescription(
                originalInformation,
                latestIntermediateScan);
        }

        try
        {
            var currentInformation =
                latestIntermediateScan
                    .CreateIndependentInformationCopy();

            var statuses =
                ProgramUpdatePackageWorkStatusService
                    .Evaluate(
                        currentInformation,
                        session.TaskList);

            if (statuses.Any(status =>
                    status.State ==
                    ProgramUpdatePackageWorkState
                        .NotAssessable))
            {
                return BuildUncertainDescription(
                    task.Description);
            }

            return BuildIntermediateDescription(
                originalInformation,
                currentInformation,
                latestIntermediateScan,
                statuses,
                visit!);
        }
        catch
        {
            /*
             * Ein Fehler in der ergänzenden
             * Fortschrittsanzeige darf nicht
             * den gesamten Checkup blockieren.
             *
             * Insbesondere darf niemals ein
             * vermeintlich erledigter Zustand
             * aus fehlerhaften Daten entstehen.
             */
            return BuildUncertainDescription(
                task.Description);
        }
    }

    // =============================================
    // ZUVERLÄSSIGKEIT
    // =============================================

    private static bool IsAnalysisReliable(
        ProgramUpdateInformation? information)
    {
        return information is not null
               && information.IsWingetAvailable == true
               && information.IsAnalysisPerformed
               && information.IsAnalysisSuccessful
               && !information.IsResultTruncated
               && information.AnalysisDate.HasValue
               && information.AvailableUpdates is not null
               && information.AvailableUpdateCount >= 0
               && information.AvailableUpdateCount
                   == information.AvailableUpdates.Count;
    }

    // =============================================
    // URSPRÜNGLICHE DARSTELLUNG
    // =============================================

    private static string BuildOriginalDescription(
        ProgramUpdateInformation information,
        CheckupTaskList taskList,
        string originalDescription)
    {
        try
        {
            var statuses =
                ProgramUpdatePackageWorkStatusService
                    .Evaluate(
                        information,
                        taskList);

            if (statuses.Any(status =>
                    status.State ==
                    ProgramUpdatePackageWorkState
                        .NotAssessable))
            {
                return BuildUncertainDescription(
                    originalDescription);
            }

            return BuildCurrentDescription(
                information,
                statuses);
        }
        catch
        {
            return BuildUncertainDescription(
                originalDescription);
        }
    }

    private static string BuildCurrentDescription(
        ProgramUpdateInformation information,
        IReadOnlyList<ProgramUpdatePackageWorkStatus> statuses)
    {
        var builder =
            new StringBuilder();

        AppendWorkStatusGroups(
            builder,
            statuses);

        if (information.AnalysisDate.HasValue)
        {
            builder.AppendLine();

            builder.Append(
                "Grundlage: WinGet-Analyse vom ");

            builder.Append(
                FormatDate(
                    information.AnalysisDate.Value));

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

    // =============================================
    // NEUE ZWISCHENPRÜFUNGSANZEIGE
    // =============================================

    private static string BuildIntermediateDescription(
        ProgramUpdateInformation original,
        ProgramUpdateInformation current,
        ProgramUpdateIntermediateScan scan,
        IReadOnlyList<ProgramUpdatePackageWorkStatus> statuses,
        CustomerCheckupVisit visit)
    {
        var builder =
            new StringBuilder();

        // -----------------------------------------
        // 1. Unveränderter Eingangsbefund
        // -----------------------------------------

        builder.Append("EINGANGSBEFUND: ");

        builder.Append(original.AvailableUpdateCount);

        builder.AppendLine(
            original.AvailableUpdateCount == 1
                ? " Programmupdate erkannt."
                : " Programmupdates erkannt.");

        if (original.AnalysisDate.HasValue)
        {
            builder.Append("Erfasst am ");

            builder.Append(
                FormatDate(
                    original.AnalysisDate.Value));

            builder.AppendLine(" Uhr.");
        }

        // -----------------------------------------
        // 2. Letzte zuverlässige Zwischenprüfung
        // -----------------------------------------

        builder.AppendLine();

        builder.Append(
            "LETZTE ZUVERLÄSSIGE ZWISCHENPRÜFUNG: ");

        builder.Append(
            FormatDate(
                scan.RecordedAt.ToLocalTime()));

        builder.AppendLine(" Uhr.");

        builder.Append(
            "WinGet meldet ");

        builder.Append(
            current.AvailableUpdateCount);

        builder.AppendLine(
            current.AvailableUpdateCount == 1
                ? " verfügbares Programmupdate."
                : " verfügbare Programmupdates.");

        // -----------------------------------------
        // 3. Aktueller Arbeitsstatus
        // -----------------------------------------

        builder.AppendLine();

        AppendWorkStatusGroups(
            builder,
            statuses,
            visit);

        // -----------------------------------------
        // 4. Seit Eingangsscan nicht mehr offen
        // -----------------------------------------

        var noLongerOffered =
            original.AvailableUpdates
                .Where(originalUpdate =>
                    originalUpdate is not null
                    && !current.AvailableUpdates.Any(
                        currentUpdate =>
                            currentUpdate is not null
                            && IsSamePackage(
                                originalUpdate,
                                currentUpdate)))
                .OrderBy(
                    update => update.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(
                    update => update.PackageId,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (noLongerOffered.Count > 0)
        {
            builder.AppendLine();

            builder.Append(
                "IM ZWISCHENBEFUND NICHT MEHR OFFEN (");

            builder.Append(
                noLongerOffered.Count);

            builder.AppendLine("):");

            foreach (var update in noLongerOffered.Take(
                         MaximumDisplayedPackagesPerGroup))
            {
                builder.Append("• ");

                builder.Append(
                    GetPackageName(update));

                builder.AppendLine(
                    " – von WinGet nicht mehr als "
                    + "Update angeboten");
            }

            AppendRemainingCount(
                builder,
                noLongerOffered.Count);
        }

        // -----------------------------------------
        // 5. Neue Pakete im Zwischenbefund
        // -----------------------------------------

        var newlyOffered =
            current.AvailableUpdates
                .Where(currentUpdate =>
                    currentUpdate is not null
                    && !original.AvailableUpdates.Any(
                        originalUpdate =>
                            originalUpdate is not null
                            && IsSamePackage(
                                originalUpdate,
                                currentUpdate)))
                .OrderBy(
                    update => update.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        if (newlyOffered.Count > 0)
        {
            builder.AppendLine();

            builder.Append(
                "SEIT DEM EINGANGSSCAN NEU ERKANNT (");

            builder.Append(
                newlyOffered.Count);

            builder.AppendLine("):");

            foreach (var update in newlyOffered.Take(
                         MaximumDisplayedPackagesPerGroup))
            {
                builder.Append("• ");

                builder.AppendLine(
                    BuildPackageVersionText(
                        update));
            }

            AppendRemainingCount(
                builder,
                newlyOffered.Count);
        }

        // -----------------------------------------
        // 6. Fachliche Einordnung
        // -----------------------------------------

        builder.AppendLine();

        builder.AppendLine();

        builder.Append(
            "Hinweis: Ein im Zwischenbefund "
            + "nicht mehr angebotenes Update "
            + "ist damit nicht automatisch als "
            + "erfolgreich installiert bestätigt. "
            + "Der ursprüngliche Eingangsbefund "
            + "und seine Bewertung bleiben erhalten.");

        return builder
            .ToString()
            .Trim();
    }

    // =============================================
    // FEHLGESCHLAGENE ZWISCHENPRÜFUNG
    // =============================================

    private static string
        BuildUnreliableIntermediateDescription(
            ProgramUpdateInformation original,
            ProgramUpdateIntermediateScan scan)
    {
        var builder =
            new StringBuilder();

        builder.Append(
            "EINGANGSBEFUND: ");

        builder.Append(
            original.AvailableUpdateCount);

        builder.AppendLine(
            original.AvailableUpdateCount == 1
                ? " ursprünglich erkanntes Programmupdate."
                : " ursprünglich erkannte Programmupdates.");

        builder.AppendLine();

        builder.AppendLine(
            "ZWISCHENPRÜFUNG NICHT ZUVERLÄSSIG");

        builder.Append(
            "Letzter Prüfversuch: ");

        builder.Append(
            FormatDate(
                scan.RecordedAt.ToLocalTime()));

        builder.AppendLine(" Uhr.");

        builder.AppendLine();

        builder.Append(
            "Es liegt kein bestätigter aktueller "
            + "Updatebestand vor. Die ursprünglichen "
            + "Paketinformationen bleiben als "
            + "historischer Befund erhalten. "
            + "Vor einer weiteren Installation "
            + "ist eine erfolgreiche erneute "
            + "WinGet-Prüfung erforderlich.");

        return builder.ToString();
    }

    // =============================================
    // ARBEITSSTATUS DARSTELLEN
    // =============================================

    private static void AppendWorkStatusGroups(
        StringBuilder builder,
        IReadOnlyList<ProgramUpdatePackageWorkStatus> statuses,
        CustomerCheckupVisit? visit = null)
    {
        var pending =
            statuses
                .Where(status =>
                    status.CanOfferInstallation
                    && !IsDocumentedException(
                        visit,
                        status.Update))
                .OrderBy(
                    status => status.Update.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        var awaitingVerification =
            statuses
                .Where(status =>
                    status.IsAwaitingVerification)
                .OrderBy(
                    status => status.Update.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        var needingReview =
            statuses
                .Where(status =>
                    status.NeedsManualReview)
                .OrderBy(
                    status => status.Update.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        var documentedExceptions =
            visit is null
                ? new List<ProgramUpdatePackageWorkStatus>()
                : statuses
                    .Where(status =>
                        status.CanOfferInstallation
                        && IsDocumentedException(
                            visit,
                            status.Update))
                    .OrderBy(
                        status => status.Update.Name,
                        StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

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

        if (documentedExceptions.Count > 0)
        {
            AppendGroup(
                builder,
                "DOKUMENTIERTE AUSNAHMEN",
                documentedExceptions,
                status =>
                    GetPackageName(status.Update)
                    + " – begründet zurückgestellt");
        }
    }

    private static bool IsDocumentedException(
        CustomerCheckupVisit? visit,
        AvailableProgramUpdate update)
    {
        if (visit is null)
        {
            return false;
        }

        return visit.ProgramUpdateExceptions.Any(
            approval =>
                approval is not null
                && approval.CheckupVisitId == visit.Id
                && approval.AppliesTo(
                    visit.Id,
                    update));
    }

    // =============================================
    // PAKETE VERGLEICHEN
    // =============================================

    private static bool IsSamePackage(
        AvailableProgramUpdate first,
        AvailableProgramUpdate second)
    {
        /*
         * Für den Vergleich, ob ein Paket
         * im aktuellen Scan noch vorkommt,
         * sind Paketkennung und Quelle
         * entscheidend.
         *
         * Die verfügbare Zielversion kann
         * sich zwischen zwei Prüfungen
         * geändert haben.
         */
        return string.Equals(
                   first.PackageId?.Trim(),
                   second.PackageId?.Trim(),
                   StringComparison.OrdinalIgnoreCase)
               && string.Equals(
                   first.Source?.Trim(),
                   second.Source?.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }

    // =============================================
    // GRUPPENFORMATIERUNG
    // =============================================

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

        AppendRemainingCount(
            builder,
            statuses.Count);
    }

    private static void AppendRemainingCount(
        StringBuilder builder,
        int totalCount)
    {
        var remainingCount =
            totalCount
            - MaximumDisplayedPackagesPerGroup;

        if (remainingCount <= 0)
        {
            return;
        }

        builder.Append("• sowie ");
        builder.Append(remainingCount);

        builder.AppendLine(
            remainingCount == 1
                ? " weiteres Paket"
                : " weitere Pakete");
    }

    // =============================================
    // PAKETBESCHREIBUNGEN
    // =============================================

    private static string DescribePendingPackage(
        ProgramUpdatePackageWorkStatus status)
    {
        var text =
            BuildPackageVersionText(
                status.Update);

        return status.State switch
        {
            ProgramUpdatePackageWorkState.Failed =>
                text
                + " – letzter Versuch fehlgeschlagen"
                + FormatExitCode(
                    status.LastExitCode),

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
            NormalizeVersion(
                update.InstalledVersion);

        var available =
            NormalizeVersion(
                update.AvailableVersion);

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

    private static string FormatDate(
        DateTime value)
    {
        return value.ToString(
            "dd.MM.yyyy HH:mm",
            GermanCulture);
    }

    private static string FormatDate(
        DateTimeOffset value)
    {
        return value.ToString(
            "dd.MM.yyyy HH:mm",
            GermanCulture);
    }

    // =============================================
    // UNKLARER STATUS
    // =============================================

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
