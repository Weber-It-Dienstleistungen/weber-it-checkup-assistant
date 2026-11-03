
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Sprint 55.14.32a
///
/// Handle-basierte Windows-Temp-Bereinigungs-Engine.
///
/// Sicherheitsregeln:
/// - Ausschließlich bereits geöffneten Stammhandle verwenden.
/// - Stammordner niemals löschen.
/// - Verknüpfungen und geschützte Einträge überspringen.
/// - Identitätsprüfungen durch die Handle-Klassen.
/// - Keine Berechtigungen verändern.
/// - Keine erzwungene rekursive Löschung.
/// - Keine automatische Wiederholung.
/// - Bereits erfolgte Änderungen dokumentieren.
///
/// Schwerwiegende Fehler müssen auch aus
/// verschachtelten Verzeichnissen bis zur
/// zentralen Fehlerbehandlung gelangen.
///
/// Die produktive Freigabe bleibt im Executor
/// ausdrücklich deaktiviert.
///
/// Testfunktionen sind nur im Debug-Build
/// verfügbar.
/// </summary>
internal static class WindowsTemporaryFilesHandleCleanupEngine
{
    private const int MaximumDirectoryDepth = 64;

    private const long MaximumProcessedEntries = 100000;

    // =============================================
    // SCHWERWIEGENDER ENGINE-FEHLER
    // =============================================

    /// <summary>
    /// Kennzeichnet einen Fehler, nach dem
    /// der gesamte Bereinigungslauf beendet
    /// werden muss.
    ///
    /// Solche Fehler dürfen nicht als
    /// gewöhnlicher Dateifehler behandelt
    /// und anschließend ignoriert werden.
    /// </summary>
    private sealed class FatalCleanupException : Exception
    {
        public FatalCleanupException(string message)
            : base(message)
        {
        }
    }

    // =============================================
    // ERGEBNISMODELL
    // =============================================

    internal sealed class ExecutionResult
    {
        public bool WasStarted { get; init; }

        public bool WasCancelled { get; init; }

        public long DeletedFileCount { get; init; }

        public long DeletedDirectoryCount { get; init; }

        public ulong DeletedSizeBytes { get; init; }

        public long FailedEntryCount { get; init; }

        public long SkippedEntryCount { get; init; }

        public long ProcessedEntryCount { get; init; }

        public IReadOnlyList<CleanupSkippedEntryReasonCount>
            SkippedEntryReasons
        {
            get;
            init;
        } = Array.Empty<CleanupSkippedEntryReasonCount>();

        public string ErrorMessage { get; init; } =
            string.Empty;
    }

    // =============================================
    // NORMALER ENGINE-AUFRUF
    // =============================================

    internal static ExecutionResult Execute(
        SafeFileHandle approvedRootDirectory,
        CancellationToken cancellationToken = default)
    {
        return ExecuteCore(
            approvedRootDirectory,
            cancellationToken,
            onFirstDeletedFile: null);
    }

#if DEBUG

    // =============================================
    // DEBUG: KONTROLLIERTER ABBRUCH
    // =============================================

    /// <summary>
    /// Fordert nach der ersten erfolgreich
    /// dokumentierten Dateilöschung über
    /// einen Callback einen Abbruch an.
    ///
    /// Ausschließlich für Regressionstests.
    /// </summary>
    internal static ExecutionResult
        ExecuteForCancellationRegressionTest(
            SafeFileHandle approvedRootDirectory,
            CancellationToken cancellationToken,
            Action cancelAfterFirstDeletedFile)
    {
        ArgumentNullException.ThrowIfNull(
            cancelAfterFirstDeletedFile);

        return ExecuteCore(
            approvedRootDirectory,
            cancellationToken,
            cancelAfterFirstDeletedFile);
    }

    // =============================================
    // DEBUG: SCHWERWIEGENDER FEHLER
    // =============================================

    /// <summary>
    /// Simuliert nach der ersten erfolgreich
    /// dokumentierten Dateilöschung einen
    /// schwerwiegenden Engine-Fehler.
    ///
    /// Ausschließlich für Regressionstests
    /// mit einer eigenen GUID-Sandbox.
    ///
    /// Der Fehler muss den gesamten Lauf
    /// beenden und bereits dokumentierte
    /// Änderungen im Ergebnis erhalten.
    ///
    /// Die Methode autorisiert keine
    /// produktiven Windows-Temp-Zielpfade.
    /// </summary>
    internal static ExecutionResult
        ExecuteForUnexpectedFailureRegressionTest(
            SafeFileHandle approvedRootDirectory)
    {
        return ExecuteCore(
            approvedRootDirectory,
            CancellationToken.None,
            () => throw new FatalCleanupException(
                "Simulierter schwerwiegender Fehler "
                + "nach der ersten Dateilöschung."));
    }

#endif

