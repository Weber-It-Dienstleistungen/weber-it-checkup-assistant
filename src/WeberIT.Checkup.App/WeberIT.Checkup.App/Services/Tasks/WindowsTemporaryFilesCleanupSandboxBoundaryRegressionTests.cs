
#if DEBUG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.13.47
///
/// Reale Sandbox-Grenzfalltests:
///
/// - Windows-Junction wird nicht verfolgt.
/// - Ziel einer Junction bleibt unverändert.
/// - Schreibgeschützte Datei bleibt erhalten.
/// - Nicht leerer Elternordner bleibt erhalten.
/// - Normale Dateien werden trotzdem gelöscht.
/// - Leere Unterordner werden entfernt.
/// - Ursachenzähler bleiben korrekt.
///
/// Ausschließlich synthetische Testdateien.
/// Keine Administratorrechte.
/// Keine produktiven Windows-Dateien.
/// Keine rekursive Zwangslöschung.
/// </summary>
public static class
    WindowsTemporaryFilesCleanupSandboxBoundaryRegressionTests
{
    private const int ProcessTimeoutMilliseconds = 30000;

    private const string NormalContent =
        "WEBER_IT_DELETE_ROOT";

    private const string ChildContent =
        "WEBER_IT_DELETE_CHILD";

    private const string ProtectedContent =
        "WEBER_IT_KEEP_READONLY";

    private const string OutsideContent =
        "WEBER_IT_KEEP_JUNCTION_TARGET";

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

        /*
         * Die Junction zeigt ausschließlich
         * auf einen zweiten Testordner
         * innerhalb derselben GUID-Sandbox.
         */
        sandbox.CreateJunction();

        var script =
            WindowsTemporaryFilesCleanupSandboxScript
                .Build(
                    sandbox.WindowsDirectory,
                    sandbox.CancellationPath,
                    sandbox.ResultPath);

        var standardOutput =
            ExecutePowerShell(script);

        Require(
            File.Exists(sandbox.ResultPath),
            "Die Sandbox-Bereinigung hat "
            + "keine Ergebnisdatei erzeugt.");

        using var document =
            JsonDocument.Parse(
                File.ReadAllText(sandbox.ResultPath));

        var result = document.RootElement;

        var results = new List<string>();

        Execute(
            "Ausführung bleibt auf Sandbox begrenzt",
            () => TestExecution(result, sandbox),
            results);

        Execute(
            "Normale Dateien werden entfernt",
            () => TestNormalFiles(sandbox),
            results);

        Execute(
            "Leerer Unterordner wird entfernt",
            () => TestEmptyDirectory(sandbox),
            results);

        Execute(
            "Schreibgeschützte Datei bleibt erhalten",
            () => TestProtectedFile(sandbox),
            results);

        Execute(
            "Nicht leerer Elternordner bleibt erhalten",
            () => TestNonEmptyDirectory(sandbox),
            results);

        Execute(
            "Windows-Junction wird nicht entfernt oder verfolgt",
            () => TestJunction(sandbox),
            results);

        Execute(
            "Junction-Ziel bleibt unverändert",
            () => TestOutsideTarget(sandbox),
            results);

        Execute(
            "Lösch- und Überspringzähler stimmen überein",
            () => TestResult(
                result,
                standardOutput),
            results);

        return
            $"Alle {results.Count} Sandbox-Grenzfalltests "
            + "erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // RECHTE PRÜFEN
    // =============================================

    private static void EnsureNonElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Diese Sandbox-Tests benötigen Windows.");
        }

        using var identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(identity);

        if (principal.IsInRole(
                WindowsBuiltInRole.Administrator))
        {
            throw new InvalidOperationException(
                "SICHERHEITSSTOPP: Die Sandbox-Tests "
                + "dürfen nicht mit "
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
            "Der Worker meldet keinen Start.");

        Require(
            !result.GetProperty("WasCancelled")
                .GetBoolean(),
            "Der Worker meldet einen Abbruch.");

        var reportedPath =
            result.GetProperty("TargetPath")
                .GetString();

        Require(
            !string.IsNullOrWhiteSpace(reportedPath),
            "Der gemeldete Zielpfad fehlt.");

        Require(
            string.Equals(
                NormalizePath(reportedPath!),
                NormalizePath(sandbox.TargetDirectory),
                StringComparison.OrdinalIgnoreCase),
            "Der Worker meldet einen "
            + "unerwarteten Zielpfad.");

        Require(
            Directory.Exists(sandbox.TargetDirectory),
            "Der freigegebene Sandbox-Stammordner "
            + "wurde entfernt.");
    }

    private static void TestNormalFiles(
        TestSandbox sandbox)
    {
        Require(
            !File.Exists(sandbox.NormalFile),
            "Die normale Datei im "
            + "Stammordner wurde nicht gelöscht.");

        Require(
            !File.Exists(sandbox.ChildFile),
            "Die normale Datei im "
            + "Unterordner wurde nicht gelöscht.");
    }

    private static void TestEmptyDirectory(
        TestSandbox sandbox)
    {
        Require(
            !Directory.Exists(sandbox.EmptyDirectory),
            "Der leere Unterordner "
            + "wurde nicht entfernt.");
    }

    private static void TestProtectedFile(
        TestSandbox sandbox)
    {
        Require(
            File.Exists(sandbox.ProtectedFile),
            "Die schreibgeschützte Datei "
            + "wurde entfernt.");

        var attributes =
            File.GetAttributes(sandbox.ProtectedFile);

        Require(
            (attributes & FileAttributes.ReadOnly) != 0,
            "Der Schreibschutz wurde verändert.");

        Require(
            File.ReadAllText(sandbox.ProtectedFile)
                == ProtectedContent,
            "Die schreibgeschützte Datei "
            + "wurde inhaltlich verändert.");
    }

    private static void TestNonEmptyDirectory(
        TestSandbox sandbox)
    {
        Require(
            Directory.Exists(sandbox.KeepDirectory),
            "Der Ordner mit geschütztem Inhalt "
            + "wurde entfernt.");

        Require(
            File.Exists(sandbox.ProtectedFile),
            "Die geschützte Datei im "
            + "erhaltenen Ordner fehlt.");

        Require(
            !File.Exists(sandbox.ChildFile),
            "Die normale Datei wurde im "
            + "erhaltenen Ordner nicht entfernt.");
    }

    private static void TestJunction(
        TestSandbox sandbox)
    {
        Require(
            Directory.Exists(sandbox.JunctionPath),
            "Die Junction wurde unerwartet entfernt.");

        var attributes =
            File.GetAttributes(sandbox.JunctionPath);

        Require(
            (attributes & FileAttributes.ReparsePoint) != 0,
            "Der Junction-Eintrag ist "
            + "kein Reparse Point mehr.");

        Require(
            (attributes & FileAttributes.Directory) != 0,
            "Der Junction-Eintrag besitzt "
            + "kein Verzeichnisattribut.");
    }

    private static void TestOutsideTarget(
        TestSandbox sandbox)
    {
        Require(
            Directory.Exists(sandbox.OutsideDirectory),
            "Das externe Sandbox-Testziel fehlt.");

        Require(
            File.Exists(sandbox.OutsideFile),
            "Die Kontroll-Datei im "
            + "Junction-Ziel wurde gelöscht.");

        Require(
            File.ReadAllText(sandbox.OutsideFile)
                == OutsideContent,
            "Die Kontroll-Datei im "
            + "Junction-Ziel wurde verändert.");
    }

    private static void TestResult(
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
                .GetInt64() == 1,
            "Es wurde nicht genau ein "
            + "leerer Unterordner entfernt.");

        var expectedBytes =
            Encoding.UTF8.GetByteCount(NormalContent)
            + Encoding.UTF8.GetByteCount(ChildContent);

        Require(
            result.GetProperty("DeletedSizeBytes")
                .GetUInt64() == (ulong)expectedBytes,
            "Die gelöschten Dateigrößen "
            + "wurden falsch berechnet.");

        Require(
            result.GetProperty("FailedEntryCount")
                .GetInt64() == 0,
            "Erwartete Sicherheitsausnahmen "
            + "wurden als technische Fehler erfasst.");

        Require(
            result.GetProperty("SkippedEntryCount")
                .GetInt64() == 3,
            "Es wurden nicht genau drei "
            + "übersprungene Einträge gemeldet.");

        Require(
            GetReasonCount(result, "ReadOnly") == 1,
            "Der Schreibschutz wurde "
            + "nicht korrekt dokumentiert.");

        Require(
            GetReasonCount(
                result,
                "DirectoryNotEmpty") == 1,
            "Der nicht leere Ordner "
            + "wurde nicht korrekt dokumentiert.");

        Require(
            GetReasonCount(
                result,
                "ReparsePoint") == 1,
            "Die Junction wurde nicht "
            + "als Reparse Point dokumentiert.");

        var reasons =
            result.GetProperty("SkippedEntryReasons");

        Require(
            reasons.ValueKind == JsonValueKind.Array
            && reasons.GetArrayLength() == 3,
            "Die Ursachenliste enthält "
            + "nicht genau drei Ursachen.");

        Require(
            reasons.EnumerateArray()
                .Sum(item =>
                    item.GetProperty("Count")
                        .GetInt64()) == 3,
            "Die Summe der Ursachen ist falsch.");

        Require(
            string.IsNullOrWhiteSpace(
                result.GetProperty("ErrorMessage")
                    .GetString()),
            "Der Worker meldet "
            + "einen unerwarteten Fehler.");

        Require(
            standardOutput.Contains(
                result.GetRawText(),
                StringComparison.Ordinal),
            "Die PowerShell-Standardausgabe "
            + "stimmt nicht mit dem Ergebnis überein.");
    }

    private static long GetReasonCount(
        JsonElement result,
        string reason)
    {
        var reasons =
            result.GetProperty("SkippedEntryReasons");

        Require(
            reasons.ValueKind == JsonValueKind.Array,
            "Die Ursachenliste fehlt.");

        foreach (var entry in reasons.EnumerateArray())
        {
            if (string.Equals(
                    entry.GetProperty("Reason")
                        .GetString(),
                    reason,
                    StringComparison.Ordinal))
            {
                return entry.GetProperty("Count")
                    .GetInt64();
            }
        }

        return 0;
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

        var encodedBootstrap =
            Convert.ToBase64String(
                Encoding.Unicode.GetBytes(
                    BootstrapScript));

        var encodedSource =
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
        startInfo.ArgumentList.Add(encodedBootstrap);

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

        process.StandardInput.Write(encodedSource);
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
                // Prozess war bereits beendet.
            }

            throw new InvalidOperationException(
                "Die PowerShell-Ausführung "
                + "hat das Zeitlimit überschritten.");
        }

        var output =
            outputTask.GetAwaiter().GetResult();

        var error =
            errorTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Die PowerShell-Ausführung "
                + "meldet einen technischen Fehler."
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
    // SANDBOX-TESTDATEN
    // =============================================

    private sealed class TestSandbox : IDisposable
    {
        public string RootDirectory { get; }

        public string WindowsDirectory { get; }

        public string TargetDirectory { get; }

        public string KeepDirectory { get; }

        public string EmptyDirectory { get; }

        public string OutsideDirectory { get; }

        public string JunctionPath { get; }

        public string CancellationPath { get; }

        public string ResultPath { get; }

        public string NormalFile { get; }

        public string ChildFile { get; }

        public string ProtectedFile { get; }

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

            KeepDirectory =
                Path.Combine(
                    TargetDirectory,
                    "KeepDirectory");

            EmptyDirectory =
                Path.Combine(
                    TargetDirectory,
                    "EmptyDirectory");

            OutsideDirectory =
                Path.Combine(
                    RootDirectory,
                    "OutsideTarget");

            JunctionPath =
                Path.Combine(
                    TargetDirectory,
                    "OutsideJunction");

            CancellationPath =
                Path.Combine(
                    RootDirectory,
                    "cancel.requested");

            ResultPath =
                Path.Combine(
                    RootDirectory,
                    "worker-result.json");

            NormalFile =
                Path.Combine(
                    TargetDirectory,
                    "delete-normal.txt");

            ChildFile =
                Path.Combine(
                    KeepDirectory,
                    "delete-child.txt");

            ProtectedFile =
                Path.Combine(
                    KeepDirectory,
                    "keep-readonly.txt");

            OutsideFile =
                Path.Combine(
                    OutsideDirectory,
                    "outside-sentinel.txt");

            /*
             * Die Verzeichnisse werden
             * einzeln geprüft und angelegt.
             *
             * Vorhandene Reparse Points
             * sind nicht zulässig.
             */
            EnsurePlainDirectory(
                Path.GetTempPath());

            EnsurePlainDirectory(
                Path.Combine(
                    Path.GetTempPath(),
                    "WeberIT.Checkup"));

            EnsurePlainDirectory(
                Path.Combine(
                    Path.GetTempPath(),
                    "WeberIT.Checkup",
                    "WindowsTempSandbox"));

            EnsurePlainDirectory(RootDirectory);
            EnsurePlainDirectory(WindowsDirectory);
            EnsurePlainDirectory(TargetDirectory);
            EnsurePlainDirectory(KeepDirectory);
            EnsurePlainDirectory(EmptyDirectory);
            EnsurePlainDirectory(OutsideDirectory);

            File.WriteAllText(
                NormalFile,
                NormalContent);

            File.WriteAllText(
                ChildFile,
                ChildContent);

            File.WriteAllText(
                ProtectedFile,
                ProtectedContent);

            File.SetAttributes(
                ProtectedFile,
                File.GetAttributes(ProtectedFile)
                    | FileAttributes.ReadOnly);

            File.WriteAllText(
                OutsideFile,
                OutsideContent);
        }

        public void CreateJunction()
        {
            /*
             * mklink /J erstellt eine
             * Windows-Verzeichnisverknüpfung.
             *
             * Hierfür sind normalerweise
             * keine Administratorrechte nötig.
             *
             * Quelle und Ziel liegen
             * innerhalb derselben eigens
             * erzeugten GUID-Sandbox.
             */
            if (Directory.Exists(JunctionPath)
                || File.Exists(JunctionPath))
            {
                throw new InvalidOperationException(
                    "Der vorgesehene Junction-Pfad "
                    + "existiert bereits.");
            }

            var commandPath =
                Path.Combine(
                    Environment.SystemDirectory,
                    "cmd.exe");

            if (!File.Exists(commandPath))
            {
                throw new FileNotFoundException(
                    "Der Windows-Befehlsprozessor "
                    + "wurde nicht gefunden.",
                    commandPath);
            }

            var startInfo =
                new ProcessStartInfo
                {
                    FileName = commandPath,
                    Arguments =
                        "/d /c mklink /J "
                        + "\""
                        + JunctionPath
                        + "\" \""
                        + OutsideDirectory
                        + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

            using var process =
                new Process
                {
                    StartInfo = startInfo
                };

            if (!process.Start())
            {
                throw new InvalidOperationException(
                    "Die Test-Junction konnte "
                    + "nicht angelegt werden.");
            }

            if (!process.WaitForExit(10000))
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
                    "Das Erstellen der Test-Junction "
                    + "hat das Zeitlimit überschritten.");
            }

            var output =
                process.StandardOutput.ReadToEnd();

            var error =
                process.StandardError.ReadToEnd();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "Die Test-Junction konnte "
                    + "nicht erstellt werden."
                    + Environment.NewLine
                    + output
                    + Environment.NewLine
                    + error);
            }

            var attributes =
                File.GetAttributes(JunctionPath);

            if ((attributes & FileAttributes.ReparsePoint) == 0)
            {
                throw new InvalidOperationException(
                    "Die erstellte Testverknüpfung "
                    + "ist kein Reparse Point.");
            }
        }

        public void Dispose()
        {
            /*
             * Junction ausschließlich als
             * Verzeichniseintrag entfernen.
             *
             * Niemals rekursiv löschen.
             */
            TryRemoveJunction();

            TryDeleteKnownFile(
                NormalFile,
                NormalContent);

            TryDeleteKnownFile(
                ChildFile,
                ChildContent);

            TryDeleteKnownFile(
                ProtectedFile,
                ProtectedContent);

            TryDeleteKnownFile(
                OutsideFile,
                OutsideContent);

            TryDeleteResultFile();

            TryDeleteEmptyDirectory(KeepDirectory);
            TryDeleteEmptyDirectory(EmptyDirectory);
            TryDeleteEmptyDirectory(TargetDirectory);
            TryDeleteEmptyDirectory(OutsideDirectory);
            TryDeleteEmptyDirectory(WindowsDirectory);
            TryDeleteEmptyDirectory(RootDirectory);
        }

        private void TryRemoveJunction()
        {
            try
            {
                if (!Directory.Exists(JunctionPath))
                {
                    return;
                }

                var attributes =
                    File.GetAttributes(JunctionPath);

                if ((attributes & FileAttributes.ReparsePoint) == 0)
                {
                    return;
                }

                Directory.Delete(
                    JunctionPath,
                    recursive: false);
            }
            catch (IOException)
            {
                // Junction bleibt zur Prüfung erhalten.
            }
            catch (UnauthorizedAccessException)
            {
                // Keine Rechteausweitung.
            }
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
                    "Ein Sandboxordner ist kein "
                    + "reguläres Verzeichnis: "
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
                // Keine erzwungene Entfernung.
            }
            catch (UnauthorizedAccessException)
            {
                // Keine Rechteausweitung.
            }
        }

        private void TryDeleteResultFile()
        {
            try
            {
                if (File.Exists(ResultPath)
                    && (File.GetAttributes(ResultPath)
                        & FileAttributes.ReparsePoint) == 0)
                {
                    File.Delete(ResultPath);
                }
            }
            catch (IOException)
            {
                // Ergebnis bleibt zur Untersuchung erhalten.
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
                "Sandbox-Grenzfalltest fehlgeschlagen: "
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
