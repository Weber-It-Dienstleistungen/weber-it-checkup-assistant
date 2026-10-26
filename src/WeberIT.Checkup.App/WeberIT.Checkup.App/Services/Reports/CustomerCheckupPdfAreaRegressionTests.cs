
#if DEBUG

using System.Reflection;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.IO;
using MigraDoc.Rendering;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Reports;

/// <summary>
/// Regressionstests für die PDF-Darstellung
/// technischer Bereichsvergleiche.
///
/// Ausschließlich in Debug-Builds verfügbar.
///
/// Alle Vergleiche werden künstlich erzeugt.
/// Es werden keine Kunden, Geräte, Datenbanken
/// oder gespeicherten Checkups verwendet.
///
/// Das PDF wird ausschließlich im
/// Arbeitsspeicher erzeugt.
///
/// Keine Dateizugriffe.
/// Keine Systemscans.
/// Keine automatischen Teststarts.
/// </summary>
public static class CustomerCheckupPdfAreaRegressionTests
{
    private static readonly Type ReportServiceType =
        typeof(CustomerCheckupPdfReportService);

    public static string RunAll()
    {
        var results = new List<string>();

        Execute(
            "Gemischte Veränderungen: Kundenbezeichnung",
            TestMixedWording,
            results);

        Execute(
            "Gemischte Veränderungen: Erklärung",
            TestMixedExplanation,
            results);

        Execute(
            "Gemischt und nicht vergleichbar unterscheiden",
            TestDistinctStatusWording,
            results);

        Execute(
            "PDF-Bereichstabelle enthält gemischten Status",
            TestMixedStatusInDocument,
            results);

        Execute(
            "PDF enthält Erklärung bei gemischten Veränderungen",
            TestMixedExplanationInDocument,
            results);

        Execute(
            "PDF enthält keinen unnötigen Hinweis",
            TestNoMixedExplanationWhenUnnecessary,
            results);

        Execute(
            "Gemischter Status erhält Warnfarbe",
            TestMixedWarningColor,
            results);

        Execute(
            "Positive und negative Statusfarben bleiben erhalten",
            TestExistingStatusColors,
            results);

        Execute(
            "Technische Bereiche werden sinnvoll sortiert",
            TestAreaSorting,
            results);

        Execute(
            "PDF-Befundzähler und Auswertbarkeit bleiben erhalten",
            TestFindingCountsAndEvaluation,
            results);

        Execute(
            "PDF behandelt fehlende Bereiche",
            TestEmptyAreaComparison,
            results);

        Execute(
            "MigraDoc rendert künstlichen Vergleich im Speicher",
            TestInMemoryPdfRendering,
            results);

        return
            $"Alle {results.Count} PDF-Bereichstests "
            + "wurden erfolgreich abgeschlossen."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =====================================================
    // KUNDENVERSTÄNDLICHE FORMULIERUNGEN
    // =====================================================

    private static void TestMixedWording()
    {
        var text =
            CustomerCheckupReportWording.GetAreaStatusText(
                CustomerCheckupAreaComparisonStatus.MixedChanges);

        Require(
            text == "Teils verbessert, teils verschlechtert",
            "Die kundenverständliche Bezeichnung "
            + "für MixedChanges ist nicht korrekt.");
    }

    private static void TestMixedExplanation()
    {
        var explanation =
            CustomerCheckupReportWording
                .GetAreaStatusExplanation(
                    CustomerCheckupAreaComparisonStatus.MixedChanges);

        RequireContains(
            explanation,
            "Verbesserungen");

        RequireContains(
            explanation,
            "Verschlechterungen");

        RequireContains(
            explanation,
            "nicht eindeutig");
    }

    private static void TestDistinctStatusWording()
    {
        var mixed =
            GetPrivateStatusText(
                CustomerCheckupAreaComparisonStatus.MixedChanges);

        var notComparable =
            GetPrivateStatusText(
                CustomerCheckupAreaComparisonStatus.NotComparable);

        Require(
            mixed == "Teils verbessert, teils verschlechtert",
            "Der PDF-Generator verwendet nicht "
            + "die neue MixedChanges-Bezeichnung.");

        Require(
            notComparable == "Technisch nicht direkt vergleichbar",
            "Der bestehende Status für fehlende "
            + "Vergleichbarkeit wurde unerwartet verändert.");

        Require(
            mixed != notComparable,
            "Gemischte Veränderungen und fehlende "
            + "Vergleichbarkeit dürfen nicht "
            + "gleich dargestellt werden.");

        var explanation =
            CustomerCheckupReportWording
                .GetAreaStatusExplanation(
                    CustomerCheckupAreaComparisonStatus.NotComparable);

        RequireContains(
            explanation,
            "nicht ausreichend zuverlässig");
    }

    // =====================================================
    // TATSÄCHLICHER PDF-DOKUMENTAUFBAU
    // =====================================================

    private static void TestMixedStatusInDocument()
    {
        var document = CreateAreaDocument(
            CreateArea(
                "SYNTHETIC MIXED",
                CustomerCheckupAreaComparisonStatus.MixedChanges,
                beforeCount: 1,
                afterCount: 1));

        var ddl = DdlWriter.WriteToString(document);

        RequireContains(
            ddl,
            "SYNTHETIC MIXED");

        RequireContains(
            ddl,
            "Teils verbessert, teils verschlechtert");

        RequireNotContains(
            ddl,
            "Technischer Zustand verschlechtert");
    }

    private static void TestMixedExplanationInDocument()
    {
        var document = CreateAreaDocument(
            CreateArea(
                "SYNTHETIC MIXED",
                CustomerCheckupAreaComparisonStatus.MixedChanges,
                beforeCount: 2,
                afterCount: 2));

        var ddl = DdlWriter.WriteToString(document);

        RequireContains(
            ddl,
            "Verbesserungen");

        RequireContains(
            ddl,
            "Verschlechterungen");

        RequireContains(
            ddl,
            "Eine insgesamt positive");
    }

    private static void TestNoMixedExplanationWhenUnnecessary()
    {
        var document = CreateAreaDocument(
            CreateArea(
                "SYNTHETIC HEALTHY",
                CustomerCheckupAreaComparisonStatus.UnchangedHealthy));

        var ddl = DdlWriter.WriteToString(document);

        RequireContains(
            ddl,
            "SYNTHETIC HEALTHY");

        RequireNotContains(
            ddl,
            "Gemischte Veränderungen");

        RequireNotContains(
            ddl,
            "Teils verbessert, teils verschlechtert");
    }

    // =====================================================
    // FARBE UND SORTIERUNG
    // =====================================================

    private static void TestMixedWarningColor()
    {
        var actual = GetPrivateStatusColor(
            CustomerCheckupAreaComparisonStatus.MixedChanges);

        var expected = Color.FromRgb(180, 83, 9);

        Require(
            actual.Equals(expected),
            "Gemischte Veränderungen müssen "
            + "in der definierten Warnfarbe erscheinen.");
    }

    private static void TestExistingStatusColors()
    {
        var improved =
            GetPrivateStatusColor(
                CustomerCheckupAreaComparisonStatus.Improved);

        var worsened =
            GetPrivateStatusColor(
                CustomerCheckupAreaComparisonStatus.Worsened);

        Require(
            improved.Equals(Color.FromRgb(21, 128, 61)),
            "Die Erfolgsfarbe wurde unerwartet verändert.");

        Require(
            worsened.Equals(Color.FromRgb(185, 28, 28)),
            "Die Fehlerfarbe wurde unerwartet verändert.");

        Require(
            !improved.Equals(worsened),
            "Verbesserungen und Verschlechterungen "
            + "müssen unterscheidbar bleiben.");
    }

    private static void TestAreaSorting()
    {
        /*
         * Die Eingabereihenfolge ist bewusst
         * NICHT die fachlich gewünschte Reihenfolge.
         *
         * Der PDF-Generator muss sortieren:
         *
         * 1. Verschlechtert
         * 2. Gemischte Veränderungen
         * 3. Unverändert gesund
         */
        var document = CreateAreaDocument(
            CreateArea(
                "SYNTHETIC HEALTHY",
                CustomerCheckupAreaComparisonStatus.UnchangedHealthy),

            CreateArea(
                "SYNTHETIC MIXED",
                CustomerCheckupAreaComparisonStatus.MixedChanges,
                beforeCount: 1,
                afterCount: 1),

            CreateArea(
                "SYNTHETIC WORSENED",
                CustomerCheckupAreaComparisonStatus.Worsened,
                beforeCount: 1,
                afterCount: 2));

        var ddl = DdlWriter.WriteToString(document);

        var worsenedIndex = ddl.IndexOf(
            "SYNTHETIC WORSENED",
            StringComparison.Ordinal);

        var mixedIndex = ddl.IndexOf(
            "SYNTHETIC MIXED",
            StringComparison.Ordinal);

        var healthyIndex = ddl.IndexOf(
            "SYNTHETIC HEALTHY",
            StringComparison.Ordinal);

        Require(
            worsenedIndex >= 0
            && mixedIndex >= 0
            && healthyIndex >= 0,
            "Nicht alle Testbereiche stehen "
            + "im Dokument.");

        Require(
            worsenedIndex < mixedIndex
            && mixedIndex < healthyIndex,
            "Die Bereiche werden nicht "
            + "in der vorgesehenen Reihenfolge "
            + "im PDF-Dokument aufgebaut.");
    }

    // =====================================================
    // DATENINTEGRITÄT UND GRENZFÄLLE
    // =====================================================

    private static void TestFindingCountsAndEvaluation()
    {
        var document = CreateAreaDocument(
            CreateArea(
                "SYNTHETIC COUNTS",
                CustomerCheckupAreaComparisonStatus.MixedChanges,
                beforeCount: 1,
                afterCount: 2));

        var ddl = DdlWriter.WriteToString(document);

        RequireContains(
            ddl,
            "SYNTHETIC COUNTS");

        RequireContains(
            ddl,
            "1 Befund");

        RequireContains(
            ddl,
            "2 Befunde");

        RequireContains(
            ddl,
            "Vorher und nachher auswertbar");
    }

    private static void TestEmptyAreaComparison()
    {
        var document = CreateAreaDocument();

        var ddl = DdlWriter.WriteToString(document);

        RequireContains(
            ddl,
            "Keine Bereichsauswertung verfügbar");

        RequireContains(
            ddl,
            "keine technischen Bereichsergebnisse");
    }

    private static void TestInMemoryPdfRendering()
    {
        /*
         * Hier wird nicht nur das MigraDoc-
         * Dokumentmodell untersucht.
         *
         * Wir lassen den tatsächlichen PDF-Renderer
         * einen künstlichen Bereichsvergleich
         * im Arbeitsspeicher erzeugen.
         *
         * Es wird ausdrücklich NICHT gespeichert.
         */
        var document = CreateAreaDocument(
            CreateArea(
                "SYNTHETIC PDF RENDER",
                CustomerCheckupAreaComparisonStatus.MixedChanges,
                beforeCount: 1,
                afterCount: 2));

        var renderer = new PdfDocumentRenderer
        {
            Document = document
        };

        renderer.RenderDocument();

        Require(
            renderer.PdfDocument.PageCount > 0,
            "Der PDF-Renderer hat keine "
            + "Dokumentseite erzeugt.");

        renderer.PdfDocument.Dispose();
    }

    // =====================================================
    // SYNTHETISCHE TESTDATEN
    // =====================================================

    private static CustomerCheckupAreaComparison CreateArea(
        string title,
        CustomerCheckupAreaComparisonStatus status,
        int beforeCount = 0,
        int afterCount = 0)
    {
        return new CustomerCheckupAreaComparison
        {
            AreaCode = "synthetic-area-" + title,

            Title = title,

            Status = status,

            WasBeforeEvaluable = true,

            IsAfterEvaluable = true,

            BeforeActionableFindingCount = beforeCount,

            AfterActionableFindingCount = afterCount,

            BeforeEvaluationNote =
                "Künstliche Eingangskontrolle",

            AfterEvaluationNote =
                "Künstliche Abschlusskontrolle"
        };
    }

    private static Document CreateAreaDocument(
        params CustomerCheckupAreaComparison[] areas)
    {
        /*
         * Wir erzeugen ausschließlich ein
         * neues, leeres MigraDoc-Dokument.
         *
         * Ein gespeicherter Kundencheckup
         * wird niemals geöffnet.
         */
        var comparison = new CustomerCheckupComparison
        {
            ComparisonModelVersion = 3,

            Areas = areas.ToList()
        };

        var document = new Document();

        // Originale Dokumentformatierung verwenden.
        InvokePrivate(
            "ConfigureStyles",
            document);

        var section = document.AddSection();

        InvokePrivate(
            "ConfigurePage",
            section);

        /*
         * Aufruf der tatsächlichen
         * PDF-Bereichsberichtsmethode.
         *
         * Die Methode bleibt produktiv
         * unverändert und private.
         *
         * Reflection wird ausschließlich
         * im Debug-Test verwendet.
         */
        InvokePrivate(
            "AddAreaComparison",
            section,
            comparison);

        return document;
    }

    // =====================================================
    // ZUGRIFF AUF PRODUKTIVE PDF-HILFSMETHODEN
    // =====================================================

    private static string GetPrivateStatusText(
        CustomerCheckupAreaComparisonStatus status)
    {
        var result = InvokePrivate(
            "GetAreaStatusText",
            status);

        return result as string
            ?? throw new InvalidOperationException(
                "Die PDF-Statusfunktion hat "
                + "keinen Text zurückgegeben.");
    }

    private static Color GetPrivateStatusColor(
        CustomerCheckupAreaComparisonStatus status)
    {
        var result = InvokePrivate(
            "GetAreaStatusColor",
            status);

        return result is Color color
            ? color
            : throw new InvalidOperationException(
                "Die PDF-Statusfunktion hat "
                + "keine Farbe zurückgegeben.");
    }

    private static object? InvokePrivate(
        string methodName,
        params object?[] arguments)
    {
        var method = ReportServiceType.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);

        if (method is null)
        {
            throw new InvalidOperationException(
                "Die erwartete PDF-Berichtsmethode "
                + $"'{methodName}' wurde nicht gefunden.");
        }

        try
        {
            return method.Invoke(
                null,
                arguments);
        }
        catch (TargetInvocationException exception)
        {
            throw new InvalidOperationException(
                "Fehler beim Ausführen der "
                + $"PDF-Berichtsmethode '{methodName}': "
                + (exception.InnerException?.Message
                   ?? exception.Message),
                exception.InnerException ?? exception);
        }
    }

    // =====================================================
    // TESTAUSWERTUNG
    // =====================================================

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
                "PDF-Regressionstest fehlgeschlagen: "
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
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireContains(
        string actual,
        string expected)
    {
        Require(
            actual.Contains(
                expected,
                StringComparison.OrdinalIgnoreCase),
            "Folgender Inhalt fehlt: "
            + expected);
    }

    private static void RequireNotContains(
        string actual,
        string unexpected)
    {
        Require(
            !actual.Contains(
                unexpected,
                StringComparison.OrdinalIgnoreCase),
            "Folgender Inhalt darf nicht erscheinen: "
            + unexpected);
    }
}

#endif
