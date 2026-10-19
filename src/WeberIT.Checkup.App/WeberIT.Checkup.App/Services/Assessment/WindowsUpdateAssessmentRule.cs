
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class WindowsUpdateAssessmentRule :
    ICheckupAssessmentRule
{
    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        var updateInformation =
            checkupSession.WindowsUpdateInformation;

        var findings =
            new List<CheckupFinding>();

        AddSearchScopeFinding(
            updateInformation,
            findings);

        AddPendingUpdateFindings(
            updateInformation,
            findings);

        AddServiceFinding(
            updateInformation,
            findings);

        AddHistoryFinding(
            updateInformation,
            findings);

        return findings;
    }

    private static void AddSearchScopeFinding(
        WindowsUpdateInformation updateInformation,
        List<CheckupFinding> findings)
    {
        if (updateInformation.IsUpdateSearchPerformed
            && updateInformation.IsUpdateSearchSuccessful)
        {
            var searchDateText =
                updateInformation.UpdateSearchDate.HasValue
                    ? "Suchzeitpunkt: "
                      + updateInformation.UpdateSearchDate
                          .Value.ToString("dd.MM.yyyy HH:mm")
                      + " Uhr. "
                    : string.Empty;

            findings.Add(
                new CheckupFinding
                {
                    Code =
                        "system.windows-update.search-successful",

                    Title =
                        "Windows-Updatesuche durchgeführt",

                    Description =
                        "Eine Windows-Updatesuche wurde "
                        + "erfolgreich durchgeführt. "
                        + searchDateText
                        + updateInformation.UpdateSearchDetails,

                    Category =
                        FindingCategory.OperatingSystem,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.SystemCondition,

                    CauseGroup =
                        "system.windows-update.search"
                });

            return;
        }

        if (updateInformation.IsUpdateSearchPerformed)
        {
            findings.Add(
                new CheckupFinding
                {
                    Code =
                        "system.windows-update.search-failed",

                    Title =
                        "Windows-Update-Status nicht ermittelbar",

                    Description =
                        "Die angeforderte Windows-Updatesuche "
                        + "konnte nicht erfolgreich abgeschlossen "
                        + "werden. Deshalb ist keine zuverlässige "
                        + "Aussage über aktuell ausstehende Updates "
                        + "möglich. "
                        + updateInformation.UpdateSearchDetails,

                    Category =
                        FindingCategory.OperatingSystem,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.InformationOnly,

                    CauseGroup =
                        "system.windows-update.search-data-quality"
                });

            return;
        }

        findings.Add(
            new CheckupFinding
            {
                Code =
                    "system.windows-update.local-scope-only",

                Title =
                    "Lokaler Updatebestand möglicherweise nicht aktuell",

                Description =
                    "Der Checkup hat den lokal gespeicherten "
                    + "Windows-Updateverlauf, den Dienstzustand "
                    + "und vorhandene Neustartindikatoren "
                    + "ausgewertet. Es wurde keine erfolgreiche "
                    + "Online-Updatesuche durchgeführt. Deshalb "
                    + "können auf den Microsoft- beziehungsweise "
                    + "konfigurierten Updateservern bereits "
                    + "neuere Updates verfügbar sein. Für eine "
                    + "zuverlässige Prüfung muss die "
                    + "Windows-Updatesuche erfolgreich "
                    + "abgeschlossen werden. "
                    + updateInformation.UpdateSearchDetails,

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Recommendation,

                AssessmentTarget =
                    FindingAssessmentTarget.InformationOnly,

                CauseGroup =
                    "system.windows-update.analysis-scope"
            });
    }

    private static void AddPendingUpdateFindings(
        WindowsUpdateInformation updateInformation,
        List<CheckupFinding> findings)
    {
        // Ohne vollständige erfolgreiche Suche darf
        // keine Aussage über ausstehende Updates
        // in die Systembewertung einfließen.
        if (!updateInformation.IsUpdateSearchPerformed
            || !updateInformation.IsUpdateSearchSuccessful)
        {
            return;
        }

        var detectedUpdates =
            updateInformation.PendingUpdates
            ?? new List<PendingWindowsUpdate>();

        if (detectedUpdates.Count == 0)
        {
            // Eine leere Liste ist ausschließlich nach
            // erfolgreicher Suche ein gültiges Ergebnis.
            // Der Suchbefund dokumentiert diesen Zustand.
            return;
        }

        var regularUpdates =
            detectedUpdates
                .Where(update => !update.IsOptional)
                .ToList();

        var optionalUpdates =
            detectedUpdates
                .Where(update => update.IsOptional)
                .ToList();

        if (regularUpdates.Count > 0)
        {
            var updateTitles =
                BuildUpdateTitleSummary(
                    regularUpdates);

            findings.Add(
                new CheckupFinding
                {
                    Code =
                        "system.windows-update.pending-updates",

                    Title =
                        regularUpdates.Count == 1
                            ? "1 Windows-Update ausstehend"
                            : regularUpdates.Count
                              + " Windows-Updates ausstehend",

                    Description =
                        "Die erfolgreiche Onlineprüfung "
                        + "hat "
                        + regularUpdates.Count
                        + " noch nicht installierte, "
                        + "regulär zugewiesene Windows-Updates "
                        + "erkannt. "
                        + "Diese sollten vor weiteren "
                        + "Wartungsmaßnahmen einzeln geprüft "
                        + "und gegebenenfalls installiert werden."
                        + Environment.NewLine
                        + Environment.NewLine
                        + updateTitles
                        + Environment.NewLine
                        + Environment.NewLine
                        + "Die Updates wurden durch den Checkup "
                        + "nicht automatisch installiert. "
                        + "Die endgültige Dringlichkeit wird "
                        + "erst im Bewertungsmodell V2 anhand "
                        + "zusätzlicher Kriterien bestimmt.",

                    Category =
                        FindingCategory.OperatingSystem,

                    Severity =
                        FindingSeverity.Recommendation,

                    AssessmentTarget =
                        FindingAssessmentTarget.SystemCondition,

                    CauseGroup =
                        "system.windows-update.pending-updates"
                });
        }

        if (optionalUpdates.Count > 0)
        {
            findings.Add(
                new CheckupFinding
                {
                    Code =
                        "system.windows-update.optional-updates",

                    Title =
                        optionalUpdates.Count == 1
                            ? "1 optionales Windows-Update erkannt"
                            : optionalUpdates.Count
                              + " optionale Windows-Updates erkannt",

                    Description =
                        "Unter den regulär zugewiesenen "
                        + "Suchergebnissen wurden "
                        + optionalUpdates.Count
                        + " Updates als optional eingeordnet. "
                        + "Sie werden derzeit nicht als "
                        + "Wartungsmangel bewertet."
                        + Environment.NewLine
                        + Environment.NewLine
                        + BuildUpdateTitleSummary(
                            optionalUpdates)
                        + Environment.NewLine
                        + Environment.NewLine
                        + "Diese Angabe stellt keine "
                        + "vollständige Suche nach allen "
                        + "optionalen Windows-Updates dar.",

                    Category =
                        FindingCategory.OperatingSystem,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.InformationOnly,

                    CauseGroup =
                        "system.windows-update.optional-updates"
                });
        }
    }

    private static string BuildUpdateTitleSummary(
        IReadOnlyCollection<PendingWindowsUpdate> updates)
    {
        const int MaximumDisplayedUpdates = 8;

        var titles =
            updates
                .Take(MaximumDisplayedUpdates)
                .Select(update =>
                    string.IsNullOrWhiteSpace(update.Title)
                        ? "Unbenanntes Windows-Update"
                        : update.Title.Trim())
                .Select(title => "• " + title)
                .ToList();

        var remainingCount =
            updates.Count - titles.Count;

        if (remainingCount > 0)
        {
            titles.Add(
                "• sowie "
                + remainingCount
                + " weitere Updates");
        }

        return string.Join(
            Environment.NewLine,
            titles);
    }

    private static void AddServiceFinding(
        WindowsUpdateInformation updateInformation,
        List<CheckupFinding> findings)
    {
        switch (updateInformation.ServiceState)
        {
            case WindowsUpdateServiceState.Running:
                findings.Add(
                    new CheckupFinding
                    {
                        Code =
                            "system.windows-update.service-running",

                        Title =
                            "Windows-Update-Dienst verfügbar",

                        Description =
                            "Der Windows-Update-Dienst wird "
                            + "derzeit ausgeführt. "
                            + updateInformation.ServiceStatusDetails,

                        Category =
                            FindingCategory.OperatingSystem,

                        Severity =
                            FindingSeverity.Information,

                        AssessmentTarget =
                            FindingAssessmentTarget.SystemCondition,

                        CauseGroup =
                            "system.windows-update.service-configuration"
                    });
                break;

            case WindowsUpdateServiceState.Stopped:
                findings.Add(
                    new CheckupFinding
                    {
                        Code =
                            "system.windows-update.service-stopped",

                        Title =
                            "Windows-Update-Dienst derzeit beendet",

                        Description =
                            "Der Windows-Update-Dienst läuft "
                            + "momentan nicht. Das ist bei einem "
                            + "bedarfsgesteuerten Windows-Dienst "
                            + "nicht automatisch fehlerhaft. "
                            + updateInformation.ServiceStatusDetails,

                        Category =
                            FindingCategory.OperatingSystem,

                        Severity =
                            FindingSeverity.Information,

                        AssessmentTarget =
                            FindingAssessmentTarget.SystemCondition,

                        CauseGroup =
                            "system.windows-update.service-configuration"
                    });
                break;

            case WindowsUpdateServiceState.Disabled:
                findings.Add(
                    new CheckupFinding
                    {
                        Code =
                            "system.windows-update.service-disabled",

                        Title =
                            "Windows-Update-Dienst deaktiviert",

                        Description =
                            "Der Windows-Update-Dienst ist "
                            + "deaktiviert. Dadurch können "
                            + "Windows-Updates möglicherweise "
                            + "nicht gesucht, heruntergeladen "
                            + "oder installiert werden. Die "
                            + "Dienstkonfiguration sollte "
                            + "kontrolliert geprüft werden.",

                        Category =
                            FindingCategory.OperatingSystem,

                        Severity =
                            FindingSeverity.Warning,

                        AssessmentTarget =
                            FindingAssessmentTarget.SystemCondition,

                        CauseGroup =
                            "system.windows-update.service-configuration"
                    });
                break;

            default:
                findings.Add(
                    new CheckupFinding
                    {
                        Code =
                            "system.windows-update.service-not-evaluable",

                        Title =
                            "Windows-Update-Dienst nicht auswertbar",

                        Description =
                            string.IsNullOrWhiteSpace(
                                updateInformation.ServiceStatusDetails)
                                ? "Der Zustand des "
                                  + "Windows-Update-Dienstes "
                                  + "konnte nicht zuverlässig "
                                  + "ermittelt werden."
                                : updateInformation.ServiceStatusDetails,

                        Category =
                            FindingCategory.OperatingSystem,

                        Severity =
                            FindingSeverity.Information,

                        AssessmentTarget =
                            FindingAssessmentTarget.InformationOnly,

                        CauseGroup =
                            "system.windows-update.service-data-quality"
                    });
                break;
        }
    }

    private static void AddHistoryFinding(
        WindowsUpdateInformation updateInformation,
        List<CheckupFinding> findings)
    {
        if (updateInformation
            .LastSuccessfulInstallationDate
            .HasValue)
        {
            var installationDate =
                updateInformation
                    .LastSuccessfulInstallationDate
                    .Value;

            findings.Add(
                new CheckupFinding
                {
                    Code =
                        "system.windows-update.history-evaluated",

                    Title =
                        "Windows-Updateverlauf ausgewertet",

                    Description =
                        "Die letzte im lokalen "
                        + "Windows-Updateverlauf erfolgreich "
                        + "registrierte Installation erfolgte "
                        + $"am {installationDate:dd.MM.yyyy} "
                        + $"um {installationDate:HH:mm} Uhr. "
                        + "Ein älterer Termin bedeutet nicht "
                        + "automatisch, dass Updates fehlen.",

                    Category =
                        FindingCategory.OperatingSystem,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.InformationOnly,

                    CauseGroup =
                        "system.windows-update.history-information"
                });
        }
        else
        {
            findings.Add(
                new CheckupFinding
                {
                    Code =
                        "system.windows-update.history-not-evaluable",

                    Title =
                        "Windows-Updateverlauf nicht auswertbar",

                    Description =
                        "Im geprüften lokalen "
                        + "Windows-Updateverlauf wurde keine "
                        + "eindeutig erfolgreiche Installation "
                        + "gefunden. Das ist nicht automatisch "
                        + "ein Fehler und kann unter anderem "
                        + "durch einen zurückgesetzten oder "
                        + "unvollständigen Verlauf entstehen.",

                    Category =
                        FindingCategory.OperatingSystem,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.InformationOnly,

                    CauseGroup =
                        "system.windows-update.history-data-quality"
                });
        }

        if (updateInformation.RecentFailures.Count == 0)
        {
            findings.Add(
                new CheckupFinding
                {
                    Code =
                        "system.windows-update.no-recent-failures",

                    Title =
                        "Keine aktuellen Updatefehler erkannt",

                    Description =
                        "In den geprüften lokalen "
                        + "Verlaufseinträgen wurden innerhalb "
                        + "der letzten 30 Tage keine weiterhin "
                        + "relevanten fehlgeschlagenen "
                        + "Updateinstallationen erkannt.",

                    Category =
                        FindingCategory.OperatingSystem,

                    Severity =
                        FindingSeverity.Information,

                    AssessmentTarget =
                        FindingAssessmentTarget.SystemCondition,

                    CauseGroup =
                        "system.windows-update.recent-failures"
                });

            return;
        }

        var failureCount =
            updateInformation.RecentFailures.Count;

        var affectedUpdates =
            string.Join(
                "; ",
                updateInformation.RecentFailures
                    .Take(3)
                    .Select(failure =>
                        failure.Title));

        findings.Add(
            new CheckupFinding
            {
                Code =
                    "system.windows-update.recent-failures",

                Title =
                    "Kürzlich fehlgeschlagene "
                    + "Updateinstallation erkannt",

                Description =
                    failureCount == 1
                        ? "Im lokalen Windows-Updateverlauf "
                          + "wurde innerhalb der letzten "
                          + "30 Tage eine fehlgeschlagene "
                          + "Installation erkannt, für die "
                          + "keine spätere erfolgreiche "
                          + "Installation desselben Updates "
                          + "gefunden wurde. Betroffen: "
                          + affectedUpdates
                          + ". Der aktuelle Zustand sollte "
                          + "in Windows Update geprüft werden."
                        : "Im lokalen Windows-Updateverlauf "
                          + $"wurden innerhalb der letzten "
                          + $"30 Tage {failureCount} "
                          + "fehlgeschlagene Installationen "
                          + "erkannt, für die keine spätere "
                          + "erfolgreiche Installation "
                          + "desselben Updates gefunden wurde. "
                          + "Betroffen sind unter anderem: "
                          + affectedUpdates
                          + ". Der aktuelle Zustand sollte "
                          + "in Windows Update geprüft werden.",

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Warning,

                AssessmentTarget =
                    FindingAssessmentTarget.SystemCondition,

                CauseGroup =
                    "system.windows-update.recent-failures"
            });
    }
}
