
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Assessment;

public class RestartAssessmentRule :
    ICheckupAssessmentRule
{
    public IEnumerable<CheckupFinding> Evaluate(
        CheckupSession checkupSession)
    {
        ArgumentNullException.ThrowIfNull(
            checkupSession);

        var restartInformation =
            checkupSession.RestartInformation;

        if (restartInformation.IsAnalysisPerformed)
        {
            return
            [
                CreateCentralRestartFinding(
                    restartInformation)
            ];
        }

        /*
         * Ältere Checkups ohne zentrale
         * Neustartanalyse bleiben weiterhin
         * mit ihrer bisherigen Bewertung
         * kompatibel.
         */
        return
        [
            CreateLegacyRestartFinding(
                checkupSession.WindowsUpdateInformation)
        ];
    }

    // =============================================
    // SCHRITT 55.12.4
    // ZENTRALE NEUSTARTBEWERTUNG
    // =============================================

    private static CheckupFinding CreateCentralRestartFinding(
        RestartInformation restartInformation)
    {
        /*
         * Bestätigende Quellen:
         *
         * - Windows Update
         * - Windows-Komponentenwartung
         * - Ausstehende Computername-Änderung
         *
         * Hinweisgebende Quelle:
         *
         * - PendingFileRenameOperations
         *
         * Ein bloßer Dateioperationshinweis
         * darf weder einen zwingenden Neustart
         * behaupten noch eine Wartungsaufgabe
         * oder einen Punktabzug verursachen.
         */

        // =========================================
        // 1. BESTÄTIGTER NEUSTARTBEDARF
        // =========================================

        if (restartInformation.IsRestartRequired == true)
        {
            var detectedSources =
                restartInformation.Sources
                    .Where(source =>
                        IsAuthoritativeSource(source)
                        && source.IsCheckSuccessful
                        && source.IsRestartRequired == true)
                    .Select(source =>
                        source.DisplayName)
                    .Where(displayName =>
                        !string.IsNullOrWhiteSpace(
                            displayName))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            var sourceText =
                detectedSources.Count > 0
                    ? string.Join(
                        ", ",
                        detectedSources)
                    : "Windows-Neustartindikatoren";

            return new CheckupFinding
            {
                Code =
                    "system.restart.required",

                Title =
                    "Windows-Neustart erforderlich",

                Description =
                    "Mindestens eine bestätigende "
                    + "Windows-Quelle meldet einen "
                    + "ausstehenden Neustart. Vor weiteren "
                    + "Wartungs- oder Reparaturmaßnahmen "
                    + "sollte der Computer kontrolliert "
                    + "neu gestartet werden. Erkannte "
                    + $"Quelle: {sourceText}.",

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Warning,

                AssessmentTarget =
                    FindingAssessmentTarget.SystemCondition,

                CauseGroup =
                    "system.restart.pending"
            };
        }

        // =========================================
        // 2. HINWEISGEBENDE QUELLEN ERMITTELN
        // =========================================

        var advisorySources =
            restartInformation.Sources
                .Where(source =>
                    !IsAuthoritativeSource(source)
                    && source.IsCheckSuccessful
                    && source.IsRestartRequired == true)
                .Select(source =>
                    source.DisplayName)
                .Where(displayName =>
                    !string.IsNullOrWhiteSpace(
                        displayName))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        // =========================================
        // 3. BESTÄTIGENDE QUELLEN OHNE BEDARF
        // =========================================

        if (restartInformation.IsAnalysisConclusive
            && restartInformation.IsRestartRequired == false)
        {
            /*
             * Die bestätigenden Quellen haben
             * keinen erforderlichen Neustart
             * gemeldet.
             *
             * Vorgemerkte Dateioperationen
             * bleiben als technischer Hinweis
             * sichtbar, erzeugen aber keine
             * Wartungsaufgabe.
             */
            if (advisorySources.Count > 0)
            {
                return new CheckupFinding
                {
                    Code =
                        "system.restart.advisory",

                    Title =
                        "Dateioperationen für nächsten "
                        + "Systemstart vorgemerkt",

                    Description =
                        "Die bestätigenden Windows-"
                        + "Quellen melden derzeit "
                        + "keinen erforderlichen Neustart. "
                        + "Unabhängig davon sind "
                        + "Dateioperationen für einen "
                        + "kommenden Systemstart "
                        + "vorgemerkt. Dies kann "
                        + "beispielsweise bei Programm- "
                        + "oder Komponentenupdates "
                        + "auftreten. Der Eintrag "
                        + "allein beweist keinen "
                        + "zwingend erforderlichen "
                        + "Neustart. "
                        + "Hinweisgebende Quelle: "
                        + string.Join(
                            ", ",
                            advisorySources)
                        + ".",

                    Category =
                        FindingCategory.OperatingSystem,

                    /*
                     * Keine Empfehlung zur
                     * Durchführung einer
                     * Wartungsmaßnahme.
                     */
                    Severity =
                        FindingSeverity.Information,

                    /*
                     * Ausschließlich dokumentieren.
                     *
                     * Der Hinweis fließt nicht
                     * als negativer Befund in
                     * die Systembewertung ein.
                     */
                    AssessmentTarget =
                        FindingAssessmentTarget.InformationOnly,

                    /*
                     * Bewusst getrennt von
                     * bestätigtem Neustartbedarf.
                     */
                    CauseGroup =
                        "system.restart.file-operations"
                };
            }

            return new CheckupFinding
            {
                Code =
                    "system.restart.none-required",

                Title =
                    "Kein ausstehender Windows-Neustart erkannt",

                Description =
                    "Die geprüften Windows-, "
                    + "Komponentenwartungs-, "
                    + "Dateioperations- und "
                    + "Computername-Indikatoren "
                    + "melden derzeit keinen "
                    + "erforderlichen Neustart. "
                    + "Die Aussage bezieht sich "
                    + "auf die von diesem Checkup "
                    + "geprüften bekannten "
                    + "Windows-Quellen.",

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Information,

                AssessmentTarget =
                    FindingAssessmentTarget.SystemCondition,

                CauseGroup =
                    "system.restart.pending"
            };
        }

        // =========================================
        // 4. NICHT VOLLSTÄNDIG AUSWERTBAR
        // =========================================

        /*
         * Ein bloßer Dateioperationshinweis
         * darf fehlgeschlagene Prüfungen der
         * bestätigenden Quellen nicht verdecken.
         *
         * Ist der Gesamtstatus nicht eindeutig,
         * bleibt das Ergebnis ausdrücklich
         * unvollständig.
         */

        var failedSources =
            restartInformation.Sources
                .Where(source =>
                    !source.IsCheckSuccessful)
                .Select(source =>
                    source.DisplayName)
                .Where(displayName =>
                    !string.IsNullOrWhiteSpace(
                        displayName))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        var failedSourceText =
            failedSources.Count > 0
                ? " Nicht vollständig auswertbar: "
                  + string.Join(
                      ", ",
                      failedSources)
                  + "."
                : string.Empty;

        var advisoryText =
            advisorySources.Count > 0
                ? " Zusätzlich wurden Hinweise "
                  + "auf vorgemerkte "
                  + "Dateioperationen erkannt: "
                  + string.Join(
                      ", ",
                      advisorySources)
                  + ". Diese Hinweise beweisen "
                  + "allein keinen zwingenden "
                  + "Neustartbedarf."
                : string.Empty;

        return new CheckupFinding
        {
            Code =
                "system.restart.not-fully-evaluable",

            Title =
                "Windows-Neustartbedarf nicht "
                + "vollständig auswertbar",

            Description =
                "Die bekannten lokalen "
                + "Windows-Indikatoren für "
                + "einen erforderlichen Neustart "
                + "konnten nicht vollständig "
                + "ausgewertet werden. Deshalb "
                + "wird nicht behauptet, "
                + "dass sicher kein Neustart "
                + "erforderlich ist."
                + failedSourceText
                + advisoryText,

            Category =
                FindingCategory.OperatingSystem,

            Severity =
                FindingSeverity.Information,

            AssessmentTarget =
                FindingAssessmentTarget.InformationOnly,

            CauseGroup =
                "system.restart.data-quality"
        };
    }

    // =============================================
    // HISTORISCHE CHECKUPS
    // =============================================

    private static CheckupFinding CreateLegacyRestartFinding(
        WindowsUpdateInformation updateInformation)
    {
        if (updateInformation.IsRestartRequired == true)
        {
            var reasons =
                updateInformation.RestartReasons.Count > 0
                    ? string.Join(
                        ", ",
                        updateInformation.RestartReasons)
                    : "Windows-Komponenten";

            return new CheckupFinding
            {
                Code =
                    "system.restart.legacy-required",

                Title =
                    "Windows-Neustart erforderlich",

                Description =
                    "Der ältere gespeicherte Checkup "
                    + "enthält einen erkannten "
                    + "Neustartbedarf. Vor weiteren "
                    + "Wartungs- oder "
                    + "Reparaturmaßnahmen sollte "
                    + "der Computer kontrolliert "
                    + "neu gestartet werden. "
                    + $"Technische Quelle: {reasons}.",

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Warning,

                AssessmentTarget =
                    FindingAssessmentTarget.SystemCondition,

                CauseGroup =
                    "system.restart.pending"
            };
        }

        if (updateInformation.IsRestartRequired == false)
        {
            return new CheckupFinding
            {
                Code =
                    "system.restart.legacy-none-required",

                Title =
                    "Kein ausstehender Windows-Neustart erkannt",

                Description =
                    "Im älteren gespeicherten Checkup "
                    + "haben die damals geprüften "
                    + "Windows- und "
                    + "Komponentenwartungsindikatoren "
                    + "keinen erforderlichen "
                    + "Neustart gemeldet.",

                Category =
                    FindingCategory.OperatingSystem,

                Severity =
                    FindingSeverity.Information,

                AssessmentTarget =
                    FindingAssessmentTarget.SystemCondition,

                CauseGroup =
                    "system.restart.pending"
            };
        }

        return new CheckupFinding
        {
            Code =
                "system.restart.legacy-not-evaluable",

            Title =
                "Windows-Neustartbedarf nicht auswertbar",

            Description =
                "Der ältere gespeicherte Checkup "
                + "enthält keine zuverlässige "
                + "Aussage über einen erforderlichen "
                + "Windows-Neustart. Ein neuer "
                + "Systemscan kann den aktuellen "
                + "Zustand mit der erweiterten "
                + "Neustartanalyse prüfen.",

            Category =
                FindingCategory.OperatingSystem,

            Severity =
                FindingSeverity.Information,

            AssessmentTarget =
                FindingAssessmentTarget.InformationOnly,

            CauseGroup =
                "system.restart.data-quality"
        };
    }

    // =============================================
    // QUELLENKLASSIFIZIERUNG
    // =============================================

    private static bool IsAuthoritativeSource(
        RestartSourceResult source)
    {
        return source.SourceType
            is RestartSourceType.WindowsUpdate
            or RestartSourceType.ComponentBasedServicing
            or RestartSourceType.PendingComputerRename;
    }
}
