
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Assessment;

/// <summary>
/// Ermittelt den fachlichen Entscheidungsstatus
/// für Programmaktualisierungen innerhalb eines
/// einzelnen Kundencheckups.
///
/// Der Status ist ausdrücklich keine zweite
/// technische Bewertung und kein Punktescore.
///
/// Insbesondere führen begründet zurückgestellte
/// Programmupdates nicht zu einer Verbesserung
/// des gemessenen Systemzustands.
/// </summary>
public static class ProgramUpdateWorkStatusService
{
    public static ProgramUpdateWorkStatus Evaluate(
        ProgramUpdateInformation information,
        CustomerCheckupVisit visit)
    {
        ArgumentNullException.ThrowIfNull(information);
        ArgumentNullException.ThrowIfNull(visit);

        /*
         * Wiederverwendung unserer bereits
         * implementierten, getesteten Auswertung.
         *
         * Hier werden keine Ausnahmen eigenständig
         * neu zugeordnet oder Versionsstände
         * abweichend interpretiert.
         */
        var evaluation =
            ProgramUpdateExceptionEvaluationService
                .Evaluate(
                    information,
                    visit);

        var documentedCount =
            evaluation.DocumentedExceptionCount;

        // =========================================
        // FEHLENDE ODER UNZUVERLÄSSIGE DATEN
        // =========================================

        if (!evaluation.IsAnalysisReliable
            || !evaluation.DetectedUpdateCount.HasValue
            || !evaluation.ApplicableExceptionCount.HasValue
            || !evaluation.WithoutExceptionCount.HasValue)
        {
            return new ProgramUpdateWorkStatus
            {
                Kind =
                    ProgramUpdateWorkStatusKind
                        .NotEvaluable,

                IsAnalysisReliable = false,

                DocumentedExceptionCount =
                    documentedCount,

                IsResultTruncated =
                    evaluation.IsResultTruncated
            };
        }

        // =========================================
        // NACHVOLLZIEHBARE ANZAHLEN
        // =========================================

        var detectedCount =
            evaluation.DetectedUpdateCount.Value;

        var deferredCount =
            evaluation.ApplicableExceptionCount.Value;

        var withoutExceptionCount =
            evaluation.WithoutExceptionCount.Value;

        /*
         * Eine ältere Ausnahme darf nicht als
         * aktuell passende Entscheidung gelten.
         *
         * Solche Einträge werden separat gezählt,
         * damit sie im späteren Bericht nicht
         * unbemerkt verschwinden.
         */
        var unmatchedExceptionCount =
            Math.Max(
                0,
                documentedCount - deferredCount);

        // =========================================
        // FACHLICHEN STATUS BESTIMMEN
        // =========================================

        ProgramUpdateWorkStatusKind kind;

        if (detectedCount == 0
            && !evaluation.IsResultTruncated)
        {
            kind =
                ProgramUpdateWorkStatusKind
                    .NoUpdatesDetected;
        }
        else if (detectedCount == 0)
        {
            kind =
                ProgramUpdateWorkStatusKind
                    .IncompleteComparison;
        }
        else if (deferredCount == 0)
        {
            kind =
                ProgramUpdateWorkStatusKind
                    .Open;
        }
        else if (withoutExceptionCount > 0)
        {
            kind =
                ProgramUpdateWorkStatusKind
                    .PartiallyDocumented;
        }
        else if (evaluation.IsResultTruncated)
        {
            /*
             * Bei einer abgeschnittenen
             * Programmliste behaupten wir keine
             * vollständige fachliche Prüfung.
             */
            kind =
                ProgramUpdateWorkStatusKind
                    .IncompleteComparison;
        }
        else
        {
            kind =
                ProgramUpdateWorkStatusKind
                    .FullyDocumented;
        }

        return new ProgramUpdateWorkStatus
        {
            Kind = kind,

            IsAnalysisReliable = true,

            DetectedUpdateCount =
                detectedCount,

            DeferredUpdateCount =
                deferredCount,

            WithoutExceptionCount =
                withoutExceptionCount,

            DocumentedExceptionCount =
                documentedCount,

            UnmatchedExceptionCount =
                unmatchedExceptionCount,

            IsResultTruncated =
                evaluation.IsResultTruncated
        };
    }
}

/// <summary>
/// Beschreibt ausschließlich den fachlichen
/// Entscheidungsstand für Programmupdates.
///
/// Diese Werte dürfen nicht als technische
/// Zustandsbewertung ausgegeben werden.
/// </summary>
public enum ProgramUpdateWorkStatusKind
{
    NotEvaluable = 0,

    NoUpdatesDetected = 1,

    Open = 2,

    PartiallyDocumented = 3,

    FullyDocumented = 4,

    IncompleteComparison = 5
}

/// <summary>
/// Lesbares Ergebnis der fachlichen
/// Programmupdate-Auswertung.
///
/// Das Objekt enthält keine veränderbaren
/// Scan- oder Kundenvorgangsdaten.
/// </summary>
public sealed class ProgramUpdateWorkStatus
{
    public ProgramUpdateWorkStatusKind Kind
    {
        get;
        init;
    } = ProgramUpdateWorkStatusKind.NotEvaluable;

    public bool IsAnalysisReliable
    {
        get;
        init;
    }

    public int? DetectedUpdateCount
    {
        get;
        init;
    }

    public int? DeferredUpdateCount
    {
        get;
        init;
    }

