
#if DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.22
///
/// Integrationstest für die vollständige
/// handle-basierte Bereinigungs-Engine.
///
/// Ein kontrollierter Bereinigungslauf
/// und ein Abbruchtest vor dem Start.
///
/// Sicherheitsregeln:
/// - Ausschließlich eigene GUID-Sandbox.
/// - Keine Administratorrechte.
/// - Kein produktiver Windows-Temp-Zugriff.
/// - Keine rekursive Testdatenbereinigung.
/// - Keine fremden Dateien verändern.
/// - Keine automatische Wiederholung.
/// </summary>
public static class
    WindowsTemporaryFilesHandleCleanupEngineRegressionTests
{
    private const string NormalContent =
        "WEBER_IT_ENGINE_DELETE_NORMAL";

    private const string NestedNormalContent =
        "WEBER_IT_ENGINE_DELETE_NESTED";

    private const string ReadOnlyContent =
        "WEBER_IT_ENGINE_KEEP_READONLY";

    private const string NestedReadOnlyContent =
        "WEBER_IT_ENGINE_KEEP_NESTED_READONLY";

    private const string SystemContent =
        "WEBER_IT_ENGINE_KEEP_SYSTEM";

    private const string LockedContent =
        "WEBER_IT_ENGINE_KEEP_LOCKED";

    private const string OutsideContent =
        "WEBER_IT_ENGINE_KEEP_OUTSIDE";

    public static string RunAll()
    {
        EnsureNonElevated();

        var results = new List<string>();

        using (var sandbox = new TestSandbox())
        {
            /*
             * Erwartete Löschgröße vor der
             * eigentlichen Bereinigung bestimmen.
             */
            var expectedDeletedBytes =
                checked(
                    (ulong)new FileInfo(
                        sandbox.NormalFile).Length
                    + (ulong)new FileInfo(
                        sandbox.NestedNormalFile).Length);

            WindowsTemporaryFilesHandleCleanupEngine
                .ExecutionResult result;

            /*
             * Die Dateisperre bleibt während
             * des gesamten Bereinigungslaufs
             * aktiv.
             *
             * Andere Dateien sollen trotzdem
             * erfolgreich bearbeitet werden.
             */
            using (var fileLock = new FileStream(
                sandbox.LockedFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None))
            {
                using var root =
                    WindowsTemporaryFilesHandleAccess
                        .OpenRootDirectory(
                            sandbox.TargetDirectory);

                result =
                    WindowsTemporaryFilesHandleCleanupEngine
                        .Execute(root);
            }

            // =================================
            // PRÜFUNG 1: GESAMTSTATUS
            // =================================

            Verify(
                "Bereinigung erfolgreich gestartet und beendet",
                result.WasStarted
                && !result.WasCancelled
                && string.IsNullOrWhiteSpace(
                    result.ErrorMessage),
                results);

            // =================================
            // PRÜFUNG 2: LÖSCHZÄHLER
            // =================================

            Verify(
                "Zwei Dateien und ein leerer Ordner entfernt",
                result.DeletedFileCount == 2
                && result.DeletedDirectoryCount == 1,
                results);

            // =================================
            // PRÜFUNG 3: SPEICHERPLATZ
            // =================================

            Verify(
                "Freigegebene Dateigröße korrekt dokumentiert",
                result.DeletedSizeBytes
                    == expectedDeletedBytes,
                results);

            // =================================
            // PRÜFUNG 4: AUSSCHLUSSZÄHLER
            // =================================

            /*
             * Insgesamt acht Einträge:
             *
             * Stamm:
             * - normale Datei
             * - gesperrte Datei
             * - schreibgeschützte Datei
             * - Systemdatei
             * - Nested
             * - Empty
             *
             * Nested:
             * - normale Datei
             * - schreibgeschützte Datei
             *
             * Fünf Ausschlüsse:
             * - 1 Dateisperre
             * - 2 Schreibschutz
             * - 1 Systemschutz
             * - 1 nicht leerer Ordner
             */
            Verify(
                "Ausschlüsse ohne technische Fehler erfasst",
                result.ProcessedEntryCount == 8
                && result.SkippedEntryCount == 5
                && result.FailedEntryCount == 0,
                results);

            // =================================
            // PRÜFUNG 5: AUSSCHLUSSGRÜNDE
            // =================================

            var reasons =
                result.SkippedEntryReasons
                    .ToDictionary(
                        entry => entry.Reason,
                        entry => entry.Count);

            Verify(
                "Dateisperre, Schreibschutz und Systemschutz korrekt klassifiziert",
                reasons.Count == 4
                && GetReasonCount(
                    reasons,
                    CleanupSkippedEntryReason.LockedByProcess) == 1
                && GetReasonCount(
                    reasons,
                    CleanupSkippedEntryReason.ReadOnly) == 2
                && GetReasonCount(
                    reasons,
                    CleanupSkippedEntryReason.SystemProtected) == 1
                && GetReasonCount(
                    reasons,
                    CleanupSkippedEntryReason.DirectoryNotEmpty) == 1,
                results);

            // =================================
            // PRÜFUNG 6: DATEIEN GELÖSCHT
            // =================================

            Verify(
                "Normale Dateien tatsächlich entfernt",
                !File.Exists(sandbox.NormalFile)
                && !File.Exists(sandbox.NestedNormalFile),
                results);

            // =================================
            // PRÜFUNG 7: LEERER ORDNER
            // =================================

            Verify(
                "Leerer Unterordner tatsächlich entfernt",
                !Directory.Exists(sandbox.EmptyDirectory),
                results);

            // =================================
            // PRÜFUNG 8: GESCHÜTZTER UNTERORDNER
            // =================================

            Verify(
                "Nicht leerer Unterordner und geschützte Datei erhalten",
                Directory.Exists(sandbox.NestedDirectory)
                && File.Exists(sandbox.NestedReadOnlyFile)
                && File.ReadAllText(
                    sandbox.NestedReadOnlyFile)
                    == NestedReadOnlyContent
                && HasAttribute(
                    sandbox.NestedReadOnlyFile,
                    FileAttributes.ReadOnly),
                results);

            // =================================
            // PRÜFUNG 9: STAMMSCHUTZ
            // =================================

            Verify(
                "Gesperrte und geschützte Stammdateien unverändert",
                File.ReadAllText(sandbox.LockedFile)
                    == LockedContent
                && File.ReadAllText(sandbox.ReadOnlyFile)
                    == ReadOnlyContent
                && File.ReadAllText(sandbox.SystemFile)
                    == SystemContent
                && HasAttribute(
                    sandbox.ReadOnlyFile,
                    FileAttributes.ReadOnly)
                && HasAttribute(
                    sandbox.SystemFile,
                    FileAttributes.System)
                && Directory.Exists(
                    sandbox.TargetDirectory)
                && File.ReadAllText(sandbox.OutsideFile)
                    == OutsideContent,
                results);
        }

        // =====================================
        // PRÜFUNG 10: ABBRUCH VOR BEGINN
        // =====================================

        using (var cancelledSandbox = new TestSandbox())
        {
            using var root =
                WindowsTemporaryFilesHandleAccess
                    .OpenRootDirectory(
                        cancelledSandbox.TargetDirectory);

            using var cancellation =
                new CancellationTokenSource();

            cancellation.Cancel();

            var cancelledResult =
                WindowsTemporaryFilesHandleCleanupEngine
                    .Execute(
                        root,
                        cancellation.Token);

            Verify(
                "Abbruch vor Start verhindert jede Bereinigung",
                cancelledResult.WasCancelled
                && !cancelledResult.WasStarted
                && cancelledResult.DeletedFileCount == 0
                && cancelledResult.DeletedDirectoryCount == 0
                && cancelledResult.ProcessedEntryCount == 0
                && File.ReadAllText(
                    cancelledSandbox.NormalFile)
                    == NormalContent
                && File.ReadAllText(
                    cancelledSandbox.NestedNormalFile)
                    == NestedNormalContent
                && Directory.Exists(
                    cancelledSandbox.EmptyDirectory),
                results);
        }

        return
            $"Alle {results.Count} Engine-Integrationstests "
            + "erfolgreich."
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                results);
    }

    // =============================================
    // TESTHILFSMETHODEN
    // =============================================

    private static long GetReasonCount(
        IReadOnlyDictionary<
            CleanupSkippedEntryReason,
            long> reasons,
        CleanupSkippedEntryReason reason)
    {
        return reasons.TryGetValue(
            reason,
            out var count)
                ? count
                : 0;
    }

    private static bool HasAttribute(
        string path,
        FileAttributes attribute)
    {
        return
            (File.GetAttributes(path) & attribute) != 0;
    }

    private static void Verify(
        string description,
        bool condition,
        ICollection<string> results)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                "Engine-Integrationstest fehlgeschlagen: "
                + description);
        }

        results.Add(
            "OK: " + description);
    }

    private static void EnsureNonElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Die Tests benötigen Windows.");
        }

        using var identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(identity);

        if (principal.IsInRole(
                WindowsBuiltInRole.Administrator))
        {
            throw new InvalidOperationException(
                "SICHERHEITSSTOPP: "
                + "Die Tests dürfen nicht "
                + "mit Administratorrechten laufen.");
        }
    }

    // =============================================
    // KÜNSTLICHE TESTUMGEBUNG
    // =============================================

    private sealed class TestSandbox : IDisposable
    {
        public string RootDirectory { get; }
        public string TargetDirectory { get; }
        public string NestedDirectory { get; }
        public string EmptyDirectory { get; }
        public string OutsideDirectory { get; }

        public string NormalFile { get; }
        public string LockedFile { get; }
        public string ReadOnlyFile { get; }
        public string SystemFile { get; }
        public string NestedNormalFile { get; }
        public string NestedReadOnlyFile { get; }
        public string OutsideFile { get; }

        public TestSandbox()
        {
            var tempRoot =
                Path.GetFullPath(
                    Path.GetTempPath());

            var sharedRoot =
                Path.Combine(
                    tempRoot,
                    "WeberIT.Checkup");

            var sandboxParent =
                Path.Combine(
                    sharedRoot,
                    "WindowsTempSandbox");

            RootDirectory =
                Path.Combine(
                    sandboxParent,
                    "Engine-"
                    + Guid.NewGuid().ToString("N"));

            TargetDirectory =
                Path.Combine(
                    RootDirectory,
                    "Temp");

            NestedDirectory =
                Path.Combine(
                    TargetDirectory,
                    "Nested");

            EmptyDirectory =
                Path.Combine(
                    TargetDirectory,
                    "Empty");

            OutsideDirectory =
                Path.Combine(
                    RootDirectory,
                    "OutsideTarget");

            NormalFile =
                Path.Combine(
                    TargetDirectory,
                    "normal.txt");

            LockedFile =
                Path.Combine(
                    TargetDirectory,
                    "locked.txt");

            ReadOnlyFile =
                Path.Combine(
                    TargetDirectory,
                    "readonly.txt");

            SystemFile =
                Path.Combine(
                    TargetDirectory,
                    "system.txt");

            NestedNormalFile =
                Path.Combine(
                    NestedDirectory,
                    "nested-normal.txt");

            NestedReadOnlyFile =
                Path.Combine(
                    NestedDirectory,
                    "nested-readonly.txt");

            OutsideFile =
                Path.Combine(
                    OutsideDirectory,
                    "outside.txt");

            if (!Directory.Exists(tempRoot)
                || IsReparsePoint(tempRoot))
            {
                throw new InvalidOperationException(
                    "Der temporäre Stammordner "
                    + "ist nicht zulässig.");
            }

            EnsurePlainDirectory(sharedRoot);
            EnsurePlainDirectory(sandboxParent);

            if (Directory.Exists(RootDirectory)
                || File.Exists(RootDirectory))
            {
                throw new InvalidOperationException(
                    "Der GUID-Sandboxordner "
                    + "existiert bereits.");
            }

            EnsurePlainDirectory(RootDirectory);
            EnsurePlainDirectory(TargetDirectory);
            EnsurePlainDirectory(NestedDirectory);
            EnsurePlainDirectory(EmptyDirectory);
            EnsurePlainDirectory(OutsideDirectory);

            File.WriteAllText(
                NormalFile,
                NormalContent);

            File.WriteAllText(
                LockedFile,
                LockedContent);

            File.WriteAllText(
                ReadOnlyFile,
                ReadOnlyContent);

            File.SetAttributes(
                ReadOnlyFile,
                File.GetAttributes(ReadOnlyFile)
                    | FileAttributes.ReadOnly);

            File.WriteAllText(
                SystemFile,
                SystemContent);

            File.SetAttributes(
                SystemFile,
                File.GetAttributes(SystemFile)
                    | FileAttributes.System);

            File.WriteAllText(
                NestedNormalFile,
                NestedNormalContent);

            File.WriteAllText(
                NestedReadOnlyFile,
                NestedReadOnlyContent);

            File.SetAttributes(
                NestedReadOnlyFile,
                File.GetAttributes(NestedReadOnlyFile)
                    | FileAttributes.ReadOnly);

            File.WriteAllText(
                OutsideFile,
                OutsideContent);
        }

        public void Dispose()
        {
            /*
             * Ausschließlich eigene Dateien
             * mit bekanntem Inhalt entfernen.
             *
             * Ordner nur löschen, wenn sie
             * tatsächlich leer sind.
             *
             * Keine rekursive Löschung.
             */
            TryDeleteKnownFile(
                NormalFile,
                NormalContent);

            TryDeleteKnownFile(
                LockedFile,
                LockedContent);

            TryDeleteKnownFile(
                ReadOnlyFile,
                ReadOnlyContent);

            TryDeleteKnownFile(
                SystemFile,
                SystemContent);

            TryDeleteKnownFile(
                NestedNormalFile,
                NestedNormalContent);

            TryDeleteKnownFile(
                NestedReadOnlyFile,
                NestedReadOnlyContent);

            TryDeleteKnownFile(
                OutsideFile,
                OutsideContent);

            TryDeleteEmptyDirectory(NestedDirectory);
            TryDeleteEmptyDirectory(EmptyDirectory);
            TryDeleteEmptyDirectory(TargetDirectory);
            TryDeleteEmptyDirectory(OutsideDirectory);
            TryDeleteEmptyDirectory(RootDirectory);
        }

        private static void EnsurePlainDirectory(
            string path)
        {
            if (!Directory.Exists(path))
            {
                if (File.Exists(path))
                {
                    throw new InvalidOperationException(
                        "Ein Sandboxpfad ist bereits "
                        + "durch eine Datei belegt.");
                }

                Directory.CreateDirectory(path);
            }

            if (IsReparsePoint(path))
            {
                throw new InvalidOperationException(
                    "Ein Sandboxordner "
                    + "ist ein Reparse Point: "
                    + path);
            }
        }

        private static bool IsReparsePoint(
            string path)
        {
            return
                (File.GetAttributes(path)
                    & FileAttributes.ReparsePoint) != 0;
        }

        private static void TryDeleteKnownFile(
            string path,
            string expectedContent)
        {
            try
            {
                if (!File.Exists(path)
                    || IsReparsePoint(path)
                    || File.ReadAllText(path)
                        != expectedContent)
                {
                    return;
                }

                var attributes =
                    File.GetAttributes(path);

                if ((attributes & FileAttributes.ReadOnly) != 0
                    || (attributes & FileAttributes.System) != 0)
                {
                    /*
                     * Ausschließlich Attribute
                     * selbst erzeugter Dateien
                     * zurücksetzen.
                     */
                    File.SetAttributes(
                        path,
                        attributes
                            & ~FileAttributes.ReadOnly
                            & ~FileAttributes.System);
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

        private static void TryDeleteEmptyDirectory(
            string path)
        {
            try
            {
                if (Directory.Exists(path)
                    && !IsReparsePoint(path)
                    && !Directory
                        .EnumerateFileSystemEntries(path)
                        .Any())
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
}

#endif
