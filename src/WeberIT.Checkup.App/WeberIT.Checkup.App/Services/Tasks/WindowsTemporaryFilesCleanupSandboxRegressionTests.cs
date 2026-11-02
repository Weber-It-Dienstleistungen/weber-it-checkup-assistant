
#if DEBUG

using System;
using System.Collections.Generic;
using System.IO;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.13.35
///
/// Regressionstests für die isolierte
/// Windows-Temp-Testumgebung.
///
/// Erstellt ausschließlich eigene,
/// leere Testverzeichnisse unter %TEMP%.
///
/// Es werden keine PowerShell-Prozesse
/// gestartet und keine Bereinigungs-
/// operationen ausgeführt.
///
/// Die Tests verwenden keine
/// Administratorrechte.
/// </summary>
public static class
    WindowsTemporaryFilesCleanupSandboxRegressionTests
{
    public static string RunAll()
    {
        var results = new List<string>();

        using var sandbox = new TestSandbox();

        Execute(
            "Gültige Sandbox wird akzeptiert",
            () => TestValidSandbox(sandbox),
            results);

        Execute(
            "Produktive Windows-Pfaderkennung fehlt",
            () => TestProductionPathExcluded(sandbox),
            results);

        Execute(
            "Sonderzeichen in Dateipfaden werden maskiert",
            () => TestPathEscaping(sandbox),
            results);

        Execute(
            "Ungültige GUID wird zurückgewiesen",
            () => TestInvalidGuid(sandbox),
            results);

        Execute(
            "Zusätzliche Verzeichnisebene wird abgelehnt",
            () => TestAdditionalDirectory(sandbox),
            results);

        Execute(
            "Abbruchmarker außerhalb der Sandbox wird abgelehnt",
            () => TestInvalidCancellationPath(sandbox),
            results);

        Execute(
            "Ergebnisdatei außerhalb der Sandbox wird abgelehnt",
            () => TestInvalidResultPath(sandbox),
            results);

        Execute(
            "Identische Kommunikationsdateien werden abgelehnt",
            () => TestIdenticalCommunicationPaths(sandbox),
            results);

        Execute(
            "Fehlender Temp-Testordner wird abgelehnt",
            () => TestMissingTargetDirectory(sandbox),
            results);

        Execute(
            "Produktiver Windows-Ordner wird abgelehnt",
            () => TestRealWindowsDirectory(sandbox),
            results);

        return
            $"Alle {results.Count} Sandbox-"
            + "Sicherheitsprüfungen erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    private static string Build(
        TestSandbox sandbox)
    {
        return WindowsTemporaryFilesCleanupSandboxScript
            .Build(
                sandbox.WindowsDirectory,
                sandbox.CancellationPath,
                sandbox.ResultPath);
    }

    // =============================================
    // ERLAUBTE TESTUMGEBUNG
    // =============================================

    private static void TestValidSandbox(
        TestSandbox sandbox)
    {
        var script = Build(sandbox);

        Require(
            !string.IsNullOrWhiteSpace(script),
            "Es wurde kein Testskript erzeugt.");

        Require(
            script.Contains(
                sandbox.WindowsDirectory,
                StringComparison.Ordinal),
            "Der freigegebene Windows-Testordner "
            + "fehlt im Skript.");

        Require(
            script.Contains(
                "[System.IO.File]::Delete(",
                StringComparison.Ordinal),
            "Die zu prüfende Dateilöschlogik fehlt.");

        Require(
            script.Contains(
                "[System.IO.Directory]::Delete(",
                StringComparison.Ordinal),
            "Die zu prüfende Ordnerlöschlogik fehlt.");
    }

    private static void TestProductionPathExcluded(
        TestSandbox sandbox)
    {
        var script = Build(sandbox);

        Require(
            !script.Contains(
                "[System.Environment+SpecialFolder]::Windows",
                StringComparison.Ordinal),
            "Die produktive Windows-Pfaderkennung "
            + "ist noch vorhanden.");

        Require(
            !script.Contains(
                "$env:WINDIR",
                StringComparison.Ordinal),
            "Der produktive WINDIR-Fallback "
            + "ist noch vorhanden.");

        Require(
            !script.Contains(
                "__WEBERIT_SANDBOX_WINDOWS_PATH__",
                StringComparison.Ordinal),
            "Der Sandbox-Platzhalter "
            + "wurde nicht ersetzt.");

        Require(
            !script.Contains(
                "__WEBERIT_CANCELLATION_MARKER__",
                StringComparison.Ordinal),
            "Der Abbruchplatzhalter "
            + "wurde nicht ersetzt.");

        Require(
            !script.Contains(
                "__WEBERIT_RESULT_FILE__",
                StringComparison.Ordinal),
            "Der Ergebnisplatzhalter "
            + "wurde nicht ersetzt.");
    }

    private static void TestPathEscaping(
        TestSandbox sandbox)
    {
        var cancellationPath =
            Path.Combine(
                sandbox.RootDirectory,
                "O'Brien-cancel.requested");

        var resultPath =
            Path.Combine(
                sandbox.RootDirectory,
                "O'Brien-result.json");

        var script =
            WindowsTemporaryFilesCleanupSandboxScript
                .Build(
                    sandbox.WindowsDirectory,
                    cancellationPath,
                    resultPath);

        Require(
            script.Contains(
                "O''Brien-cancel.requested",
                StringComparison.Ordinal),
            "Der Abbruchpfad wurde nicht maskiert.");

        Require(
            script.Contains(
                "O''Brien-result.json",
                StringComparison.Ordinal),
            "Der Ergebnispfad wurde nicht maskiert.");
    }

    // =============================================
    // UNERLAUBTE TESTPFADE
    // =============================================

    private static void TestInvalidGuid(
        TestSandbox sandbox)
    {
        var invalidWindows =
            Path.Combine(
                Path.GetTempPath(),
                "WeberIT.Checkup",
                "WindowsTempSandbox",
                "keine-gueltige-guid",
                "Windows");

        ExpectRejected(
            () =>
                WindowsTemporaryFilesCleanupSandboxScript
                    .Build(
                        invalidWindows,
                        sandbox.CancellationPath,
                        sandbox.ResultPath));
    }

    private static void TestAdditionalDirectory(
        TestSandbox sandbox)
    {
        var invalidWindows =
            Path.Combine(
                sandbox.WindowsDirectory,
                "NochEinOrdner");

        ExpectRejected(
            () =>
                WindowsTemporaryFilesCleanupSandboxScript
                    .Build(
                        invalidWindows,
                        sandbox.CancellationPath,
                        sandbox.ResultPath));
    }

    private static void TestInvalidCancellationPath(
        TestSandbox sandbox)
    {
        var invalidCancellationPath =
            Path.Combine(
                sandbox.WindowsDirectory,
                "cancel.requested");

        ExpectRejected(
            () =>
                WindowsTemporaryFilesCleanupSandboxScript
                    .Build(
                        sandbox.WindowsDirectory,
                        invalidCancellationPath,
                        sandbox.ResultPath));
    }

    private static void TestInvalidResultPath(
        TestSandbox sandbox)
    {
        var invalidResultPath =
            Path.Combine(
                sandbox.WindowsDirectory,
                "worker-result.json");

        ExpectRejected(
            () =>
                WindowsTemporaryFilesCleanupSandboxScript
                    .Build(
                        sandbox.WindowsDirectory,
                        sandbox.CancellationPath,
                        invalidResultPath));
    }

    private static void TestIdenticalCommunicationPaths(
        TestSandbox sandbox)
    {
        ExpectRejected(
            () =>
                WindowsTemporaryFilesCleanupSandboxScript
                    .Build(
                        sandbox.WindowsDirectory,
                        sandbox.CancellationPath,
                        sandbox.CancellationPath));
    }

    private static void TestMissingTargetDirectory(
        TestSandbox sandbox)
    {
        /*
         * Ausschließlich unser eigener
         * leerer Testordner wird vorübergehend
         * entfernt und danach wieder angelegt.
         *
         * Keine produktive Datei wird berührt.
         */
        Directory.Delete(
            sandbox.TargetDirectory,
            recursive: false);

        try
        {
            ExpectRejected(
                () => Build(sandbox));
        }
        finally
        {
            Directory.CreateDirectory(
                sandbox.TargetDirectory);
        }
    }

    private static void TestRealWindowsDirectory(
        TestSandbox sandbox)
    {
        var realWindows =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);

        Require(
            !string.IsNullOrWhiteSpace(realWindows),
            "Das tatsächliche Windows-Verzeichnis "
            + "konnte nicht ermittelt werden.");

        ExpectRejected(
            () =>
                WindowsTemporaryFilesCleanupSandboxScript
                    .Build(
                        realWindows,
                        sandbox.CancellationPath,
                        sandbox.ResultPath));
    }

    // =============================================
    // TESTHILFSMETHODEN
    // =============================================

    private static void ExpectRejected(
        Action action)
    {
        var rejected = false;

        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(
            rejected,
            "Ein nicht freigegebener Testpfad "
            + "wurde unerwartet akzeptiert.");
    }

    private static void Execute(
        string title,
        Action test,
        ICollection<string> results)
    {
        try
        {
            test();

            results.Add(
                "OK: " + title);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Sandbox-Sicherheitstest "
                + "fehlgeschlagen: "
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

    // =============================================
    // ISOLIERTE TESTVERZEICHNISSE
    // =============================================

    private sealed class TestSandbox : IDisposable
    {
        public string RootDirectory { get; }

        public string WindowsDirectory { get; }

        public string TargetDirectory { get; }

        public string CancellationPath { get; }

        public string ResultPath { get; }

        public TestSandbox()
        {
            RootDirectory =
                Path.Combine(
                    Path.GetFullPath(
                        Path.GetTempPath()),
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

            Directory.CreateDirectory(
                TargetDirectory);
        }

        public void Dispose()
        {
            /*
             * Kein rekursives Löschen!
             *
             * Wir entfernen ausschließlich
             * die zuvor angelegten, leeren
             * Testverzeichnisse.
             *
             * Unerwartete Inhalte bleiben
             * zu Untersuchungszwecken erhalten.
             */
            TryRemoveEmpty(TargetDirectory);
            TryRemoveEmpty(WindowsDirectory);
            TryRemoveEmpty(RootDirectory);
        }

        private static void TryRemoveEmpty(
            string directory)
        {
            try
            {
                if (Directory.Exists(directory)
                    && (File.GetAttributes(directory)
                        & FileAttributes.ReparsePoint) == 0)
                {
                    Directory.Delete(
                        directory,
                        recursive: false);
                }
            }
            catch (IOException)
            {
                // Ordner nicht leer oder gesperrt.
                // Keine erzwungene Entfernung.
            }
            catch (UnauthorizedAccessException)
            {
                // Keine Rechteausweitung.
            }
        }
    }
}

#endif
