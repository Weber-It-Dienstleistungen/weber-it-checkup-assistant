
#if DEBUG

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.13.28a
///
/// Struktur- und Vertragstests für den
/// erhöhten Windows-Temp-Executor.
///
/// Die Tests erzeugen lediglich den
/// PowerShell-Skripttext im Arbeitsspeicher
/// und prüfen die JSON-Auswertung.
///
/// Es wird kein PowerShell-Prozess gestartet,
/// keine Administratorfreigabe angefordert
/// und keine Datei geöffnet oder gelöscht.
///
/// WICHTIG:
/// Strukturtests ersetzen keine spätere
/// Prüfung des tatsächlichen Skriptverhaltens.
/// </summary>
public static class
    WindowsTemporaryFilesCleanupBaselineRegressionTests
{
    private static readonly Type ExecutorType =
        typeof(WindowsTemporaryFilesCleanupExecutor);

    public static string RunAll()
    {
        var results = new List<string>();

        Execute(
            "Windows-Temp-Ziel wird ausdrücklich begrenzt",
            TestTargetRestrictions,
            results);

        Execute(
            "Stammordner und Reparse Points werden geschützt",
            TestRootSafety,
            results);

        Execute(
            "Einträge bleiben auf Zielordner begrenzt",
            TestDescendantRestriction,
            results);

        Execute(
            "Ordner werden nicht rekursiv erzwungen",
            TestNonRecursiveDeletion,
            results);

        Execute(
            "Abbruchmarker bleibt Bestandteil des Ablaufs",
            TestCancellationContract,
            results);

        Execute(
            "Erfolgs- und Fehlerzähler bleiben vorhanden",
            TestResultCounters,
            results);

        Execute(
            "PowerShell-Pfade werden sicher maskiert",
            TestPowerShellLiteralEscaping,
            results);

        Execute(
            "Startskript enthält Integritätsprüfung",
            TestVerifiedLauncher,
            results);

        Execute(
            "Ergebnis wird als JSON übertragen",
            TestJsonResultContract,
            results);

        Execute(
            "Bisheriges Ergebnis-JSON bleibt lesbar",
            TestLegacyJson,
            results);

        Execute(
            "JSON aus Standardausgabe bleibt lesbar",
            TestStandardOutputFallback,
            results);

        Execute(
            "Ungültiges JSON wird zurückgewiesen",
            TestInvalidJson,
            results);

        return
            $"Alle {results.Count} Windows-Temp-Basistests "
            + "erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    private static void TestTargetRestrictions()
    {
        var script = BuildScript();

        RequireContains(
            script,
            "[System.Environment+SpecialFolder]::Windows",
            "Ermittlung des Windows-Verzeichnisses fehlt.");

        RequireContains(
            script,
            "'Temp'",
            "Der ausdrücklich erlaubte Temp-Ordner fehlt.");

        RequireContains(
            script,
            "$normalizedTargetParent",
            "Prüfung des direkten Elternordners fehlt.");

        RequireContains(
            script,
            "$normalizedWindowsDirectory",
            "Vergleich mit dem Windows-Verzeichnis fehlt.");
    }

    private static void TestRootSafety()
    {
        var script = BuildScript();

        RequireContains(
            script,
            "[System.IO.Path]::GetPathRoot(",
            "Prüfung des Laufwerksstamms fehlt.");

        RequireContains(
            script,
            "[System.IO.File]::GetAttributes(",
            "Attributprüfung fehlt.");

        RequireContains(
            script,
            "[System.IO.FileAttributes]::ReparsePoint",
            "Schutz vor Reparse Points fehlt.");
    }

    private static void TestDescendantRestriction()
    {
        var script = BuildScript();

        RequireContains(
            script,
            "$rootPrefix",
            "Das erforderliche Zielpfadpräfix fehlt.");

        RequireContains(
            script,
            "$candidatePath.StartsWith(",
            "Die Nachkommenprüfung fehlt.");

        RequireContains(
            script,
            "[System.StringComparison]::OrdinalIgnoreCase",
            "Der vorgesehene Pfadvergleich fehlt.");
    }

    private static void TestNonRecursiveDeletion()
    {
        var script = BuildScript();

        RequireContains(
            script,
            "[System.IO.Directory]::Delete(",
            "Das kontrollierte Entfernen leerer Ordner fehlt.");

        RequireContains(
            script,
            "$false)",
            "Die nichtrekursive Löschung fehlt.");

        RequireContains(
            script,
            "DeleteAfterChildren",
            "Die Verarbeitung nach den Kindeinträgen fehlt.");

        Require(
            !script.Contains(
                "Remove-Item -Recurse",
                StringComparison.OrdinalIgnoreCase),
            "Es wurde ein rekursiver PowerShell-"
            + "Löschbefehl gefunden.");
    }

    private static void TestCancellationContract()
    {
        var script = BuildScript();

        RequireContains(
            script,
            "function Test-CancellationRequested",
            "Die Abbruchprüfung fehlt.");

        RequireContains(
            script,
            "$result.WasCancelled",
            "Der dokumentierte Abbruchstatus fehlt.");

        RequireContains(
            script,
            "if (Test-CancellationRequested)",
            "Die Prüfung des Abbruchmarkers fehlt.");
    }

    private static void TestResultCounters()
    {
        var script = BuildScript();

        RequireContains(
            script,
            "DeletedFileCount = [int64]0",
            "Dateizähler fehlt.");

        RequireContains(
            script,
            "DeletedDirectoryCount = [int64]0",
            "Ordnerzähler fehlt.");

        RequireContains(
            script,
            "DeletedSizeBytes = [uint64]0",
            "Größenzähler fehlt.");

        RequireContains(
            script,
            "FailedEntryCount = [int64]0",
            "Technischer Fehlerzähler fehlt.");

        RequireContains(
            script,
            "SkippedEntryCount = [int64]0",
            "Zähler übersprungener Einträge fehlt.");
    }

    private static void TestPowerShellLiteralEscaping()
    {
        var script = BuildScript(
            @"C:\Test\O'Brien\cancel.requested",
            @"C:\Test\O'Brien\result.json");

        RequireContains(
            script,
            "O''Brien",
            "Apostrophe im Pfad wurden nicht maskiert.");

        Require(
            !script.Contains(
                "__WEBERIT_CANCELLATION_MARKER__",
                StringComparison.Ordinal),
            "Der Abbruchplatzhalter wurde nicht ersetzt.");

        Require(
            !script.Contains(
                "__WEBERIT_RESULT_FILE__",
                StringComparison.Ordinal),
            "Der Ergebnisplatzhalter wurde nicht ersetzt.");
    }

    private static void TestVerifiedLauncher()
    {
        var method = GetMethod(
            "BuildVerifiedLauncherScript");

        var result = method.Invoke(
            null,
            [
                @"C:\Test\cleanup.ps1",
                "ABCDEF123456"
            ]);

        Require(
            result is string,
            "Das Startskript konnte nicht erzeugt werden.");

        var script = (string)result!;

        RequireContains(
            script,
            "$expectedHash",
            "Der erwartete Hashwert fehlt.");

        RequireContains(
            script,
            "ComputeHash(",
            "Die Hashberechnung fehlt.");

        RequireContains(
            script,
            "$actualHash",
            "Der tatsächliche Hashwert fehlt.");

        RequireContains(
            script,
            "& $scriptPath",
            "Der geprüfte Skriptstart fehlt.");
    }

    private static void TestJsonResultContract()
    {
        var script = BuildScript();

        RequireContains(
            script,
            "function Write-WorkerResult",
            "Die Ergebnisausgabe fehlt.");

        RequireContains(
            script,
            "ConvertTo-Json -Compress -Depth 4",
            "Die JSON-Serialisierung fehlt.");

        RequireContains(
            script,
            "[System.IO.File]::WriteAllText(",
            "Die Ergebnisdatei wird nicht geschrieben.");

        /*
         * Schritt 55.13.28a
         *
         * Die alte Prüfung erwartete exakt:
         *
         * [System.Console]::Out.WriteLine($json)
         *
         * Das neue Skript schreibt denselben
         * Aufruf über mehrere Zeilen.
         *
         * Wir erlauben deshalb ausschließlich
         * zusätzlichen Leerraum an den
         * syntaktischen Trennstellen.
         *
         * Der konkrete Methodenaufruf und
         * das Argument $json bleiben Pflicht.
         */
        Require(
            Regex.IsMatch(
                script,
                @"\[System\.Console\]::Out\.WriteLine\s*\(\s*\$json\s*\)",
                RegexOptions.CultureInvariant),
            "Die zusätzliche Standardausgabe fehlt.");
    }

    private static void TestLegacyJson()
    {
        const string json =
            """
            {
              "WasStarted": true,
              "WasCancelled": false,
              "TargetPath": "C:\\Windows\\Temp",
              "DeletedFileCount": 2,
              "DeletedDirectoryCount": 1,
              "DeletedSizeBytes": 2048,
              "FailedEntryCount": 0,
              "SkippedEntryCount": 3,
              "ErrorMessage": ""
            }
            """;

        var worker = DeserializeWorker(json);

        Require(
            ReadLong(worker, "DeletedFileCount") == 2,
            "Der Dateizähler wurde nicht übernommen.");

        Require(
            ReadLong(worker, "SkippedEntryCount") == 3,
            "Der Überspringzähler ging verloren.");
    }

    private static void TestStandardOutputFallback()
    {
        const string json =
            """
            {"WasStarted":true,"SkippedEntryCount":4}
            """;

        var method = GetMethod(
            "TryParseWorkerResultFromOutput");

        object?[] arguments =
        [
            "PowerShell-Startmeldung"
            + Environment.NewLine
            + json,
            null
        ];

        var success = method.Invoke(
            null,
            arguments);

        Require(
            success is true,
            "Die JSON-Standardausgabe "
            + "wurde nicht erkannt.");

        Require(
            arguments[1] is not null,
            "Das gelesene Ergebnis fehlt.");

        Require(
            ReadLong(
                arguments[1]!,
                "SkippedEntryCount") == 4,
            "Der Überspringzähler ging beim "
            + "Fallback verloren.");
    }

    private static void TestInvalidJson()
    {
        var method = GetMethod(
            "TryDeserializeWorkerResult");

        object?[] arguments =
        [
            "Das ist kein JSON-Ergebnis",
            null
        ];

        var success = method.Invoke(
            null,
            arguments);

        Require(
            success is false,
            "Ungültiges JSON wurde akzeptiert.");
    }

    private static string BuildScript(
        string cancellationPath =
            @"C:\Test\cancel.requested",
        string resultPath =
            @"C:\Test\worker-result.json")
    {
        var method = GetMethod(
            "BuildCleanupScript");

        var result = method.Invoke(
            null,
            [
                cancellationPath,
                resultPath
            ]);

        return result as string
            ?? throw new InvalidOperationException(
                "Das Bereinigungsskript wurde "
                + "nicht als Text zurückgegeben.");
    }

    private static object DeserializeWorker(
        string json)
    {
        var method = GetMethod(
            "TryDeserializeWorkerResult");

        object?[] arguments =
        [
            json,
            null
        ];

        var success = method.Invoke(
            null,
            arguments);

        Require(
            success is true,
            "Das Ergebnis-JSON konnte "
            + "nicht gelesen werden.");

        return arguments[1]
            ?? throw new InvalidOperationException(
                "Das deserialisierte Ergebnis fehlt.");
    }

    private static MethodInfo GetMethod(
        string methodName)
    {
        return ExecutorType.GetMethod(
                   methodName,
                   BindingFlags.NonPublic
                   | BindingFlags.Static)
               ?? throw new InvalidOperationException(
                   "Die interne Methode wurde "
                   + "nicht gefunden: "
                   + methodName);
    }

    private static long ReadLong(
        object instance,
        string propertyName)
    {
        var property = instance
            .GetType()
            .GetProperty(propertyName);

        Require(
            property is not null,
            "Die Ergebniseigenschaft fehlt: "
            + propertyName);

        var value = property!.GetValue(instance);

        Require(
            value is long,
            "Der Ergebniszähler hat "
            + "einen unerwarteten Typ.");

        return (long)value!;
    }

    private static void RequireContains(
        string source,
        string expected,
        string message)
    {
        Require(
            source.Contains(
                expected,
                StringComparison.Ordinal),
            message);
    }

    private static void Execute(
        string title,
        Action test,
        ICollection<string> results)
    {
        try
        {
            test();
            results.Add("OK: " + title);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Windows-Temp-Basistest fehlgeschlagen: "
                + title
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
}

#endif
