
#if DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.13.25
///
/// Prüfungen für das neue, noch nicht
/// produktiv eingebundene Windows-Temp-
/// PowerShell-Skript.
///
/// Die Tests prüfen die Skriptstruktur
/// und lassen den Windows-PowerShell-
/// Parser die tatsächliche Syntax prüfen.
///
/// Das Bereinigungsskript wird NICHT
/// ausgeführt.
///
/// Keine Administratorrechte.
/// Keine Löschvorgänge.
/// Keine temporären Skriptdateien.
/// </summary>
public static class
    WindowsTemporaryFilesCleanupPowerShellScriptRegressionTests
{
    private const int ExpectedTestCount = 14;

    private const int ProcessTimeoutMilliseconds = 30000;

    private const string ParserBootstrap =
        """
        $ErrorActionPreference = 'Stop'

        try
        {
            $encodedSource =
                [System.Console]::In.ReadToEnd()

            $sourceBytes =
                [System.Convert]::FromBase64String(
                    $encodedSource)

            $sourceText =
                [System.Text.Encoding]::Unicode.GetString(
                    $sourceBytes)

            $tokens = $null
            $parseErrors = $null

            [void][System.Management.Automation.Language.Parser]::ParseInput(
                $sourceText,
                [ref]$tokens,
                [ref]$parseErrors)

            if (
                $null -ne $parseErrors -and
                $parseErrors.Length -gt 0
            )
            {
                foreach ($parseError in $parseErrors)
                {
                    [System.Console]::Error.WriteLine(
                        $parseError.ToString())
                }

                exit 1
            }

            [System.Console]::Out.WriteLine(
                'SYNTAX_OK')

            exit 0
        }
        catch
        {
            [System.Console]::Error.WriteLine(
                $_.Exception.ToString())

            exit 1
        }
        """;

    public static string RunAll()
    {
        var script = BuildScript();

        var checks = new (string Title, Action Test)[]
        {
            (
                "Skriptplatzhalter werden ersetzt",
                () => TestPlaceholders(script)
            ),
            (
                "Apostrophe in Pfaden werden maskiert",
                TestPathEscaping
            ),
            (
                "Ziel bleibt auf Windows-Temp begrenzt",
                () => TestTargetRestriction(script)
            ),
            (
                "Stammordner und Reparse Points geschützt",
                () => TestRootSafety(script)
            ),
            (
                "Einträge bleiben innerhalb des Zielordners",
                () => TestDescendantBoundary(script)
            ),
            (
                "Ordner werden nur nichtrekursiv gelöscht",
                () => TestNonRecursiveDeletion(script)
            ),
            (
                "Kontrollierter Abbruch bleibt vorhanden",
                () => TestCancellation(script)
            ),
            (
                "Geschützte Dateiattribute werden erkannt",
                () => TestProtectedAttributes(script)
            ),
            (
                "Zusätzliche Attributprüfungen vorhanden",
                () => TestFinalAttributeChecks(script)
            ),
            (
                "Klassifizierungshelfer ist eingebunden",
                () => TestClassifierIntegration(script)
            ),
            (
                "Nicht leere Ordner werden gezielt behandelt",
                () => TestDirectoryNotEmptyHandling(script)
            ),
            (
                "Ursachenzähler werden im JSON ausgegeben",
                () => TestResultContract(script)
            ),
            (
                "Gefährliche Zwangsoperationen fehlen",
                () => TestForbiddenCommands(script)
            ),
            (
                "Windows-PowerShell bestätigt gültige Syntax",
                () => TestPowerShellSyntax(script)
            )
        };

        var results = new List<string>();

        foreach (var check in checks)
        {
            try
            {
                check.Test();

                results.Add(
                    "OK: " + check.Title);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Windows-Temp-Skripttest fehlgeschlagen: "
                    + check.Title
                    + Environment.NewLine
                    + exception.Message,
                    exception);
            }
        }

        Require(
            results.Count == ExpectedTestCount,
            "Unerwartete Anzahl durchgeführter Tests.");

        return
            $"Alle {results.Count} Windows-Temp-"
            + "Skriptprüfungen erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    private static string BuildScript()
    {
        return
            WindowsTemporaryFilesCleanupPowerShellScript
                .Build(
                    @"C:\WeberIT-Test\cancel.requested",
                    @"C:\WeberIT-Test\worker-result.json");
    }

    private static void TestPlaceholders(
        string script)
    {
        RequireContains(
            script,
            @"C:\WeberIT-Test\cancel.requested",
            "Der Abbruchpfad fehlt.");

        RequireContains(
            script,
            @"C:\WeberIT-Test\worker-result.json",
            "Der Ergebnisdateipfad fehlt.");

        RequireNotContains(
            script,
            "__WEBERIT_CANCELLATION_MARKER__",
            "Der Abbruchplatzhalter wurde nicht ersetzt.");

        RequireNotContains(
            script,
            "__WEBERIT_RESULT_FILE__",
            "Der Ergebnisplatzhalter wurde nicht ersetzt.");
    }

    private static void TestPathEscaping()
    {
        var script =
            WindowsTemporaryFilesCleanupPowerShellScript
                .Build(
                    @"C:\O'Brien\cancel.requested",
                    @"C:\O'Brien\worker-result.json");

        RequireContains(
            script,
            @"C:\O''Brien\cancel.requested",
            "Der Abbruchpfad wurde nicht korrekt maskiert.");

        RequireContains(
            script,
            @"C:\O''Brien\worker-result.json",
            "Der Ergebnispfad wurde nicht korrekt maskiert.");
    }

    private static void TestTargetRestriction(
        string script)
    {
        RequireContains(
            script,
            "[System.Environment+SpecialFolder]::Windows",
            "Ermittlung des Windows-Verzeichnisses fehlt.");

        RequireContains(
            script,
            "[System.IO.Path]::Combine(",
            "Kontrollierte Zielpfadbildung fehlt.");

        RequireContains(
            script,
            "'Temp'",
            "Die feste Windows-Temp-Kennung fehlt.");

        RequireContains(
            script,
            "$normalizedTargetParent",
            "Die Prüfung des direkten Elternordners fehlt.");

        RequireContains(
            script,
            "$normalizedWindowsDirectory",
            "Die Prüfung des Windows-Verzeichnisses fehlt.");
    }

    private static void TestRootSafety(
        string script)
    {
        RequireContains(
            script,
            "[System.IO.Path]::GetPathRoot(",
            "Die Prüfung des Laufwerksstamms fehlt.");

        RequireContains(
            script,
            "$targetAttributes",
            "Die Attribute des Stammordners fehlen.");

        RequireContains(
            script,
            "Der Windows-Temp-Stammordner ist ein Umleitungs- oder Verknüpfungspunkt.",
            "Die Ablehnung eines umgeleiteten Stammordners fehlt.");
    }

    private static void TestDescendantBoundary(
        string script)
    {
        RequireContains(
            script,
            "$rootPrefix",
            "Das erlaubte Zielpfadpräfix fehlt.");

        RequireContains(
            script,
            "$candidatePath.StartsWith(",
            "Die Prüfung jedes Kandidatenpfads fehlt.");

        RequireContains(
            script,
            "[System.StringComparison]::OrdinalIgnoreCase",
            "Der Windows-Pfadvergleich fehlt.");

        RequireContains(
            script,
            "Ein zu verarbeitender Eintrag liegt außerhalb des Windows-Temp-Ordners.",
            "Die Ablehnung fremder Pfade fehlt.");
    }

    private static void TestNonRecursiveDeletion(
        string script)
    {
        RequireContains(
            script,
            "[System.IO.Directory]::Delete(",
            "Die kontrollierte Ordnerlöschung fehlt.");

        RequireContains(
            script,
            "DeleteAfterChildren",
            "Die nachgelagerte Ordnerverarbeitung fehlt.");

        RequireContains(
            script,
            "$false)",
            "Die nichtrekursive Ordnerlöschung fehlt.");
    }

    private static void TestCancellation(
        string script)
    {
        RequireContains(
            script,
            "function Test-CancellationRequested",
            "Die Abbruchfunktion fehlt.");

        RequireContains(
            script,
            "if (Test-CancellationRequested)",
            "Die Prüfung des Abbruchmarkers fehlt.");

        RequireContains(
            script,
            "$result.WasCancelled = $true",
            "Der dokumentierte Abbruchstatus fehlt.");
    }

    private static void TestProtectedAttributes(
        string script)
    {
        RequireContains(
            script,
            "function Get-ProtectedEntryReason",
            "Die zentrale Attributklassifizierung fehlt.");

        RequireContains(
            script,
            "'ReparsePoint'",
            "Die Reparse-Point-Klassifizierung fehlt.");

        RequireContains(
            script,
            "'SystemProtected'",
            "Die Systemschutz-Klassifizierung fehlt.");

        RequireContains(
            script,
            "'ReadOnly'",
            "Die Schreibschutz-Klassifizierung fehlt.");
    }

    private static void TestFinalAttributeChecks(
        string script)
    {
        RequireContains(
            script,
            "$finalFileAttributes",
            "Die erneute Dateiattributprüfung fehlt.");

        RequireContains(
            script,
            "$finalProtectionReason",
            "Die abschließende Dateischutzprüfung fehlt.");

        RequireContains(
            script,
            "$finalDirectoryAttributes",
            "Die erneute Ordnerattributprüfung fehlt.");

        RequireContains(
            script,
            "$currentDirectoryAttributes",
            "Die Attributprüfung vor dem Betreten "
            + "eines Unterordners fehlt.");
    }

    private static void TestClassifierIntegration(
        string script)
    {
        RequireContains(
            script,
            "function Get-SkipReasonFromException",
            "Die Ausnahme-Klassifizierung fehlt.");

        RequireContains(
            script,
            "function Record-FileSystemException",
            "Die zentrale Ausnahmebehandlung fehlt.");

        RequireContains(
            script,
            "Record-FileSystemException",
            "Der Aufruf der Ausnahmebehandlung fehlt.");

        RequireContains(
            script,
            "Add-SkippedEntry",
            "Die Erfassung erwarteter Ausschlüsse fehlt.");

        RequireContains(
            script,
            "$result.FailedEntryCount",
            "Die getrennte Erfassung technischer "
            + "Fehler fehlt.");
    }

    private static void TestDirectoryNotEmptyHandling(
        string script)
    {
        RequireContains(
            script,
            "-AllowDirectoryNotEmpty $true",
            "Die explizite Freigabe für nicht leere "
            + "Ordner bei der Ordnerlöschung fehlt.");

        RequireContains(
            script,
            "'DirectoryNotEmpty'",
            "Der passende Ursachencode fehlt.");
    }

    private static void TestResultContract(
        string script)
    {
        RequireContains(
            script,
            "SkippedEntryCount = [int64]0",
            "Der Gesamtzähler übersprungener "
            + "Einträge fehlt.");

        RequireContains(
            script,
            "SkippedEntryReasons = @()",
            "Die JSON-Ursachenliste fehlt.");

        RequireContains(
            script,
            "$result.SkippedEntryReasons",
            "Die Ergebnisübernahme der Ursachen fehlt.");

        RequireContains(
            script,
            "Get-SkippedReasonRecords",
            "Die Erstellung der Ursachenliste fehlt.");

        RequireContains(
            script,
            "ConvertTo-Json -Compress -Depth 4",
            "Die JSON-Ergebnisausgabe fehlt.");

        RequireContains(
            script,
            "Write-WorkerResult",
            "Die Ergebnisübertragung fehlt.");
    }

    private static void TestForbiddenCommands(
        string script)
    {
        string[] forbiddenCommands =
        [
            "Remove-Item -Recurse",
            "Remove-Item -Force",
            "takeown.exe",
            "icacls.exe",
            "Stop-Process",
            "Set-Acl",
            "Clear-Content"
        ];

        foreach (var command in forbiddenCommands)
        {
            RequireNotContains(
                script,
                command,
                "Das Skript enthält eine nicht freigegebene "
                + "Operation: "
                + command);
        }
    }

    // =============================================
    // ECHTE POWERSHELL-SYNTAXPRÜFUNG
    // =============================================

    private static void TestPowerShellSyntax(
        string script)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Die PowerShell-Syntaxprüfung "
                + "benötigt Windows.");
        }

        var windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        if (string.IsNullOrWhiteSpace(
            windowsDirectory))
        {
            throw new InvalidOperationException(
                "Das Windows-Verzeichnis wurde "
                + "nicht gefunden.");
        }

        var powerShellPath =
            Path.Combine(
                windowsDirectory,
                "System32",
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");

        if (!File.Exists(powerShellPath))
        {
            throw new FileNotFoundException(
                "Windows-PowerShell wurde nicht gefunden.",
                powerShellPath);
        }

        /*
         * Der kurze Parser-Bootstrap wird
         * als Startargument übertragen.
         *
         * Das große Bereinigungsskript
         * kommt über stdin.
         *
         * Der Parser analysiert den Text,
         * führt ihn aber NICHT aus.
         */
        var encodedBootstrap =
            Convert.ToBase64String(
                Encoding.Unicode.GetBytes(
                    ParserBootstrap));

        var encodedScript =
            Convert.ToBase64String(
                Encoding.Unicode.GetBytes(
                    script));

        var startInfo =
            new ProcessStartInfo
            {
                FileName = powerShellPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

        startInfo.ArgumentList.Add(
            "-NoProfile");

        startInfo.ArgumentList.Add(
            "-NonInteractive");

        startInfo.ArgumentList.Add(
            "-EncodedCommand");

        startInfo.ArgumentList.Add(
            encodedBootstrap);

        using var process =
            new Process
            {
                StartInfo = startInfo
            };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "Der PowerShell-Parser konnte "
                + "nicht gestartet werden.");
        }

        var outputTask =
            process.StandardOutput.ReadToEndAsync();

        var errorTask =
            process.StandardError.ReadToEndAsync();

        process.StandardInput.Write(
            encodedScript);

        process.StandardInput.Close();

        if (!process.WaitForExit(
            ProcessTimeoutMilliseconds))
        {
            try
            {
                process.Kill(
                    entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Prozess ist bereits beendet.
            }

            throw new InvalidOperationException(
                "Die PowerShell-Syntaxprüfung "
                + "hat das Zeitlimit überschritten.");
        }

        var output =
            outputTask
                .GetAwaiter()
                .GetResult();

        var error =
            errorTask
                .GetAwaiter()
                .GetResult();

        Require(
            process.ExitCode == 0,
            "Der PowerShell-Parser meldet einen Fehler:"
            + Environment.NewLine
            + error
            + Environment.NewLine
            + output);

        Require(
            output
                .Split(
                    [
                        "\r\n",
                        "\n",
                        "\r"
                    ],
                    StringSplitOptions.RemoveEmptyEntries)
                .Any(
                    line =>
                        string.Equals(
                            line.Trim(),
                            "SYNTAX_OK",
                            StringComparison.Ordinal)),
            "Der PowerShell-Parser hat keine "
            + "gültige Syntax bestätigt."
            + Environment.NewLine
            + output
            + Environment.NewLine
            + error);
    }

    // =============================================
    // TESTHILFSMETHODEN
    // =============================================

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

    private static void RequireNotContains(
        string source,
        string forbidden,
        string message)
    {
        Require(
            !source.Contains(
                forbidden,
                StringComparison.OrdinalIgnoreCase),
            message);
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
