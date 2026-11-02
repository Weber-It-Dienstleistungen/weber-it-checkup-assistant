
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
/// Sprint 55.13.43
///
/// Prüft echte Dateisperren und Systemattribute
/// während der isolierten Windows-Temp-Bereinigung.
///
/// Ausschließlich selbst erzeugte Sandbox-Dateien.
/// Keine Administratorrechte.
/// Keine Änderungen am produktiven Skript.
/// Keine rekursiven Zwangslöschungen.
/// </summary>
public static class
    WindowsTemporaryFilesCleanupSandboxProtectionRegressionTests
{
    private const int ProcessTimeoutMilliseconds = 30000;

    private const string NormalContent =
        "WEBER_IT_DELETE_NORMAL";

    private const string LockedContent =
        "WEBER_IT_KEEP_LOCKED";

    private const string SystemContent =
        "WEBER_IT_KEEP_SYSTEM";

    private const string OutsideContent =
        "WEBER_IT_KEEP_OUTSIDE";

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

        var results = new List<string>();

        /*
         * Die echte Dateisperre muss bestehen,
         * während der PowerShell-Prozess
         * die Bereinigung versucht.
         *
         * FileShare.None verhindert,
         * dass der andere Prozess die
         * Datei zum Löschen freigibt.
         */
        using (var lockedStream = new FileStream(
                   sandbox.LockedFile,
                   FileMode.Open,
                   FileAccess.ReadWrite,
                   FileShare.None))
        {
            var standardOutput =
                ExecutePowerShell(script);

            Require(
                File.Exists(sandbox.ResultPath),
                "Die PowerShell-Ausführung hat "
                + "keine Ergebnisdatei erzeugt.");

            using var document =
                JsonDocument.Parse(
                    File.ReadAllText(
                        sandbox.ResultPath));

            var workerResult =
                document.RootElement;

            Execute(
                "Bereinigung bleibt auf Sandbox-Ziel begrenzt",
                () => TestTarget(
                    workerResult,
                    sandbox),
                results);

            Execute(
                "Normale Testdatei wird entfernt",
                () => TestNormalDeletion(
                    workerResult,
                    sandbox),
                results);

            Execute(
                "Exklusiv gesperrte Datei bleibt erhalten",
                () => TestLockedFile(sandbox),
                results);

            Execute(
                "Systemgeschützte Datei bleibt erhalten",
                () => TestSystemFile(sandbox),
                results);

            Execute(
                "Prozesssperre wird als Ausschluss dokumentiert",
                () => TestLockedReason(workerResult),
                results);

            Execute(
                "Systemschutz wird als Ausschluss dokumentiert",
                () => TestSystemReason(workerResult),
                results);

            Execute(
                "Ergebniszähler und Außenbereich bleiben korrekt",
                () => TestResultConsistency(
                    workerResult,
                    standardOutput,
                    sandbox),
                results);
        }

        /*
         * Erst nach dem Ende der echten
         * Dateisperre prüfen wir zusätzlich
         * den Inhalt der gesperrten Datei.
         */
        Execute(
            "Gesperrte Datei bleibt inhaltlich unverändert",
            () => TestLockedContent(sandbox),
            results);

        return
            $"Alle {results.Count} Sandbox-Schutztests "
            + "erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // SICHERHEITSPRÜFUNG
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
                "SICHERHEITSSTOPP: Sandbox-Schutztests "
                + "dürfen nicht mit Administratorrechten "
                + "ausgeführt werden.");
        }
    }

    // =============================================
    // ERGEBNIS PRÜFEN
    // =============================================

    private static void TestTarget(
        JsonElement result,
        TestSandbox sandbox)
    {
        Require(
            result.GetProperty("WasStarted")
                .GetBoolean(),
            "Die Bereinigung wurde nicht gestartet.");

        Require(
            !result.GetProperty("WasCancelled")
                .GetBoolean(),
            "Die Bereinigung wurde "
            + "unerwartet abgebrochen.");

        var actualPath =
            result.GetProperty("TargetPath")
                .GetString();

        Require(
            !string.IsNullOrWhiteSpace(actualPath),
            "Der gemeldete Zielpfad fehlt.");

        Require(
            string.Equals(
                NormalizePath(actualPath!),
                NormalizePath(sandbox.TargetDirectory),
                StringComparison.OrdinalIgnoreCase),
            "Der Bereinigungsprozess meldet "
            + "einen unerwarteten Zielpfad.");

        Require(
            Directory.Exists(sandbox.TargetDirectory),
            "Der Sandbox-Stammordner "
            + "wurde unerlaubt entfernt.");
    }

    private static void TestNormalDeletion(
        JsonElement result,
        TestSandbox sandbox)
    {
        Require(
            !File.Exists(sandbox.NormalFile),
            "Die gewöhnliche Testdatei "
            + "wurde nicht gelöscht.");

        Require(
            result.GetProperty("DeletedFileCount")
                .GetInt64() == 1,
            "Es wurde nicht genau eine "
            + "Dateilöschung gemeldet.");

        Require(
            result.GetProperty("DeletedDirectoryCount")
                .GetInt64() == 0,
            "Es wurden unerwartete "
            + "Ordnerlöschungen gemeldet.");

        Require(
            result.GetProperty("DeletedSizeBytes")
                .GetUInt64()
                == (ulong)Encoding.UTF8.GetByteCount(
                    NormalContent),
            "Die gelöschte Dateigröße "
            + "wurde falsch erfasst.");
    }

    private static void TestLockedFile(
        TestSandbox sandbox)
    {
        /*
         * Die Dateisperre ist an dieser
         * Stelle weiterhin aktiv.
         */
        Require(
            File.Exists(sandbox.LockedFile),
            "Die gesperrte Testdatei "
            + "wurde unerlaubt gelöscht.");
    }

    private static void TestSystemFile(
        TestSandbox sandbox)
    {
        Require(
            File.Exists(sandbox.SystemFile),
            "Die systemgeschützte Testdatei "
            + "wurde unerlaubt gelöscht.");

        Require(
            File.ReadAllText(sandbox.SystemFile)
                == SystemContent,
            "Der Inhalt der Systemdatei "
            + "wurde verändert.");

        var attributes =
            File.GetAttributes(sandbox.SystemFile);

        Require(
            (attributes & FileAttributes.System) != 0,
            "Das Systemattribut wurde "
            + "unerlaubt entfernt.");
    }

    private static void TestLockedReason(
        JsonElement result)
    {
        Require(
            GetReasonCount(
                result,
                "LockedByProcess") == 1,
            "Die echte Dateisperre wurde "
            + "nicht korrekt als "
            + "LockedByProcess klassifiziert.");
    }

    private static void TestSystemReason(
        JsonElement result)
    {
        Require(
            GetReasonCount(
                result,
                "SystemProtected") == 1,
            "Das Systemattribut wurde "
            + "nicht korrekt als "
            + "SystemProtected klassifiziert.");
    }

    private static void TestResultConsistency(
        JsonElement result,
        string standardOutput,
        TestSandbox sandbox)
    {
        Require(
            result.GetProperty("SkippedEntryCount")
                .GetInt64() == 2,
            "Es wurden nicht genau zwei "
            + "Ausschlüsse gemeldet.");

        Require(
            result.GetProperty("FailedEntryCount")
                .GetInt64() == 0,
            "Erwartete Ausschlüsse wurden "
            + "als technische Fehler gewertet.");

        Require(
            string.IsNullOrWhiteSpace(
                result.GetProperty("ErrorMessage")
                    .GetString()),
            "Der Worker meldet einen "
            + "unerwarteten technischen Fehler.");

        var reasons =
            result.GetProperty("SkippedEntryReasons");

        Require(
            reasons.ValueKind == JsonValueKind.Array
            && reasons.GetArrayLength() == 2,
            "Die Ursachenliste muss genau "
            + "zwei verschiedene Gründe enthalten.");

        var detailedTotal =
            reasons.EnumerateArray()
                .Sum(entry =>
                    entry.GetProperty("Count")
                        .GetInt64());

        Require(
            detailedTotal == 2,
            "Die Ursachenzähler stimmen "
            + "nicht mit dem Gesamtzähler überein.");

        Require(
            File.Exists(sandbox.OutsideFile),
            "Die äußere Kontroll-Datei "
            + "wurde gelöscht.");

        Require(
            File.ReadAllText(sandbox.OutsideFile)
                == OutsideContent,
            "Die äußere Kontroll-Datei "
            + "wurde verändert.");

        Require(
            standardOutput.Contains(
                result.GetRawText(),
                StringComparison.Ordinal),
            "Die Standardausgabe und die "
            + "JSON-Ergebnisdatei stimmen "
            + "nicht überein.");
    }

    private static void TestLockedContent(
        TestSandbox sandbox)
    {
        Require(
            File.Exists(sandbox.LockedFile),
            "Die ehemals gesperrte Datei fehlt.");

        Require(
            File.ReadAllText(sandbox.LockedFile)
                == LockedContent,
            "Der Inhalt der ehemals "
            + "gesperrten Datei wurde verändert.");
    }

    private static long GetReasonCount(
        JsonElement result,
        string expectedReason)
    {
        var reasons =
            result.GetProperty("SkippedEntryReasons");

        Require(
            reasons.ValueKind == JsonValueKind.Array,
            "Die Ursachenliste fehlt.");

        foreach (var item in reasons.EnumerateArray())
        {
            var reason =
                item.GetProperty("Reason")
                    .GetString();

            if (string.Equals(
                    reason,
                    expectedReason,
                    StringComparison.Ordinal))
            {
                return item.GetProperty("Count")
                    .GetInt64();
            }
        }

        return 0;
    }

    // =============================================
    // POWERSHELL AUSFÜHREN
    // =============================================

    private static string ExecutePowerShell(
        string source)
    {
        var windowsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        if (string.IsNullOrWhiteSpace(windowsDirectory))
        {
            throw new InvalidOperationException(
                "Das Windows-Verzeichnis "
                + "konnte nicht bestimmt werden.");
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
                "Die Sandbox-Schutztests haben "
                + "das Zeitlimit überschritten.");
        }

        var standardOutput =
            outputTask.GetAwaiter().GetResult();

        var standardError =
            errorTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Der PowerShell-Testprozess "
                + "meldet einen Fehler."
                + Environment.NewLine
                + "Exitcode: "
                + process.ExitCode
                + Environment.NewLine
                + standardError
                + Environment.NewLine
                + standardOutput);
        }

        return standardOutput;
    }

    // =============================================
    // SANDBOX-TESTDATEN
    // =============================================

    private sealed class TestSandbox : IDisposable
    {
        public string RootDirectory { get; }

        public string WindowsDirectory { get; }

        public string TargetDirectory { get; }

        public string CancellationPath { get; }

        public string ResultPath { get; }

        public string NormalFile { get; }

        public string LockedFile { get; }

        public string SystemFile { get; }

        public string OutsideFile { get; }

        public TestSandbox()
        {
            var temporaryRoot =
                Path.GetFullPath(
                    Path.GetTempPath());

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

            LockedFile =
                Path.Combine(
                    TargetDirectory,
                    "keep-locked.txt");

            SystemFile =
                Path.Combine(
                    TargetDirectory,
                    "keep-system.txt");

            OutsideFile =
                Path.Combine(
                    RootDirectory,
                    "outside-sentinel.txt");

            /*
             * Nur die vorgeschriebene
             * GUID-Sandboxstruktur anlegen.
             *
             * Reparse Points in bereits
             * vorhandenen Verzeichnissen
             * werden abgelehnt.
             */
            EnsurePlainDirectory(temporaryRoot);
            EnsurePlainDirectory(applicationRoot);
            EnsurePlainDirectory(sandboxBase);
            EnsurePlainDirectory(RootDirectory);
            EnsurePlainDirectory(WindowsDirectory);
            EnsurePlainDirectory(TargetDirectory);

            File.WriteAllText(
                NormalFile,
                NormalContent);

            File.WriteAllText(
                LockedFile,
                LockedContent);

            File.WriteAllText(
                SystemFile,
                SystemContent);

            File.SetAttributes(
                SystemFile,
                File.GetAttributes(SystemFile)
                    | FileAttributes.System);

            File.WriteAllText(
                OutsideFile,
                OutsideContent);
        }

        public void Dispose()
        {
            /*
             * Nur namentlich bekannte
             * Testdateien entfernen.
             *
             * Keine rekursive Löschung.
             *
             * Bei unerwartetem Inhalt
             * bleibt die Testdatei erhalten.
             */
            TryDeleteKnownFile(
                NormalFile,
                NormalContent);

            TryDeleteKnownFile(
                LockedFile,
                LockedContent);

            TryDeleteKnownFile(
                SystemFile,
                SystemContent);

            TryDeleteKnownFile(
                OutsideFile,
                OutsideContent);

            TryDeleteResultFile(ResultPath);

            TryDeleteEmptyDirectory(
                TargetDirectory);

            TryDeleteEmptyDirectory(
                WindowsDirectory);

            TryDeleteEmptyDirectory(
                RootDirectory);
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

                /*
                 * Attribute werden ausschließlich
                 * zur Entfernung unserer eigenen
                 * Testdateien zurückgesetzt.
                 *
                 * Das produktive PowerShell-Skript
                 * verändert keine Attribute.
                 */
                if ((attributes & FileAttributes.System) != 0)
                {
                    File.SetAttributes(
                        path,
                        attributes & ~FileAttributes.System);
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
                "Sandbox-Schutztest fehlgeschlagen: "
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
            throw new InvalidOperationException(
                message);
        }
    }
}

#endif
