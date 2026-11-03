
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
/// Sprint 55.14.31a
///
/// Koordiniert einen vollständigen,
/// handle-basierten Bereinigungslauf.
///
/// Verwendet ausschließlich die bereits
/// entwickelten Klassen für:
///
/// - Verzeichnisaufzählung
/// - Prüfung von Dateiobjekten
/// - Dateilöschung über Handles
/// - Nichtrekursive Verzeichnislöschung
///
/// Der Stammordner wird nicht gelöscht.
///
/// Die produktive Freigabe bleibt im
/// WindowsTemporaryFilesCleanupExecutor
/// ausdrücklich gesperrt.
///
/// Neuer Debug-Testzugang:
/// Ein Regressionstest kann nach der ersten
/// erfolgreich dokumentierten Dateilöschung
/// kontrolliert einen Abbruch anfordern.
///
/// Keine zeitgesteuerten Testabbrüche.
/// Keine Änderung der produktiven Löschlogik.
/// </summary>
internal static class WindowsTemporaryFilesHandleCleanupEngine
{
    private const int MaximumDirectoryDepth = 64;

    private const long MaximumProcessedEntries = 100000;

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
    // AUSSCHLIESSLICH DEBUG-REGRESSIONSTEST
    // =============================================

    /// <summary>
    /// Ermöglicht einen deterministischen
    /// Abbruchtest ausschließlich im Debug-Build.
    ///
    /// Der Test übergibt ein zuvor geprüftes
    /// Stammhandle seiner eigenen GUID-Sandbox.
    ///
    /// Nach der ersten erfolgreich dokumentierten
    /// Dateilöschung wird der Callback aufgerufen.
    ///
    /// Dieser kann den übergebenen
    /// CancellationToken abbrechen.
    ///
    /// Die Funktion autorisiert ausdrücklich
    /// keine produktiven Zielpfade.
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

