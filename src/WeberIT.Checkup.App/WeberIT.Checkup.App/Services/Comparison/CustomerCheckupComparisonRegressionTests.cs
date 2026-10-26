
#if DEBUG

using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Comparison;

/// <summary>
/// Wiederholbare Regressionstests für den
/// Vorher-/Nachher-Vergleich.
///
/// Ausschließlich für Debug-Builds.
///
/// Verwendet künstliche Checkup-Daten.
/// Keine Datenbank, keine echten Geräte,
/// keine Dateizugriffe und keine Systemscans.
///
/// Die Tests werden ausdrücklich aufgerufen.
/// Sie starten nicht automatisch beim
/// Programmstart.
/// </summary>
public static class CustomerCheckupComparisonRegressionTests
{
    private const string SyntheticAreaCode =
        "synthetic-area";

    private const string OperatingSystemAreaCode =
        "operating-system";

    private static readonly string[] SystemAreas =
    [
        "system.operating-system",
        "system.security",
        "system.windows-update",
        "system.program-updates",
        "system.restart",
        "system.devices",
        "system.storage",
        "system.startup",
        "system.cleanup"
    ];

    private static readonly string[] HardwareAreas =
    [
        "hardware.memory",
        "hardware.storage.health",
        "hardware.storage.technology",
        "hardware.tpm",
        "hardware.platform"
    ];