    // =============================================
    // GEMEINSAMER ENGINE-KERN
    // =============================================

    private static ExecutionResult ExecuteCore(
        SafeFileHandle approvedRootDirectory,
        CancellationToken cancellationToken,
        Action? onFirstDeletedFile)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Die handle-basierte Bereinigung "
                + "benötigt Windows.");
        }

        ArgumentNullException.ThrowIfNull(
            approvedRootDirectory);

        if (approvedRootDirectory.IsInvalid
            || approvedRootDirectory.IsClosed)
        {
            throw new InvalidOperationException(
                "Das freigegebene Stammhandle "
                + "ist ungültig.");
        }

        // =========================================
        // STAMMHANDLE PRÜFEN
        // =========================================

        /*
         * Die produktive Autorisierung des
         * Zielbereichs ist Aufgabe des Aufrufers.
         *
         * Die Engine prüft zusätzlich die
         * Eigenschaften des geöffneten Stamms.
         */
        var rootSnapshot =
            WindowsTemporaryFilesHandleAccess
                .ReadSnapshot(
                    approvedRootDirectory);

        if (!rootSnapshot.IsDirectory
            || rootSnapshot.IsReparsePoint
            || rootSnapshot.IsReadOnly
            || rootSnapshot.IsSystemProtected
            || rootSnapshot.FileIndex == 0)
        {
            throw new InvalidOperationException(
                "Der freigegebene Stammordner "
                + "ist für die Bereinigung "
                + "nicht zulässig.");
        }

        var state =
            new ExecutionState(
                onFirstDeletedFile);

        // =========================================
        // ABBRUCH VOR BEGINN
        // =========================================

        if (cancellationToken.IsCancellationRequested)
        {
            state.WasCancelled = true;

            return state.CreateResult();
        }

        state.WasStarted = true;

        // =========================================
        // BEREINIGUNG AUSFÜHREN
        // =========================================

        try
        {
            ProcessDirectory(
                approvedRootDirectory,
                depth: 0,
                state,
                cancellationToken);

            /*
             * Auch einen unmittelbar nach
             * der letzten Dateiverarbeitung
             * angeforderten Abbruch erfassen.
             */
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            state.WasCancelled = true;
        }
        catch (Exception exception)
        {
            /*
             * Schwerwiegender Fehler:
             *
             * Keine weitere Verarbeitung.
             * Kein automatischer Wiederanlauf.
             *
             * Bereits dokumentierte Löschungen
             * bleiben vollständig erhalten.
             */
            state.RecordFatalError(
                exception);
        }

        return state.CreateResult();
    }

    // =============================================
    // VERZEICHNIS VERARBEITEN
    // =============================================

    private static bool ProcessDirectory(
        SafeFileHandle directoryHandle,
        int depth,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (depth > MaximumDirectoryDepth)
        {
            throw new FatalCleanupException(
                "Die maximale Sicherheitstiefe "
                + "der Verzeichnisdurchsuchung "
                + "wurde überschritten.");
        }

        IReadOnlyList<
            WindowsTemporaryFilesHandleDirectoryEnumeration.DirectoryEntry>
            entries;

        // =========================================
        // DIREKTE EINTRÄGE AUFZÄHLEN
        // =========================================

        try
        {
            entries =
                WindowsTemporaryFilesHandleDirectoryEnumeration
                    .EnumerateDirectChildren(
                        directoryHandle,
                        cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FatalCleanupException)
        {
            throw;
        }
        catch (Exception exception)
        {
            state.RecordEntryException(
                exception,
                allowDirectoryNotEmpty: false);

            return false;
        }

        // =========================================
        // EINTRÄGE EINZELN VERARBEITEN
        // =========================================

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            state.ProcessedEntryCount++;

            if (state.ProcessedEntryCount
                > MaximumProcessedEntries)
            {
                /*
                 * Die Sicherheitsgrenze ist
                 * global für den gesamten
                 * Bereinigungslauf gültig.
                 *
                 * Der Fehler muss daher auch
                 * aus Unterordnern bis nach
                 * ExecuteCore gelangen.
                 */
                throw new FatalCleanupException(
                    "Die Sicherheitsgrenze für "
                    + "die Gesamtzahl verarbeiteter "
                    + "Einträge wurde überschritten.");
            }

            ProcessEntry(
                directoryHandle,
                entry,
                depth,
                state,
                cancellationToken);
        }

        return true;
    }

    // =============================================
    // EINTRAG UNTERSUCHEN
    // =============================================

    private static void ProcessEntry(
        SafeFileHandle parentDirectory,
        WindowsTemporaryFilesHandleDirectoryEnumeration.DirectoryEntry
            entry,
        int depth,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        /*
         * Bereits bei der Aufzählung
         * erkannte Verknüpfungen niemals
         * betreten.
         */
        if (entry.IsListedReparsePoint)
        {
            state.RecordSkip(
                CleanupSkippedEntryReason.ReparsePoint);

            return;
        }

        WindowsTemporaryFilesHandleAccess.EntrySnapshot
            inspectedSnapshot;

        // =========================================
        // EINTRAG ÜBER HANDLE UNTERSUCHEN
        // =========================================

        try
        {
            using var inspectionHandle =
                WindowsTemporaryFilesHandleAccess
                    .OpenChildForInspection(
                        parentDirectory,
                        entry.Name);

            inspectedSnapshot =
                WindowsTemporaryFilesHandleAccess
                    .ReadSnapshot(
                        inspectionHandle);
        }
        catch (Exception exception)
        {
            state.RecordEntryException(
                exception,
                allowDirectoryNotEmpty: false);

            return;
        }

        // =========================================
        // SCHUTZATTRIBUTE BERÜCKSICHTIGEN
        // =========================================

        if (inspectedSnapshot.IsReparsePoint)
        {
            state.RecordSkip(
                CleanupSkippedEntryReason.ReparsePoint);

            return;
        }

        if (inspectedSnapshot.IsSystemProtected)
        {
            state.RecordSkip(
                CleanupSkippedEntryReason.SystemProtected);

            return;
        }

        if (inspectedSnapshot.IsReadOnly)
        {
            state.RecordSkip(
                CleanupSkippedEntryReason.ReadOnly);

            return;
        }

        // =========================================
        // DATEI ODER UNTERORDNER
        // =========================================

        if (inspectedSnapshot.IsDirectory)
        {
            ProcessChildDirectory(
                parentDirectory,
                entry.Name,
                inspectedSnapshot,
                depth,
                state,
                cancellationToken);

            return;
        }

        ProcessFile(
            parentDirectory,
            entry.Name,
            inspectedSnapshot,
            state,
            cancellationToken);
    }

    // =============================================
    // DATEI LÖSCHEN
    // =============================================

    private static void ProcessFile(
        SafeFileHandle parentDirectory,
        string entryName,
        WindowsTemporaryFilesHandleAccess.EntrySnapshot
            inspectedSnapshot,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // =========================================
        // KONTROLLIERTE DATEILÖSCHUNG
        // =========================================

        try
        {
            /*
             * Die Löschklasse öffnet die Datei
             * erneut relativ zum Elternhandle,
             * kontrolliert ihre Identität
             * und löscht ausschließlich über
             * den geprüften Handle.
             */
            var deleted =
                WindowsTemporaryFilesHandleDeletion
                    .DeleteInspectedFile(
                        parentDirectory,
                        entryName,
                        inspectedSnapshot);

            /*
             * Das bestätigte Ergebnis wird
             * unmittelbar dokumentiert.
             */
            state.RecordDeletedFile(
                deleted.SizeBytes);
        }
        catch (Exception exception)
        {
            /*
             * Gewöhnliche Dateifehler
             * betreffen nur diesen Eintrag.
             */
            state.RecordEntryException(
                exception,
                allowDirectoryNotEmpty: false);

            return;
        }

        // =========================================
        // DEBUG-TESTBENACHRICHTIGUNG
        // =========================================

        /*
         * Der Debug-Callback wird erst
         * NACH dem erfolgreichen Abschluss
         * und der Ergebnisdokumentation
         * aufgerufen.
         *
         * Er liegt bewusst außerhalb des
         * gewöhnlichen Dateifehlerhandlers.
         *
         * Dadurch kann ein injizierter
         * schwerwiegender Fehler korrekt
         * bis zur zentralen Behandlung
         * propagiert werden.
         *
         * Im normalen Aufruf ist kein
         * Callback hinterlegt.
         */
        state.NotifyFirstDeletedFile();
    }

    // =============================================
    // UNTERORDNER VERARBEITEN
    // =============================================

    private static void ProcessChildDirectory(
        SafeFileHandle parentDirectory,
        string entryName,
        WindowsTemporaryFilesHandleAccess.EntrySnapshot
            inspectedSnapshot,
        int parentDepth,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // =========================================
        // TIEFENBEGRENZUNG
        // =========================================

        if (parentDepth >= MaximumDirectoryDepth)
        {
            state.RecordSkip(
                CleanupSkippedEntryReason.OtherSafetySkip);

            return;
        }

        var completed = false;

        // =========================================
        // UNTERORDNER DURCHSUCHEN
        // =========================================

        try
        {
            /*
             * Unterordner relativ zum
             * geöffneten Elternhandle öffnen.
             *
             * Die Identität muss mit dem
             * vorherigen Snapshot
             * übereinstimmen.
             *
             * Vor der späteren Löschung
             * wird der Unterordnerhandle
             * vollständig geschlossen.
             */
            using (var childDirectory =
                WindowsTemporaryFilesHandleDirectoryEnumeration
                    .OpenChildDirectoryForEnumeration(
                        parentDirectory,
                        entryName,
                        inspectedSnapshot))
            {
                completed =
                    ProcessDirectory(
                        childDirectory,
                        parentDepth + 1,
                        state,
                        cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FatalCleanupException)
        {
            /*
             * NEU:
             *
             * Globale Sicherheitsstopps
             * dürfen nicht als gewöhnliche
             * Unterordnerfehler behandelt
             * werden.
             *
             * Der Fehler muss über alle
             * Rekursionsebenen bis zur
             * zentralen Behandlung gelangen.
             */
            throw;
        }
        catch (Exception exception)
        {
            state.RecordEntryException(
                exception,
                allowDirectoryNotEmpty: false);

            return;
        }

        // =========================================
        // UNVOLLSTÄNDIGE DURCHSUCHUNG
        // =========================================

        if (!completed)
        {
            /*
             * Der Unterordner darf nicht
             * als vollständig bereinigt
             * behandelt werden.
             */
            state.RecordSkip(
                CleanupSkippedEntryReason.OtherSafetySkip);

            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // =========================================
        // LEEREN UNTERORDNER ENTFERNEN
        // =========================================

        try
        {
            /*
             * Windows entfernt ausschließlich
             * tatsächlich leere Ordner.
             *
             * Identität und Schutzattribute
             * werden am Löschhandle erneut
             * kontrolliert.
             */
            WindowsTemporaryFilesHandleDirectoryDeletion
                .DeleteInspectedEmptyDirectory(
                    parentDirectory,
                    entryName,
                    inspectedSnapshot);

            state.DeletedDirectoryCount++;
        }
        catch (Exception exception)
        {
            state.RecordEntryException(
                exception,
                allowDirectoryNotEmpty: true);
        }
    }

    // =============================================
    // LAUFZUSTAND UND ERGEBNISZÄHLER
    // =============================================

    private sealed class ExecutionState
    {
        private readonly Dictionary<
            CleanupSkippedEntryReason,
            long> _skipReasons = new();

        /*
         * Im normalen Aufruf ist dieser
         * Callback immer null.
         *
         * Nur Debug-Testmethoden können
         * einen Callback übergeben.
         */
        private readonly Action? _onFirstDeletedFile;

        public ExecutionState(
            Action? onFirstDeletedFile)
        {
            _onFirstDeletedFile =
                onFirstDeletedFile;
        }

        public bool WasStarted { get; set; }

        public bool WasCancelled { get; set; }

        public long DeletedFileCount { get; private set; }

        public long DeletedDirectoryCount { get; set; }

        public ulong DeletedSizeBytes { get; private set; }

        public long FailedEntryCount { get; private set; }

        public long SkippedEntryCount { get; private set; }

        public long ProcessedEntryCount { get; set; }

        public string ErrorMessage { get; private set; } =
            string.Empty;

        // =========================================
        // DATEILÖSCHUNG DOKUMENTIEREN
        // =========================================

        public void RecordDeletedFile(ulong sizeBytes)
        {
            DeletedFileCount++;

            /*
             * Einen Überlauf der
             * aufsummierten Dateigröße
             * sicher verhindern.
             */
            if (sizeBytes >
                ulong.MaxValue - DeletedSizeBytes)
            {
                DeletedSizeBytes =
                    ulong.MaxValue;
            }
            else
            {
                DeletedSizeBytes +=
                    sizeBytes;
            }
        }

        // =========================================
        // DEBUG-CALLBACK BENACHRICHTIGEN
        // =========================================

        public void NotifyFirstDeletedFile()
        {
            /*
             * Ausschließlich nach der
             * ersten erfolgreich
             * dokumentierten Dateilöschung.
             *
             * Bei allen regulären Aufrufen
             * geschieht hier nichts.
             */
            if (DeletedFileCount == 1)
            {
                _onFirstDeletedFile?.Invoke();
            }
        }

        // =========================================
        // ERWARTETEN AUSSCHLUSS ERFASSEN
        // =========================================

        public void RecordSkip(
            CleanupSkippedEntryReason reason)
        {
            SkippedEntryCount++;

            if (_skipReasons.TryGetValue(
                    reason,
                    out var count))
            {
                _skipReasons[reason] =
                    count + 1;
            }
            else
            {
                _skipReasons[reason] =
                    1;
            }
        }

        // =========================================
        // DATEISYSTEMFEHLER KLASSIFIZIEREN
        // =========================================

        public void RecordEntryException(
            Exception exception,
            bool allowDirectoryNotEmpty)
        {
            /*
             * Ein erwarteter Sicherheitsstopp
             * ist kein technischer Löschfehler.
             *
             * Insbesondere Identitätswechsel
             * und abgelehnte Verknüpfungen
             * werden als kontrollierter
             * Ausschluss erfasst.
             */
            if (exception is InvalidOperationException
                || exception is ArgumentException)
            {
                RecordSkip(
                    CleanupSkippedEntryReason.OtherSafetySkip);

                return;
            }

            int errorCode;

            if (exception is Win32Exception win32Exception)
            {
                errorCode =
                    win32Exception.NativeErrorCode;
            }
            else if (exception is IOException ioException)
            {
                errorCode =
                    ioException.HResult & 0xFFFF;
            }
            else
            {
                FailedEntryCount++;

                return;
            }

            switch (errorCode)
            {
                // ERROR_FILE_NOT_FOUND
                // ERROR_PATH_NOT_FOUND
                case 2:
                case 3:
                    RecordSkip(
                        CleanupSkippedEntryReason.AlreadyMissing);
                    return;

                // ERROR_SHARING_VIOLATION
                // ERROR_LOCK_VIOLATION
                case 32:
                case 33:
                    RecordSkip(
                        CleanupSkippedEntryReason.LockedByProcess);
                    return;

                // ERROR_DIR_NOT_EMPTY
                case 145 when allowDirectoryNotEmpty:
                    RecordSkip(
                        CleanupSkippedEntryReason.DirectoryNotEmpty);
                    return;

                default:
                    FailedEntryCount++;
                    return;
            }
        }

        // =========================================
        // SCHWERWIEGENDEN FEHLER ERFASSEN
        // =========================================

        public void RecordFatalError(Exception exception)
        {
            FailedEntryCount++;

            ErrorMessage =
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Der Bereinigungslauf wurde "
                      + "unerwartet abgebrochen."
                    : exception.Message;
        }

        // =========================================
        // ERGEBNIS ERSTELLEN
        // =========================================

        public ExecutionResult CreateResult()
        {
            var reasons =
                _skipReasons
                    .OrderBy(item => item.Key)
                    .Select(item =>
                        new CleanupSkippedEntryReasonCount
                        {
                            Reason = item.Key,
                            Count = item.Value
                        })
                    .ToList();

            return new ExecutionResult
            {
                WasStarted =
                    WasStarted,

                WasCancelled =
                    WasCancelled,

                DeletedFileCount =
                    DeletedFileCount,

                DeletedDirectoryCount =
                    DeletedDirectoryCount,

                DeletedSizeBytes =
                    DeletedSizeBytes,

                FailedEntryCount =
                    FailedEntryCount,

                SkippedEntryCount =
                    SkippedEntryCount,

                ProcessedEntryCount =
                    ProcessedEntryCount,

                SkippedEntryReasons =
                    reasons,

                ErrorMessage =
                    ErrorMessage
            };
        }
    }
}