    public int? WithoutExceptionCount
    {
        get;
        init;
    }

    public int DocumentedExceptionCount
    {
        get;
        init;
    }

    public int UnmatchedExceptionCount
    {
        get;
        init;
    }

    public bool IsResultTruncated
    {
        get;
        init;
    }

    /// <summary>
    /// Gibt an, ob sämtliche auswertbaren
    /// Programmaktualisierungen eine passende
    /// dokumentierte Ausnahme besitzen.
    ///
    /// Dies ist KEIN Nachweis einer Behebung.
    /// </summary>
    public bool IsDecisionDocumentationComplete =>
        Kind == ProgramUpdateWorkStatusKind.FullyDocumented;

    /// <summary>
    /// Gibt an, ob erkannte Aktualisierungen
    /// ohne passende Ausnahme verbleiben.
    /// </summary>
    public bool HasUpdatesWithoutException =>
        IsAnalysisReliable
        && WithoutExceptionCount is > 0;

    public string StatusText =>
        Kind switch
        {
            ProgramUpdateWorkStatusKind.NotEvaluable =>
                "Nicht zuverlässig auswertbar",

            ProgramUpdateWorkStatusKind.NoUpdatesDetected =>
                "Keine ausstehenden Updates erkannt",

            ProgramUpdateWorkStatusKind.Open =>
                "Offen – keine Ausnahmen dokumentiert",

            ProgramUpdateWorkStatusKind.PartiallyDocumented =>
                "Teilweise fachlich entschieden",

            ProgramUpdateWorkStatusKind.FullyDocumented =>
                "Alle erkannten Updates fachlich dokumentiert",

            ProgramUpdateWorkStatusKind.IncompleteComparison =>
                "Fachlicher Abgleich unvollständig",

            _ =>
                "Unbekannter Bearbeitungsstatus"
        };

    /// <summary>
    /// Kundenverständliche Beschreibung
    /// ohne fachlich irreführende Aussagen
    /// über tatsächlich installierte Updates.
    /// </summary>
    public string SummaryText
    {
        get
        {
            if (!IsAnalysisReliable
                || !DetectedUpdateCount.HasValue
                || !DeferredUpdateCount.HasValue
                || !WithoutExceptionCount.HasValue)
            {
                return
                    "Für die Programmaktualisierungen "
                    + "liegt keine ausreichend "
                    + "zuverlässige Analyse vor. "
                    + "Der fachliche Entscheidungsstand "
                    + "kann deshalb nicht belastbar "
                    + "bestimmt werden.";
            }

            var summary =
                $"{DetectedUpdateCount.Value} "
                + "Programmaktualisierungen erkannt. "
                + $"{DeferredUpdateCount.Value} "
                + "davon wurden mit fachlicher "
                + "Begründung zurückgestellt. "
                + $"{WithoutExceptionCount.Value} "
                + "verbleiben ohne dokumentierte Ausnahme.";

            switch (Kind)
            {
                case ProgramUpdateWorkStatusKind.NoUpdatesDetected:
                    summary +=
                        " Die WinGet-Analyse meldet "
                        + "keine aktuell ausstehenden "
                        + "Aktualisierungen für die "
                        + "von ihr erfassten Programme.";
                    break;

                case ProgramUpdateWorkStatusKind.Open:
                    summary +=
                        " Für die erkannten "
                        + "Aktualisierungen liegen "
                        + "keine passenden "
                        + "Ausnahmeentscheidungen vor.";
                    break;

                case ProgramUpdateWorkStatusKind.PartiallyDocumented:
                    summary +=
                        " Der fachliche "
                        + "Entscheidungsstand ist "
                        + "noch nicht vollständig.";
                    break;

                case ProgramUpdateWorkStatusKind.FullyDocumented:
                    summary +=
                        " Für alle erkannten "
                        + "Aktualisierungen liegt "
                        + "eine passende begründete "
                        + "Ausnahme vor. "
                        + "Die Updates sind dadurch "
                        + "nicht installiert worden.";
                    break;

                case ProgramUpdateWorkStatusKind.IncompleteComparison:
                    summary +=
                        " Aufgrund einer unvollständigen "
                        + "Programmliste kann keine "
                        + "vollständige fachliche "
                        + "Zuordnung bestätigt werden.";
                    break;
            }

            if (UnmatchedExceptionCount > 0)
            {
                summary +=
                    $" Zusätzlich bestehen "
                    + $"{UnmatchedExceptionCount} "
                    + "dokumentierte Ausnahmen, "
                    + "die nicht mehr exakt zu den "
                    + "aktuell erkannten Programm- "
                    + "und Versionsständen passen.";
            }

            if (IsResultTruncated)
            {
                summary +=
                    " Die gespeicherte Programmliste "
                    + "ist gekürzt; nicht enthaltene "
                    + "Einträge konnten nicht "
                    + "einzeln abgeglichen werden.";
            }

            return summary;
        }
    }

    /// <summary>
    /// Fester Hinweis zur Abgrenzung gegenüber
    /// dem technischen Systemscore.
    /// </summary>
    public string TechnicalNoticeText =>
        "Der fachliche Bearbeitungsstatus "
        + "dokumentiert Technikerentscheidungen. "
        + "Er ersetzt weder die Installation "
        + "ausstehender Aktualisierungen "
        + "noch eine technische Sicherheitsprüfung. "
        + "Der gemessene Systemscore bleibt "
        + "davon unabhängig.";
}