        /*
         * Den Stamm vor Beginn überprüfen.
         *
         * Der spätere produktive Aufrufer
         * muss zusätzlich die Zielidentität
         * und den erlaubten Zielbereich
         * ausdrücklich kontrollieren.
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

        try
        {
            ProcessDirectory(
                approvedRootDirectory,
                depth: 0,
                state,
                cancellationToken);

            /*
             * Auch nach dem letzten Eintrag
             * einen inzwischen angeforderten
             * Abbruch berücksichtigen.
             *
             * Dadurch wird ein unmittelbar
             * nach der letzten Änderung
             * eingetroffener Abbruch nicht
             * versehentlich als normal
             * abgeschlossener Lauf gemeldet.
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
             * Ein unerwarteter Fehler beendet
             * den gesamten Durchlauf.
             *
             * Bereits erfolgreich entfernte
             * Einträge bleiben im Ergebnis
             * dokumentiert.
             *
             * Es erfolgt kein automatischer
             * Wiederholungsversuch.
             */
            state.RecordFatalError(
                exception);
        }

        return state.CreateResult();
    }

    // =============================================
    // VERZEICHNIS VERARBEITEN
    // =============================================

    /// <summary>
    /// Durchsucht den geöffneten Ordner
    /// und bearbeitet dessen direkte Einträge.
    ///
    /// Rückgabe false:
    /// Die Verarbeitung war unvollständig.
    /// Der Aufrufer darf den Ordner anschließend
    /// nicht als vollständig bereinigt behandeln.
    ///
    /// Der geöffnete Stammordner selbst
    /// wird niemals gelöscht.
    /// </summary>
    private static bool ProcessDirectory(
        SafeFileHandle directoryHandle,
        int depth,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (depth > MaximumDirectoryDepth)
        {
            throw new InvalidOperationException(
                "Die maximale Sicherheitstiefe "
                + "der Verzeichnisdurchsuchung "
                + "wurde überschritten.");
        }

        IReadOnlyList<
            WindowsTemporaryFilesHandleDirectoryEnumeration.DirectoryEntry>
            entries;

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
        catch (Exception exception)
        {
            state.RecordEntryException(
                exception,
                allowDirectoryNotEmpty: false);

            return false;
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            state.ProcessedEntryCount++;

            if (state.ProcessedEntryCount
                > MaximumProcessedEntries)
            {
                throw new InvalidOperationException(
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
         *
         * Eine spätere echte Löschung
         * verlässt sich trotzdem nicht
         * auf dieses Aufzählungsattribut.
         */
        if (entry.IsListedReparsePoint)
        {
            state.RecordSkip(
                CleanupSkippedEntryReason.ReparsePoint);

            return;
        }

        WindowsTemporaryFilesHandleAccess.EntrySnapshot
            inspectedSnapshot;

        /*
         * Den Eintrag relativ zum geöffneten
         * Elternhandle untersuchen.
         *
         * Der Untersuchungshandle wird
         * anschließend geschlossen, bevor
         * ein exklusiver Löschversuch erfolgt.
         */
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

        try
        {
            /*
             * Die Löschklasse öffnet die Datei
             * erneut relativ zum Elternhandle,
             * prüft ihre tatsächliche Identität
             * und löscht ausschließlich
             * über den geprüften Handle.
             */
            var deleted =
                WindowsTemporaryFilesHandleDeletion
                    .DeleteInspectedFile(
                        parentDirectory,
                        entryName,
                        inspectedSnapshot);

            state.RecordDeletedFile(
                deleted.SizeBytes);
        }
        catch (Exception exception)
        {
            state.RecordEntryException(
                exception,
                allowDirectoryNotEmpty: false);
        }
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

        /*
         * Zu tiefe Verzeichnisstrukturen
         * werden sicherheitsbedingt
         * nicht weiter betreten.
         */
        if (parentDepth >= MaximumDirectoryDepth)
        {
            state.RecordSkip(
                CleanupSkippedEntryReason.OtherSafetySkip);

            return;
        }

        var completed = false;

        try
        {
            /*
             * Unterordner relativ zum
             * geöffneten Elternhandle öffnen.
             *
             * Die Identität muss mit der
             * vorherigen Untersuchung
             * übereinstimmen.
             *
             * Wichtig:
             * Der Unterordnerhandle muss
             * vollständig geschlossen sein,
             * bevor wir diesen Ordner
             * zum Löschen öffnen.
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
        catch (Exception exception)
        {
            state.RecordEntryException(
                exception,
                allowDirectoryNotEmpty: false);

            return;
        }

        /*
         * Wenn ein Unterordner nicht
         * vollständig durchsucht werden
         * konnte, unternehmen wir keinen
         * Löschversuch an ihm.
         */
        if (!completed)
        {
            state.RecordSkip(
                CleanupSkippedEntryReason.OtherSafetySkip);

            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            /*
             * Windows entfernt nur
             * tatsächlich leere Ordner.
             *
             * Identität und Schutzattribute
             * werden nochmals am geöffneten
             * Löschhandle kontrolliert.
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
    // LAUFZUSTAND UND ZÄHLER
    // =============================================

    private sealed class ExecutionState
    {
        private readonly Dictionary<
            CleanupSkippedEntryReason,
            long> _skipReasons = new();

        /*
         * Im normalen produktiven Aufruf
         * ist dieser Callback immer null.
         *
         * Ausschließlich der Debug-Testzugang
         * kann ihn setzen.
         *
         * Er wird genau einmal nach der ersten
         * erfolgreich dokumentierten
         * Dateilöschung aufgerufen.
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

        // -------------------------------------
        // ERFOLGREICHE DATEILÖSCHUNG
        // -------------------------------------

        public void RecordDeletedFile(ulong sizeBytes)
        {
            DeletedFileCount++;

            /*
             * Keine Überläufe bei der
             * Gesamtgröße zulassen.
             */
            if (sizeBytes >
                ulong.MaxValue - DeletedSizeBytes)
            {
                DeletedSizeBytes = ulong.MaxValue;
            }
            else
            {
                DeletedSizeBytes += sizeBytes;
            }

            /*
             * Der Callback wird erst nach
             * vollständig erfolgter
             * Ergebnisdokumentation ausgeführt.
             *
             * Dadurch kann ein Regressionstest
             * exakt an dieser Stelle einen
             * Abbruch anfordern.
             *
             * Der normale Aufruf besitzt
             * keinen Callback.
             */
            if (DeletedFileCount == 1)
            {
                _onFirstDeletedFile?.Invoke();
            }
        }

        // -------------------------------------
        // ERWARTETEN AUSSCHLUSS DOKUMENTIEREN
        // -------------------------------------

        public void RecordSkip(
            CleanupSkippedEntryReason reason)
        {
            SkippedEntryCount++;

            if (_skipReasons.TryGetValue(
                    reason,
                    out var count))
            {
                _skipReasons[reason] = count + 1;
            }
            else
            {
                _skipReasons[reason] = 1;
            }
        }

        // -------------------------------------
        // DATEISYSTEMFEHLER KLASSIFIZIEREN
        // -------------------------------------

        public void RecordEntryException(
            Exception exception,
            bool allowDirectoryNotEmpty)
        {
            /*
             * Ein erwarteter Sicherheitsstopp
             * ist kein technischer Löschfehler.
             *
             * Dazu gehören insbesondere
             * Identitätswechsel und von
             * den nativen Zugriffsklassen
             * abgelehnte Verknüpfungen.
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
                errorCode = win32Exception.NativeErrorCode;
            }
            else if (exception is IOException ioException)
            {
                errorCode = ioException.HResult & 0xFFFF;
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

        // -------------------------------------
        // SCHWERWIEGENDEN ABBRUCH ERFASSEN
        // -------------------------------------

        public void RecordFatalError(Exception exception)
        {
            FailedEntryCount++;

            ErrorMessage =
                string.IsNullOrWhiteSpace(exception.Message)
                    ? "Der Bereinigungslauf wurde "
                      + "unerwartet abgebrochen."
                    : exception.Message;
        }

        // -------------------------------------
        // ERGEBNIS ERSTELLEN
        // -------------------------------------

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
                WasStarted = WasStarted,
                WasCancelled = WasCancelled,

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
