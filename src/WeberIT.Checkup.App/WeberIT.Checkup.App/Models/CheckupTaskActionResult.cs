
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

public class CheckupTaskActionResult
{
    public Guid Id { get; set; } =
        Guid.NewGuid();

    public string ActionCode { get; set; } =
        string.Empty;

    public string ActionTitle { get; set; } =
        string.Empty;

    public string TargetDescription { get; set; } =
        string.Empty;

    public CheckupTaskActionStatus Status { get; set; } =
        CheckupTaskActionStatus.Unknown;

    public string Summary { get; set; } =
        string.Empty;

    public string Details { get; set; } =
        string.Empty;

    public int? ExitCode { get; set; }

    public bool RestartRequired { get; set; }

    public bool RestartStatusWasConclusive { get; set; } =
        true;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    // Kontrollierter Neustart: gespeicherter Auftrag und spaeterer Nachweis.
    public Guid? ControlledRestartVisitId { get; set; }
    public Guid? ControlledRestartRequestId { get; set; }
    public DateTimeOffset? ControlledRestartBootTimeUtc { get; set; }

    // =============================================
    // SPRINT 55.14.34n
    // STRUKTURIERTES BEREINIGUNGSPROTOKOLL
    // =============================================

    /// <summary>
    /// Zugehöriger Kundencheckup einer
    /// strukturiert dokumentierten Bereinigung.
    ///
    /// Null bedeutet:
    /// Keine eindeutige Zuordnung dokumentiert.
    ///
    /// Historische Aktionen ohne diese Angabe
    /// dürfen nicht automatisch mit einem
    /// beliebigen Kundencheckup verknüpft werden.
    /// </summary>
    public Guid? CleanupCustomerCheckupVisitId
    {
        get;
        set;
    }

    /// <summary>
    /// Vollständiges technisches Ergebnis
    /// einer kontrollierten Bereinigung.
    ///
    /// Diese Daten ergänzen Summary und Details,
    /// ersetzen deren bisherige Anzeige aber nicht.
    ///
    /// Alte Aktionen besitzen hier null.
    ///
    /// Das Ergebnis wird erst in einem folgenden
    /// Integrationsschritt bei einer echten
    /// Bereinigungsaktion übernommen.
    /// </summary>
    public CleanupActionExecutionResult?
        CleanupExecutionResult
    {
        get;
        set;
    }

    /// <summary>
    /// Kennzeichnet einen strukturierten Datensatz,
    /// der grundsätzlich für eine spätere
    /// eindeutige Zuordnung infrage kommt.
    ///
    /// Eine konkrete Zuordnung muss zusätzlich
    /// gegen den aktuellen Kundencheckup,
    /// die Kategorie und die Messzeitpunkte
    /// geprüft werden.
    /// </summary>
    [JsonIgnore]
    public bool HasCorrelatableCleanupExecution =>
        CleanupCustomerCheckupVisitId.HasValue
        && CleanupCustomerCheckupVisitId.Value
            != Guid.Empty
        && CleanupExecutionResult is not null
        && !CleanupExecutionResult.WasBlocked
        && CleanupExecutionResult.PlanId
            != Guid.Empty
        && CleanupExecutionResult.CategoryResults
            is { Count: 1 }
        && CleanupExecutionResult.CategoryResults[0]
            is { WasStarted: true }
        && StartedAt.HasValue
        && FinishedAt.HasValue
        && StartedAt.Value
            == CleanupExecutionResult.StartedAt
        && FinishedAt.Value
            == CleanupExecutionResult.FinishedAt;

    // =============================================
    // TECHNISCHES ERGEBNIS UNABHÄNGIG SICHERN
    // =============================================

    /// <summary>
    /// Übernimmt ein echtes Ausführungsergebnis
    /// als unabhängige strukturierte Kopie.
    ///
    /// Der aufrufende Dienst muss die optionale
    /// Kundencheckup-ID zuvor aus dem tatsächlich
    /// geöffneten Kunden- und Gerätekontext
    /// ermitteln.
    ///
    /// Es wird ausdrücklich kein Kundencheckup
    /// anhand von Zeitstempeln geraten.
    ///
    /// Auch fehlgeschlagene oder abgebrochene,
    /// bereits gestartete Aktionen können
    /// dokumentiert werden.
    /// </summary>
    public void CaptureCleanupExecution(
        CleanupActionExecutionResult source,
        Guid? customerCheckupVisitId)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (CleanupExecutionResult is not null)
        {
            throw new InvalidOperationException(
                "Für diese technische Aktion wurde "
                + "bereits ein strukturiertes "
                + "Bereinigungsergebnis hinterlegt.");
        }

