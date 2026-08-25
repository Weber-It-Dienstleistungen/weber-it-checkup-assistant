using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Pdf;
using System.IO;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Reports;

public sealed class CustomerCheckupPdfReportService :
    ICustomerCheckupPdfReportService
{
    private const double ContentWidthCentimeters =
        17.8;

    private static readonly Color AccentColor =
        Color.FromRgb(
            37,
            99,
            235);

    private static readonly Color HeadingColor =
        Color.FromRgb(
            15,
            23,
            42);

    private static readonly Color TextColor =
        Color.FromRgb(
            30,
            41,
            59);

    private static readonly Color MutedTextColor =
        Color.FromRgb(
            71,
            85,
            105);

    private static readonly Color BorderColor =
        Color.FromRgb(
            203,
            213,
            225);

    private static readonly Color SurfaceColor =
        Color.FromRgb(
            248,
            250,
            252);

    private static readonly Color SecondarySurfaceColor =
        Color.FromRgb(
            226,
            232,
            240);

    private static readonly Color SuccessColor =
        Color.FromRgb(
            21,
            128,
            61);

    private static readonly Color WarningColor =
        Color.FromRgb(
            180,
            83,
            9);

    private static readonly Color DangerColor =
        Color.FromRgb(
            185,
            28,
            28);

    public void Export(
        Customer customer,
        CustomerDevice device,
        CustomerCheckupVisit customerCheckupVisit,
        string filePath)
    {
        ArgumentNullException.ThrowIfNull(
            customer);

        ArgumentNullException.ThrowIfNull(
            device);

        ArgumentNullException.ThrowIfNull(
            customerCheckupVisit);

        ValidateReportVisit(
            customerCheckupVisit);

        if (string.IsNullOrWhiteSpace(
                filePath))
        {
            throw new ArgumentException(
                "Für den Kundencheckup-Bericht wurde kein gültiger "
                + "Zielpfad angegeben.",
                nameof(filePath));
        }

        var normalizedFilePath =
            NormalizePdfFilePath(
                filePath);

        EnsureTargetDirectoryExists(
            normalizedFilePath);

        var document =
            CreateDocument(
                customer,
                device,
                customerCheckupVisit);

        var renderer =
            new PdfDocumentRenderer
            {
                Document =
                    document
            };

        renderer.PdfDocument.PageLayout =
            PdfPageLayout.SinglePage;

        renderer.PdfDocument
            .ViewerPreferences
            .FitWindow =
                true;

        renderer.RenderDocument();

        renderer.PdfDocument.Info.Title =
            document.Info.Title;

        renderer.PdfDocument.Info.Author =
            document.Info.Author;

        renderer.PdfDocument.Info.Subject =
            document.Info.Subject;

        renderer.Save(
            normalizedFilePath);

        var reportFile =
            new FileInfo(
                normalizedFilePath);

        if (!reportFile.Exists
            || reportFile.Length == 0)
        {
            throw new IOException(
                "Der Kundencheckup-Bericht wurde nicht als gültige "
                + "PDF-Datei gespeichert.");
        }
    }

    private static void ValidateReportVisit(
        CustomerCheckupVisit customerCheckupVisit)
    {
        if ((!customerCheckupVisit.IsInProgress
             && !customerCheckupVisit.IsCompleted)
            || !customerCheckupVisit.HasAfterCheckup
            || !customerCheckupVisit.HasComparison)
        {
            throw new InvalidOperationException(
                "Der Kundencheckup-Bericht kann erst erstellt werden, "
                + "wenn Nachher-Scan, Vergleich und Technikerangaben "
                + "vollständig gespeichert wurden.");
        }

        var afterCheckup =
            customerCheckupVisit.AfterCheckup;

        var comparison =
            customerCheckupVisit.Comparison;

        if (afterCheckup?.ScanDate is null
            || comparison is null)
        {
            throw new InvalidOperationException(
                "Der gespeicherte Abschlussentwurf enthält keinen "
                + "vollständigen Nachher-Zustand.");
        }

        if (comparison.CustomerCheckupVisitId
                != customerCheckupVisit.Id
            || comparison.BeforeScanDate
                != customerCheckupVisit.BeforeCheckup.ScanDate
            || comparison.AfterScanDate
                != afterCheckup.ScanDate)
        {
            throw new InvalidOperationException(
                "Der gespeicherte Vorher-/Nachher-Vergleich gehört "
                + "nicht vollständig zu diesem Kundencheckup.");
        }

        if (string.IsNullOrWhiteSpace(
                customerCheckupVisit.TechnicianSummary)
            || string.IsNullOrWhiteSpace(
                customerCheckupVisit.NextSteps)
            || !customerCheckupVisit.NextCheckupDate.HasValue)
        {
            throw new InvalidOperationException(
                "Der Abschlussentwurf enthält keine vollständigen "
                + "Technikerangaben.");
        }
    }

    private static Document CreateDocument(
        Customer customer,
        CustomerDevice device,
        CustomerCheckupVisit customerCheckupVisit)
    {
        var comparison =
            customerCheckupVisit.Comparison!;

        var document =
            new Document();

        document.Info.Title =
            "Weber IT Kundencheckup-Abschlussbericht";

        document.Info.Author =
            "Weber IT-Dienstleistungen";

        document.Info.Subject =
            "Kundenspezifischer Vorher-/Nachher-Bericht eines "
            + "Windows-PC-Checkups";

        document.Info.Keywords =
            "Windows, Kundencheckup, Vorher-Nachher, "
            + "Wartung, Weber IT";

        ConfigureStyles(
            document);

        var section =
            document.AddSection();

        ConfigurePage(
            section);

        AddHeaderAndFooter(
            section,
            customer);

        AddTitle(
            section,
            customer,
            device,
            customerCheckupVisit);

        AddTechnicianSummary(
            section,
            customerCheckupVisit);

        AddResultOverview(
            section,
            comparison);

        AddScoreComparison(
            section,
            comparison);

        AddAreaComparison(
            section,
            comparison);

        AddActions(
            section,
            comparison);

        AddFindings(
            section,
            comparison);

        AddTasks(
            section,
            comparison);

        AddComparisonNotes(
            section,
            comparison);

        AddReportNotice(
            section);

        return document;
    }

    private static void ConfigureStyles(
        Document document)
    {
        var normalStyle =
            document.Styles[
                StyleNames.Normal]!;

        normalStyle.Font.Name =
            "Arial";

        normalStyle.Font.Size =
            Unit.FromPoint(
                9);

        normalStyle.Font.Color =
            TextColor;

        normalStyle.ParagraphFormat.SpaceAfter =
            Unit.FromPoint(
                3);

        var headingStyle =
            document.Styles[
                StyleNames.Heading1]!;

        headingStyle.Font.Name =
            "Arial";

        headingStyle.Font.Size =
            Unit.FromPoint(
                15);

        headingStyle.Font.Bold =
            true;

        headingStyle.Font.Color =
            HeadingColor;

        headingStyle.ParagraphFormat.SpaceBefore =
            Unit.FromPoint(
                12);

        headingStyle.ParagraphFormat.SpaceAfter =
            Unit.FromPoint(
                7);

        headingStyle.ParagraphFormat.KeepWithNext =
            true;
    }

    private static void ConfigurePage(
        Section section)
    {
        section.PageSetup.PageFormat =
            PageFormat.A4;

        section.PageSetup.Orientation =
            Orientation.Portrait;

        section.PageSetup.TopMargin =
            Unit.FromCentimeter(
                1.7);

        section.PageSetup.BottomMargin =
            Unit.FromCentimeter(
                1.7);

        section.PageSetup.LeftMargin =
            Unit.FromCentimeter(
                1.6);

        section.PageSetup.RightMargin =
            Unit.FromCentimeter(
                1.6);
    }

    private static void AddHeaderAndFooter(
        Section section,
        Customer customer)
    {
        var header =
            section.Headers.Primary
                .AddParagraph(
                    "Weber IT-Dienstleistungen · Kundencheckup");

        header.Format.Font.Name =
            "Arial";

        header.Format.Font.Size =
            Unit.FromPoint(
                8);

        header.Format.Font.Color =
            MutedTextColor;

        header.Format.Alignment =
            ParagraphAlignment.Right;

        var footer =
            section.Footers.Primary
                .AddParagraph();

        footer.AddText(
            "Kundencheckup "
            + SafeText(
                customer.CustomerNumber,
                "ohne Kundennummer")
            + " · Seite ");

        footer.AddPageField();

        footer.Format.Font.Name =
            "Arial";

        footer.Format.Font.Size =
            Unit.FromPoint(
                8);

        footer.Format.Font.Color =
            MutedTextColor;

        footer.Format.Alignment =
            ParagraphAlignment.Center;
    }

    private static void AddTitle(
        Section section,
        Customer customer,
        CustomerDevice device,
        CustomerCheckupVisit customerCheckupVisit)
    {
        var afterCheckup =
            customerCheckupVisit.AfterCheckup!;

        var deviceInformation =
            afterCheckup.DeviceInformation;

        var operatingSystem =
            afterCheckup.OperatingSystemInformation;

        var title =
            section.AddParagraph(
                "Kundencheckup-Abschlussbericht");

        title.Format.Font.Name =
            "Arial";

        title.Format.Font.Size =
            Unit.FromPoint(
                22);

        title.Format.Font.Bold =
            true;

        title.Format.Font.Color =
            HeadingColor;

        title.Format.SpaceAfter =
            Unit.FromPoint(
                3);

        var subtitle =
            section.AddParagraph(
                "Dokumentation des Ausgangszustands, der "
                + "Aufgabenbearbeitung, der technischen Maßnahmen "
                + "und der abschließenden Kontrollprüfung");

        subtitle.Format.Font.Size =
            Unit.FromPoint(
                11);

        subtitle.Format.Font.Color =
            MutedTextColor;

        subtitle.Format.SpaceAfter =
            Unit.FromPoint(
                12);

        AddCallout(
            section,
            "Kundenspezifischer Abschlussbericht",
            "Dieser Bericht gehört zum Kundencheckup für "
            + SafeText(
                customer.DisplayName,
                "den ausgewählten Kunden")
            + ". Er fasst den gesicherten Vorher-Zustand, "
            + "die dokumentierte Bearbeitung und den technischen "
            + "Zustand nach der Abschlusskontrolle zusammen.",
            AccentColor);

        AddKeyValueTable(
            section,
            new[]
            {
                (
                    "Kunde",
                    SafeText(
                        customer.DisplayName)),

                (
                    "Kundennummer",
                    SafeText(
                        customer.CustomerNumber)),

                (
                    "Kontakt",
                    BuildCustomerContactText(
                        customer)),

                (
                    "Anschrift",
                    BuildCustomerAddressText(
                        customer)),

                (
                    "Gespeichertes Gerät",
                    SafeText(
                        device.DisplayName)),

                (
                    "Computername",
                    SafeText(
                        deviceInformation.Name)),

                (
                    "Hersteller / Modell",
                    SafeText(
                        deviceInformation.Manufacturer)
                    + " / "
                    + SafeText(
                        deviceInformation.Model)),

                (
                    "Seriennummer",
                    SafeText(
                        deviceInformation.SerialNumber)),

                (
                    "Betriebssystem",
                    SafeText(
                        operatingSystem.Name)
                    + " · Version "
                    + SafeText(
                        operatingSystem.Version)
                    + " · Build "
                    + SafeText(
                        operatingSystem.BuildNumber)),

                (
                    "Eingangsscan",
                    FormatDateTime(
                        customerCheckupVisit
                            .BeforeCheckup
                            .ScanDate)),

                (
                    "Abschlusskontrolle",
                    FormatDateTime(
                        afterCheckup.ScanDate)),

                (
                    "Bericht erstellt",
                    FormatDateTime(
                        DateTime.Now))
            });
    }

    private static void AddTechnicianSummary(
        Section section,
        CustomerCheckupVisit customerCheckupVisit)
    {
        AddSectionHeading(
            section,
            "1. Zusammenfassung und weiteres Vorgehen");

        AddCallout(
            section,
            "Technikerzusammenfassung",
            customerCheckupVisit.TechnicianSummary,
            AccentColor);

        AddCallout(
            section,
            "Nächste Schritte",
            customerCheckupVisit.NextSteps,
            WarningColor);

        AddKeyValueTable(
            section,
            new[]
            {
                (
                    "Empfohlener nächster Checkup",
                    FormatDate(
                        customerCheckupVisit
                            .NextCheckupDate))
            });
    }

    private static void AddResultOverview(
        Section section,
        CustomerCheckupComparison comparison)
    {
        AddSectionHeading(
            section,
            "2. Technische Ergebnisübersicht");

        var table =
            CreateTable(
                section,
                4.45,
                4.45,
                4.45,
                4.45);

        AddHeaderRow(
            table,
            "Technisch behoben",
            "Weiterhin vorhanden",
            "Neu erkannt",
            "Nicht erneut auswertbar");

        var row =
            AddDataRow(
                table,
                comparison
                    .ResolvedFindingCount
                    .ToString(),

                comparison
                    .StillOpenFindingCount
                    .ToString(),

                comparison
                    .NewlyDetectedFindingCount
                    .ToString(),

                comparison
                    .NotReevaluatableFindingCount
                    .ToString());

        SetCellHighlight(
            row.Cells[0],
            SuccessColor);

        SetCellHighlight(
            row.Cells[1],
            WarningColor);

        SetCellHighlight(
            row.Cells[2],
            DangerColor);

        SetCellHighlight(
            row.Cells[3],
            MutedTextColor);

        AddSpacer(
            section,
            6);

        AddCallout(
            section,
            "Einordnung der Ergebnisübersicht",
            "Diese vier Werte beschreiben ausschließlich die "
            + "technische Befundlage nach der Abschlusskontrolle. "
            + "Sie sagen nicht aus, ob ein zugehöriger Arbeitspunkt "
            + "unbearbeitet war. Aufgabenbearbeitung und technischer "
            + "Befund werden bewusst getrennt dokumentiert.",
            AccentColor);

        var actionSummary =
            comparison.SuccessfulActionCount
            + " erfolgreich · "
            + comparison.FailedActionCount
            + " fehlgeschlagen · "
            + comparison.CancelledActionCount
            + " abgebrochen";

        AddCallout(
            section,
            "Dokumentierte technische Aktionen",
            actionSummary
            + (comparison.HasRestartRequirement
                ? ". Mindestens eine technische Aktion meldet "
                  + "Neustartbedarf."
                : ". Kein ausdrücklicher Neustartbedarf "
                  + "dokumentiert.")
            + " Manuell oder geführt bearbeitete Aufgaben "
            + "werden separat in der Aufgabendokumentation "
            + "ausgewiesen.",
            comparison.FailedActionCount > 0
                ? DangerColor
                : AccentColor);
    }

    private static void AddScoreComparison(
        Section section,
        CustomerCheckupComparison comparison)
    {
        AddSectionHeading(
            section,
            "3. Vergleich der Zustandsbewertungen");

        var table =
            CreateTable(
                section,
                3.8,
                2.6,
                2.6,
                3.0,
                5.8);

        AddHeaderRow(
            table,
            "Bewertung",
            "Vorher",
            "Nachher",
            "Änderung",
            "Einordnung");

        AddScoreRow(
            table,
            "Systemzustand",
            comparison.SystemScore);

        AddScoreRow(
            table,
            "Hardwarezustand",
            comparison.HardwareScore);

        AddSpacer(
            section,
            5);
    }

    private static void AddScoreRow(
        Table table,
        string title,
        CustomerCheckupScoreComparison score)
    {
        var row =
            AddDataRow(
                table,
                title,
                FormatScore(
                    score.BeforeScore),
                FormatScore(
                    score.AfterScore),
                GetScoreChangeText(
                    score),
                SafeText(
                    score.ComparisonReason,
                    score.IsComparable
                        ? "Direkt vergleichbar"
                        : "Nicht direkt vergleichbar"));

        row.Cells[0].Format.Font.Bold =
            true;

        row.Cells[3].Format.Font.Color =
            GetScoreChangeColor(
                score.Change);
    }

    private static void AddAreaComparison(
        Section section,
        CustomerCheckupComparison comparison)
    {
        AddSectionHeading(
            section,
            "4. Vergleich nach technischen Bereichen");

        var areas =
            comparison.Areas
                .OrderBy(
                    area =>
                        GetAreaStatusOrder(
                            area.Status))
                .ThenBy(
                    area =>
                        area.Title,
                    StringComparer
                        .CurrentCultureIgnoreCase)
                .ToList();

        if (areas.Count == 0)
        {
            AddCallout(
                section,
                "Keine Bereichsauswertung verfügbar",
                "Der gespeicherte Vergleich enthält keine "
                + "technischen Bereichsergebnisse.",
                WarningColor);

            return;
        }

        var table =
            CreateTable(
                section,
                4.6,
                2.2,
                2.2,
                4.0,
                4.8);

        AddHeaderRow(
            table,
            "Bereich",
            "Vorher",
            "Nachher",
            "Technisches Ergebnis",
            "Auswertbarkeit");

        var rowIndex =
            0;

        foreach (var area in areas)
        {
            var row =
                AddDataRow(
                    table,
                    SafeText(
                        area.Title),
                    FormatFindingCount(
                        area.BeforeActionableFindingCount),
                    FormatFindingCount(
                        area.AfterActionableFindingCount),
                    GetAreaStatusText(
                        area.Status),
                    GetAreaEvaluationText(
                        area));

            row.Cells[0].Format.Font.Bold =
                true;

            row.Cells[3].Format.Font.Color =
                GetAreaStatusColor(
                    area.Status);

            ShadeAlternatingRow(
                row,
                rowIndex++);
        }

        AddSpacer(
            section,
            5);
    }

    private static void AddActions(
        Section section,
        CustomerCheckupComparison comparison)
    {
        AddSectionHeading(
            section,
            "5. Dokumentierte technische Aktionen");

        var actions =
            comparison.Actions
                .OrderBy(
                    action =>
                        action.StartedAt
                        ?? DateTimeOffset.MaxValue)
                .ThenBy(
                    action =>
                        action.ActionTitle,
                    StringComparer
                        .CurrentCultureIgnoreCase)
                .ToList();

        if (actions.Count == 0)
        {
            AddCallout(
                section,
                "Keine technischen Aktionen dokumentiert",
                "Im Arbeitsstand dieses Kundencheckups sind "
                + "keine ausgeführten technischen Aktionen "
                + "gespeichert. Manuell oder geführt bearbeitete "
                + "Aufgaben können dennoch in Abschnitt 7 "
                + "dokumentiert sein.",
                MutedTextColor);

            return;
        }

        var table =
            CreateTable(
                section,
                4.6,
                2.8,
                3.0,
                7.4);

        AddHeaderRow(
            table,
            "Aktion",
            "Status",
            "Zeitpunkt",
            "Technisches Ergebnis");

        var rowIndex =
            0;

        foreach (var action in actions)
        {
            var row =
                AddDataRow(
                    table,
                    SafeText(
                        action.ActionTitle,
                        action.TaskTitle),
                    GetActionStatusText(
                        action.Status),
                    FormatDateTimeOffset(
                        action.FinishedAt
                        ?? action.StartedAt),
                    BuildActionResultText(
                        action));

            row.Cells[0].Format.Font.Bold =
                true;

            row.Cells[1].Format.Font.Color =
                GetActionStatusColor(
                    action.Status);

            ShadeAlternatingRow(
                row,
                rowIndex++);
        }

        AddSpacer(
            section,
            5);
    }

    private static void AddFindings(
        Section section,
        CustomerCheckupComparison comparison)
    {
        AddSectionHeading(
            section,
            "6. Verbleibende, neue oder nicht erneut "
            + "auswertbare Befunde");

        var findings =
            comparison.Findings
                .Where(
                    finding =>
                        finding.Status
                        != CustomerCheckupFindingComparisonStatus
                            .Resolved)
                .OrderBy(
                    finding =>
                        GetFindingStatusOrder(
                            finding.Status))
                .ThenByDescending(
                    finding =>
                        GetSeverityOrder(
                            finding.AfterSeverity
                            ?? finding.BeforeSeverity))
                .ThenBy(
                    finding =>
                        finding.Title,
                    StringComparer
                        .CurrentCultureIgnoreCase)
                .ToList();

        if (findings.Count == 0)
        {
            AddCallout(
                section,
                "Keine verbleibenden technischen Befunde",
                "Es wurden keine verbleibenden, neuen oder "
                + "nicht erneut auswertbaren technischen Befunde "
                + "festgestellt.",
                SuccessColor);

            return;
        }

        var table =
            CreateTable(
                section,
                4.8,
                2.4,
                3.0,
                7.6);

        AddHeaderRow(
            table,
            "Befund",
            "Einstufung",
            "Befundstatus",
            "Beschreibung nach Abschlusskontrolle");

        var rowIndex =
            0;

        foreach (var finding in findings)
        {
            var severity =
                finding.AfterSeverity
                ?? finding.BeforeSeverity;

            var row =
                AddDataRow(
                    table,
                    SafeText(
                        finding.Title),
                    GetSeverityText(
                        severity),
                    GetFindingStatusText(
                        finding.Status),
                    SafeText(
                        finding.AfterDescription,
                        finding.BeforeDescription));

            row.Cells[0].Format.Font.Bold =
                true;

            row.Cells[1].Format.Font.Color =
                GetSeverityColor(
                    severity);

            row.Cells[2].Format.Font.Color =
                GetFindingStatusColor(
                    finding.Status);

            ShadeAlternatingRow(
                row,
                rowIndex++);
        }

        AddSpacer(
            section,
            5);
    }

    private static void AddTasks(
        Section section,
        CustomerCheckupComparison comparison)
    {
        AddSectionHeading(
            section,
            "7. Aufgabenbearbeitung und Dokumentation");

        var tasks =
            comparison.Tasks
                .OrderBy(
                    task =>
                        GetTaskStatusOrder(
                            task.Status))
                .ThenByDescending(
                    task =>
                        GetTaskPriorityOrder(
                            task.Priority))
                .ThenBy(
                    task =>
                        task.Title,
                    StringComparer
                        .CurrentCultureIgnoreCase)
                .ToList();

        if (tasks.Count == 0)
        {
            AddCallout(
                section,
                "Keine Aufgaben dokumentiert",
                "Der gespeicherte Kundencheckup enthält "
                + "keine auswertbare Aufgabendokumentation.",
                MutedTextColor);

            return;
        }

        AddCallout(
            section,
            "Aufgabenstatus und technischer Befund",
            "Der Aufgabenstatus dokumentiert, wie ein Arbeitspunkt "
            + "bearbeitet wurde. Er ist nicht automatisch mit dem "
            + "technischen Befund identisch. Ob ein Befund nach der "
            + "Bearbeitung weiterhin vorhanden ist, ergibt sich aus "
            + "der Abschlusskontrolle.",
            AccentColor);

        var table =
            CreateTable(
                section,
                4.8,
                2.4,
                3.6,
                7.0);

        AddHeaderRow(
            table,
            "Aufgabe",
            "Priorität",
            "Aufgabenstatus",
            "Technikernotiz / Begründung");

        var rowIndex =
            0;

        foreach (var task in tasks)
        {
            var row =
                AddDataRow(
                    table,
                    SafeText(
                        task.Title),
                    GetTaskPriorityText(
                        task.Priority),
                    GetTaskStatusText(
                        task.Status),
                    BuildTaskNoteText(
                        task));

            row.Cells[0].Format.Font.Bold =
                true;

            row.Cells[1].Format.Font.Color =
                GetTaskPriorityColor(
                    task.Priority);

            row.Cells[2].Format.Font.Color =
                GetTaskStatusColor(
                    task.Status);

            ShadeAlternatingRow(
                row,
                rowIndex++);
        }

        AddSpacer(
            section,
            5);
    }

    private static void AddComparisonNotes(
        Section section,
        CustomerCheckupComparison comparison)
    {
        var notes =
            comparison.ComparisonNotes
                .Where(
                    note =>
                        !string.IsNullOrWhiteSpace(
                            note))
                .Select(
                    note =>
                        note.Trim())
                .ToList();

        if (notes.Count == 0)
        {
            return;
        }

        AddSectionHeading(
            section,
            "8. Hinweise zur Vergleichbarkeit");

        foreach (var note in notes)
        {
            var paragraph =
                section.AddParagraph(
                    "• " + note);

            paragraph.Format.LeftIndent =
                Unit.FromCentimeter(
                    0.3);

            paragraph.Format.FirstLineIndent =
                Unit.FromCentimeter(
                    -0.3);

            paragraph.Format.Font.Color =
                MutedTextColor;

            paragraph.Format.SpaceAfter =
                Unit.FromPoint(
                    4);
        }

        AddSpacer(
            section,
            4);
    }

    private static void AddReportNotice(
        Section section)
    {
        AddSectionHeading(
            section,
            "Berichtshinweis");

        AddCallout(
            section,
            "Technische Dokumentation",
            "Dieser Bericht dokumentiert den zu den "
            + "Scanzeitpunkten aus Windows auslesbaren Zustand "
            + "sowie die im Kundencheckup gespeicherten "
            + "Aufgabenbearbeitungen und technischen Aktionen. "
            + "Er ersetzt keine Datensicherung, Herstellergarantie "
            + "oder physische Laborprüfung. Nicht auswertbare "
            + "Bereiche und nicht direkt vergleichbare Bewertungen "
            + "sind gekennzeichnet. Aufgabenstatus und technische "
            + "Befundlage werden bewusst getrennt ausgewiesen.",
            MutedTextColor);
    }

    private static void AddSectionHeading(
        Section section,
        string text)
    {
        section
            .AddParagraph(
                text)
            .Style =
                StyleNames.Heading1;
    }

    private static void AddKeyValueTable(
        Section section,
        IEnumerable<(string Label, string Value)> values)
    {
        var table =
            CreateTable(
                section,
                4.5,
                13.3);

        var rowIndex =
            0;

        foreach (var value in values)
        {
            var row =
                AddDataRow(
                    table,
                    value.Label,
                    SafeText(
                        value.Value));

            row.Cells[0].Format.Font.Bold =
                true;

            row.Cells[0].Shading.Color =
                SecondarySurfaceColor;

            ShadeAlternatingCell(
                row.Cells[1],
                rowIndex++);
        }

        AddSpacer(
            section,
            5);
    }

    private static void AddCallout(
        Section section,
        string title,
        string message,
        Color accentColor)
    {
        var table =
            CreateTable(
                section,
                ContentWidthCentimeters);

        var cell =
            table
                .AddRow()
                .Cells[0];

        cell.Shading.Color =
            SurfaceColor;

        cell.Borders.Color =
            accentColor;

        cell.Borders.Width =
            Unit.FromPoint(
                0.8);

        var titleParagraph =
            cell.AddParagraph(
                SafeText(
                    title));

        titleParagraph.Format.Font.Bold =
            true;

        titleParagraph.Format.Font.Color =
            accentColor;

        titleParagraph.Format.SpaceAfter =
            Unit.FromPoint(
                3);

        var messageParagraph =
            cell.AddParagraph(
                SafeText(
                    message));

        messageParagraph.Format.Font.Color =
            TextColor;

        AddSpacer(
            section,
            6);
    }

    private static Table CreateTable(
        Section section,
        params double[] columnWidthsCentimeters)
    {
        var table =
            section.AddTable();

        table.Borders.Color =
            BorderColor;

        table.Borders.Width =
            Unit.FromPoint(
                0.5);

        table.Format.Font.Name =
            "Arial";

        table.Format.Font.Size =
            Unit.FromPoint(
                8.5);

        foreach (var width
                 in columnWidthsCentimeters)
        {
            table.AddColumn(
                Unit.FromCentimeter(
                    width));
        }

        return table;
    }

    private static void AddHeaderRow(
        Table table,
        params string[] headings)
    {
        var row =
            table.AddRow();

        row.HeadingFormat =
            true;

        for (var index = 0;
             index < headings.Length;
             index++)
        {
            var cell =
                row.Cells[index];

            cell.Shading.Color =
                HeadingColor;

            cell.Format.Font.Bold =
                true;

            cell.Format.Font.Color =
                Colors.White;

            cell.AddParagraph(
                SafeText(
                    headings[index]));
        }
    }

    private static Row AddDataRow(
        Table table,
        params string[] values)
    {
        var row =
            table.AddRow();

        for (var index = 0;
             index < values.Length;
             index++)
        {
            var paragraph =
                row.Cells[index]
                    .AddParagraph(
                        SafeText(
                            values[index]));

            paragraph.Format.SpaceAfter =
                Unit.FromPoint(
                    0);
        }

        return row;
    }

    private static void SetCellHighlight(
        Cell cell,
        Color color)
    {
        cell.Format.Font.Bold =
            true;

        cell.Format.Font.Size =
            Unit.FromPoint(
                15);

        cell.Format.Font.Color =
            color;

        cell.Format.Alignment =
            ParagraphAlignment.Center;
    }

    private static void ShadeAlternatingRow(
        Row row,
        int rowIndex)
    {
        if (rowIndex % 2 == 0)
        {
            return;
        }

        for (var index = 0;
             index < row.Cells.Count;
             index++)
        {
            row.Cells[index].Shading.Color =
                SurfaceColor;
        }
    }

    private static void ShadeAlternatingCell(
        Cell cell,
        int rowIndex)
    {
        if (rowIndex % 2 == 1)
        {
            cell.Shading.Color =
                SurfaceColor;
        }
    }

    private static void AddSpacer(
        Section section,
        double points)
    {
        section
            .AddParagraph()
            .Format
            .SpaceAfter =
                Unit.FromPoint(
                    points);
    }

    private static string BuildCustomerContactText(
        Customer customer)
    {
        return JoinAvailable(
            customer.Email,
            customer.Phone);
    }

    private static string BuildCustomerAddressText(
        Customer customer)
    {
        var cityLine =
            JoinAvailable(
                customer.PostalCode,
                customer.City,
                " ");

        return JoinAvailable(
            customer.Street,
            cityLine,
            ", ");
    }

    private static string JoinAvailable(
        string? first,
        string? second,
        string separator = " · ")
    {
        var values =
            new[]
            {
                first,
                second
            }
            .Where(
                value =>
                    !string.IsNullOrWhiteSpace(
                        value))
            .Select(
                value =>
                    value!.Trim())
            .ToList();

        return values.Count == 0
            ? "Nicht hinterlegt"
            : string.Join(
                separator,
                values);
    }

    private static string BuildActionResultText(
        CustomerCheckupActionSummary action)
    {
        var values =
            new List<string>();

        AddIfPresent(
            values,
            action.Summary);

        if (!string.IsNullOrWhiteSpace(
                action.TargetDescription))
        {
            values.Add(
                "Ziel: "
                + action.TargetDescription.Trim());
        }

        if (action.ExitCode.HasValue)
        {
            values.Add(
                "Exitcode: "
                + action.ExitCode.Value);
        }

        if (action.RestartRequired)
        {
            values.Add(
                "Neustart erforderlich");
        }

        return values.Count == 0
            ? "Keine zusätzliche Ergebnisbeschreibung verfügbar."
            : string.Join(
                " · ",
                values);
    }

    private static string BuildTaskNoteText(
        CustomerCheckupTaskComparison task)
    {
        var values =
            new List<string>();

        AddIfPresent(
            values,
            task.TechnicianNote);

        AddIfPresent(
            values,
            task.StatusReason);

        if (task.RestartRequired)
        {
            values.Add(
                "Neustartbedarf dokumentiert");
        }

        return values.Count == 0
            ? "Keine zusätzliche Notiz hinterlegt."
            : string.Join(
                " · ",
                values);
    }

    private static void AddIfPresent(
        ICollection<string> values,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(
                value))
        {
            values.Add(
                value.Trim());
        }
    }

    private static string GetAreaEvaluationText(
        CustomerCheckupAreaComparison area)
    {
        if (area.WasBeforeEvaluable
            && area.IsAfterEvaluable)
        {
            return
                "Vorher und nachher auswertbar";
        }

        if (!area.WasBeforeEvaluable
            && !area.IsAfterEvaluable)
        {
            return
                "In beiden Scans nicht belastbar auswertbar";
        }

        return area.IsAfterEvaluable
            ? "Erst im Nachher-Scan belastbar auswertbar"
            : "Im Nachher-Scan nicht erneut auswertbar";
    }

    private static string GetScoreChangeText(
        CustomerCheckupScoreComparison score)
    {
        if (!score.IsComparable
            || !score.Difference.HasValue)
        {
            return
                "Nicht direkt vergleichbar";
        }

        return score.Difference.Value switch
        {
            > 0 =>
                "+"
                + score.Difference.Value,

            < 0 =>
                score.Difference.Value.ToString(),

            _ =>
                "±0"
        };
    }

    private static Color GetScoreChangeColor(
        CustomerCheckupScoreChange change)
    {
        return change switch
        {
            CustomerCheckupScoreChange.Improved =>
                SuccessColor,

            CustomerCheckupScoreChange.Worsened =>
                DangerColor,

            CustomerCheckupScoreChange.Unchanged =>
                MutedTextColor,

            _ =>
                WarningColor
        };
    }

    private static string GetAreaStatusText(
        CustomerCheckupAreaComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupAreaComparisonStatus
                .UnchangedHealthy =>
                    "Technisch unverändert in Ordnung",

            CustomerCheckupAreaComparisonStatus
                .Improved =>
                    "Technischer Befund behoben",

            CustomerCheckupAreaComparisonStatus
                .ImprovedButStillNeedsAttention =>
                    "Technisch verbessert, Restbefund vorhanden",

            CustomerCheckupAreaComparisonStatus
                .UnchangedNeedsAttention =>
                    "Technischer Befund weiterhin vorhanden",

            CustomerCheckupAreaComparisonStatus
                .Worsened =>
                    "Technischer Zustand verschlechtert",

            CustomerCheckupAreaComparisonStatus
                .NewlyNeedsAttention =>
                    "Neuer technischer Befund",

            _ =>
                "Technisch nicht direkt vergleichbar"
        };
    }

    private static Color GetAreaStatusColor(
        CustomerCheckupAreaComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupAreaComparisonStatus
                .UnchangedHealthy =>
                    SuccessColor,

            CustomerCheckupAreaComparisonStatus
                .Improved =>
                    SuccessColor,

            CustomerCheckupAreaComparisonStatus
                .ImprovedButStillNeedsAttention =>
                    WarningColor,

            CustomerCheckupAreaComparisonStatus
                .UnchangedNeedsAttention =>
                    WarningColor,

            CustomerCheckupAreaComparisonStatus
                .Worsened =>
                    DangerColor,

            CustomerCheckupAreaComparisonStatus
                .NewlyNeedsAttention =>
                    DangerColor,

            _ =>
                MutedTextColor
        };
    }

    private static int GetAreaStatusOrder(
        CustomerCheckupAreaComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupAreaComparisonStatus
                .Worsened =>
                    0,

            CustomerCheckupAreaComparisonStatus
                .NewlyNeedsAttention =>
                    1,

            CustomerCheckupAreaComparisonStatus
                .UnchangedNeedsAttention =>
                    2,

            CustomerCheckupAreaComparisonStatus
                .ImprovedButStillNeedsAttention =>
                    3,

            CustomerCheckupAreaComparisonStatus
                .NotComparable =>
                    4,

            CustomerCheckupAreaComparisonStatus
                .Improved =>
                    5,

            _ =>
                6
        };
    }

    private static string GetFindingStatusText(
        CustomerCheckupFindingComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupFindingComparisonStatus
                .StillOpen =>
                    "Weiterhin vorhanden",

            CustomerCheckupFindingComparisonStatus
                .NewlyDetected =>
                    "Neu erkannt",

            CustomerCheckupFindingComparisonStatus
                .NotReevaluatable =>
                    "Nicht erneut auswertbar",

            _ =>
                "Technisch behoben"
        };
    }

    private static Color GetFindingStatusColor(
        CustomerCheckupFindingComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupFindingComparisonStatus
                .Resolved =>
                    SuccessColor,

            CustomerCheckupFindingComparisonStatus
                .StillOpen =>
                    WarningColor,

            CustomerCheckupFindingComparisonStatus
                .NewlyDetected =>
                    DangerColor,

            _ =>
                MutedTextColor
        };
    }

    private static int GetFindingStatusOrder(
        CustomerCheckupFindingComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupFindingComparisonStatus
                .NewlyDetected =>
                    0,

            CustomerCheckupFindingComparisonStatus
                .StillOpen =>
                    1,

            CustomerCheckupFindingComparisonStatus
                .NotReevaluatable =>
                    2,

            _ =>
                3
        };
    }

    private static string GetSeverityText(
        FindingSeverity? severity)
    {
        return severity switch
        {
            FindingSeverity.Critical =>
                "Kritisch",

            FindingSeverity.Warning =>
                "Warnung",

            FindingSeverity.Recommendation =>
                "Empfehlung",

            FindingSeverity.Information =>
                "Information",

            _ =>
                "Nicht eingestuft"
        };
    }

    private static Color GetSeverityColor(
        FindingSeverity? severity)
    {
        return severity switch
        {
            FindingSeverity.Critical =>
                DangerColor,

            FindingSeverity.Warning =>
                WarningColor,

            FindingSeverity.Recommendation =>
                AccentColor,

            _ =>
                MutedTextColor
        };
    }

    private static int GetSeverityOrder(
        FindingSeverity? severity)
    {
        return severity switch
        {
            FindingSeverity.Critical =>
                4,

            FindingSeverity.Warning =>
                3,

            FindingSeverity.Recommendation =>
                2,

            FindingSeverity.Information =>
                1,

            _ =>
                0
        };
    }

    private static string GetActionStatusText(
        CheckupTaskActionStatus status)
    {
        return status switch
        {
            CheckupTaskActionStatus.Successful =>
                "Erfolgreich",

            CheckupTaskActionStatus.Failed =>
                "Fehlgeschlagen",

            CheckupTaskActionStatus.Cancelled =>
                "Abgebrochen",

            _ =>
                "Unbekannt"
        };
    }

    private static Color GetActionStatusColor(
        CheckupTaskActionStatus status)
    {
        return status switch
        {
            CheckupTaskActionStatus.Successful =>
                SuccessColor,

            CheckupTaskActionStatus.Failed =>
                DangerColor,

            CheckupTaskActionStatus.Cancelled =>
                WarningColor,

            _ =>
                MutedTextColor
        };
    }

    private static string GetTaskStatusText(
        CustomerCheckupTaskComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupTaskComparisonStatus
                .Completed =>
                    "Abgeschlossen",

            CustomerCheckupTaskComparisonStatus
                .CompletedButStillDetected =>
                    "Abgeschlossen – Befund weiterhin vorhanden",

            CustomerCheckupTaskComparisonStatus
                .StillOpen =>
                    "Aufgabe weiterhin offen",

            CustomerCheckupTaskComparisonStatus
                .NoLongerDetected =>
                    "Abgeschlossen – Befund nicht mehr erkannt",

            CustomerCheckupTaskComparisonStatus
                .Skipped =>
                    "Übersprungen",

            CustomerCheckupTaskComparisonStatus
                .NotFeasible =>
                    "Nicht durchführbar",

            CustomerCheckupTaskComparisonStatus
                .NewlyDetected =>
                    "Neu erkannt – Aufgabe offen",

            _ =>
                "Nicht erneut auswertbar"
        };
    }

    private static Color GetTaskStatusColor(
        CustomerCheckupTaskComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupTaskComparisonStatus
                .Completed =>
                    SuccessColor,

            CustomerCheckupTaskComparisonStatus
                .NoLongerDetected =>
                    SuccessColor,

            CustomerCheckupTaskComparisonStatus
                .CompletedButStillDetected =>
                    WarningColor,

            CustomerCheckupTaskComparisonStatus
                .StillOpen =>
                    WarningColor,

            CustomerCheckupTaskComparisonStatus
                .NewlyDetected =>
                    DangerColor,

            CustomerCheckupTaskComparisonStatus
                .NotFeasible =>
                    DangerColor,

            _ =>
                MutedTextColor
        };
    }

    private static int GetTaskStatusOrder(
        CustomerCheckupTaskComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupTaskComparisonStatus
                .NewlyDetected =>
                    0,

            CustomerCheckupTaskComparisonStatus
                .StillOpen =>
                    1,

            CustomerCheckupTaskComparisonStatus
                .CompletedButStillDetected =>
                    2,

            CustomerCheckupTaskComparisonStatus
                .NotFeasible =>
                    3,

            CustomerCheckupTaskComparisonStatus
                .NotReevaluatable =>
                    4,

            CustomerCheckupTaskComparisonStatus
                .Skipped =>
                    5,

            CustomerCheckupTaskComparisonStatus
                .Completed =>
                    6,

            _ =>
                7
        };
    }

    private static string GetTaskPriorityText(
        CheckupTaskPriority priority)
    {
        return priority switch
        {
            CheckupTaskPriority.Required =>
                "Erforderlich",

            CheckupTaskPriority.Recommended =>
                "Empfohlen",

            _ =>
                "Optional"
        };
    }

    private static Color GetTaskPriorityColor(
        CheckupTaskPriority priority)
    {
        return priority switch
        {
            CheckupTaskPriority.Required =>
                DangerColor,

            CheckupTaskPriority.Recommended =>
                WarningColor,

            _ =>
                AccentColor
        };
    }

    private static int GetTaskPriorityOrder(
        CheckupTaskPriority priority)
    {
        return priority switch
        {
            CheckupTaskPriority.Required =>
                3,

            CheckupTaskPriority.Recommended =>
                2,

            _ =>
                1
        };
    }

    private static string FormatFindingCount(
        int count)
    {
        return count switch
        {
            0 =>
                "Keine",

            1 =>
                "1 Befund",

            _ =>
                count
                + " Befunde"
        };
    }

    private static string FormatScore(
        int? score)
    {
        return score.HasValue
            ? score.Value
              + " / 100"
            : "Nicht verfügbar";
    }

    private static string FormatDate(
        DateTime? date)
    {
        return date.HasValue
            ? date.Value.ToString(
                "dd.MM.yyyy")
            : "Nicht festgelegt";
    }

    private static string FormatDateTime(
        DateTime? dateTime)
    {
        return dateTime.HasValue
            ? dateTime.Value.ToString(
                "dd.MM.yyyy HH:mm")
              + " Uhr"
            : "Nicht verfügbar";
    }

    private static string FormatDateTimeOffset(
        DateTimeOffset? dateTime)
    {
        return dateTime.HasValue
            ? dateTime.Value
                .LocalDateTime
                .ToString(
                    "dd.MM.yyyy HH:mm")
              + " Uhr"
            : "Nicht dokumentiert";
    }

    private static string SafeText(
        string? value,
        string fallback = "Nicht verfügbar")
    {
        return string.IsNullOrWhiteSpace(
                value)
            ? fallback
            : value.Trim();
    }

    private static string NormalizePdfFilePath(
        string filePath)
    {
        var normalizedPath =
            Path.GetFullPath(
                filePath.Trim());

        return string.Equals(
                Path.GetExtension(
                    normalizedPath),
                ".pdf",
                StringComparison.OrdinalIgnoreCase)
            ? normalizedPath
            : Path.ChangeExtension(
                normalizedPath,
                ".pdf");
    }

    private static void EnsureTargetDirectoryExists(
        string filePath)
    {
        var directoryPath =
            Path.GetDirectoryName(
                filePath);

        if (!string.IsNullOrWhiteSpace(
                directoryPath))
        {
            Directory.CreateDirectory(
                directoryPath);
        }
    }
}