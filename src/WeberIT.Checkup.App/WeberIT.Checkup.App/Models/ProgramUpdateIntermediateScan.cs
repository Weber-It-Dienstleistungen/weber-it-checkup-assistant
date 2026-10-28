
using System.Text.Json.Serialization;

namespace WeberIT.Checkup.App.Models;

/// <summary>
/// Dokumentiert eine eigenständige WinGet-Zwischenprüfung
/// während eines laufenden Kundencheckups.
///
/// Der ursprüngliche Eingangsscan, vorhandene
/// Aktionsprotokolle und die spätere Abschlusskontrolle
/// bleiben davon unabhängig.
///
/// Dieses Modell führt selbst keine Systemaktionen aus.
/// </summary>
public sealed class ProgramUpdateIntermediateScan
{
    /// <summary>
    /// Eindeutige Kennung dieser Zwischenprüfung.
    /// </summary>
    public Guid Id { get; set; } =
        Guid.NewGuid();

    /// <summary>
    /// Zugehöriger Kundencheckup.
    /// </summary>
    public Guid CustomerCheckupVisitId { get; set; }

    /// <summary>
    /// Zeitpunkt der Übernahme des Scanergebnisses.
    /// </summary>
    public DateTimeOffset RecordedAt { get; set; } =
        DateTimeOffset.Now;

    /// <summary>
    /// Unabhängig gespeichertes WinGet-Ergebnis.
    ///
    /// Es wird ausdrücklich nicht dieselbe Instanz
    /// verwendet wie im ursprünglichen Eingangsscan.
    /// </summary>
    public ProgramUpdateInformation ScanInformation
    {
        get;
        set;
    } = new();

    /// <summary>
    /// Gibt an, ob die Zwischenprüfung einen
    /// belastbaren und vollständigen Updatebestand
    /// geliefert hat.
    ///
    /// Eine fehlgeschlagene Prüfung darf niemals
    /// als Bestätigung von null Updates gelten.
    /// </summary>
    [JsonIgnore]
    public bool IsReliable
    {
        get
        {
            var information =
                ScanInformation;

            if (information is null)
            {
                return false;
            }

            var updates =
                information.AvailableUpdates;

            return
                information.IsWingetAvailable == true
                && information.IsAnalysisPerformed
                && information.IsAnalysisSuccessful
                && !information.IsResultTruncated
                && information.AnalysisDate.HasValue
                && information.AvailableUpdateCount >= 0
                && updates is not null
                && information.AvailableUpdateCount
                    == updates.Count
                && updates.All(
                    update => update is not null);
        }
    }

    /// <summary>
    /// Anzahl der zuverlässig erkannten offenen
    /// Updates.
    ///
    /// Null bedeutet hier ausdrücklich:
    /// Keine belastbare Aussage möglich.
    ///
    /// Der Wert 0 bedeutet dagegen:
    /// Eine vollständige Analyse hat kein
    /// verfügbares Update festgestellt.
    /// </summary>
    [JsonIgnore]
    public int? ConfirmedAvailableUpdateCount =>
        IsReliable
            ? ScanInformation.AvailableUpdateCount
            : null;

    /// <summary>
    /// Erstellt eine neue Zwischenprüfung aus einem
    /// vorhandenen WinGet-Analyseergebnis.
    ///
    /// Sämtliche Paketinformationen werden kopiert.
    /// Änderungen am ursprünglichen Scanobjekt
    /// beeinflussen diesen Datensatz nicht.
    /// </summary>
    public static ProgramUpdateIntermediateScan Capture(
        Guid customerCheckupVisitId,
        ProgramUpdateInformation information)
    {
        if (customerCheckupVisitId == Guid.Empty)
        {
            throw new ArgumentException(
                "Für die WinGet-Zwischenprüfung "
                + "ist eine gültige Kundencheckup-ID "
                + "erforderlich.",
                nameof(customerCheckupVisitId));
        }

        ArgumentNullException.ThrowIfNull(information);

        return new ProgramUpdateIntermediateScan
        {
            Id = Guid.NewGuid(),

            CustomerCheckupVisitId =
                customerCheckupVisitId,

            RecordedAt = DateTimeOffset.Now,

            ScanInformation =
                CopyInformation(information)
        };
    }

    /// <summary>
    /// Erstellt eine unabhängige Kopie des
    /// bestehenden Zwischenprüfungsdatensatzes.
    ///
    /// Kennung, Kundencheckup-Zuordnung und
    /// ursprünglicher Erfassungszeitpunkt
    /// bleiben erhalten.
    /// </summary>
    public ProgramUpdateIntermediateScan
        CreateIndependentCopy()
    {
        return new ProgramUpdateIntermediateScan
        {
            Id = Id,

            CustomerCheckupVisitId =
                CustomerCheckupVisitId,

            RecordedAt = RecordedAt,

            ScanInformation =
                CopyInformation(ScanInformation)
        };
    }

    /// <summary>
    /// Liefert eine unabhängige Kopie der
    /// gespeicherten WinGet-Informationen.
    ///
    /// Dadurch kann eine spätere Arbeitsansicht
    /// mit den Informationen arbeiten, ohne
    /// versehentlich den gespeicherten Befund
    /// zu verändern.
    /// </summary>
    public ProgramUpdateInformation
        CreateIndependentInformationCopy()
    {
        return CopyInformation(ScanInformation);
    }

    /// <summary>
    /// Kopiert sämtliche bestehenden Eigenschaften
    /// des WinGet-Analysemodells einschließlich
    /// der einzelnen Paketinformationen.
    /// </summary>
    private static ProgramUpdateInformation
        CopyInformation(
            ProgramUpdateInformation source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var copiedUpdates =
            new List<AvailableProgramUpdate>();

        if (source.AvailableUpdates is not null)
        {
            foreach (var update
                     in source.AvailableUpdates)
            {
                if (update is null)
                {
                    throw new InvalidOperationException(
                        "Die WinGet-Analyse enthält "
                        + "einen ungültigen Paketeintrag.");
                }

                copiedUpdates.Add(
                    new AvailableProgramUpdate
                    {
                        PackageId =
                            update.PackageId,

                        Name =
                            update.Name,

                        InstalledVersion =
                            update.InstalledVersion,

                        AvailableVersion =
                            update.AvailableVersion,

                        Source =
                            update.Source
                    });
            }
        }

        return new ProgramUpdateInformation
        {
            IsWingetAvailable =
                source.IsWingetAvailable,

            WingetVersion =
                source.WingetVersion,

            IsAnalysisPerformed =
                source.IsAnalysisPerformed,

            IsAnalysisSuccessful =
                source.IsAnalysisSuccessful,

            AnalysisDate =
                source.AnalysisDate,

            AnalysisDetails =
                source.AnalysisDetails,

            AvailableUpdateCount =
                source.AvailableUpdateCount,

            IsResultTruncated =
                source.IsResultTruncated,

            AvailableUpdates =
                copiedUpdates
        };
    }
}