        if (customerCheckupVisitId == Guid.Empty)
        {
            throw new ArgumentException(
                "Eine leere Kundencheckup-ID "
                + "ist nicht zulässig.",
                nameof(customerCheckupVisitId));
        }

        if (source.WasBlocked
            || source.PlanId == Guid.Empty
            || source.CategoryResults is not { Count: 1 }
            || source.CategoryResults[0] is null
            || !source.CategoryResults[0].WasStarted)
        {
            throw new InvalidOperationException(
                "Das Ausführungsergebnis enthält "
                + "keine eindeutig gestartete "
                + "Bereinigungskategorie.");
        }

        if (source.FinishedAt < source.StartedAt)
        {
            throw new InvalidOperationException(
                "Der Abschlusszeitpunkt der Bereinigung "
                + "liegt vor ihrem Startzeitpunkt.");
        }

        if (!StartedAt.HasValue
            || !FinishedAt.HasValue
            || StartedAt.Value != source.StartedAt
            || FinishedAt.Value != source.FinishedAt)
        {
            throw new InvalidOperationException(
                "Die Ausführungszeitpunkte stimmen "
                + "nicht mit dem Aufgabenprotokoll "
                + "überein.");
        }

        /*
         * Die fachliche Statusklassifizierung
         * bleibt dieselbe wie beim bestehenden
         * Bereinigungsdialog.
         */
        var expectedStatus =
            source.IsSuccessful
            || source.IsPartiallySuccessful
                ? CheckupTaskActionStatus.Successful
                : source.WasCancelled
                    ? CheckupTaskActionStatus.Cancelled
                    : CheckupTaskActionStatus.Failed;

        if (Status != expectedStatus)
        {
            throw new InvalidOperationException(
                "Der Aufgabenstatus widerspricht "
                + "dem technischen Bereinigungsergebnis.");
        }

        /*
         * Zuerst vollständig kopieren.
         * Erst danach Eigenschaften übernehmen.
         *
         * Dadurch bleibt der bisherige
         * Aufgabenstatus bei einem Fehler
         * unverändert.
         */
        var independentResult =
            new CleanupActionExecutionResult
            {
                PlanId =
                    source.PlanId,

                WasBlocked =
                    source.WasBlocked,

                WasCancelled =
                    source.WasCancelled,

                ErrorMessage =
                    source.ErrorMessage,

                StartedAt =
                    source.StartedAt,

                FinishedAt =
                    source.FinishedAt,

                CategoryResults =
                    source.CategoryResults
                        .Select(CopyCategoryResult)
                        .ToList()
            };

        CleanupExecutionResult =
            independentResult;