    /// <summary>
    /// Führt alle definierten Regressionstests aus.
    ///
    /// Ein fehlgeschlagener Test löst eine Ausnahme
    /// mit dem betreffenden Testnamen aus.
    ///
    /// Bei Erfolg wird eine Zusammenfassung
    /// zurückgegeben.
    /// </summary>
    public static string RunAll()
    {
        var results = new List<string>();

        // Bestehende Score-Regressionstests

        Execute(
            "Identische Bewertungsbereiche",
            TestIdenticalAreas,
            results);

        Execute(
            "Unterschiedliche Systembereiche",
            TestDifferentSystemAreas,
            results);

        Execute(
            "Unterschiedliche Hardwarebereiche",
            TestDifferentHardwareAreas,
            results);

        Execute(
            "Unterschiedliche Modellbereiche",
            TestDifferentDefinedAreas,
            results);

        Execute(
            "Unterschiedliche Scoring-Versionen",
            TestDifferentScoringVersions,
            results);

        Execute(
            "Historische Daten ohne Bereichskennungen",
            TestMissingAreaCodes,
            results);

        Execute(
            "Doppelte Bereichskennungen",
            TestDuplicateAreaCodes,
            results);

        Execute(
            "Ungültige Bereichszuordnung",
            TestInvalidAreaMembership,
            results);

        Execute(
            "Unterschiedliche Reihenfolge und Schreibweise",
            TestAreaOrderAndCase,
            results);

        Execute(
            "Fehlender Score",
            TestMissingScore,
            results);

        // Neue Integrationstests für Vergleichsmodell V3

        Execute(
            "Integration: Gemischte Veränderungen bei gleicher Befundzahl",
            TestIntegratedMixedChanges,
            results);

        Execute(
            "Integration: Geringerer Schweregrad",
            TestIntegratedSeverityImprovement,
            results);

        Execute(
            "Integration: Höherer Schweregrad",
            TestIntegratedSeverityWorsening,
            results);

        Execute(
            "Integration: Geringere Befundhäufigkeit",
            TestIntegratedReducedOccurrenceCount,
            results);

        Execute(
            "Integration: Fehlende Abschlussauswertung",
            TestIntegratedMissingAreaEvaluation,
            results);

        // Isolierte fachliche Bereichstests

        Execute(
            "Bereich: Keine Auffälligkeiten",
            TestHealthyArea,
            results);

        Execute(
            "Bereich: Behobener Befund",
            TestResolvedAreaFinding,
            results);

        Execute(
            "Bereich: Neuer Befund",
            TestNewAreaFinding,
            results);

        Execute(
            "Bereich: Unveränderter Handlungsbedarf",
            TestUnchangedAreaFinding,
            results);

        Execute(
            "Bereich: Gemischte Veränderungen",
            TestMixedAreaFindings,
            results);

        Execute(
            "Bereich: Schweregrad und Häufigkeit gegenläufig",
            TestOpposingSeverityAndOccurrence,
            results);

        Execute(
            "Bereich: Fehlende Vergleichbarkeit",
            TestAreaNotComparable,
            results);

        Execute(
            "Bereich: Befund nicht erneut auswertbar",
            TestFindingNotComparable,
            results);

        Execute(
            "Bereich: Neutrale Informationen",
            TestInformationalFinding,
            results);

        Execute(
            "Bereich: Widersprüchliche Befunddaten",
            TestContradictoryFindingData,
            results);

        return
            $"Alle {results.Count} Regressionstests "
            + "wurden erfolgreich abgeschlossen."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =====================================================
    // BESTEHENDE TESTS – SCOREVERGLEICH
    // =====================================================

    private static void TestIdenticalAreas()
    {
        var comparison = CreateComparison();

        Require(
            comparison.ComparisonModelVersion == 3,
            "Die Vergleichsmodell-Version muss 3 sein.");

        Require(
            comparison.SystemScore.IsComparable,
            "Der Systemscore muss vergleichbar sein.");

        Require(
            comparison.HardwareScore.IsComparable,
            "Der Hardwarescore muss vergleichbar sein.");

        Require(
            comparison.SystemScore.Difference == 5,
            "Die Systemscore-Differenz muss +5 sein.");

        Require(
            comparison.HardwareScore.Difference == 5,
            "Die Hardwarescore-Differenz muss +5 sein.");

        Require(
            comparison.SystemScore.Change
                == CustomerCheckupScoreChange.Improved,
            "Der Systemscore muss als verbessert gelten.");

        Require(
            comparison.HardwareScore.Change
                == CustomerCheckupScoreChange.Improved,
            "Der Hardwarescore muss als verbessert gelten.");
    }

    private static void TestDifferentSystemAreas()
    {
        var comparison = CreateComparison(
            (before, after) =>
            {
                var codes =
                    after.Assessment
                        .SystemCondition
                        .AvailableAreaCodes;

                /*
                 * Vorher: Windows-Sicherheit verfügbar.
                 * Nachher: stattdessen Neustartstatus.
                 *
                 * Die Gesamtzahl bleibt identisch.
                 */
                codes.Remove("system.security");
                codes.Add("system.restart");
            });

        Require(
            !comparison.SystemScore.IsComparable,
            "Unterschiedliche Systembereiche dürfen "
            + "nicht direkt vergleichbar sein.");

        Require(
            comparison.HardwareScore.IsComparable,
            "Die unveränderte Hardwarebewertung "
            + "muss weiterhin vergleichbar sein.");

        Require(
            !comparison.SystemScore.Difference.HasValue,
            "Bei nicht vergleichbaren Systemwerten "
            + "darf keine Differenz vorliegen.");
    }

    private static void TestDifferentHardwareAreas()
    {
        var comparison = CreateComparison(
            (before, after) =>
            {
                var codes =
                    after.Assessment
                        .HardwareCondition
                        .AvailableAreaCodes;

                /*
                 * Gleiche Anzahl auswertbarer
                 * Hardwarebereiche, andere Identität.
                 */
                codes.Remove("hardware.memory");
                codes.Add("hardware.platform");
            });

        Require(
            !comparison.HardwareScore.IsComparable,
            "Unterschiedliche Hardwarebereiche dürfen "
            + "nicht direkt vergleichbar sein.");

        Require(
            comparison.SystemScore.IsComparable,
            "Der unveränderte Systemscore "
            + "muss vergleichbar bleiben.");
    }

    private static void TestDifferentDefinedAreas()
    {
        var comparison = CreateComparison(
            (before, after) =>
            {
                var codes =
                    after.Assessment
                        .SystemCondition
                        .EvaluatedAreaCodes;

                /*
                 * Die Menge der grundsätzlich
                 * definierten Bereiche verändert
                 * sich, ihre Anzahl jedoch nicht.
                 */
                codes.Remove("system.restart");
                codes.Add("system.example-new-area");
            });

        Require(
            !comparison.SystemScore.IsComparable,
            "Unterschiedliche definierte Bereiche "
            + "müssen den Vergleich verhindern.");
    }

    private static void TestDifferentScoringVersions()
    {
        var comparison = CreateComparison(
            (before, after) =>
            {
                before.Assessment.ScoringVersion = 1;
                after.Assessment.ScoringVersion = 2;
            });

        Require(
            !comparison.SystemScore.IsComparable,
            "Unterschiedliche Scoring-Versionen "
            + "müssen den Systemvergleich verhindern.");

        Require(
            !comparison.HardwareScore.IsComparable,
            "Unterschiedliche Scoring-Versionen "
            + "müssen den Hardwarevergleich verhindern.");
    }

    private static void TestMissingAreaCodes()
    {
        var comparison = CreateComparison(
            (before, after) =>
            {
                /*
                 * Simuliert ältere gespeicherte
                 * Bewertungsdaten ohne die
                 * neuen Bereichskennungen.
                 */
                before.Assessment
                    .SystemCondition
                    .EvaluatedAreaCodes
                    .Clear();

                before.Assessment
                    .SystemCondition
                    .AvailableAreaCodes
                    .Clear();
            });

        Require(
            !comparison.SystemScore.IsComparable,
            "Fehlende Bereichskennungen dürfen "
            + "keinen direkten Vergleich erlauben.");

        Require(
            comparison.HardwareScore.IsComparable,
            "Die vollständigen Hardwaredaten "
            + "müssen vergleichbar bleiben.");
    }

    private static void TestDuplicateAreaCodes()
    {
        var comparison = CreateComparison(
            (before, after) =>
            {
                var codes =
                    after.Assessment
                        .SystemCondition
                        .AvailableAreaCodes;

                /*
                 * Anzahl unverändert,
                 * aber eine Kennung doppelt.
                 */
                codes[1] = codes[0];
            });

        Require(
            !comparison.SystemScore.IsComparable,
            "Doppelte Kennungen müssen "
            + "den Vergleich verhindern.");
    }

    private static void TestInvalidAreaMembership()
    {
        var comparison = CreateComparison(
            (before, after) =>
            {
                var codes =
                    after.Assessment
                        .HardwareCondition
                        .AvailableAreaCodes;

                /*
                 * Ein als auswertbar deklarierter
                 * Bereich existiert nicht im
                 * definierten Bewertungsmodell.
                 */
                codes[0] =
                    "hardware.unknown-area";
            });

        Require(
            !comparison.HardwareScore.IsComparable,
            "Eine ungültige Bereichszuordnung "
            + "muss den Vergleich verhindern.");
    }

    private static void TestAreaOrderAndCase()
    {
        var comparison = CreateComparison(
            (before, after) =>
            {
                var system =
                    after.Assessment.SystemCondition;

                system.EvaluatedAreaCodes =
                    system.EvaluatedAreaCodes
                        .AsEnumerable()
                        .Reverse()
                        .Select(code =>
                            code.ToUpperInvariant())
                        .ToList();

                system.AvailableAreaCodes =
                    system.AvailableAreaCodes
                        .AsEnumerable()
                        .Reverse()
                        .Select(code =>
                            code.ToUpperInvariant())
                        .ToList();

                var hardware =
                    after.Assessment.HardwareCondition;

                hardware.EvaluatedAreaCodes =
                    hardware.EvaluatedAreaCodes
                        .AsEnumerable()
                        .Reverse()
                        .Select(code =>
                            code.ToUpperInvariant())
                        .ToList();

                hardware.AvailableAreaCodes =
                    hardware.AvailableAreaCodes
                        .AsEnumerable()
                        .Reverse()
                        .Select(code =>
                            code.ToUpperInvariant())
                        .ToList();
            });

        Require(
            comparison.SystemScore.IsComparable,
            "Reihenfolge und Groß-/Kleinschreibung "
            + "dürfen den Systemvergleich "
            + "nicht verhindern.");

        Require(
            comparison.HardwareScore.IsComparable,
            "Reihenfolge und Groß-/Kleinschreibung "
            + "dürfen den Hardwarevergleich "
            + "nicht verhindern.");
    }

    private static void TestMissingScore()
    {
        var comparison = CreateComparison(
            (before, after) =>
            {
                after.Assessment
                    .SystemCondition
                    .Score = null;
            });

        Require(
            !comparison.SystemScore.IsComparable,
            "Ein fehlender Score muss "
            + "den Vergleich verhindern.");

        Require(
            comparison.HardwareScore.IsComparable,
            "Der unabhängige Hardwarescore "
            + "muss vergleichbar bleiben.");
    }

    // =====================================================
    // SCHRITT 52.4 – INTEGRATIONSTESTS
    // =====================================================

    private static void TestIntegratedMixedChanges()
    {
        var comparison = CreateComparison(
            (before, after) =>
            {
                EnableOperatingSystemEvaluation(
                    before,
                    after);

                /*
                 * Vorher ein kleinerer Befund,
                 * nachher ein anderer, kritischer
                 * Befund.
                 *
                 * Anzahl vorher: 1
                 * Anzahl nachher: 1
                 *
                 * Eine bloße Zählung würde
                 * keine Änderung erkennen.
                 */
                before.Assessment.Findings.Add(
                    CreateScanFinding(
                        "system.operating-system.synthetic-old",
                        FindingSeverity.Recommendation));

                after.Assessment.Findings.Add(
                    CreateScanFinding(
                        "system.operating-system.synthetic-new",
                        FindingSeverity.Critical));
            });

        RequireOperatingSystemAreaStatus(
            comparison,
            CustomerCheckupAreaComparisonStatus.MixedChanges);

        Require(
            comparison.ResolvedFindingCount == 1,
            "Der alte Befund muss als "
            + "nicht mehr festgestellt gelten.");

        Require(
            comparison.NewlyDetectedFindingCount == 1,
            "Der neue kritische Befund "
            + "muss erhalten bleiben.");
    }

    private static void TestIntegratedSeverityImprovement()
    {
        var comparison = CreateComparison(
            (before, after) =>
            {
                EnableOperatingSystemEvaluation(
                    before,
                    after);

                const string code =
                    "system.operating-system.synthetic-severity";

                before.Assessment.Findings.Add(
                    CreateScanFinding(
                        code,
                        FindingSeverity.Warning));

                after.Assessment.Findings.Add(
                    CreateScanFinding(
                        code,
                        FindingSeverity.Recommendation));
            });

        RequireOperatingSystemAreaStatus(
            comparison,
            CustomerCheckupAreaComparisonStatus
                .ImprovedButStillNeedsAttention);
    }

    private static void TestIntegratedSeverityWorsening()
    {
        var comparison = CreateComparison(
            (before, after) =>
            {
                EnableOperatingSystemEvaluation(
                    before,
                    after);

                const string code =
                    "system.operating-system.synthetic-severity";

                before.Assessment.Findings.Add(
                    CreateScanFinding(
                        code,
                        FindingSeverity.Recommendation));

                after.Assessment.Findings.Add(
                    CreateScanFinding(
                        code,
                        FindingSeverity.Critical));
            });

        RequireOperatingSystemAreaStatus(
            comparison,
            CustomerCheckupAreaComparisonStatus.Worsened);
    }

    private static void TestIntegratedReducedOccurrenceCount()
    {
        var comparison = CreateComparison(
            (before, after) =>
            {
                EnableOperatingSystemEvaluation(
                    before,
                    after);

                const string code =
                    "system.operating-system.synthetic-frequency";

                before.Assessment.Findings.Add(
                    CreateScanFinding(
                        code,
                        FindingSeverity.Warning));

                before.Assessment.Findings.Add(
                    CreateScanFinding(
                        code,
                        FindingSeverity.Warning));

                after.Assessment.Findings.Add(
                    CreateScanFinding(
                        code,
                        FindingSeverity.Warning));
            });

        RequireOperatingSystemAreaStatus(
            comparison,
            CustomerCheckupAreaComparisonStatus
                .ImprovedButStillNeedsAttention);
    }

    private static void TestIntegratedMissingAreaEvaluation()
    {
        var comparison = CreateComparison(
            (before, after) =>
            {
                /*
                 * Nur im Eingangsscan sind
                 * Betriebssystemdaten vorhanden.
                 */
                before.OperatingSystemInformation.Name =
                    "SYNTHETIC-OPERATING-SYSTEM";

                after.OperatingSystemInformation.Name =
                    string.Empty;

                before.Assessment.Findings.Add(
                    CreateScanFinding(
                        "system.operating-system.synthetic-missing",
                        FindingSeverity.Warning));
            });

        RequireOperatingSystemAreaStatus(
            comparison,
            CustomerCheckupAreaComparisonStatus.NotComparable);
    }

    // =====================================================
    // SCHRITT 52.4 – ISOLIERTE FACHLICHE TESTS
    // =====================================================

    private static void TestHealthyArea()
    {
        RequireAreaStatus(
            CustomerCheckupAreaComparisonStatus.UnchangedHealthy);
    }

    private static void TestResolvedAreaFinding()
    {
        RequireAreaStatus(
            CustomerCheckupAreaComparisonStatus.Improved,
            CreateAreaFinding(
                CustomerCheckupFindingComparisonStatus.Resolved,
                FindingSeverity.Warning,
                null,
                beforeCount: 1,
                afterCount: 0));
    }

    private static void TestNewAreaFinding()
    {
        RequireAreaStatus(
            CustomerCheckupAreaComparisonStatus.NewlyNeedsAttention,
            CreateAreaFinding(
                CustomerCheckupFindingComparisonStatus.NewlyDetected,
                null,
                FindingSeverity.Critical,
                beforeCount: 0,
                afterCount: 1));
    }

    private static void TestUnchangedAreaFinding()
    {
        RequireAreaStatus(
            CustomerCheckupAreaComparisonStatus.UnchangedNeedsAttention,
            CreateAreaFinding(
                CustomerCheckupFindingComparisonStatus.StillOpen,
                FindingSeverity.Warning,
                FindingSeverity.Warning,
                beforeCount: 1,
                afterCount: 1));
    }

    private static void TestMixedAreaFindings()
    {
        RequireAreaStatus(
            CustomerCheckupAreaComparisonStatus.MixedChanges,
            CreateAreaFinding(
                CustomerCheckupFindingComparisonStatus.Resolved,
                FindingSeverity.Recommendation,
                null,
                beforeCount: 1,
                afterCount: 0),
            CreateAreaFinding(
                CustomerCheckupFindingComparisonStatus.NewlyDetected,
                null,
                FindingSeverity.Critical,
                beforeCount: 0,
                afterCount: 1));
    }

    private static void TestOpposingSeverityAndOccurrence()
    {
        /*
         * Derselbe Befund:
         *
         * Vorher: eine Warnung.
         * Nachher: zwei Empfehlungen.
         *
         * Schweregrad verbessert,
         * Häufigkeit verschlechtert.
         *
         * Keine eindeutige Gesamtrichtung.
         */
        RequireAreaStatus(
            CustomerCheckupAreaComparisonStatus.MixedChanges,
            CreateAreaFinding(
                CustomerCheckupFindingComparisonStatus.StillOpen,
                FindingSeverity.Warning,
                FindingSeverity.Recommendation,
                beforeCount: 1,
                afterCount: 2));
    }

    private static void TestAreaNotComparable()
    {
        var finding = CreateAreaFinding(
            CustomerCheckupFindingComparisonStatus.Resolved,
            FindingSeverity.Warning,
            null,
            beforeCount: 1,
            afterCount: 0);

        var status =
            CustomerCheckupAreaStatusPolicy.Determine(
                SyntheticAreaCode,
                wasBeforeEvaluable: true,
                isAfterEvaluable: false,
                [finding]);

        Require(
            status == CustomerCheckupAreaComparisonStatus.NotComparable,
            "Ein nicht auswertbarer Nachher-Bereich "
            + "darf keine Verbesserung melden.");
    }

    private static void TestFindingNotComparable()
    {
        RequireAreaStatus(
            CustomerCheckupAreaComparisonStatus.NotComparable,
            CreateAreaFinding(
                CustomerCheckupFindingComparisonStatus.NotReevaluatable,
                FindingSeverity.Warning,
                null,
                beforeCount: 1,
                afterCount: 0,
                wasBeforeEvaluable: true,
                isAfterEvaluable: false));
    }

    private static void TestInformationalFinding()
    {
        /*
         * Neutrale Informationen dürfen weder
         * als Mangel noch als Verbesserung
         * oder Verschlechterung zählen.
         */
        RequireAreaStatus(
            CustomerCheckupAreaComparisonStatus.UnchangedHealthy,
            CreateAreaFinding(
                CustomerCheckupFindingComparisonStatus.Informational,
                FindingSeverity.Information,
                FindingSeverity.Information,
                beforeCount: 1,
                afterCount: 1,
                wasBeforeEvaluable: false,
                isAfterEvaluable: false));
    }

    private static void TestContradictoryFindingData()
    {
        /*
         * Der Befund behauptet "behoben",
         * besitzt nachher aber weiterhin
         * einen handlungsrelevanten Status.
         *
         * Eine solche Kombination darf
         * nicht positiv gewertet werden.
         */
        RequireAreaStatus(
            CustomerCheckupAreaComparisonStatus.NotComparable,
            CreateAreaFinding(
                CustomerCheckupFindingComparisonStatus.Resolved,
                FindingSeverity.Warning,
                FindingSeverity.Warning,
                beforeCount: 1,
                afterCount: 1));
    }

    // =====================================================
    // SYNTHETISCHE TESTDATEN
    // =====================================================

    private static CustomerCheckupComparison CreateComparison(
        Action<CheckupSession, CheckupSession>? configure = null)
    {
        /*
         * Beide Scans und der Kundencheckup
         * werden ausschließlich im Arbeitsspeicher
         * erzeugt.
         *
         * Die Gerätekennung ist frei erfunden.
         * Es wird kein physisches Gerät geprüft.
         */

        var beforeTime =
            new DateTime(
                2026, 10, 1, 10, 0, 0);

        var afterTime =
            beforeTime.AddMinutes(30);

        var before =
            CreateSyntheticSession(
                beforeTime,
                systemScore: 80,
                hardwareScore: 85);

        var after =
            CreateSyntheticSession(
                afterTime,
                systemScore: 85,
                hardwareScore: 90);

        configure?.Invoke(before, after);

        var visit =
            CustomerCheckupVisit.Start(before);

        var working =
            new CheckupSession();

        working.CustomerCheckupVisits.Add(
            visit);

        var comparisonService =
            new CustomerCheckupComparisonService(
                new DeviceIdentityService());

        return comparisonService.Compare(
            visit,
            working,
            after);
    }

    private static CheckupSession CreateSyntheticSession(
        DateTime scanDate,
        int systemScore,
        int hardwareScore)
    {
        return new CheckupSession
        {
            ScanDate =
                scanDate,

            DeviceInformation =
                new DeviceInformation
                {
                    Name =
                        "SYNTHETIC-REGRESSION-DEVICE",

                    SerialNumber =
                        "SYNTHETIC-TEST-ONLY-00001"
                },

            Assessment =
                new CheckupAssessment
                {
                    ScoringVersion =
                        2,

                    AssessmentCreatedAt =
                        scanDate,

                    SystemCondition =
                        CreateCondition(
                            SystemAreas,
                            SystemAreas
                                .Where(code =>
                                    code != "system.restart")
                                .ToArray(),
                            systemScore,
                            AssessmentDataQuality.Good),

                    HardwareCondition =
                        CreateCondition(
                            HardwareAreas,
                            HardwareAreas
                                .Where(code =>
                                    code != "hardware.platform")
                                .ToArray(),
                            hardwareScore,
                            AssessmentDataQuality.Sufficient)
                }
        };
    }

    private static ConditionAssessment CreateCondition(
        IReadOnlyCollection<string> evaluatedCodes,
        IReadOnlyCollection<string> availableCodes,
        int score,
        AssessmentDataQuality quality)
    {
        return new ConditionAssessment
        {
            Score =
                score,

            DataQuality =
                quality,

            EvaluatedAreaCount =
                evaluatedCodes.Count,

            AvailableAreaCount =
                availableCodes.Count,

            EvaluatedAreaCodes =
                evaluatedCodes.ToList(),

            AvailableAreaCodes =
                availableCodes.ToList()
        };
    }

    private static void EnableOperatingSystemEvaluation(
        CheckupSession before,
        CheckupSession after)
    {
        /*
         * Der fachliche Bereich "Betriebssystem"
         * soll in beiden künstlichen Scans
         * zuverlässig auswertbar sein.
         */
        before.OperatingSystemInformation.Name =
            "SYNTHETIC-OPERATING-SYSTEM";

        after.OperatingSystemInformation.Name =
            "SYNTHETIC-OPERATING-SYSTEM";
    }

    private static CheckupFinding CreateScanFinding(
        string code,
        FindingSeverity severity)
    {
        return new CheckupFinding
        {
            Code = code,
            Title = "Künstlicher Betriebsystembefund",
            Description = "Ausschließlich synthetischer Testbefund",
            Category = FindingCategory.OperatingSystem,
            Severity = severity
        };
    }

    private static CustomerCheckupFindingComparison CreateAreaFinding(
        CustomerCheckupFindingComparisonStatus status,
        FindingSeverity? beforeSeverity,
        FindingSeverity? afterSeverity,
        int beforeCount,
        int afterCount,
        bool wasBeforeEvaluable = true,
        bool isAfterEvaluable = true)
    {
        return new CustomerCheckupFindingComparison
        {
            AreaCode =
                SyntheticAreaCode,

            MatchKey =
                Guid.NewGuid().ToString("N"),

            Title =
                "Künstlicher Bereichsbefund",

            Status =
                status,

            BeforeSeverity =
                beforeSeverity,

            AfterSeverity =
                afterSeverity,

            BeforeOccurrenceCount =
                beforeCount,

            AfterOccurrenceCount =
                afterCount,

            WasBeforeAreaEvaluable =
                wasBeforeEvaluable,

            IsAfterAreaEvaluable =
                isAfterEvaluable
        };
    }

    // =====================================================
    // AUSWERTUNG UND TESTHILFEN
    // =====================================================

    private static void RequireOperatingSystemAreaStatus(
        CustomerCheckupComparison comparison,
        CustomerCheckupAreaComparisonStatus expected)
    {
        var area =
            comparison.Areas.FirstOrDefault(item =>
                string.Equals(
                    item.AreaCode,
                    OperatingSystemAreaCode,
                    StringComparison.OrdinalIgnoreCase));

        Require(
            area is not null,
            "Der integrierte Bereich "
            + "'Betriebssystem' fehlt.");

        Require(
            area!.Status == expected,
            "Unerwartete integrierte Bereichsbewertung. "
            + $"Erwartet: {expected}. "
            + $"Tatsächlich: {area.Status}.");
    }

    private static void RequireAreaStatus(
        CustomerCheckupAreaComparisonStatus expected,
        params CustomerCheckupFindingComparison[] findings)
    {
        var actual =
            CustomerCheckupAreaStatusPolicy.Determine(
                SyntheticAreaCode,
                wasBeforeEvaluable: true,
                isAfterEvaluable: true,
                findings);

        Require(
            actual == expected,
            "Unerwartete fachliche Bereichsbewertung. "
            + $"Erwartet: {expected}. "
            + $"Tatsächlich: {actual}.");
    }

    private static void Execute(
        string name,
        Action test,
        ICollection<string> results)
    {
        try
        {
            test();

            results.Add(
                "OK: " + name);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Regressionstest fehlgeschlagen: "
                + name
                + Environment.NewLine
                + exception.Message,
                exception);
        }
    }

    private static void Require(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                message);
        }
    }
}

#endif
