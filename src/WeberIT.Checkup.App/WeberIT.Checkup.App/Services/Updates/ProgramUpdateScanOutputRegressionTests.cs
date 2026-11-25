#if DEBUG

using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Updates;

/// <summary>
/// Reale Parser- und Ergebnislogik mit künstlichen Prozessausgaben.
/// Startet weder WinGet noch sonstige Prozesse.
/// </summary>
public static class ProgramUpdateScanOutputRegressionTests
{
    private const string GermanEmpty =
        "Es wurde kein installiertes Paket gefunden, das den Eingabekriterien entspricht.";

    private const string GermanUnknown =
        "Mindestens 1 Paket verfügt über Versionsnummern, die nicht ermittelt werden können. Verwenden Sie „--include-unknown“, um alle Ergebnisse anzuzeigen.";

    private const string EnglishEmpty =
        "No installed package found matching input criteria.";

    private const string EnglishUnknown =
        "10 package(s) have version numbers that cannot be determined. Use --include-unknown to see all results.";

    public static string RunAll()
    {
        var results = new List<string>();

        void Run(string title, Action test)
        {
            try
            {
                test();
                results.Add("OK: " + title);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "WinGet-Ausgabe-Regressionstest fehlgeschlagen: " + title, exception);
            }
        }

        Run("Deutsche Praxisausgabe ergibt bestaetigten begrenzten Nullbefund", () =>
        {
            var info = Evaluate(GermanEmpty + "\r\n" + GermanUnknown);
            RequireZero(info);
            Require(info.AnalysisDetails.Contains("nicht ermittelbaren Versionsnummern")
                && info.AnalysisDetails.Contains("nicht zuverlässig bewertet"),
                "Die Einschraenkung unbekannter Versionen muss sichtbar bleiben.");
        });

        Run("Englische Ausgabe mit unbekannten Versionen wird erkannt", () =>
        {
            var info = Evaluate(EnglishEmpty + "\n" + EnglishUnknown);
            RequireZero(info);
            Require(info.AnalysisDetails.Contains("nicht ermittelbaren Versionsnummern"),
                "Englischer Hinweis muss erhalten bleiben.");
        });

        Run("Eindeutige leere Liste ohne Zusatzhinweis", () =>
            RequireZero(Evaluate("\r-\r" + GermanEmpty + "\r\n")));

        Run("Unbekannte Versionen ohne Nullmeldung sind kein bestaetigtes Ergebnis", () =>
            RequireFailure(Evaluate(GermanUnknown)));

        Run("Neuer Sonderfall gilt ausschliesslich bei Exitcode 0", () =>
        {
            RequireFailure(Evaluate(GermanEmpty, exitCode: unchecked((int)0x8A15002B)));
            RequireFailure(Evaluate(GermanEmpty, exitCode: 1));
            RequireFailure(Evaluate(GermanEmpty, exitCode: null));
        });

        Run("Fehlerausgabe und Prozessfehler verbieten Nullbefund", () =>
        {
            RequireFailure(Evaluate(GermanEmpty, errorOutput: "Source unavailable"));
            RequireFailure(Evaluate(GermanEmpty, processError: "Process error"));
        });

        Run("Nicht gestartete und zeitlich abgebrochene Abfragen bleiben Fehler", () =>
        {
            RequireFailure(Evaluate(GermanEmpty, wasStarted: false));
            RequireFailure(Evaluate(GermanEmpty, timedOut: true));
        });

        Run("Widerspruechliche Summen und unbekannte Warnungen werden abgelehnt", () =>
        {
            RequireFailure(Evaluate(GermanEmpty + "\n1 upgrades available."));
            RequireFailure(Evaluate(GermanEmpty + "\nQuelle nicht erreichbar"));
            RequireFailure(Evaluate(GermanEmpty + "\nFehler --include-unknown Versionsnummern"));
            RequireFailure(Evaluate(GermanEmpty + "\n" + GermanEmpty));
        });

        Run("Leere Ausgabe und unvollstaendige Tabelle bleiben nicht auswertbar", () =>
        {
            RequireFailure(Evaluate(""));
            RequireFailure(Evaluate(GermanEmpty + "\nName  Id  Version  Available\n----------\nTest  Test.App  1  2"));
        });

        Run("Bisheriges bestaetigtes Nullergebnis bleibt gueltig", () =>
        {
            RequireZero(Evaluate("No applicable upgrade found."));
            RequireZero(Evaluate("No applicable upgrade found.", exitCode: unchecked((int)0x8A15002B)));
            RequireFailure(Evaluate("No applicable upgrade found.", exitCode: unchecked((int)0x8A15002B),
                errorOutput: "Source unavailable"));
        });

        return $"Alle {results.Count} WinGet-Ausgabe-Regressionstests erfolgreich."
            + Environment.NewLine + string.Join(Environment.NewLine, results);
    }

    private static ProgramUpdateInformation Evaluate(string output,
        int? exitCode = 0, string errorOutput = "", bool wasStarted = true,
        bool timedOut = false, string processError = "") =>
        ProgramUpdateInformationProvider.EvaluateUpgradeListingForRegression(
            output, exitCode, errorOutput, wasStarted, timedOut, processError);

    private static void RequireZero(ProgramUpdateInformation info)
    {
        Require(info.IsAnalysisPerformed && info.IsAnalysisSuccessful
            && info.AvailableUpdateCount == 0 && info.AvailableUpdates.Count == 0
            && !info.IsResultTruncated, "Bestaetigtes Nullergebnis erwartet.");
    }

    private static void RequireFailure(ProgramUpdateInformation info) =>
        Require(!info.IsAnalysisSuccessful, "Nicht belastbare Ausgabe wurde akzeptiert.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

#endif
