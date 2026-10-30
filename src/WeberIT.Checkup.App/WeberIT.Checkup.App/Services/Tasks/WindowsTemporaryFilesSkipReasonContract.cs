
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Tasks;

/// <summary>
/// Ein einzelner Ursachen-Zähler aus dem
/// JSON-Ergebnis der erhöhten
/// Windows-Temp-Bereinigung.
///
/// Die Ursache wird als stabiler Name
/// übertragen, nicht als numerischer
/// Enum-Wert.
/// </summary>
internal sealed class WindowsTemporaryFilesSkipReasonRecord
{
    public string Reason { get; set; } =
        string.Empty;

    public long Count { get; set; }
}

/// <summary>
/// Sprint 55.13.17
///
/// Validiert und normalisiert die vom
/// erhöhten PowerShell-Prozess gelieferten
/// Überspringursachen.
///
/// Ausschließlich definierte Enum-Namen
/// sind erlaubt. Numerische Zeichenfolgen,
/// abweichende Schreibweisen und
/// unbekannte Codes werden abgelehnt.
///
/// Keine Dateisystemoperationen.
///
/// Historische Ergebnisse ohne
/// Ursachendetails bleiben kompatibel.
/// </summary>
internal static class WindowsTemporaryFilesSkipReasonContract
{
    public static List<CleanupSkippedEntryReasonCount>
        Normalize(
            IEnumerable<WindowsTemporaryFilesSkipReasonRecord?>?
                reportedReasons,
            long skippedEntryCount)
    {
        if (skippedEntryCount < 0)
        {
            throw new InvalidOperationException(
                "Das erhöhte Bereinigungsergebnis "
                + "enthält einen negativen Zähler "
                + "für übersprungene Einträge.");
        }

        var normalizedReasons =
            new List<CleanupSkippedEntryReasonCount>();

        /*
         * Historische Ergebnisse besitzen
         * gegebenenfalls nur den Gesamtzähler.
         *
         * Fehlende Ursachendetails werden
         * niemals nachträglich erfunden.
         */
        if (reportedReasons is null)
        {
            return normalizedReasons;
        }

        var knownReasons =
            new HashSet<CleanupSkippedEntryReason>();

        long documentedCount = 0;

        foreach (var entry in reportedReasons)
        {
            if (entry is null)
            {
                throw new InvalidOperationException(
                    "Das erhöhte Bereinigungsergebnis "
                    + "enthält einen leeren "
                    + "Ursacheneintrag.");
            }

            if (string.IsNullOrWhiteSpace(
                    entry.Reason))
            {
                throw new InvalidOperationException(
                    "Eine übersprungene Datei besitzt "
                    + "keinen dokumentierten "
                    + "Ursachencode.");
            }

            /*
             * WICHTIG:
             *
             * Enum.TryParse allein genügt
             * nicht, weil auch numerische
             * Zeichenfolgen akzeptiert werden.
             *
             * Wir vergleichen zusätzlich mit
             * dem kanonischen Enum-Namen.
             *
             * Damit sind ausschließlich
             * exakt vereinbarte Namen erlaubt.
             */
            if (!Enum.TryParse<CleanupSkippedEntryReason>(
                    entry.Reason,
                    ignoreCase: false,
                    out var reason)
                || !Enum.IsDefined(
                    typeof(CleanupSkippedEntryReason),
                    reason)
                || !string.Equals(
                    Enum.GetName(reason),
                    entry.Reason,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Der erhöhte Bereinigungsprozess "
                    + "hat einen ungültigen oder "
                    + "unbekannten Überspringgrund "
                    + "gemeldet: "
                    + entry.Reason);
            }

            if (entry.Count <= 0)
            {
                throw new InvalidOperationException(
                    "Der erhöhte Bereinigungsprozess "
                    + "hat einen ungültigen "
                    + "Ursachenzähler gemeldet: "
                    + entry.Reason);
            }

            /*
             * Jeder Ursachencode darf
             * höchstens einmal vorkommen.
             *
             * Doppelte Einträge werden
             * nicht stillschweigend addiert.
             */
            if (!knownReasons.Add(reason))
            {
                throw new InvalidOperationException(
                    "Der erhöhte Bereinigungsprozess "
                    + "hat einen Überspringgrund "
                    + "mehrfach gemeldet: "
                    + entry.Reason);
            }

            /*
             * Überläufe dürfen niemals
             * zu falschen Gesamtzahlen
             * führen.
             */
            try
            {
                documentedCount =
                    checked(
                        documentedCount
                        + entry.Count);
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    "Die Ursachenzähler der erhöhten "
                    + "Bereinigung überschreiten "
                    + "den gültigen Wertebereich.",
                    exception);
            }

            if (documentedCount > skippedEntryCount)
            {
                throw new InvalidOperationException(
                    "Der erhöhte Bereinigungsprozess "
                    + "meldet mehr dokumentierte "
                    + "Überspringursachen als "
                    + "übersprungene Einträge.");
            }

            normalizedReasons.Add(
                new CleanupSkippedEntryReasonCount
                {
                    Reason = reason,
                    Count = entry.Count
                });
        }

        /*
         * Eine unvollständige Aufschlüsselung
         * ist zulässig.
         *
         * Die Differenz bleibt später
         * als nicht näher aufgeschlüsselt
         * erkennbar.
         */
        return normalizedReasons
            .OrderBy(entry => entry.Reason)
            .ToList();
    }
}