        CleanupCustomerCheckupVisitId =
            customerCheckupVisitId;
    }

    // =============================================
    // KATEGORIEERGEBNIS TIEF KOPIEREN
    // =============================================

    private static CleanupActionCategoryExecutionResult
        CopyCategoryResult(
            CleanupActionCategoryExecutionResult source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new CleanupActionCategoryExecutionResult
        {
            Category =
                source.Category,

            CategoryTitle =
                source.CategoryTitle,

            TargetPath =
                source.TargetPath,

            WasStarted =
                source.WasStarted,

            WasCancelled =
                source.WasCancelled,

            WasTraversalCompleted =
                source.WasTraversalCompleted,

            DeletedFileCount =
                source.DeletedFileCount,

            DeletedDirectoryCount =
                source.DeletedDirectoryCount,

            DeletedSizeBytes =
                source.DeletedSizeBytes,

            FailedEntryCount =
                source.FailedEntryCount,

            SkippedEntryCount =
                source.SkippedEntryCount,

            ErrorMessage =
                source.ErrorMessage,

            SkippedEntryReasons =
                (source.SkippedEntryReasons
                 ?? new List<CleanupSkippedEntryReasonCount>())
                    .Select(
                        entry =>
                            new CleanupSkippedEntryReasonCount
                            {
                                Reason =
                                    entry.Reason,

                                Count =
                                    entry.Count
                            })
                    .ToList()
        };
    }

    // =============================================
    // BESTEHENDE STATUS- UND ANZEIGEEIGENSCHAFTEN
    // =============================================

    [JsonIgnore]
    public bool WasSuccessful =>
        Status == CheckupTaskActionStatus.Successful;

    [JsonIgnore]
    public bool WasCancelled =>
        Status == CheckupTaskActionStatus.Cancelled;

    // =============================================
    // SPRINT 55.14.41
    // TECHNISCHEN BEREINIGUNGSSTATUS SICHTBAR MACHEN
    // =============================================

    /*
     * Der fachliche Workflow-Status Successful
     * umfasst bei Bereinigungsaktionen auch einen
     * technisch dokumentierten Teilerfolg.
     *
     * Die sichtbare Aussage darf deshalb nicht
     * allein aus diesem Workflow-Status entstehen.
     *
     * Gespeicherte Aktionen werden nicht umbewertet
     * oder nachträglich verändert.
     */
    [JsonIgnore]
    public string StatusText =>
        Status switch
        {
            CheckupTaskActionStatus.Successful
                when IsCleanupAction =>
                    GetCleanupTechnicalStatusText(),

            CheckupTaskActionStatus.Successful =>
                "Technisch erfolgreich ausgeführt",

            CheckupTaskActionStatus.Failed =>
                "Fehlgeschlagen",

            CheckupTaskActionStatus.Cancelled =>
                "Abgebrochen",

            _ =>
                "Ergebnis nicht eindeutig"
        };

    [JsonIgnore]
    private bool IsCleanupAction =>
        string.Equals(
            ActionCode,
            "action.cleanup.selected-safe-categories",
            StringComparison.Ordinal);

    private string GetCleanupTechnicalStatusText()
    {
        var execution =
            CleanupExecutionResult;

        // Historische Aktionen ohne strukturierten
        // Löschdatensatz nicht nachträglich
        // als fehlerfrei behaupten.
        if (execution is null)
        {
            return
                "Bereinigung dokumentiert – Details prüfen";
        }

        if (execution.IsWorkCompleted
            && execution.FailedEntryCount > 0)
        {
            return "Bereinigung abgeschlossen – "
                + $"{execution.FailedEntryCount:N0} "
                + "nicht löschbare Einträge dokumentiert";
        }

        if (execution.IsPartiallySuccessful)
        {
            var failures =
                execution.FailedEntryCount;

            return failures switch
            {
                > 1 =>
                    $"Teilweise erfolgreich – {failures:N0} Löschfehler",

                1 =>
                    "Teilweise erfolgreich – 1 Löschfehler",

                _ =>
                    "Teilweise erfolgreich – technische Fehler"
            };
        }

        if (execution.IsSuccessful)
        {
            return execution.CategoryResults.All(
                    category =>
                        category.HasOnlyExpectedSafetySkips)
                ? "Technisch erfolgreich ausgeführt"
                : "Technisch beendet – Ausschlüsse ungeklärt";
        }

        return
            "Bereinigung dokumentiert – Status prüfen";
    }

    [JsonIgnore]
    public string RestartRequirementText
    {
        get
        {
            if (!RestartStatusWasConclusive)
            {
                return
                    "Neustartstatus nicht eindeutig";
            }

            return RestartRequired
                ? "Neustart erforderlich"
                : "Kein Neustartbedarf gemeldet";
        }
    }

    [JsonIgnore]
    public string StartedAtText =>
        StartedAt.HasValue
            ? StartedAt.Value
                .ToLocalTime()
                .ToString("dd.MM.yyyy HH:mm")
              + " Uhr"
            : "Startzeitpunkt nicht verfügbar";

    [JsonIgnore]
    public string FinishedAtText =>
        FinishedAt.HasValue
            ? FinishedAt.Value
                .ToLocalTime()
                .ToString("dd.MM.yyyy HH:mm")
              + " Uhr"
            : "Abschlusszeitpunkt nicht verfügbar";

    [JsonIgnore]
    public string ExitCodeText =>
        ExitCode.HasValue
            ? ExitCode.Value.ToString()
            : "Nicht verfügbar";

    [JsonIgnore]
    public TimeSpan Duration =>
        StartedAt.HasValue
        && FinishedAt.HasValue
        && FinishedAt.Value > StartedAt.Value
            ? FinishedAt.Value - StartedAt.Value
            : TimeSpan.Zero;

    [JsonIgnore]
    public string DurationText
    {
        get
        {
            if (!StartedAt.HasValue
                || !FinishedAt.HasValue)
            {
                return "Nicht verfügbar";
            }

            if (Duration.TotalMinutes >= 1)
            {
                return
                    $"{(int)Duration.TotalMinutes} Min. "
                    + $"{Duration.Seconds} Sek.";
            }

            return
                $"{Math.Max(
                    1,
                    (int)Math.Ceiling(
                        Duration.TotalSeconds))} Sek.";
        }
    }
}
