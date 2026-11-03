
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
/// Sprint 55.14.31d
///
/// Integrationstests für die vollständige
/// handle-basierte Bereinigungs-Engine.
///
/// Geprüft werden:
/// - Regulärer Bereinigungslauf.
/// - Löschzähler und Dateigrößen.
/// - Dateisperren und Schutzattribute.
/// - Unveränderte Außenbereiche.
/// - Abbruch vor Ausführungsbeginn.
/// - Abbruch nach der ersten Dateilöschung.
/// - Erhalt bestätigter Teilergebnisse.
///
/// Sicherheitsregeln:
/// - Ausschließlich eigene GUID-Sandboxen.
/// - Keine Administratorrechte.
/// - Kein produktiver Windows-Temp-Zugriff.
/// - Keine rekursive Testdatenbereinigung.
/// - Keine fremden Dateien verändern.
/// - Keine automatische Wiederholung.
/// - Keine zeitgesteuerten Testabbrüche.
///
/// Im Abbruchszenario bleibt locked.txt
/// während der Engine-Ausführung ausdrücklich
/// exklusiv geöffnet.
///
/// Der produktive Freigabeschalter bleibt
/// unverändert geschlossen.
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
                File.Exists(sandbox.LockedFile)
                && File.ReadAllText(sandbox.LockedFile)
                    == LockedContent
                && File.Exists(sandbox.ReadOnlyFile)
                && File.ReadAllText(sandbox.ReadOnlyFile)
                    == ReadOnlyContent
                && File.Exists(sandbox.SystemFile)
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
                && File.Exists(sandbox.OutsideFile)
                && File.ReadAllText(sandbox.OutsideFile)
                    == OutsideContent,
                results);
        }

        // =====================================
        // PRÜFUNG 10: ABBRUCH VOR UND
        // WÄHREND DER BEREINIGUNG
        // =====================================

        /*
         * Diese Prüfung besteht aus zwei
         * unabhängigen Szenarien:
         *
         * A: Abbruch vor Ausführungsbeginn.
         *
         * B: Abbruch unmittelbar nach der
         *    ersten bestätigten Dateilöschung.
         *
         * Beide Szenarien verwenden eigene
         * GUID-Sandboxen.
         *
         * Damit bleiben zehn fachliche
         * Engine-Integrationstests erhalten.
         */

        // =====================================
        // SZENARIO A: ABBRUCH VOR START
        // =====================================

        var cancellationBeforeStartIsSafe =
            false;

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

            cancellationBeforeStartIsSafe =
                cancelledResult.WasCancelled
                && !cancelledResult.WasStarted
                && cancelledResult.DeletedFileCount == 0
                && cancelledResult.DeletedDirectoryCount == 0
                && cancelledResult.ProcessedEntryCount == 0
                && string.IsNullOrWhiteSpace(
                    cancelledResult.ErrorMessage)
                && File.Exists(
                    cancelledSandbox.NormalFile)
                && File.ReadAllText(
                    cancelledSandbox.NormalFile)
                    == NormalContent
                && File.Exists(
                    cancelledSandbox.NestedNormalFile)
                && File.ReadAllText(
                    cancelledSandbox.NestedNormalFile)
                    == NestedNormalContent
                && Directory.Exists(
                    cancelledSandbox.EmptyDirectory)
                && File.Exists(
                    cancelledSandbox.OutsideFile)
                && File.ReadAllText(
                    cancelledSandbox.OutsideFile)
                    == OutsideContent;
        }

        // =====================================
        // SZENARIO B: ABBRUCH NACH ERSTER
        // ERFOLGREICHER DATEILÖSCHUNG
        // =====================================

        var cancellationDuringExecutionIsSafe =
            false;

        using (var runningSandbox = new TestSandbox())
        {
            /*
             * Zwei normale, selbst erzeugte
             * Dateien sind löschbar:
             *
             * - normal.txt
             * - nested-normal.txt
             *
             * Die übrigen Testdateien bleiben
             * geschützt oder gesperrt.
             */
            var normalFileBytes =
                checked(
                    (ulong)new FileInfo(
                        runningSandbox.NormalFile).Length);

            var nestedFileBytes =
                checked(
                    (ulong)new FileInfo(
                        runningSandbox.NestedNormalFile).Length);

            using var cancellation =
                new CancellationTokenSource();

            var callbackCount =
                0;

            WindowsTemporaryFilesHandleCleanupEngine
                .ExecutionResult result;

            // =================================
            // EXKLUSIVE DATEISPERRE
            // =================================

            /*
             * WICHTIGE KORREKTUR:
             *
             * Im vorigen Testentwurf wurde
             * locked.txt nicht exklusiv
             * geöffnet.
             *
             * Dadurch war die Datei regulär
             * löschbar und konnte bereits
             * die erste gelöschte Datei sein.
             *
             * Jetzt bleibt sie wie im
             * regulären Integrationstest
             * während der gesamten
             * Engine-Ausführung gesperrt.
             *
             * Erst nach Freigabe der Sperre
             * kontrollieren wir ihren Inhalt.
             */
            using (var fileLock = new FileStream(
                runningSandbox.LockedFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None))
            {
                using var root =
                    WindowsTemporaryFilesHandleAccess
                        .OpenRootDirectory(
                            runningSandbox.TargetDirectory);

                /*
                 * Die Engine ruft den Callback
                 * genau nach der ersten
                 * erfolgreich dokumentierten
                 * Dateilöschung auf.
                 *
                 * Kein Thread.Sleep.
                 * Kein Timer.
                 * Kein zweiter Prozess.
                 *
                 * Der Callback fordert nur
                 * den kontrollierten Abbruch an.
                 */
                result =
                    WindowsTemporaryFilesHandleCleanupEngine
                        .ExecuteForCancellationRegressionTest(
                            root,
                            cancellation.Token,
                            () =>
                            {
                                callbackCount++;
                                cancellation.Cancel();
                            });
            }

            // =================================
            // TATSÄCHLICHE DATEIZUSTÄNDE
            // =================================

            var normalFileStillExists =
                File.Exists(
                    runningSandbox.NormalFile);

            var nestedFileStillExists =
                File.Exists(
                    runningSandbox.NestedNormalFile);

            /*
             * Genau eine der beiden Dateien
             * muss erhalten geblieben sein.
             *
             * Welche zuerst verarbeitet wird,
             * ist bewusst nicht vorgegeben.
             */
            var exactlyOneFileRemains =
                normalFileStillExists
                != nestedFileStillExists;

            var remainingFileIsUnchanged =
                normalFileStillExists
                    ? File.ReadAllText(
                        runningSandbox.NormalFile)
                        == NormalContent
                    : nestedFileStillExists
                      && File.ReadAllText(
                          runningSandbox.NestedNormalFile)
                          == NestedNormalContent;

            /*
             * Wenn die normale Datei noch
             * vorhanden ist, muss die Datei
             * im Unterordner gelöscht worden
             * sein – und umgekehrt.
             */
            var expectedDeletedBytes =
                normalFileStillExists
                    ? nestedFileBytes
                    : normalFileBytes;

            /*
             * Der leere Testunterordner kann
             * bereits vor der ersten Datei
             * verarbeitet worden sein.
             *
             * Deshalb darf sein Zustand
             * nicht von einer angenommenen
             * Aufzählungsreihenfolge abhängen.
             */
            var emptyDirectoryStillExists =
                Directory.Exists(
                    runningSandbox.EmptyDirectory);

            var directoryCountIsConsistent =
                result.DeletedDirectoryCount
                    == (emptyDirectoryStillExists ? 0 : 1);

            // =================================
            // SCHUTZ DER ÜBRIGEN DATEIEN
            // =================================

            /*
             * Fehlende Dateien sollen zu
             * einer fehlgeschlagenen
             * Testbedingung führen.
             *
             * Deshalb vor jedem Lesezugriff
             * zunächst die Existenz prüfen.
             */
            var protectedEntriesAreUnchanged =
                Directory.Exists(
                    runningSandbox.NestedDirectory)
                && File.Exists(
                    runningSandbox.ReadOnlyFile)
                && File.ReadAllText(
                    runningSandbox.ReadOnlyFile)
                    == ReadOnlyContent
                && File.Exists(
                    runningSandbox.NestedReadOnlyFile)
                && File.ReadAllText(
                    runningSandbox.NestedReadOnlyFile)
                    == NestedReadOnlyContent
                && File.Exists(
                    runningSandbox.SystemFile)
                && File.ReadAllText(
                    runningSandbox.SystemFile)
                    == SystemContent
                && File.Exists(
                    runningSandbox.LockedFile)
                && File.ReadAllText(
                    runningSandbox.LockedFile)
                    == LockedContent
                && HasAttribute(
                    runningSandbox.ReadOnlyFile,
                    FileAttributes.ReadOnly)
                && HasAttribute(
                    runningSandbox.NestedReadOnlyFile,
                    FileAttributes.ReadOnly)
                && HasAttribute(
                    runningSandbox.SystemFile,
                    FileAttributes.System);

            var outsideAreaIsUnchanged =
                Directory.Exists(
                    runningSandbox.TargetDirectory)
                && File.Exists(
                    runningSandbox.OutsideFile)
                && File.ReadAllText(
                    runningSandbox.OutsideFile)
                    == OutsideContent;

            // =================================
            // GESAMTBEWERTUNG
            // =================================

            cancellationDuringExecutionIsSafe =
                callbackCount == 1
                && cancellation.IsCancellationRequested
                && result.WasStarted
                && result.WasCancelled
                && result.DeletedFileCount == 1
                && result.DeletedSizeBytes
                    == expectedDeletedBytes
                && result.FailedEntryCount == 0
                && string.IsNullOrWhiteSpace(
                    result.ErrorMessage)
                && exactlyOneFileRemains
                && remainingFileIsUnchanged
                && directoryCountIsConsistent
                && protectedEntriesAreUnchanged
                && outsideAreaIsUnchanged;
        }

        Verify(
            "Abbruch vor Start und nach erster Löschung sicher dokumentiert",
            cancellationBeforeStartIsSafe
            && cancellationDuringExecutionIsSafe,
            results);

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
                File.GetAttributes(
                    NestedReadOnlyFile)
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
