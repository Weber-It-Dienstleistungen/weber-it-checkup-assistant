
using System;
using System.Collections.Generic;
using System.Linq;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Assessment;

namespace WeberIT.Checkup.App.Services.Reports;

/// <summary>
/// Bereitet fachlich begründete Programmupdate-Ausnahmen
/// für den Kundencheckup-Bericht auf.
///
/// Technische Scanergebnisse, gespeicherte Entscheidungen,
/// Aktionsprotokolle und Bewertungen werden nicht verändert.
///
/// Eine dokumentierte Zurückstellung ist keine Reparatur.
/// Ob eine Aktualisierung weiterhin aussteht, wird
/// ausschließlich anhand der Abschlusskontrolle beurteilt.
/// </summary>
public static class CustomerCheckupProgramUpdateReportService
{
    public static CustomerCheckupProgramUpdateReportData Create(
        CustomerCheckupVisit visit)
    {
        ArgumentNullException.ThrowIfNull(visit);

        var beforeInformation =
            visit.BeforeCheckup?.ProgramUpdateInformation;

        var afterInformation =
            visit.AfterCheckup?.ProgramUpdateInformation;

        var beforeReliable =
            IsAnalysisReliable(beforeInformation);

        var afterReliable =
            IsAnalysisReliable(afterInformation);

        var beforeUpdates =
            GetUniqueUpdates(beforeInformation);

        var afterUpdates =
            GetUniqueUpdates(afterInformation);

        var beforeComplete =
            IsUpdateListComplete(
                beforeInformation,
                beforeReliable,
                beforeUpdates.Count);

        var afterComplete =
            IsUpdateListComplete(
                afterInformation,
                afterReliable,
                afterUpdates.Count);

        /*
         * Wir verwenden den bereits vorhandenen
         * Auswertungsdienst als fachliche Grundlage.
         *
         * Seine Zählwerte werden allerdings nur
         * übernommen, wenn die gespeicherte Einzelliste
         * vollständig ist.
         */
        ProgramUpdateExceptionEvaluation? beforeEvaluation =
            beforeInformation is not null
                ? ProgramUpdateExceptionEvaluationService.Evaluate(
                    beforeInformation,
                    visit)
                : null;

        ProgramUpdateExceptionEvaluation? afterEvaluation =
            afterInformation is not null
                ? ProgramUpdateExceptionEvaluationService.Evaluate(
                    afterInformation,
                    visit)
                : null;

        /*
         * Nur Entscheidungen dieses Vorgangs mit
         * einer tatsächlich gespeicherten Begründung
         * dürfen im Bericht berücksichtigt werden.
         *
         * Bei mehrfach gespeicherter identischer
         * Entscheidung zählt die jüngste Fassung.
         *
         * Entscheidungen über unterschiedliche
         * Versionsstände bleiben getrennt.
         */
        var exceptions =
            visit.ProgramUpdateExceptions
                .Where(exception =>
                    exception is not null
                    && exception.CheckupVisitId == visit.Id
                    && !string.IsNullOrWhiteSpace(
                        exception.Reason))
                .GroupBy(
                    BuildExceptionIdentity,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                    group.OrderByDescending(
                        exception => exception.ApprovedAt)
                    .First())
                .OrderBy(exception =>
                    exception.PackageName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(exception =>
                    exception.PackageId,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        var decisions =
            new List<CustomerCheckupProgramUpdateDecision>();

        foreach (var exception in exceptions)
        {
            var detectedBefore =
                beforeReliable
                && beforeUpdates.Any(update =>
                    exception.AppliesTo(
                        visit.Id,
                        update));

            var detectedAfter =
                afterReliable
                && afterUpdates.Any(update =>
                    exception.AppliesTo(
                        visit.Id,
                        update));

            bool? presentAtBeginning =
                detectedBefore
                    ? true
                    : beforeComplete
                        ? false
                        : null;

            /*
             * Eine positive Erkennung beim Nachher-Scan
             * ist auch dann verwertbar, wenn dessen
             * gespeicherte Einzelliste unvollständig ist.
             *
             * Eine fehlende Erkennung bedeutet dagegen
             * nur bei vollständiger Liste, dass das
             * konkrete Update nicht mehr erkannt wurde.
             */
            var verificationStatus =
                detectedAfter
                    ? CustomerCheckupProgramUpdateVerificationStatus
                        .StillDetected
                    : afterComplete
                        ? CustomerCheckupProgramUpdateVerificationStatus
                            .NotDetectedAtCompletion
                        : CustomerCheckupProgramUpdateVerificationStatus
                            .NotReliablyVerifiable;

            decisions.Add(
                new CustomerCheckupProgramUpdateDecision
                {
                    ExceptionId = exception.Id,

                    PackageId =
                        Clean(exception.PackageId),

                    PackageName =
                        GetPackageName(exception),

                    InstalledVersion =
                        Clean(exception.InstalledVersion),

                    AvailableVersion =
                        Clean(exception.AvailableVersion),

                    Source =
                        Clean(exception.Source),

                    Reason =
                        Clean(exception.Reason),

                    TechnicianNote =
                        Clean(exception.TechnicianNote),

                    ApprovedAt =
                        exception.ApprovedAt,

                    WasDetectedAtBeginning =
                        presentAtBeginning,

                    VerificationStatus =
                        verificationStatus
                });
        }

        var initialCount =
            beforeReliable
                ? beforeInformation!.AvailableUpdateCount
                : (int?)null;

        var finalCount =
            afterReliable
                ? afterInformation!.AvailableUpdateCount
                : (int?)null;

        /*
         * Eine genaue Anzahl der Updates ohne
         * dokumentierte Ausnahme ist nur möglich,
         * wenn sämtliche einzelnen Paketidentitäten
         * für den betreffenden Scan vorliegen.
         */
        var initialWithoutDecisionCount =
            beforeComplete
                ? beforeEvaluation?.WithoutExceptionCount
                : null;

        var finalWithoutDecisionCount =
            afterComplete
                ? afterEvaluation?.WithoutExceptionCount
                : null;

        return new CustomerCheckupProgramUpdateReportData
        {
            IsInitialAnalysisReliable =
                beforeReliable,

            IsFinalAnalysisReliable =
                afterReliable,

            IsInitialUpdateListComplete =
                beforeComplete,

            IsFinalUpdateListComplete =
                afterComplete,

            InitialDetectedUpdateCount =
                initialCount,

            FinalDetectedUpdateCount =
                finalCount,

            InitialWithoutDecisionCount =
                initialWithoutDecisionCount,

            FinalWithoutDecisionCount =
                finalWithoutDecisionCount,

            DocumentedDecisionCount =
                decisions.Count,

            Decisions =
                decisions,

            CustomerSummary =
                BuildCustomerSummary(decisions),

            TechnicalNote =
                BuildTechnicalNote(
                    beforeReliable,
                    afterReliable,
                    beforeComplete,
                    afterComplete)
        };
    }

    private static bool IsAnalysisReliable(
        ProgramUpdateInformation? information)
    {
        return information is not null
               && information.IsWingetAvailable == true
               && information.IsAnalysisPerformed
               && information.IsAnalysisSuccessful
               && information.AvailableUpdateCount >= 0;
    }

    private static List<AvailableProgramUpdate>
        GetUniqueUpdates(
            ProgramUpdateInformation? information)
    {
        if (information?.AvailableUpdates is null)
        {
            return new List<AvailableProgramUpdate>();
        }

        return information.AvailableUpdates
            .Where(update =>
                update is not null
                && !string.IsNullOrWhiteSpace(
                    update.PackageId)
                && !string.IsNullOrWhiteSpace(
                    update.Source)
                && !string.IsNullOrWhiteSpace(
                    update.InstalledVersion)
                && !string.IsNullOrWhiteSpace(
                    update.AvailableVersion))
            .GroupBy(
                BuildUpdateIdentity,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static bool IsUpdateListComplete(
        ProgramUpdateInformation? information,
        bool isReliable,
        int uniqueUpdateCount)
    {
        return isReliable
               && information is not null
               && !information.IsResultTruncated
               && information.AvailableUpdateCount
                   == uniqueUpdateCount;
    }

    private static string BuildUpdateIdentity(
        AvailableProgramUpdate update)
    {
        return string.Join(
            "\u001F",
            Clean(update.PackageId),
            Clean(update.Source),
            Clean(update.InstalledVersion),
            Clean(update.AvailableVersion));
    }

    private static string BuildExceptionIdentity(
        ProgramUpdateException exception)
    {
        return string.Join(
            "\u001F",
            Clean(exception.PackageId),
            Clean(exception.Source),
            Clean(exception.InstalledVersion),
            Clean(exception.AvailableVersion));
    }

    private static string GetPackageName(
        ProgramUpdateException exception)
    {
        if (!string.IsNullOrWhiteSpace(
            exception.PackageName))
        {
            return exception.PackageName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(
            exception.PackageId))
        {
            return exception.PackageId.Trim();
        }

        return "Nicht näher bezeichnetes Programm";
    }

    private static string Clean(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string BuildCustomerSummary(
        IReadOnlyCollection<CustomerCheckupProgramUpdateDecision>
            decisions)
    {
        if (decisions.Count == 0)
        {
            return "In diesem Kundencheckup wurden keine "
                   + "Programmaktualisierungen begründet "
                   + "zurückgestellt.";
        }

        var introduction =
            decisions.Count == 1
                ? "Eine Programmaktualisierung wurde "
                  + "bewusst zurückgestellt und die "
                  + "Entscheidung begründet."
                : $"{decisions.Count} Programmaktualisierungen "
                  + "wurden bewusst zurückgestellt "
                  + "und die Entscheidungen begründet.";

        var stillDetected =
            decisions.Count(decision =>
                decision.VerificationStatus ==
                CustomerCheckupProgramUpdateVerificationStatus
                    .StillDetected);

        var notVerifiable =
            decisions.Count(decision =>
                decision.VerificationStatus ==
                CustomerCheckupProgramUpdateVerificationStatus
                    .NotReliablyVerifiable);

        var notDetected =
            decisions.Count(decision =>
                decision.VerificationStatus ==
                CustomerCheckupProgramUpdateVerificationStatus
                    .NotDetectedAtCompletion);

        var details =
            new List<string>();

        if (stillDetected > 0)
        {
            details.Add(
                stillDetected == 1
                    ? "Eine zurückgestellte Aktualisierung "
                      + "wurde bei der Abschlusskontrolle "
                      + "weiterhin erkannt."
                    : $"{stillDetected} zurückgestellte "
                      + "Aktualisierungen wurden bei der "
                      + "Abschlusskontrolle weiterhin erkannt.");
        }

        if (notDetected > 0)
        {
            details.Add(
                notDetected == 1
                    ? "Eine weitere Aktualisierung wurde "
                      + "bei der Abschlusskontrolle nicht "
                      + "mehr erkannt. Damit ist ihre "
                      + "Installation nicht automatisch bestätigt."
                    : $"{notDetected} weitere Aktualisierungen "
                      + "wurden bei der Abschlusskontrolle "
                      + "nicht mehr erkannt. Damit ist ihre "
                      + "Installation nicht automatisch bestätigt.");
        }

        if (notVerifiable > 0)
        {
            details.Add(
                "Bei mindestens einer zurückgestellten "
                + "Aktualisierung konnte der aktuelle "
                + "Stand nicht zuverlässig überprüft werden.");
        }

        return introduction + " " + string.Join(" ", details);
    }

    private static string BuildTechnicalNote(
        bool beforeReliable,
        bool afterReliable,
        bool beforeComplete,
        bool afterComplete)
    {
        var statements =
            new List<string>
            {
                "Dokumentierte Zurückstellungen sind "
                + "fachliche Technikerentscheidungen. "
                + "Sie verändern weder die technischen "
                + "Scanergebnisse noch den Systemscore.",

                "Eine erfolgreich ausgeführte "
                + "Wartungsaktion ist nicht automatisch "
                + "ein nachgewiesener behobener Befund."
            };

        if (!beforeReliable)
        {
            statements.Add(
                "Die Programmupdate-Prüfung des "
                + "Eingangsscans war nicht zuverlässig. "
                + "Ein vollständiger Abgleich mit den "
                + "ursprünglich erkannten Updates "
                + "ist deshalb nicht möglich.");
        }
        else if (!beforeComplete)
        {
            statements.Add(
                "Die gespeicherte Update-Einzelliste "
                + "des Eingangsscans ist unvollständig. "
                + "Nicht aufgeführte Pakete können "
                + "nicht einzeln zugeordnet werden.");
        }

        if (!afterReliable)
        {
            statements.Add(
                "Die Programmupdate-Prüfung der "
                + "Abschlusskontrolle war nicht "
                + "zuverlässig auswertbar. "
                + "Ob die zurückgestellten Updates "
                + "weiterhin ausstehen, kann daraus "
                + "nicht sicher abgeleitet werden.");
        }
        else if (!afterComplete)
        {
            statements.Add(
                "Die Abschlusskontrolle lieferte "
                + "keine vollständige Update-Einzelliste. "
                + "Erkannte Pakete können zugeordnet "
                + "werden; aus fehlenden Einträgen "
                + "darf keine erfolgreiche "
                + "Aktualisierung abgeleitet werden.");
        }
        else
        {
            statements.Add(
                "Die vollständige Update-Einzelliste "
                + "der Abschlusskontrolle erlaubt "
                + "den Abgleich der konkreten "
                + "Paket- und Versionsstände. "
                + "Nicht mehr erkannte Updates "
                + "gelten dadurch nicht automatisch "
                + "als nachweislich installiert.");
        }

        return string.Join(" ", statements);
    }
}

/// <summary>
/// Berichtsdaten zu Programmaktualisierungen.
/// Sämtliche Werte werden ausschließlich aus
/// den gespeicherten Checkup-Daten abgeleitet.
/// </summary>
public sealed class CustomerCheckupProgramUpdateReportData
{
    public bool IsInitialAnalysisReliable { get; init; }

    public bool IsFinalAnalysisReliable { get; init; }

    public bool IsInitialUpdateListComplete { get; init; }

    public bool IsFinalUpdateListComplete { get; init; }

    public int? InitialDetectedUpdateCount { get; init; }

    public int? FinalDetectedUpdateCount { get; init; }

    public int? InitialWithoutDecisionCount { get; init; }

    public int? FinalWithoutDecisionCount { get; init; }

    public int DocumentedDecisionCount { get; init; }

    public IReadOnlyList<CustomerCheckupProgramUpdateDecision>
        Decisions
    { get; init; } =
            Array.Empty<CustomerCheckupProgramUpdateDecision>();

    public string CustomerSummary { get; init; } =
        string.Empty;

    public string TechnicalNote { get; init; } =
        string.Empty;

    public bool HasDocumentedDecisions =>
        DocumentedDecisionCount > 0;

    public int ConfirmedStillDetectedCount =>
        Decisions.Count(decision =>
            decision.VerificationStatus ==
            CustomerCheckupProgramUpdateVerificationStatus
                .StillDetected);

    public int NotReliablyVerifiableCount =>
        Decisions.Count(decision =>
            decision.VerificationStatus ==
            CustomerCheckupProgramUpdateVerificationStatus
                .NotReliablyVerifiable);
}

/// <summary>
/// Eine dokumentierte Technikerentscheidung
/// einschließlich ihres überprüfbaren Status
/// bei der Abschlusskontrolle.
/// </summary>
public sealed class CustomerCheckupProgramUpdateDecision
{
    public Guid ExceptionId { get; init; }

    public string PackageId { get; init; } =
        string.Empty;

    public string PackageName { get; init; } =
        string.Empty;

    public string InstalledVersion { get; init; } =
        string.Empty;

    public string AvailableVersion { get; init; } =
        string.Empty;

    public string Source { get; init; } =
        string.Empty;

    public string Reason { get; init; } =
        string.Empty;

    public string TechnicianNote { get; init; } =
        string.Empty;

    public DateTime ApprovedAt { get; init; }

    /// <summary>
    /// True: Im Eingangsscan erkannt.
    /// False: Bei vollständigem Eingangsscan
    /// nicht in der Einzelliste enthalten.
    /// Null: Nicht zuverlässig feststellbar.
    /// </summary>
    public bool? WasDetectedAtBeginning { get; init; }

    public CustomerCheckupProgramUpdateVerificationStatus
        VerificationStatus
    { get; init; } =
            CustomerCheckupProgramUpdateVerificationStatus
                .NotReliablyVerifiable;

    public bool IsConfirmedStillPending =>
        VerificationStatus ==
        CustomerCheckupProgramUpdateVerificationStatus
            .StillDetected;

    public string VerificationText =>
        VerificationStatus switch
        {
            CustomerCheckupProgramUpdateVerificationStatus
                .StillDetected =>
                    "Weiterhin als Update erkannt",

            CustomerCheckupProgramUpdateVerificationStatus
                .NotDetectedAtCompletion =>
                    "Bei der Abschlusskontrolle "
                    + "nicht mehr erkannt",

            _ =>
                "Bei der Abschlusskontrolle "
                + "nicht zuverlässig überprüfbar"
        };

    public string VersionText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(InstalledVersion)
                || string.IsNullOrWhiteSpace(AvailableVersion))
            {
                return "Versionsstand nicht vollständig dokumentiert";
            }

            return InstalledVersion
                   + " → "
                   + AvailableVersion;
        }
    }
}

/// <summary>
/// Status einer konkreten zurückgestellten
/// Programmaktualisierung nach der Abschlusskontrolle.
/// </summary>
public enum CustomerCheckupProgramUpdateVerificationStatus
{
    NotReliablyVerifiable = 0,

    StillDetected = 1,

    NotDetectedAtCompletion = 2
}
