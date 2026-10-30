
using System.IO;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Fachliche Einordnung von Einträgen,
/// die bei einer kontrollierten Bereinigung
/// nicht verarbeitet werden können.
///
/// Die Klassifizierung führt selbst keine
/// Dateioperationen aus.
/// </summary>
internal enum CleanupEntryIssueKind
{
    AlreadyMissing,
    ReparsePoint,
    ReadOnly,
    SystemProtected,
    LockedByProcess,
    DirectoryNotEmpty,
    AccessDenied,
    OtherIoFailure,
    UnexpectedFailure
}

/// <summary>
/// Unterscheidet erwartete Ausnahmen von
/// tatsächlichen technischen Fehlern.
/// </summary>
internal enum CleanupEntryDisposition
{
    ExpectedSkip,
    TechnicalFailure
}

/// <summary>
/// Fachliches Ergebnis einer Einordnung.
/// </summary>
internal sealed record CleanupEntryIssue(
    CleanupEntryIssueKind Kind,
    CleanupEntryDisposition Disposition,
    string Description)
{
    public bool IsExpectedSkip =>
        Disposition == CleanupEntryDisposition.ExpectedSkip;

    public bool IsTechnicalFailure =>
        Disposition == CleanupEntryDisposition.TechnicalFailure;
}

/// <summary>
/// SCHRITT 55.13.1
///
/// Zentrale, ausschließlich lesende
/// Klassifizierung typischer Probleme
/// bei der kontrollierten Dateibereinigung.
///
/// Ein erwarteter Schutz- oder Sperrzustand
/// ist kein technischer Löschfehler.
///
/// Unklare Ursachen werden niemals
/// automatisch als harmlos eingestuft.
/// </summary>
internal static class CleanupEntryIssueClassifier
{
    private const uint SharingViolationHResult =
        0x80070020;

    private const uint LockViolationHResult =
        0x80070021;

    private const uint DirectoryNotEmptyHResult =
        0x80070091;

    /// <summary>
    /// Prüft bereits bekannte Dateiattribute.
    ///
    /// Es erfolgt kein eigener Dateizugriff.
    ///
    /// null bedeutet, dass anhand dieser
    /// Attribute kein Schutzgrund erkannt wurde.
    /// </summary>
    public static CleanupEntryIssue?
        ClassifyProtectedFileAttributes(
            FileAttributes attributes)
    {
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            return new CleanupEntryIssue(
                CleanupEntryIssueKind.ReparsePoint,
                CleanupEntryDisposition.ExpectedSkip,
                "Verknüpfung oder Dateisystemverweis: "
                + "aus Sicherheitsgründen nicht verfolgt.");
        }

        if ((attributes & FileAttributes.System) != 0)
        {
            return new CleanupEntryIssue(
                CleanupEntryIssueKind.SystemProtected,
                CleanupEntryDisposition.ExpectedSkip,
                "Als Systemdatei gekennzeichnet: "
                + "nicht automatisch gelöscht.");
        }

        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            return new CleanupEntryIssue(
                CleanupEntryIssueKind.ReadOnly,
                CleanupEntryDisposition.ExpectedSkip,
                "Schreibgeschützte Datei: "
                + "nicht automatisch verändert.");
        }

        return null;
    }

    /// <summary>
    /// Ordnet eine bereits aufgetretene
    /// Dateisystemausnahme ein.
    ///
    /// Bekannte Attribute können zusätzlich
    /// übergeben werden, werden hier aber
    /// niemals selbst ausgelesen.
    /// </summary>
    public static CleanupEntryIssue ClassifyException(
        Exception exception,
        FileAttributes? knownAttributes = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        /*
         * Ein Abbruch ist keine Dateisperre
         * und kein technischer Löschfehler.
         *
         * Er muss vom aufrufenden Executor
         * weiterhin getrennt behandelt werden.
         */
        if (exception is OperationCanceledException)
        {
            throw new ArgumentException(
                "Ein Abbruch darf nicht als "
                + "Dateiproblem klassifiziert werden.",
                nameof(exception));
        }

        /*
         * Eine inzwischen entfernte Datei
         * bedeutet keinen Fehlversuch.
         */
        if (exception is FileNotFoundException
            or DirectoryNotFoundException)
        {
            return new CleanupEntryIssue(
                CleanupEntryIssueKind.AlreadyMissing,
                CleanupEntryDisposition.ExpectedSkip,
                "Der Eintrag ist inzwischen "
                + "nicht mehr vorhanden.");
        }

        /*
         * Wenn bereits Dateiattribute
         * bekannt sind, nutzen wir diese
         * zur eindeutigeren Einordnung.
         */
        if (knownAttributes.HasValue)
        {
            var protectedIssue =
                ClassifyProtectedFileAttributes(
                    knownAttributes.Value);

            if (protectedIssue is not null)
            {
                return protectedIssue;
            }
        }

        /*
         * Windows meldet für eine Datei,
         * dass sie von einem anderen Prozess
         * verwendet oder gesperrt wird.
         *
         * Wir versuchen nicht, die Sperre
         * gewaltsam zu umgehen.
         */
        if (exception is IOException)
        {
            var hResult =
                unchecked(
                    (uint)exception.HResult);

            if (hResult is SharingViolationHResult
                or LockViolationHResult)
            {
                return new CleanupEntryIssue(
                    CleanupEntryIssueKind.LockedByProcess,
                    CleanupEntryDisposition.ExpectedSkip,
                    "Datei oder Ordner wird derzeit "
                    + "von einem Prozess verwendet "
                    + "oder ist gesperrt.");
            }

            /*
             * Ein nach der Verarbeitung
             * noch gefüllter Ordner wird
             * nicht rekursiv erzwungen.
             *
             * Ursache können verbliebene
             * geschützte oder neu erzeugte
             * Einträge sein.
             */
            if (hResult == DirectoryNotEmptyHResult)
            {
                return new CleanupEntryIssue(
                    CleanupEntryIssueKind.DirectoryNotEmpty,
                    CleanupEntryDisposition.ExpectedSkip,
                    "Ordner enthält noch Einträge "
                    + "und wurde deshalb nicht entfernt.");
            }

            return new CleanupEntryIssue(
                CleanupEntryIssueKind.OtherIoFailure,
                CleanupEntryDisposition.TechnicalFailure,
                "Ein nicht als erwartete Sperre "
                + "klassifizierbarer Datei- "
                + "oder Datenträgerfehler ist aufgetreten.");
        }

        /*
         * Zugriff verweigert kann viele
         * Ursachen haben.
         *
         * Ohne eindeutige Attribute oder
         * Sperrkennzeichen dürfen wir
         * diesen Zustand nicht pauschal
         * als erwartetes Überspringen zählen.
         */
        if (exception is UnauthorizedAccessException)
        {
            return new CleanupEntryIssue(
                CleanupEntryIssueKind.AccessDenied,
                CleanupEntryDisposition.TechnicalFailure,
                "Zugriff verweigert; die Ursache "
                + "ist nicht eindeutig als "
                + "erwarteter Schutzfall bestätigt.");
        }

        /*
         * Alle unbekannten Fehler bleiben
         * technische Fehler.
         */
        return new CleanupEntryIssue(
            CleanupEntryIssueKind.UnexpectedFailure,
            CleanupEntryDisposition.TechnicalFailure,
            "Unerwarteter technischer Fehler "
            + "bei der Dateiverarbeitung.");
    }
}
