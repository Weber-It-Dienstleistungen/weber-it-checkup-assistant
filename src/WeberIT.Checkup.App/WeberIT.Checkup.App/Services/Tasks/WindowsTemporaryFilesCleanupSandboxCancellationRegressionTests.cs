
#if DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.13.37
///
/// Erster tatsächlicher PowerShell-Verhaltenstest
/// des Windows-Temp-Bereinigungsskripts.
///
/// Getestet wird ausschließlich der sofortige
/// Abbruch in einer isolierten Sandbox.
///
/// Der Abbruchmarker wird angelegt, bevor
/// der PowerShell-Prozess gestartet wird.
///
/// Keine Administratorrechte erlaubt.
/// Kein Zugriff auf produktives Windows-Temp.
/// Keine rekursiven Bereinigungsoperationen.
/// </summary>
public static class
    WindowsTemporaryFilesCleanupSandboxCancellationRegressionTests
{
    private const int ProcessTimeoutMilliseconds = 30000;

    private const string ExpectedContent =
        "WEBER_IT_SANDBOX_TEST_DATEI";

    private const string OutsideContent =
        "WEBER_IT_AUSSERHALB_DES_TESTZIELS";

    private const string BootstrapScript =
        """
        $ErrorActionPreference = 'Stop'

        try
        {
            $encodedSource =
                [System.Console]::In.ReadToEnd()

            $sourceBytes =
                [System.Convert]::FromBase64String(
                    $encodedSource)

            $source =
                [System.Text.Encoding]::Unicode.GetString(
                    $sourceBytes)

            $scriptBlock =
                [System.Management.Automation.ScriptBlock]::Create(
                    $source)

            & $scriptBlock

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
        EnsureSafeExecutionEnvironment();

        using var sandbox = new TestSandbox();

        var script =
            WindowsTemporaryFilesCleanupSandboxScript
                .Build(
                    sandbox.WindowsDirectory,
                    sandbox.CancellationPath,
                    sandbox.ResultPath);

        var standardOutput =
            ExecutePowerShell(script);

        if (!File.Exists(sandbox.ResultPath))
        {
            throw new InvalidOperationException(
                "Das PowerShell-Skript hat keine "
                + "Ergebnisdatei erzeugt.");
        }

        using var document =
            JsonDocument.Parse(
                File.ReadAllText(sandbox.ResultPath));

        var result = document.RootElement;

        var results = new List<string>();

        Execute(
            "Abbruchmarker beendet die Bereinigung",
            () => TestCancellation(result),
            results);

        Execute(
            "Zielpfad bleibt auf Sandbox begrenzt",
            () => TestTargetPath(result, sandbox),
            results);

        Execute(
            "Testdatei innerhalb des Zielbereichs bleibt erhalten",
            () => TestInsideFile(sandbox),
            results);

        Execute(
            "Datei außerhalb des Zielbereichs bleibt erhalten",
            () => TestOutsideFile(sandbox),
            results);

        Execute(
            "PowerShell-Ergebnis wird zusätzlich ausgegeben",
            () => TestStandardOutput(
                standardOutput,
                result),
            results);

        return
            $"Alle {results.Count} Sandbox-Abbruchtests "
            + "erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // AUSFÜHRUNGSUMGEBUNG
    // =============================================

    private static void EnsureSafeExecutionEnvironment()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Der Sandbox-Verhaltenstest "
                + "benötigt Windows.");
        }

        /*
         * Ein erhöhter Debug-Prozess ist
         * für diesen Test nicht erlaubt.
         *
         * Dadurch startet auch die
         * untergeordnete PowerShell nicht
         * mit Administratorrechten.
         */
        using var identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(identity);

        if (principal.IsInRole(
                WindowsBuiltInRole.Administrator))
        {
            throw new InvalidOperationException(
                "Der Sandbox-Verhaltenstest darf "
                + "nicht aus einer als Administrator "
                + "gestarteten Visual-Studio-Instanz "
                + "ausgeführt werden.");
        }
    }

    // =============================================
    // ABRUCHERGEBNIS PRÜFEN
    // =============================================

    private static void TestCancellation(
        JsonElement result)
    {
        Require(
            result.GetProperty("WasStarted")
                .GetBoolean(),
            "Der Worker hat keinen Start gemeldet.");

        Require(
            result.GetProperty("WasCancelled")
                .GetBoolean(),
            "Der Abbruchmarker wurde "
            + "nicht berücksichtigt.");

        Require(
            result.GetProperty("DeletedFileCount")
                .GetInt64() == 0,
            "Trotz Abbruch wurden "
            + "Dateilöschungen gemeldet.");

        Require(
            result.GetProperty("DeletedDirectoryCount")
                .GetInt64() == 0,
            "Trotz Abbruch wurden "
            + "Ordnerlöschungen gemeldet.");

        Require(
            result.GetProperty("FailedEntryCount")
                .GetInt64() == 0,
            "Der Abbruch wurde fälschlich "
            + "als technischer Fehler erfasst.");

        Require(
            result.GetProperty("SkippedEntryCount")
                .GetInt64() == 0,
            "Trotz sofortigem Abbruch wurden "
            + "Ausschlüsse gemeldet.");

        Require(
            string.IsNullOrEmpty(
                result.GetProperty("ErrorMessage")
                    .GetString()),
            "Das Abbruchergebnis enthält "
            + "eine unerwartete Fehlermeldung.");
    }

    private static void TestTargetPath(
        JsonElement result,
        TestSandbox sandbox)
    {
        var reportedPath =
            result.GetProperty("TargetPath")
                .GetString();

        Require(
            !string.IsNullOrWhiteSpace(reportedPath),
            "Der Zielpfad fehlt im Ergebnis.");

        var expected =
            NormalizePath(sandbox.TargetDirectory);

        var actual =
            NormalizePath(reportedPath!);

        Require(
            string.Equals(
                expected,
                actual,
                StringComparison.OrdinalIgnoreCase),
            "Die PowerShell-Ausführung meldet "
            + "einen anderen als den "
            + "freigegebenen Sandbox-Zielpfad.");
    }

    private static void TestInsideFile(
        TestSandbox sandbox)
    {
        Require(
            File.Exists(sandbox.InsideFile),
            "Die Testdatei innerhalb der "
            + "Sandbox wurde entfernt.");

        Require(
            File.ReadAllText(sandbox.InsideFile)
                == ExpectedContent,
            "Die Testdatei innerhalb "
            + "der Sandbox wurde verändert.");
    }

    private static void TestOutsideFile(
        TestSandbox sandbox)
    {
        Require(
            File.Exists(sandbox.OutsideFile),
            "Die Datei außerhalb des "
            + "Testzielbereichs wurde entfernt.");

        Require(
            File.ReadAllText(sandbox.OutsideFile)
                == OutsideContent,
            "Die Datei außerhalb des "
            + "Testzielbereichs wurde verändert.");
    }

    private static void TestStandardOutput(
        string standardOutput,
        JsonElement result)
    {
        var expectedJson =
            result.GetRawText();

        Require(
            standardOutput.Contains(
                expectedJson,
                StringComparison.Ordinal),
            "Das JSON-Ergebnis wurde nicht "
            + "vollständig über die "
            + "Standardausgabe übertragen.");
    }

    // =============================================
    // POWERSHELL STARTEN
    // =============================================

    private static string ExecutePowerShell(
        string source)
    {
        var windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

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
                "Windows-PowerShell wurde "
                + "nicht gefunden.",
                powerShellPath);
        }

        var bootstrapBase64 =
            Convert.ToBase64String(
                Encoding.Unicode.GetBytes(
                    BootstrapScript));

        var sourceBase64 =
            Convert.ToBase64String(
                Encoding.Unicode.GetBytes(
                    source));

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

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(bootstrapBase64);

        using var process =
            new Process
            {
                StartInfo = startInfo
            };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "Windows-PowerShell konnte für "
                + "den Sandbox-Test nicht "
                + "gestartet werden.");
        }

        var outputTask =
            process.StandardOutput.ReadToEndAsync();

        var errorTask =
            process.StandardError.ReadToEndAsync();

        process.StandardInput.Write(sourceBase64);
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
                // Prozess bereits beendet.
            }

            throw new InvalidOperationException(
                "Die Sandbox-Ausführung "
                + "hat das Zeitlimit überschritten.");
        }

        var output =
            outputTask.GetAwaiter().GetResult();

        var error =
            errorTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Das Sandbox-Skript meldet "
                + "einen technischen Fehler."
                + Environment.NewLine
                + "Exitcode: "
                + process.ExitCode
                + Environment.NewLine
                + error
                + Environment.NewLine
                + output);
        }

        return output;
    }

    // =============================================
    // TESTVERZEICHNIS
    // =============================================

    private sealed class TestSandbox : IDisposable
    {
        public string RootDirectory { get; }

        public string WindowsDirectory { get; }

        public string TargetDirectory { get; }

        public string CancellationPath { get; }

        public string ResultPath { get; }

        public string InsideFile { get; }

        public string OutsideFile { get; }

        public TestSandbox()
        {
            RootDirectory =
                Path.Combine(
                    Path.GetFullPath(Path.GetTempPath()),
                    "WeberIT.Checkup",
                    "WindowsTempSandbox",
                    Guid.NewGuid().ToString("N"));

            WindowsDirectory =
                Path.Combine(
                    RootDirectory,
                    "Windows");

            TargetDirectory =
                Path.Combine(
                    WindowsDirectory,
                    "Temp");

            CancellationPath =
                Path.Combine(
                    RootDirectory,
                    "cancel.requested");

            ResultPath =
                Path.Combine(
                    RootDirectory,
                    "worker-result.json");

            InsideFile =
                Path.Combine(
                    TargetDirectory,
                    "test-content.txt");

            OutsideFile =
                Path.Combine(
                    RootDirectory,
                    "outside-sentinel.txt");

            Directory.CreateDirectory(
                TargetDirectory);

            File.WriteAllText(
                InsideFile,
                ExpectedContent);

            File.WriteAllText(
                OutsideFile,
                OutsideContent);

            /*
             * Entscheidend:
             *
             * Der Abbruchmarker existiert
             * bereits vor dem Start
             * des PowerShell-Prozesses.
             */
            File.WriteAllText(
                CancellationPath,
                "CANCEL");
        }

        public void Dispose()
        {
            /*
             * Ausschließlich namentlich
             * bekannte Testdateien entfernen.
             *
             * Keine rekursive Löschung.
             */
            TryDeleteFile(ResultPath);
            TryDeleteFile(CancellationPath);
            TryDeleteFile(InsideFile);
            TryDeleteFile(OutsideFile);

            TryDeleteEmptyDirectory(
                TargetDirectory);

            TryDeleteEmptyDirectory(
                WindowsDirectory);

            TryDeleteEmptyDirectory(
                RootDirectory);
        }

        private static void TryDeleteFile(
            string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                var attributes =
                    File.GetAttributes(path);

                if ((attributes
                    & FileAttributes.ReparsePoint) != 0)
                {
                    return;
                }

                File.Delete(path);
            }
            catch (IOException)
            {
                // Testartefakt bleibt erhalten.
            }
            catch (UnauthorizedAccessException)
            {
                // Keine Rechteausweitung.
            }
        }

        private static void TryDeleteEmptyDirectory(
            string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    return;
                }

                var attributes =
                    File.GetAttributes(path);

                if ((attributes
                    & FileAttributes.ReparsePoint) != 0)
                {
                    return;
                }

                Directory.Delete(
                    path,
                    recursive: false);
            }
            catch (IOException)
            {
                // Kein erzwungenes Entfernen.
            }
            catch (UnauthorizedAccessException)
            {
                // Keine Rechteausweitung.
            }
        }
    }

    // =============================================
    // TESTHILFSMETHODEN
    // =============================================

    private static string NormalizePath(
        string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
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
                "Sandbox-Abbruchtest fehlgeschlagen: "
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
