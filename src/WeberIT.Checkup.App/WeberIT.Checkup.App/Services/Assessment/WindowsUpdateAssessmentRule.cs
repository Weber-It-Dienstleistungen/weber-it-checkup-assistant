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

        AddServiceFinding(
            updateInformation,
            findings);

        AddRestartFinding(
            updateInformation,
            findings);

        AddHistoryFinding(
            updateInformation,
            findings);

        return findings;
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
                        Title =
                            "Windows-Update-Dienst verfügbar",

                        Description =
                            "Der Windows-Update-Dienst wird "
                            + "derzeit ausgeführt. "
                            + updateInformation
                                .ServiceStatusDetails,

                        Category =
                            FindingCategory.OperatingSystem,

                        Severity =
                            FindingSeverity.Information
                    });
                break;

            case WindowsUpdateServiceState.Stopped:
                findings.Add(
                    new CheckupFinding
                    {
                        Title =
                            "Windows-Update-Dienst derzeit beendet",

                        Description =
                            "Der Windows-Update-Dienst läuft "
                            + "momentan nicht. Das ist bei einem "
                            + "bedarfsgesteuerten Windows-Dienst "
                            + "nicht automatisch fehlerhaft. "
                            + updateInformation
                                .ServiceStatusDetails,

                        Category =
                            FindingCategory.OperatingSystem,

                        Severity =
                            FindingSeverity.Information
                    });
                break;

            case WindowsUpdateServiceState.Disabled:
                findings.Add(
                    new CheckupFinding
                    {
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
                            FindingSeverity.Warning
                    });
                break;

            default:
                findings.Add(
                    new CheckupFinding
                    {
                        Title =
                            "Windows-Update-Dienst nicht auswertbar",

                        Description =
                            string.IsNullOrWhiteSpace(
                                updateInformation
                                    .ServiceStatusDetails)
                                ? "Der Zustand des "
                                  + "Windows-Update-Dienstes "
                                  + "konnte nicht zuverlässig "
                                  + "ermittelt werden."
                                : updateInformation
                                    .ServiceStatusDetails,

                        Category =
                            FindingCategory.OperatingSystem,

                        Severity =
                            FindingSeverity.Information
                    });
                break;
        }
    }

    private static void AddRestartFinding(
        WindowsUpdateInformation updateInformation,
        List<CheckupFinding> findings)
    {
        if (updateInformation.IsRestartRequired == true)
        {
            var reasons =
                updateInformation.RestartReasons.Count > 0
                    ? string.Join(
                        ", ",
                        updateInformation.RestartReasons)
                    : "Windows-Komponenten";

            findings.Add(
                new CheckupFinding
                {
                    Title =
                        "Windows-Neustart erforderlich",

                    Description =
                        "Windows meldet einen ausstehenden "
                        + "Neustart. Vor weiteren Wartungs- "
                        + "oder Reparaturmaßnahmen sollte "
                        + "der Computer kontrolliert neu "
                        + "gestartet werden. Technische "
                        + $"Quelle: {reasons}.",

                    Category =
                        FindingCategory.OperatingSystem,

                    Severity =
                        FindingSeverity.Warning
                });

            return;
        }

        if (updateInformation.IsRestartRequired == false)
        {
            findings.Add(
                new CheckupFinding
                {
                    Title =
                        "Kein ausstehender Windows-Neustart erkannt",

                    Description =
                        "Die geprüften lokalen Windows- "
                        + "und Komponentenwartungsindikatoren "
                        + "melden derzeit keinen erforderlichen "
                        + "Neustart.",

                    Category =
                        FindingCategory.OperatingSystem,

                    Severity =
                        FindingSeverity.Information
                });

            return;
        }

        findings.Add(
            new CheckupFinding
            {
                Title =
                    "Windows-Neustartbedarf nicht auswertbar",

                Description =
                    "Die lokalen Windows-Indikatoren für "
                    + "einen erforderlichen Neustart konnten "
                    + "nicht zuverlässig ausgewertet werden.",

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Information
            });
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
                        FindingSeverity.Information
                });
        }
        else
        {
            findings.Add(
                new CheckupFinding
                {
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
                        FindingSeverity.Information
                });
        }

        if (updateInformation.RecentFailures.Count == 0)
        {
            findings.Add(
                new CheckupFinding
                {
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
                        FindingSeverity.Information
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
                    FindingSeverity.Warning
            });
    }
}