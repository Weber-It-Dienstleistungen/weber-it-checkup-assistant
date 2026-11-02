
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
/// Sprint 55.13.39
///
/// Prüft den tatsächlichen Löschablauf des
/// Windows-Temp-PowerShell-Skripts ausschließlich
/// mit selbst erstellten Sandbox-Testdaten.
///
/// Keine Administratorrechte.
/// Keine produktiven Windows-Temp-Dateien.
/// Keine rekursiven Zwangslöschungen.
/// </summary>
public static class
    WindowsTemporaryFilesCleanupSandboxDeletionRegressionTests
{
    private const int ProcessTimeoutMilliseconds = 30000;

    private const string RootFileContent =
        "WEBER_IT_SANDBOX_ROOT_FILE";

    private const string NestedFileContent =
        "WEBER_IT_SANDBOX_NESTED_FILE";

    private const string ProtectedFileContent =
        "WEBER_IT_SANDBOX_READ_ONLY";

    private const string OutsideFileContent =
        "WEBER_IT_OUTSIDE_SENTINEL";

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
        EnsureNonElevated();

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
                "Die Sandbox-Bereinigung hat "
                + "keine Ergebnisdatei erzeugt.");
        }

        using var document =
            JsonDocument.Parse(
                File.ReadAllText(sandbox.ResultPath));

        var result = document.RootElement;

        var results = new List<string>();

        Execute(
            "Löschlauf meldet ausschließlich Sandbox-Ziel",
            () => TestExecution(result, sandbox),
            results);

        Execute(
            "Normale Testdateien werden entfernt",
            () => TestFilesDeleted(sandbox),
            results);

        Execute(
            "Leere Testunterordner werden entfernt",
            () => TestDirectoriesDeleted(sandbox),
            results);

        Execute(
            "Schreibgeschützte Testdatei bleibt erhalten",
            () => TestProtectedFile(result, sandbox),
            results);

        Execute(
            "Datei außerhalb des Zielordners bleibt erhalten",
            () => TestOutsideFile(sandbox),
            results);

        Execute(
            "Löschzähler und JSON-Ausgabe stimmen überein",
            () => TestResultCounters(
                result,
                standardOutput),
            results);

        return
            $"Alle {results.Count} Sandbox-Löschtests "
            + "erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // AUSFÜHRUNGSRECHTE
    // =============================================

    private static void EnsureNonElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Dieser Test benötigt Windows.");
        }

        using var identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(identity);

        if (principal.IsInRole(
                WindowsBuiltInRole.Administrator))
        {
            throw new InvalidOperationException(
                "SICHERHEITSSTOPP: Der Sandbox-"
                + "Löschtest darf nicht mit "
                + "Administratorrechten laufen.");
        }
    }

    // =============================================
    // ERGEBNISPRÜFUNGEN
    // =============================================

    private static void TestExecution(
        JsonElement result,
        TestSandbox sandbox)
    {
        Require(
            result.GetProperty("WasStarted")
                .GetBoolean(),
            "Der Löschlauf wurde nicht gestartet.");

        Require(
            !result.GetProperty("WasCancelled")
                .GetBoolean(),
            "Der Löschlauf wurde unerwartet abgebrochen.");

        var reportedPath =
            result.GetProperty("TargetPath")
                .GetString();

        Require(
            !string.IsNullOrWhiteSpace(reportedPath),
            "Der Zielpfad fehlt.");

        Require(
            string.Equals(
                NormalizePath(reportedPath!),
                NormalizePath(sandbox.TargetDirectory),
                StringComparison.OrdinalIgnoreCase),
            "PowerShell meldet einen nicht "
            + "freigegebenen Zielpfad.");

        Require(
            Directory.Exists(
                sandbox.TargetDirectory),
            "Der Stammordner wurde entfernt.");
    }

    private static void TestFilesDeleted(
        TestSandbox sandbox)
    {
        Require(
            !File.Exists(sandbox.RootFile),
            "Die normale Testdatei "
            + "wurde nicht gelöscht.");

        Require(
            !File.Exists(sandbox.NestedFile),
            "Die verschachtelte Testdatei "
            + "wurde nicht gelöscht.");
    }

    private static void TestDirectoriesDeleted(
        TestSandbox sandbox)
    {
        Require(
            !Directory.Exists(
                sandbox.NestedDirectory),
            "Der geleerte Unterordner "
            + "wurde nicht entfernt.");

        Require(
            !Directory.Exists(
                sandbox.EmptyDirectory),
            "Der bereits leere Unterordner "
            + "wurde nicht entfernt.");
    }

    private static void TestProtectedFile(
        JsonElement result,
        TestSandbox sandbox)
    {
        Require(
            File.Exists(sandbox.ProtectedFile),
            "Die schreibgeschützte Testdatei "
            + "wurde unerlaubt entfernt.");

        Require(
            File.ReadAllText(sandbox.ProtectedFile)
                == ProtectedFileContent,
            "Der Inhalt der geschützten "
            + "Testdatei wurde verändert.");

        var attributes =
            File.GetAttributes(
                sandbox.ProtectedFile);

        Require(
            (attributes & FileAttributes.ReadOnly) != 0,
            "Der Schreibschutz wurde verändert.");

        Require(
            result.GetProperty("SkippedEntryCount")
                .GetInt64() == 1,
            "Der schreibgeschützte Eintrag "
            + "wurde nicht korrekt gezählt.");

        var reasons =
            result.GetProperty("SkippedEntryReasons");

        Require(
            reasons.ValueKind == JsonValueKind.Array
            && reasons.GetArrayLength() == 1,
            "Die Ursachenliste enthält "
            + "nicht genau einen Eintrag.");

        var reason = reasons[0];

        Require(
            reason.GetProperty("Reason")
                .GetString() == "ReadOnly",
            "Der Schreibschutz wurde "
            + "falsch klassifiziert.");

        Require(
            reason.GetProperty("Count")
                .GetInt64() == 1,
            "Der Ursachenzähler ist falsch.");
    }

    private static void TestOutsideFile(
        TestSandbox sandbox)
    {
        Require(
            File.Exists(sandbox.OutsideFile),
            "Die Kontroll-Datei außerhalb "
            + "des Zielordners wurde gelöscht.");

        Require(
            File.ReadAllText(sandbox.OutsideFile)
                == OutsideFileContent,
            "Die Kontroll-Datei außerhalb "
            + "des Zielordners wurde verändert.");
    }

    private static void TestResultCounters(
        JsonElement result,
        string standardOutput)
    {
        Require(
            result.GetProperty("DeletedFileCount")
                .GetInt64() == 2,
            "Es wurden nicht genau zwei "
            + "Dateilöschungen gemeldet.");

        Require(
            result.GetProperty("DeletedDirectoryCount")
                .GetInt64() == 2,
            "Es wurden nicht genau zwei "
            + "Ordnerlöschungen gemeldet.");

        var expectedBytes =
            Encoding.UTF8.GetByteCount(RootFileContent)
            + Encoding.UTF8.GetByteCount(NestedFileContent);

        Require(
            result.GetProperty("DeletedSizeBytes")
                .GetUInt64() == (ulong)expectedBytes,
            "Die gelöschte Dateigröße "
            + "wurde falsch ermittelt.");

        Require(
            result.GetProperty("FailedEntryCount")
                .GetInt64() == 0,
            "Der Löschlauf meldet "
            + "technische Fehler.");

        Require(
            string.IsNullOrWhiteSpace(
                result.GetProperty("ErrorMessage")
                    .GetString()),
            "Der Löschlauf meldet "
            + "eine unerwartete Fehlermeldung.");

        Require(
            standardOutput.Contains(
                result.GetRawText(),
                StringComparison.Ordinal),
            "Die JSON-Standardausgabe "
            + "stimmt nicht mit der "
            + "Ergebnisdatei überein.");
    }

    // =============================================
    // POWERSHELL-PROZESS
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
                "Der PowerShell-Testprozess "
                + "konnte nicht gestartet werden.");
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
                "Der Sandbox-Löschtest "
                + "hat das Zeitlimit überschritten.");
        }

        var output =
            outputTask.GetAwaiter().GetResult();

        var error =
            errorTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Der Sandbox-Löschlauf "
                + "meldet einen Fehler."
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
    // ISOLIERTE TESTDATEN
    // =============================================

    private sealed class TestSandbox : IDisposable
    {
        public string RootDirectory { get; }

        public string WindowsDirectory { get; }

        public string TargetDirectory { get; }

        public string NestedDirectory { get; }

        public string EmptyDirectory { get; }

        public string CancellationPath { get; }

        public string ResultPath { get; }

        public string RootFile { get; }

        public string NestedFile { get; }

        public string ProtectedFile { get; }

        public string OutsideFile { get; }

        public TestSandbox()
        {
            var temporaryRoot =
                Path.GetFullPath(Path.GetTempPath());

            var applicationRoot =
                Path.Combine(
                    temporaryRoot,
                    "WeberIT.Checkup");

            var sandboxBase =
                Path.Combine(
                    applicationRoot,
                    "WindowsTempSandbox");

            RootDirectory =
                Path.Combine(
                    sandboxBase,
                    Guid.NewGuid().ToString("N"));

            WindowsDirectory =
                Path.Combine(
                    RootDirectory,
                    "Windows");

            TargetDirectory =
                Path.Combine(
                    WindowsDirectory,
                    "Temp");

            NestedDirectory =
                Path.Combine(
                    TargetDirectory,
                    "Nested");

            EmptyDirectory =
                Path.Combine(
                    TargetDirectory,
                    "Empty");

            CancellationPath =
                Path.Combine(
                    RootDirectory,
                    "cancel.requested");

            ResultPath =
                Path.Combine(
                    RootDirectory,
                    "worker-result.json");

            RootFile =
                Path.Combine(
                    TargetDirectory,
                    "delete-root.txt");

            NestedFile =
                Path.Combine(
                    NestedDirectory,
                    "delete-nested.txt");

            ProtectedFile =
                Path.Combine(
                    TargetDirectory,
                    "keep-readonly.txt");

            OutsideFile =
                Path.Combine(
                    RootDirectory,
                    "outside-sentinel.txt");

            /*
             * Verzeichnis für Verzeichnis
             * kontrolliert anlegen.
             *
             * Bereits vorhandene Reparse Points
             * werden vor dem Anlegen weiterer
             * Testdaten zurückgewiesen.
             */
            EnsurePlainDirectory(temporaryRoot);
            EnsurePlainDirectory(applicationRoot);
            EnsurePlainDirectory(sandboxBase);
            EnsurePlainDirectory(RootDirectory);
            EnsurePlainDirectory(WindowsDirectory);
            EnsurePlainDirectory(TargetDirectory);
            EnsurePlainDirectory(NestedDirectory);
            EnsurePlainDirectory(EmptyDirectory);

            File.WriteAllText(
                RootFile,
                RootFileContent);

            File.WriteAllText(
                NestedFile,
                NestedFileContent);

            File.WriteAllText(
                ProtectedFile,
                ProtectedFileContent);

            File.SetAttributes(
                ProtectedFile,
                File.GetAttributes(ProtectedFile)
                    | FileAttributes.ReadOnly);

            File.WriteAllText(
                OutsideFile,
                OutsideFileContent);

            /*
             * Kein Abbruchmarker:
             * Dieser Test soll ausdrücklich
             * die kontrollierte Löschung
             * der eigenen Testdaten prüfen.
             */
        }

        public void Dispose()
        {
            /*
             * Testartefakte werden nur über
             * ihre explizit bekannten Pfade
             * entfernt.
             *
             * Keine rekursive Entfernung.
             */
            TryDeleteKnownFile(
                RootFile,
                RootFileContent);

            TryDeleteKnownFile(
                NestedFile,
                NestedFileContent);

            TryDeleteKnownFile(
                ProtectedFile,
                ProtectedFileContent);

            TryDeleteKnownFile(
                OutsideFile,
                OutsideFileContent);

            TryDeleteResultFile(ResultPath);

            TryDeleteEmptyDirectory(NestedDirectory);
            TryDeleteEmptyDirectory(EmptyDirectory);
            TryDeleteEmptyDirectory(TargetDirectory);
            TryDeleteEmptyDirectory(WindowsDirectory);
            TryDeleteEmptyDirectory(RootDirectory);
        }

        private static void EnsurePlainDirectory(
            string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var attributes =
                File.GetAttributes(path);

            if ((attributes & FileAttributes.ReparsePoint) != 0
                || (attributes & FileAttributes.Directory) == 0)
            {
                throw new InvalidOperationException(
                    "Ein Testverzeichnis ist "
                    + "kein regulärer Ordner: "
                    + path);
            }
        }

        private static void TryDeleteKnownFile(
            string path,
            string expectedContent)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                var attributes =
                    File.GetAttributes(path);

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return;
                }

                if (File.ReadAllText(path)
                    != expectedContent)
                {
                    return;
                }

                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(
                        path,
                        attributes & ~FileAttributes.ReadOnly);
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

        private static void TryDeleteResultFile(
            string path)
        {
            try
            {
                if (File.Exists(path)
                    && (File.GetAttributes(path)
                        & FileAttributes.ReparsePoint) == 0)
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Ergebnisdatei bleibt erhalten.
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
                if (Directory.Exists(path)
                    && (File.GetAttributes(path)
                        & FileAttributes.ReparsePoint) == 0)
                {
                    Directory.Delete(
                        path,
                        recursive: false);
                }
            }
            catch (IOException)
            {
                // Keine erzwungene Entfernung.
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

    private static string NormalizePath(string path)
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
                "Sandbox-Löschtest fehlgeschlagen: "
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
