
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Pdf;
using System.IO;
using System.Text.RegularExpressions;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Reports;

public sealed class CustomerCheckupPdfReportService :
    ICustomerCheckupPdfReportService
{
    private const double ContentWidthCentimeters = 17.8;

    private const string ProgramUpdateActionCode =
        "action.program-updates.selected-upgrades";

    private static readonly Color AccentColor =
        Color.FromRgb(37, 99, 235);

    private static readonly Color HeadingColor =
        Color.FromRgb(15, 23, 42);

    private static readonly Color TextColor =
        Color.FromRgb(30, 41, 59);

    private static readonly Color MutedTextColor =
        Color.FromRgb(71, 85, 105);

    private static readonly Color BorderColor =
        Color.FromRgb(203, 213, 225);

    private static readonly Color SurfaceColor =
        Color.FromRgb(248, 250, 252);

    private static readonly Color SecondarySurfaceColor =
        Color.FromRgb(226, 232, 240);

    private static readonly Color SuccessColor =
        Color.FromRgb(21, 128, 61);

    private static readonly Color WarningColor =
        Color.FromRgb(180, 83, 9);

    private static readonly Color DangerColor =
        Color.FromRgb(185, 28, 28);

    public void Export(
        Customer customer,
        CustomerDevice device,
        CustomerCheckupVisit customerCheckupVisit,
        string filePath)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(customerCheckupVisit);

        ValidateReportVisit(customerCheckupVisit);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "Für den Kundencheckup-Bericht wurde kein gültiger "
                + "Zielpfad angegeben.",
                nameof(filePath));
        }

        var normalizedFilePath =
            NormalizePdfFilePath(filePath);

        EnsureTargetDirectoryExists(normalizedFilePath);

        var document = CreateDocument(
            customer,
            device,
            customerCheckupVisit);

        var renderer = new PdfDocumentRenderer
        {
            Document = document
        };

        renderer.PdfDocument.PageLayout =
            PdfPageLayout.SinglePage;

        renderer.PdfDocument.ViewerPreferences.FitWindow = true;

        renderer.RenderDocument();

        renderer.PdfDocument.Info.Title =
            document.Info.Title;

        renderer.PdfDocument.Info.Author =
            document.Info.Author;

        renderer.PdfDocument.Info.Subject =
            document.Info.Subject;

        renderer.Save(normalizedFilePath);

        var reportFile = new FileInfo(normalizedFilePath);

        if (!reportFile.Exists || reportFile.Length == 0)
        {
            throw new IOException(
                "Der Kundencheckup-Bericht wurde nicht "
                + "als gültige PDF-Datei gespeichert.");
        }
    }

    private static void ValidateReportVisit(
        CustomerCheckupVisit visit)
    {
        if ((!visit.IsInProgress && !visit.IsCompleted)
            || !visit.HasAfterCheckup
            || !visit.HasComparison)
        {
            throw new InvalidOperationException(
                "Der Bericht benötigt einen gespeicherten "
                + "Nachher-Scan und einen vollständigen Vergleich.");
        }

        var afterCheckup = visit.AfterCheckup;
        var comparison = visit.Comparison;

        if (afterCheckup?.ScanDate is null
            || comparison is null)
        {
            throw new InvalidOperationException(
                "Der gespeicherte Abschluss enthält "
                + "keinen vollständigen Nachher-Zustand.");
        }

        if (comparison.CustomerCheckupVisitId != visit.Id
            || comparison.BeforeScanDate
                != visit.BeforeCheckup.ScanDate
            || comparison.AfterScanDate
                != afterCheckup.ScanDate)
        {
            throw new InvalidOperationException(
                "Der gespeicherte Vergleich gehört "
                + "nicht vollständig zu diesem Kundencheckup.");
        }

        if (string.IsNullOrWhiteSpace(visit.TechnicianSummary)
            || string.IsNullOrWhiteSpace(visit.NextSteps)
            || !visit.NextCheckupDate.HasValue)
        {
            throw new InvalidOperationException(
                "Der Abschluss enthält noch keine "
                + "vollständigen Technikerangaben.");
        }
    }

    private static Document CreateDocument(
        Customer customer,
        CustomerDevice device,
        CustomerCheckupVisit visit)
    {
        var comparison = visit.Comparison!;

        var programUpdateReport =
            CustomerCheckupProgramUpdateReportService.Create(visit);

        var document = new Document();

        document.Info.Title =
            "Weber IT Kundencheckup-Abschlussbericht";

        document.Info.Author =
            "Weber IT-Dienstleistungen";

        document.Info.Subject =
            "Kundenbericht mit technischer Dokumentation";

        document.Info.Keywords =
            "Kundencheckup, Windows, Wartung, Weber IT";

        ConfigureStyles(document);

        // ===========================================
        // TEIL 1: KUNDENBERICHT
        // ===========================================

        var customerSection = document.AddSection();

        ConfigurePage(customerSection);

        AddSectionHeader(
            customerSection,
            "Kundenbericht");

        AddCustomerTitle(
            customerSection,
            customer,
            device,
            visit);

        AddCustomerSummary(
            customerSection,
            comparison);

        AddCustomerProgramUpdateDecisions(
            customerSection,
            programUpdateReport);

        AddTechnicianSummary(
            customerSection,
            visit);

        AddCustomerRecommendations(
            customerSection,
            comparison);

        AddPositiveFindings(
            customerSection,
            comparison);

        // ===========================================
        // TEIL 2: TECHNISCHER ANHANG
        // ===========================================

        var appendixSection = document.AddSection();

        ConfigurePage(appendixSection);

        AddSectionHeader(
            appendixSection,
            "Technischer Anhang");

        AddTechnicalAppendixIntroduction(appendixSection);

        AddTechnicalDeviceDetails(
            appendixSection,
            customer,
            device,
            visit);

        AddResultOverview(appendixSection, comparison);
        AddScoreComparison(appendixSection, comparison);
        AddAreaComparison(appendixSection, comparison);

        /*
         * Schritt 55.6.2:
         *
         * Die neue Einzelpaketübersicht ist direkt
         * in Abschnitt E integriert.
         *
         * Sie verwendet ausschließlich bereits
         * gespeicherte Aktionsprotokolle.
         */
        AddActions(appendixSection, comparison);

        AddFindings(appendixSection, comparison);
        AddTasks(appendixSection, comparison);

        AddProgramUpdateDecisions(
            appendixSection,
            programUpdateReport);

        AddComparisonNotes(appendixSection, comparison);
        AddReportNotice(appendixSection);

        return document;
    }

    private static void ConfigureStyles(Document document)
    {
        var normal = document.Styles[StyleNames.Normal]!;

        normal.Font.Name = "Arial";
        normal.Font.Size = Unit.FromPoint(9);
        normal.Font.Color = TextColor;

        normal.ParagraphFormat.SpaceAfter =
            Unit.FromPoint(3);

        var heading = document.Styles[StyleNames.Heading1]!;

        heading.Font.Name = "Arial";
        heading.Font.Size = Unit.FromPoint(15);
        heading.Font.Bold = true;
        heading.Font.Color = HeadingColor;

        heading.ParagraphFormat.SpaceBefore =
            Unit.FromPoint(12);

        heading.ParagraphFormat.SpaceAfter =
            Unit.FromPoint(7);

        heading.ParagraphFormat.KeepWithNext = true;
    }

    private static void ConfigurePage(Section section)
    {
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Portrait;

        section.PageSetup.TopMargin =
            Unit.FromCentimeter(1.7);

        section.PageSetup.BottomMargin =
            Unit.FromCentimeter(1.7);

        section.PageSetup.LeftMargin =
            Unit.FromCentimeter(1.6);

        section.PageSetup.RightMargin =
            Unit.FromCentimeter(1.6);
    }

    /// <summary>
    /// Die vollständige Fußzeile wird ausschließlich
    /// im nachgelagerten Branding-Service erzeugt.
    /// </summary>
    private static void AddSectionHeader(
        Section section,
        string reportPart)
    {
        var header = section.Headers.Primary.AddParagraph(
            "Weber IT-Dienstleistungen · " + reportPart);

        header.Format.Font.Name = "Arial";
        header.Format.Font.Size = Unit.FromPoint(8);
        header.Format.Font.Color = MutedTextColor;
        header.Format.Alignment = ParagraphAlignment.Right;
    }

    // ===========================================
    // KUNDENHAUPTBERICHT
    // ===========================================

    private static void AddCustomerTitle(
        Section section,
        Customer customer,
        CustomerDevice device,
        CustomerCheckupVisit visit)
    {
        var title = section.AddParagraph(
            "Ihr Weber IT Kundencheckup");

        title.Format.Font.Name = "Arial";
        title.Format.Font.Size = Unit.FromPoint(21);
        title.Format.Font.Bold = true;
        title.Format.Font.Color = HeadingColor;
        title.Format.SpaceAfter = Unit.FromPoint(4);

        var subtitle = section.AddParagraph(
            "Die Ergebnisse der Computerprüfung "
            + "verständlich zusammengefasst.");

        subtitle.Format.Font.Size = Unit.FromPoint(11);
        subtitle.Format.Font.Color = MutedTextColor;
        subtitle.Format.SpaceAfter = Unit.FromPoint(10);

        AddKeyValueTable(
            section,
            new[]
            {
                (
                    "Kunde",
                    SafeText(customer.DisplayName)
                ),
                (
                    "Geprüftes Gerät",
                    SafeText(device.DisplayName)
                ),
                (
                    "Datum der Kontrolle",
                    FormatDateTime(
                        visit.AfterCheckup!.ScanDate)
                )
            });
    }

    private static void AddCustomerSummary(
        Section section,
        CustomerCheckupComparison comparison)
    {
        var summary =
            CustomerCheckupCustomerSummary.Create(comparison);

        AddSectionHeading(
            section,
            "Ihr Ergebnis auf einen Blick");

        var headlineColor =
            HasCriticalOpenFindings(comparison)
                ? DangerColor
                : summary.HasOpenFindings
                    ? WarningColor
                    : SuccessColor;

        AddCallout(
            section,
            "Gesamtergebnis",
            summary.Headline,
            headlineColor);

        AddCustomerScoreGauges(
            section,
            comparison);

        AddCallout(
            section,
            "Was wurde erreicht?",
            summary.WorkResultText,
            summary.HasResolvedFindings
                ? SuccessColor
                : MutedTextColor);

        AddCallout(
            section,
            "Was empfehlen wir als Nächstes?",
            summary.RecommendationText,
            summary.HasOpenFindings
                ? WarningColor
                : SuccessColor);

        if (comparison.Areas.Any(area =>
                area.Status ==
                CustomerCheckupAreaComparisonStatus.NotComparable))
        {
            var notice = section.AddParagraph();

            notice.AddFormattedText(
                "Hinweis zur Prüfung: ",
                TextFormat.Bold);

            notice.AddText(summary.CheckupCoverageText);

            notice.Format.Font.Size = Unit.FromPoint(8);
            notice.Format.Font.Color = MutedTextColor;
            notice.Format.SpaceBefore = Unit.FromPoint(3);
            notice.Format.SpaceAfter = Unit.FromPoint(4);
        }
    }

    private static void AddCustomerScoreGauges(
        Section section,
        CustomerCheckupComparison comparison)
    {
        var table = CreateTable(
            section,
            8.9,
            8.9);

        table.Borders.Visible = false;

        var row = table.AddRow();

        AddCustomerGaugeCell(
            row.Cells[0],
            "SYSTEMZUSTAND",
            comparison.SystemScore);

        AddCustomerGaugeCell(
            row.Cells[1],
            "HARDWAREZUSTAND",
            comparison.HardwareScore);

        var explanation = section.AddParagraph(
            "Äußerer Bogen: aktueller Zustand. "
            + "Innerer Bogen: Eingangswert, sofern "
            + "zuverlässig vergleichbar.");

        explanation.Format.Font.Size =
            Unit.FromPoint(7.5);

        explanation.Format.Font.Color =
            MutedTextColor;

        explanation.Format.Alignment =
            ParagraphAlignment.Center;

        explanation.Format.SpaceAfter =
            Unit.FromPoint(5);
    }

    private static void AddCustomerGaugeCell(
        Cell cell,
        string title,
        CustomerCheckupScoreComparison score)
    {
        var titleParagraph =
            cell.AddParagraph(title);

        titleParagraph.Format.Alignment =
            ParagraphAlignment.Center;

        titleParagraph.Format.Font.Name = "Arial";
        titleParagraph.Format.Font.Size = Unit.FromPoint(10);
        titleParagraph.Format.Font.Bold = true;
        titleParagraph.Format.Font.Color = HeadingColor;
        titleParagraph.Format.SpaceAfter = Unit.FromPoint(0);

        var imageParagraph = cell.AddParagraph();

        imageParagraph.Format.Alignment =
            ParagraphAlignment.Center;

        imageParagraph.Format.SpaceBefore =
            Unit.FromPoint(0);

        imageParagraph.Format.SpaceAfter =
            Unit.FromPoint(0);

        var gaugeImage = imageParagraph.AddImage(
            CustomerCheckupGaugeImage.CreateBase64Png(
                score));

        gaugeImage.LockAspectRatio = true;

        gaugeImage.Width =
            Unit.FromCentimeter(7.4);
    }

    private static void AddCustomerProgramUpdateDecisions(
        Section section,
        CustomerCheckupProgramUpdateReportData report)
    {
        if (!report.HasDocumentedDecisions)
        {
            return;
        }

        AddCallout(
            section,
            "Bewusst zurückgestellte Programmupdates",
            report.CustomerSummary,
            WarningColor);
    }

    private static bool HasCriticalOpenFindings(
        CustomerCheckupComparison comparison)
    {
        return comparison.Findings.Any(finding =>
            IsOpenFinding(finding)
            && (finding.AfterSeverity
                ?? finding.BeforeSeverity)
                == FindingSeverity.Critical);
    }

    private static void AddTechnicianSummary(
        Section section,
        CustomerCheckupVisit visit)
    {
        AddSectionHeading(
            section,
            "Einschätzung und weiteres Vorgehen");

        AddCallout(
            section,
            "Einschätzung Ihres Technikers",
            visit.TechnicianSummary,
            AccentColor);

        AddCallout(
            section,
            "Die nächsten Schritte",
            visit.NextSteps,
            WarningColor);

        var paragraph = section.AddParagraph(
            "Empfohlener nächster Checkup: "
            + FormatDate(visit.NextCheckupDate));

        paragraph.Format.Font.Bold = true;
        paragraph.Format.Font.Color = HeadingColor;
        paragraph.Format.SpaceAfter = Unit.FromPoint(6);
    }

    private static void AddCustomerRecommendations(
        Section section,
        CustomerCheckupComparison comparison)
    {
        AddSectionHeading(
            section,
            "Unsere Empfehlungen für Sie");

        var findings = comparison.Findings
            .Where(IsOpenFinding)
            .OrderByDescending(finding =>
                GetSeverityOrder(
                    finding.AfterSeverity
                    ?? finding.BeforeSeverity))
            .ThenBy(finding =>
                GetFindingStatusOrder(finding.Status))
            .ThenBy(
                finding => finding.Title,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (findings.Count == 0)
        {
            AddCallout(
                section,
                "Keine offenen Befundempfehlungen",
                "Die Abschlusskontrolle hat keine "
                + "offenen technischen Befunde ergeben.",
                SuccessColor);

            return;
        }

        const int maxCustomerRecommendations = 5;

        var visibleFindings = findings
            .Take(maxCustomerRecommendations)
            .ToList();

        var table = CreateTable(
            section,
            2.8,
            11.7,
            3.3);

        AddHeaderRow(
            table,
            "Wichtigkeit",
            "Was wir empfehlen",
            "Status");

        var rowIndex = 0;

        foreach (var finding in visibleFindings)
        {
            var severity =
                finding.AfterSeverity
                ?? finding.BeforeSeverity;

            var row = AddDataRow(
                table,
                CustomerCheckupReportWording
                    .GetFindingPriorityText(severity),
                SafeText(finding.Title),
                CustomerCheckupReportWording
                    .GetFindingStatusText(finding.Status));

            row.Cells[0].Format.Font.Color =
                GetSeverityColor(severity);

            row.Cells[1].Format.Font.Bold = true;

            row.Cells[2].Format.Font.Color =
                GetFindingStatusColor(finding.Status);

            ShadeAlternatingRow(
                row,
                rowIndex++);
        }

        AddSpacer(section, 4);

        var remainingCount =
            findings.Count - visibleFindings.Count;

        var note = remainingCount switch
        {
            1 =>
                "Ein weiterer Hinweis und alle Einzelheiten "
                + "finden Sie im technischen Anhang.",

            > 1 =>
                $"{remainingCount} weitere Hinweise und "
                + "alle Einzelheiten finden Sie "
                + "im technischen Anhang.",

            _ =>
                "Die ausführlichen Erläuterungen "
                + "zu diesen Hinweisen finden Sie "
                + "im technischen Anhang."
        };

        var paragraph = section.AddParagraph(note);

        paragraph.Format.Font.Size =
            Unit.FromPoint(8.5);

        paragraph.Format.Font.Color =
            MutedTextColor;

        paragraph.Format.SpaceAfter =
            Unit.FromPoint(6);
    }

    // ===========================================
    // BESTÄTIGTE SCHUTZFUNKTIONEN
    // ===========================================

    private static void AddPositiveFindings(
        Section section,
        CustomerCheckupComparison comparison)
    {
        var positiveFindings = comparison.Findings
            .Where(finding =>
                finding.Status ==
                    CustomerCheckupFindingComparisonStatus.Informational
                && finding.AfterSeverity ==
                    FindingSeverity.Information
                && finding.AfterOccurrenceCount > 0
                && GetPositiveSecurityFindingOrder(
                    finding.Code) < int.MaxValue)
            .OrderBy(finding =>
                GetPositiveSecurityFindingOrder(
                    finding.Code))
            .ThenBy(
                finding => finding.Title,
                StringComparer.CurrentCultureIgnoreCase)
            .Take(3)
            .ToList();

        if (positiveFindings.Count == 0)
        {
            return;
        }

        var text = string.Join(
            " · ",
            positiveFindings.Select(finding =>
                SafeText(finding.Title)));

        var paragraph = section.AddParagraph();

        paragraph.AddFormattedText(
            "Bestätigte Schutzfunktionen: ",
            TextFormat.Bold);

        paragraph.AddText(text);

        paragraph.Format.Font.Size =
            Unit.FromPoint(8.5);

        paragraph.Format.Font.Color =
            TextColor;

        paragraph.Format.SpaceBefore =
            Unit.FromPoint(5);

        paragraph.Format.SpaceAfter =
            Unit.FromPoint(5);
    }

    private static int GetPositiveSecurityFindingOrder(
        string? code)
    {
        return code switch
        {
            "system.security.antivirus-enabled" => 0,
            "system.security.active-firewall-enabled" => 1,
            "system.security.secure-boot-enabled" => 2,
            "hardware.tpm.version-2-active" => 3,
            "system.security.uac-enabled" => 4,
            _ => int.MaxValue
        };
    }

    private static bool IsOpenFinding(
        CustomerCheckupFindingComparison finding)
    {
        return finding.Status is
            CustomerCheckupFindingComparisonStatus.StillOpen
            or CustomerCheckupFindingComparisonStatus.NewlyDetected
            or CustomerCheckupFindingComparisonStatus.NotReevaluatable;
    }

    // ===========================================
    // TECHNISCHER ANHANG
    // ===========================================

    private static void AddTechnicalAppendixIntroduction(
        Section section)
    {
        var heading = section.AddParagraph(
            "Technischer Anhang");

        heading.Format.Font.Name = "Arial";
        heading.Format.Font.Size = Unit.FromPoint(19);
        heading.Format.Font.Bold = true;
        heading.Format.Font.Color = HeadingColor;
        heading.Format.SpaceAfter = Unit.FromPoint(5);

        var introduction = section.AddParagraph(
            "Dieser Teil enthält die ausführlichen "
            + "Prüfergebnisse, den Vergleich vor und nach "
            + "der Bearbeitung sowie die dokumentierten "
            + "technischen Maßnahmen.");

        introduction.Format.Font.Color = MutedTextColor;
        introduction.Format.SpaceAfter = Unit.FromPoint(10);
    }

    private static void AddTechnicalDeviceDetails(
        Section section,
        Customer customer,
        CustomerDevice device,
        CustomerCheckupVisit visit)
    {
        AddSectionHeading(
            section,
            "A. Gerät und Prüfdaten");

        var after = visit.AfterCheckup!;

        var deviceInformation =
            after.DeviceInformation;

        var operatingSystem =
            after.OperatingSystemInformation;

        AddKeyValueTable(
            section,
            new[]
            {
                ("Kunde", SafeText(customer.DisplayName)),
                ("Kundennummer", SafeText(customer.CustomerNumber)),
                ("Kontakt", BuildCustomerContactText(customer)),
                ("Anschrift", BuildCustomerAddressText(customer)),
                ("Gespeichertes Gerät", SafeText(device.DisplayName)),
                ("Computername", SafeText(deviceInformation.Name)),
                (
                    "Hersteller / Modell",
                    SafeText(deviceInformation.Manufacturer)
                    + " / "
                    + SafeText(deviceInformation.Model)
                ),
                (
                    "Seriennummer",
                    SafeText(deviceInformation.SerialNumber)
                ),
                (
                    "Betriebssystem",
                    SafeText(operatingSystem.Name)
                    + " · Version "
                    + SafeText(operatingSystem.Version)
                    + " · Build "
                    + SafeText(operatingSystem.BuildNumber)
                ),
                (
                    "Eingangsscan",
                    FormatDateTime(
                        visit.BeforeCheckup.ScanDate)
                ),
                (
                    "Abschlusskontrolle",
                    FormatDateTime(after.ScanDate)
                ),
                (
                    "Bericht erstellt",
                    FormatDateTime(DateTime.Now)
                )
            });
    }

    private static void AddResultOverview(
        Section section,
        CustomerCheckupComparison comparison)
    {
        AddSectionHeading(
            section,
            "B. Technische Ergebnisübersicht");

        var table = CreateTable(
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

        var row = AddDataRow(
            table,
            comparison.ResolvedFindingCount.ToString(),
            comparison.StillOpenFindingCount.ToString(),
            comparison.NewlyDetectedFindingCount.ToString(),
            comparison.NotReevaluatableFindingCount.ToString());

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

        AddSpacer(section, 5);

        AddCallout(
            section,
            "Einordnung der Ergebnisübersicht",
            "Die Werte beschreiben ausschließlich "
            + "die technische Befundlage nach der "
            + "Abschlusskontrolle. Neutrale Informationen "
            + "werden nicht als offene oder behobene "
            + "Probleme gezählt. Der Aufgabenstatus "
            + "wird separat dokumentiert. Erfolgreich "
            + "ausgeführte Wartungsmaßnahmen werden "
            + "nicht automatisch als behobene Befunde "
            + "gezählt.",
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
                ? ". Mindestens eine Aktion meldet Neustartbedarf."
                : ". Kein ausdrücklicher Neustartbedarf "
                  + "dokumentiert."),
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
            "C. Vergleich der Zustandsbewertungen");

        var table = CreateTable(
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

        AddSpacer(section, 5);

        AddCallout(
            section,
            "So lesen Sie die Punktbewertungen",
            "Eine Veränderung der Punktzahl ist nur "
            + "dann als Verbesserung oder Verschlechterung "
            + "zu verstehen, wenn beide Scans ausreichend "
            + "vergleichbar sind. Andernfalls sind die "
            + "einzelnen Punktwerte lediglich "
            + "Momentaufnahmen.",
            MutedTextColor);
    }

    private static void AddScoreRow(
        Table table,
        string title,
        CustomerCheckupScoreComparison score)
    {
        var row = AddDataRow(
            table,
            title,
            FormatScore(score.BeforeScore),
            FormatScore(score.AfterScore),
            GetScoreChangeText(score),
            SafeText(
                score.ComparisonReason,
                score.IsComparable
                    ? "Direkt vergleichbar"
                    : "Nicht direkt vergleichbar"));

        row.Cells[0].Format.Font.Bold = true;

        row.Cells[3].Format.Font.Color =
            GetScoreChangeColor(score.Change);
    }

    // ===========================================
    // BEREICHSVERGLEICH
    // ===========================================

    private static void AddAreaComparison(
        Section section,
        CustomerCheckupComparison comparison)
    {
        AddSectionHeading(
            section,
            "D. Vergleich nach technischen Bereichen");

        var areas = comparison.Areas
            .OrderBy(area =>
                GetAreaStatusOrder(area.Status))
            .ThenBy(
                area => area.Title,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (areas.Count == 0)
        {
            AddCallout(
                section,
                "Keine Bereichsauswertung verfügbar",
                "Der gespeicherte Vergleich enthält "
                + "keine technischen Bereichsergebnisse.",
                WarningColor);

            return;
        }

        var table = CreateTable(
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

        var rowIndex = 0;

        foreach (var area in areas)
        {
            var row = AddDataRow(
                table,
                SafeText(area.Title),
                FormatFindingCount(
                    area.BeforeActionableFindingCount),
                FormatFindingCount(
                    area.AfterActionableFindingCount),
                GetAreaStatusText(area.Status),
                GetAreaEvaluationText(area));

            row.Cells[0].Format.Font.Bold = true;

            row.Cells[3].Format.Font.Color =
                GetAreaStatusColor(area.Status);

            ShadeAlternatingRow(
                row,
                rowIndex++);
        }

        AddSpacer(section, 5);

        if (areas.Any(area =>
                area.Status ==
                CustomerCheckupAreaComparisonStatus.MixedChanges))
        {
            AddCallout(
                section,
                "Gemischte Veränderungen",
                CustomerCheckupReportWording
                    .GetAreaStatusExplanation(
                        CustomerCheckupAreaComparisonStatus.MixedChanges),
                WarningColor);
        }
    }

    // ===========================================
    // SCHRITT 55.6.2
    // PROGRAMMUPDATE-EINZELERGEBNISSE
    // ===========================================

    private static void AddProgramUpdateExecutionOverview(
        Section section,
        CustomerCheckupProgramUpdateExecutionOverview overview)
    {
        if (overview.ActionCount == 0)
        {
            return;
        }

        var heading = section.AddParagraph(
            "Programmupdates: Ergebnisse je Paket");

        heading.Format.Font.Name = "Arial";
        heading.Format.Font.Size = Unit.FromPoint(11);
        heading.Format.Font.Bold = true;
        heading.Format.Font.Color = HeadingColor;

        heading.Format.SpaceBefore =
            Unit.FromPoint(5);

        heading.Format.SpaceAfter =
            Unit.FromPoint(5);

        /*
         * Wichtig:
         *
         * Gezählt werden verschiedene Pakete.
         *
         * Mehrfach wiederholte Updateversuche
         * werden nicht als mehrere erfolgreich
         * aktualisierte Programme dargestellt.
         */
        if (!overview.HasEntries)
        {
            AddTechnicalActionDetail(
                section,
                "Auswertung",
                "Für die dokumentierten WinGet-Aktionen "
                + "liegen keine sicher zuordenbaren "
                + "Paketinformationen vor.");

            return;
        }

        var summaryParts = new List<string>
        {
            overview.TechnicallySuccessfulPackageCount
            + " Pakete mit mindestens einem "
            + "technisch erfolgreichen WinGet-Aufruf",

            overview.FailedPackageCount
            + " Pakete ohne erfolgreichen Aufruf, "
            + "aber mit dokumentiertem Fehler"
        };

        var uncertainCount =
            overview.NotStartedPackageCount
            + overview.NotExecutedOrUnrecordedPackageCount
            + overview.NotVerifiablePackageCount;

        if (uncertainCount > 0)
        {
            summaryParts.Add(
                uncertainCount
                + " Pakete ohne belegten "
                + "technischen Erfolg");
        }

        var summaryParagraph = section.AddParagraph(
            string.Join(" · ", summaryParts));

        summaryParagraph.Format.Font.Color = TextColor;

        summaryParagraph.Format.SpaceAfter =
            Unit.FromPoint(6);

        var table = CreateTable(
            section,
            5.7,
            4.0,
            3.1,
            5.0);

        AddHeaderRow(
            table,
            "Programm / Paket-ID",
            "Einzelergebnis",
            "Aufrufe",
            "Technischer Hinweis");

        var rowIndex = 0;

        foreach (var entry in overview.Entries)
        {
            var row = AddDataRow(
                table,
                SafeText(entry.PackageName)
                + Environment.NewLine
                + entry.PackageId,
                GetProgramUpdateExecutionStatusText(
                    entry.Status),
                GetProgramUpdateCommandCountText(
                    entry),
                GetProgramUpdateExecutionNote(
                    entry));

            row.Cells[0].Format.Font.Bold = true;

            row.Cells[1].Format.Font.Color =
                GetProgramUpdateExecutionStatusColor(
                    entry.Status);

            ShadeAlternatingRow(
                row,
                rowIndex++);
        }

        AddSpacer(section, 5);

        var explanation = section.AddParagraph(
            "Ein technisch erfolgreicher Aufruf "
            + "(WinGet-Exitcode 0) ist kein eigenständiger "
            + "Nachweis einer installierten Zielversion. "
            + "Die beim Abschlussscan weiterhin erkannten "
            + "Updatebedarfe stehen in Abschnitt F. "
            + "Die folgenden Aktionsprotokolle dokumentieren "
            + "die einzelnen Wartungsdurchläufe; "
            + "deren Gesamtstatus kann trotz erfolgreicher "
            + "Einzelbefehle fehlgeschlagen sein.");

        explanation.Format.Font.Size =
            Unit.FromPoint(8);

        explanation.Format.Font.Color =
            MutedTextColor;

        explanation.Format.SpaceAfter =
            Unit.FromPoint(9);
    }

    private static string GetProgramUpdateExecutionStatusText(
        CustomerCheckupProgramUpdateExecutionStatus status)
    {
        return status switch
        {
            CustomerCheckupProgramUpdateExecutionStatus
                .TechnicallySuccessful =>
                    "Technisch erfolgreich",

            CustomerCheckupProgramUpdateExecutionStatus
                .Failed =>
                    "Fehlgeschlagen",

            CustomerCheckupProgramUpdateExecutionStatus
                .NotStarted =>
                    "Nicht gestartet",

            CustomerCheckupProgramUpdateExecutionStatus
                .NotExecutedOrUnrecorded =>
                    "Nicht ausgeführt / nicht belegt",

            _ =>
                "Nicht sicher auswertbar"
        };
    }

    private static Color GetProgramUpdateExecutionStatusColor(
        CustomerCheckupProgramUpdateExecutionStatus status)
    {
        return status switch
        {
            CustomerCheckupProgramUpdateExecutionStatus
                .TechnicallySuccessful =>
                    SuccessColor,

            CustomerCheckupProgramUpdateExecutionStatus
                .Failed =>
                    DangerColor,

            CustomerCheckupProgramUpdateExecutionStatus
                .NotStarted =>
                    WarningColor,

            _ =>
                MutedTextColor
        };
    }

    private static string GetProgramUpdateCommandCountText(
        CustomerCheckupProgramUpdateExecutionEntry entry)
    {
        if (entry.RecordedCommandCount == 0)
        {
            return "Keine belegten Einzelbefehle";
        }

        var parts = new List<string>();

        if (entry.SuccessfulCommandCount > 0)
        {
            parts.Add(
                entry.SuccessfulCommandCount
                + " erfolgreich");
        }

        if (entry.FailedCommandCount > 0)
        {
            parts.Add(
                entry.FailedCommandCount
                + " fehlgeschlagen");
        }

        var remainingCount =
            entry.RecordedCommandCount
            - entry.SuccessfulCommandCount
            - entry.FailedCommandCount;

        if (remainingCount > 0)
        {
            parts.Add(
                remainingCount
                + " ohne eindeutiges Ergebnis");
        }

        return string.Join(
            " · ",
            parts);
    }

    private static string GetProgramUpdateExecutionNote(
        CustomerCheckupProgramUpdateExecutionEntry entry)
    {
        switch (entry.Status)
        {
            case CustomerCheckupProgramUpdateExecutionStatus
                .TechnicallySuccessful:

                return entry.FailedCommandCount > 0
                    ? "Mindestens ein Aufruf endete mit "
                      + "Exitcode 0. Weitere Versuche "
                      + "waren fehlerhaft."
                    : "Mindestens ein Aufruf endete "
                      + "mit Exitcode 0.";

            case CustomerCheckupProgramUpdateExecutionStatus
                .Failed:

                return entry.LastFailureExitCode.HasValue
                    ? "Letzter protokollierter Fehlercode: "
                      + entry.LastFailureExitCode.Value
                    : "WinGet meldete einen Fehler.";

            case CustomerCheckupProgramUpdateExecutionStatus
                .NotStarted:

                return "Ein Einzelbefehl wurde erfasst, "
                       + "aber nicht technisch gestartet.";

            case CustomerCheckupProgramUpdateExecutionStatus
                .NotExecutedOrUnrecorded:

                return "Das Paket wurde ausgewählt. "
                       + "Eine Einzelausführung ist "
                       + "nicht protokolliert.";

            default:

                return "Das gespeicherte Protokoll reicht "
                       + "für eine sichere Aussage nicht aus.";
        }
    }

    private static bool IsProgramUpdateAction(
        CustomerCheckupActionSummary action)
    {
        return string.Equals(
            action.ActionCode,
            ProgramUpdateActionCode,
            StringComparison.Ordinal);
    }

    // ===========================================
    // DOKUMENTIERTE WARTUNGSAKTIONEN
    // ===========================================

    private static void AddActions(
        Section section,
        CustomerCheckupComparison comparison)
    {
        AddSectionHeading(
            section,
            "E. Dokumentierte technische Aktionen");

        var actions = comparison.Actions
            .OrderBy(action =>
                action.StartedAt ?? DateTimeOffset.MaxValue)
            .ThenBy(
                action => action.ActionTitle,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (actions.Count == 0)
        {
            AddCallout(
                section,
                "Keine technischen Aktionen dokumentiert",
                "In diesem Kundencheckup sind keine "
                + "ausgeführten technischen Aktionen "
                + "gespeichert. Manuell bearbeitete "
                + "Aufgaben können dennoch in Abschnitt G "
                + "dokumentiert sein.",
                MutedTextColor);

            return;
        }

        var introduction = section.AddParagraph(
            "Zunächst sehen Sie, welche Wartungsarbeiten "
            + "ausgeführt wurden. Anschließend folgen "
            + "das protokollierte Ergebnis und der "
            + "technische Umfang jeder Aktion. Ein "
            + "erfolgreicher Ablauf bedeutet nicht "
            + "automatisch, dass ein zuvor gemessener "
            + "Befund behoben wurde.");

        introduction.Format.Font.Color =
            MutedTextColor;

        introduction.Format.SpaceAfter =
            Unit.FromPoint(7);

        /*
         * Die neue Übersicht wertet vorhandene
         * Einzelprotokolle aus.
         *
         * Sie verändert weder die Aktionsdaten
         * noch die gespeicherten Statuswerte.
         */
        var programUpdateOverview =
            CustomerCheckupProgramUpdateExecutionReport.Create(
                actions);

        AddProgramUpdateExecutionOverview(
            section,
            programUpdateOverview);

        foreach (var action in actions)
        {
            var isProgramUpdate =
                IsProgramUpdateAction(action);

            /*
             * Eine fehlgeschlagene Gesamtaktion
             * kann technisch erfolgreiche
             * Einzelbefehle enthalten.
             *
             * Der gespeicherte Status wird nicht
             * umgeschrieben.
             *
             * Für die kundenlesbare Ausgabe
             * wird eine solche Aktion lediglich
             * präziser als "Nicht vollständig"
             * bezeichnet.
             */
            var actionStatusText =
                GetActionStatusText(action.Status);

            CustomerCheckupProgramUpdateExecutionOverview?
                individualOverview = null;

            if (isProgramUpdate)
            {
                individualOverview =
                    CustomerCheckupProgramUpdateExecutionReport.Create(
                        new[] { action });

                if (action.Status == CheckupTaskActionStatus.Failed
                    && individualOverview
                        .TechnicallySuccessfulPackageCount > 0)
                {
                    actionStatusText =
                        "Nicht vollständig";
                }
            }

            var table = CreateTable(
                section,
                9.0,
                3.5,
                5.3);

            AddHeaderRow(
                table,
                "Wartungsaktion",
                "Ausführung",
                "Zeitpunkt");

            var row = AddDataRow(
                table,
                SafeText(
                    action.ActionTitle,
                    action.TaskTitle),
                actionStatusText,
                FormatDateTimeOffset(
                    action.FinishedAt
                    ?? action.StartedAt));

            row.Cells[0].Format.Font.Bold = true;

            row.Cells[1].Format.Font.Color =
                actionStatusText == "Nicht vollständig"
                    ? WarningColor
                    : GetActionStatusColor(
                        action.Status);

            AddTechnicalActionDetail(
                section,
                "Protokolliertes Ergebnis",
                PrepareCompletedActionReportText(
                    action.Summary,
                    "Keine zusätzliche Ergebnisbeschreibung verfügbar."));

            /*
             * Die ursprünglich mehrfach ausgegebenen
             * umfangreichen WinGet-Auswahllisten
             * werden durch die neue Paketübersicht
             * ersetzt.
             *
             * Die tatsächlich ausgewählten Pakete
             * bleiben dort nachvollziehbar.
             *
             * Andere Wartungsaktionen behalten ihre
             * bisherigen vollständigen Beschreibungen.
             */
            if (isProgramUpdate
                && programUpdateOverview.HasEntries)
            {
                AddTechnicalActionDetail(
                    section,
                    "Ziel und Umfang",
                    "Kontrollierte Ausführung ausgewählter "
                    + "Programmupdates. Die ausgewählten "
                    + "Pakete und deren protokollierte "
                    + "Einzelergebnisse finden Sie in "
                    + "der vorstehenden Paketübersicht.");

                if (individualOverview is not null
                    && individualOverview.HasEntries)
                {
                    AddTechnicalActionDetail(
                        section,
                        "Einzelergebnisse dieses Durchlaufs",
                        individualOverview
                            .TechnicallySuccessfulPackageCount
                        + " Pakete technisch erfolgreich · "
                        + individualOverview
                            .FailedPackageCount
                        + " fehlgeschlagen · "
                        + (
                            individualOverview.Entries.Count
                            - individualOverview
                                .TechnicallySuccessfulPackageCount
                            - individualOverview
                                .FailedPackageCount
                        )
                        + " ohne nachgewiesenen Erfolg");
                }
            }
            else if (!string.IsNullOrWhiteSpace(
                         action.TargetDescription))
            {
                AddTechnicalActionDetail(
                    section,
                    "Ziel und Umfang",
                    PrepareCompletedActionReportText(
                        action.TargetDescription));
            }

            var executionDetails = new List<string>();

            if (action.ExitCode.HasValue)
            {
                executionDetails.Add(
                    "Rückgabecode: "
                    + action.ExitCode.Value);
            }

            if (action.RestartRequired)
            {
                executionDetails.Add(
                    "Neustart erforderlich");
            }

            if (executionDetails.Count > 0)
            {
                AddTechnicalActionDetail(
                    section,
                    "Weitere technische Angaben",
                    string.Join(
                        " · ",
                        executionDetails));
            }

            AddSpacer(section, 9);
        }
    }

    private static void AddTechnicalActionDetail(
        Section section,
        string title,
        string content)
    {
        var paragraph = section.AddParagraph();

        paragraph.AddFormattedText(
            title + ": ",
            TextFormat.Bold);

        paragraph.AddText(
            SafeText(content));

        paragraph.Format.LeftIndent =
            Unit.FromCentimeter(0.25);

        paragraph.Format.RightIndent =
            Unit.FromCentimeter(0.25);

        paragraph.Format.Font.Size =
            Unit.FromPoint(8.5);

        paragraph.Format.Font.Color =
            TextColor;

        paragraph.Format.SpaceBefore =
            Unit.FromPoint(3);

        paragraph.Format.SpaceAfter =
            Unit.FromPoint(2);
    }

    private static string PrepareCompletedActionReportText(
        string? value,
        string fallback = "Nicht dokumentiert")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var text = value.Trim();

        text = Regex.Replace(
            text,
            @"\b(?:Die\s+)?Abschlusskontrolle\s+steht\s+(?:noch\s+)?aus\.?",
            "Zum Zeitpunkt der Aktion stand die "
            + "Abschlusskontrolle noch aus; "
            + "inzwischen wurde sie durchgeführt.",
            RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant);

        text = Regex.Replace(
            text,
            @"(?<![A-Za-z0-9])([A-Za-z])::(?=\\|/|\s|$|[.,;])",
            "$1:",
            RegexOptions.CultureInvariant);

        return text;
    }

    // ===========================================
    // BEFUNDE
    // ===========================================

    private static void AddFindings(
        Section section,
        CustomerCheckupComparison comparison)
    {
        AddSectionHeading(
            section,
            "F. Verbleibende und neue Befunde");

        var findings = comparison.Findings
            .Where(IsOpenFinding)
            .OrderBy(finding =>
                GetFindingStatusOrder(finding.Status))
            .ThenByDescending(finding =>
                GetSeverityOrder(
                    finding.AfterSeverity
                    ?? finding.BeforeSeverity))
            .ThenBy(
                finding => finding.Title,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (findings.Count == 0)
        {
            AddCallout(
                section,
                "Keine verbleibenden technischen Befunde",
                "Es wurden keine verbleibenden, "
                + "neuen oder nicht erneut auswertbaren "
                + "technischen Befunde festgestellt.",
                SuccessColor);

            return;
        }

        var table = CreateTable(
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
            "Technische Beschreibung");

        var rowIndex = 0;

        foreach (var finding in findings)
        {
            var severity =
                finding.AfterSeverity
                ?? finding.BeforeSeverity;

            var row = AddDataRow(
                table,
                SafeText(finding.Title),
                GetSeverityText(severity),
                GetFindingStatusText(finding.Status),
                SafeText(
                    finding.AfterDescription,
                    finding.BeforeDescription));

            row.Cells[0].Format.Font.Bold = true;

            row.Cells[1].Format.Font.Color =
                GetSeverityColor(severity);

            row.Cells[2].Format.Font.Color =
                GetFindingStatusColor(finding.Status);

            ShadeAlternatingRow(
                row,
                rowIndex++);
        }

        AddSpacer(section, 5);
    }

    // ===========================================
    // AUFGABEN
    // ===========================================

    private static void AddTasks(
        Section section,
        CustomerCheckupComparison comparison)
    {
        AddSectionHeading(
            section,
            "G. Aufgabenbearbeitung und Dokumentation");

        var tasks = comparison.Tasks
            .OrderBy(task =>
                GetTaskStatusOrder(task.Status))
            .ThenByDescending(task =>
                GetTaskPriorityOrder(task.Priority))
            .ThenBy(
                task => task.Title,
                StringComparer.CurrentCultureIgnoreCase)
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
            "Der Aufgabenstatus dokumentiert, wie "
            + "ein Arbeitspunkt bearbeitet wurde. "
            + "Ob der zugehörige technische Befund "
            + "weiterhin vorhanden ist, ergibt sich "
            + "aus der Abschlusskontrolle.",
            AccentColor);

        var table = CreateTable(
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

        var rowIndex = 0;

        foreach (var task in tasks)
        {
            var row = AddDataRow(
                table,
                SafeText(task.Title),
                GetTaskPriorityText(task.Priority),
                GetTaskStatusText(task.Status),
                BuildTaskNoteText(task));

            row.Cells[0].Format.Font.Bold = true;

            row.Cells[1].Format.Font.Color =
                GetTaskPriorityColor(task.Priority);

            row.Cells[2].Format.Font.Color =
                GetTaskStatusColor(task.Status);

            ShadeAlternatingRow(
                row,
                rowIndex++);
        }

        AddSpacer(section, 5);
    }

    private static void AddProgramUpdateDecisions(
        Section section,
        CustomerCheckupProgramUpdateReportData report)
    {
        if (!report.HasDocumentedDecisions)
        {
            return;
        }

        AddSectionHeading(
            section,
            "Dokumentierte Entscheidungen zu Programmupdates");

        var introduction = section.AddParagraph(
            "Diese Aktualisierungen wurden während des "
            + "Kundencheckups bewusst zurückgestellt. "
            + "Der Status beschreibt, was bei der "
            + "Abschlusskontrolle ermittelt werden konnte. "
            + "Eine Zurückstellung zählt nicht als "
            + "erfolgreich installierte Aktualisierung.");

        introduction.Format.Font.Color =
            MutedTextColor;

        introduction.Format.SpaceAfter =
            Unit.FromPoint(6);

        var table = CreateTable(
            section,
            4.3,
            3.1,
            4.2,
            6.2);

        AddHeaderRow(
            table,
            "Programm / Quelle",
            "Versionsstand",
            "Status bei Abschluss",
            "Begründung / Technikerhinweis");

        var rowIndex = 0;

        foreach (var decision in report.Decisions)
        {
            var notes = new List<string>
            {
                SafeText(decision.Reason)
            };

            if (!string.IsNullOrWhiteSpace(
                    decision.TechnicianNote))
            {
                notes.Add(
                    "Technikerhinweis: "
                    + decision.TechnicianNote.Trim());
            }

            notes.Add(
                decision.WasDetectedAtBeginning switch
                {
                    true =>
                        "Im Eingangsscan erkannt",

                    false =>
                        "Im Eingangsscan nicht zugeordnet",

                    _ =>
                        "Eingangsscan nicht sicher zuordenbar"
                });

            notes.Add(
                "Entscheidung vom "
                + FormatDateTime(decision.ApprovedAt));

            var row = AddDataRow(
                table,
                SafeText(decision.PackageName)
                + " · "
                + SafeText(
                    decision.Source,
                    "Quelle nicht dokumentiert"),
                SafeText(decision.VersionText),
                SafeText(decision.VerificationText),
                string.Join(" · ", notes));

            row.Cells[0].Format.Font.Bold = true;

            row.Cells[2].Format.Font.Color =
                decision.IsConfirmedStillPending
                    ? WarningColor
                    : MutedTextColor;

            ShadeAlternatingRow(
                row,
                rowIndex++);
        }

        AddSpacer(section, 5);

        AddCallout(
            section,
            "Fachliche Einordnung",
            report.TechnicalNote,
            MutedTextColor);
    }

    private static void AddComparisonNotes(
        Section section,
        CustomerCheckupComparison comparison)
    {
        var notes = comparison.ComparisonNotes
            .Where(note =>
                !string.IsNullOrWhiteSpace(note))
            .Select(note => note.Trim())
            .ToList();

        if (notes.Count == 0)
        {
            return;
        }

        AddSectionHeading(
            section,
            "H. Hinweise zur Vergleichbarkeit");

        foreach (var note in notes)
        {
            var paragraph = section.AddParagraph(
                "• " + note);

            paragraph.Format.LeftIndent =
                Unit.FromCentimeter(0.3);

            paragraph.Format.FirstLineIndent =
                Unit.FromCentimeter(-0.3);

            paragraph.Format.Font.Color =
                MutedTextColor;

            paragraph.Format.SpaceAfter =
                Unit.FromPoint(4);
        }

        AddSpacer(section, 4);
    }

    private static void AddReportNotice(
        Section section)
    {
        var paragraph = section.AddParagraph(
            "Hinweis: Die technische Dokumentation "
            + "beschreibt den zu den Scanzeitpunkten "
            + "aus Windows auslesbaren Zustand sowie "
            + "die gespeicherten Arbeitsschritte. "
            + "Sie ersetzt keine Datensicherung, "
            + "Herstellergarantie oder physische "
            + "Laborprüfung. Nicht auswertbare Bereiche "
            + "sind entsprechend gekennzeichnet.");

        paragraph.Format.Font.Size =
            Unit.FromPoint(8);

        paragraph.Format.Font.Color =
            MutedTextColor;

        paragraph.Format.SpaceBefore =
            Unit.FromPoint(7);
    }

    // ===========================================
    // GEMEINSAME LAYOUT-HILFSMETHODEN
    // ===========================================

    private static void AddSectionHeading(
        Section section,
        string text)
    {
        section.AddParagraph(text).Style =
            StyleNames.Heading1;
    }

    private static void AddKeyValueTable(
        Section section,
        IEnumerable<(string Label, string Value)> values)
    {
        var table = CreateTable(
            section,
            4.5,
            13.3);

        var rowIndex = 0;

        foreach (var value in values)
        {
            var row = AddDataRow(
                table,
                value.Label,
                SafeText(value.Value));

            row.Cells[0].Format.Font.Bold = true;

            row.Cells[0].Shading.Color =
                SecondarySurfaceColor;

            ShadeAlternatingCell(
                row.Cells[1],
                rowIndex++);
        }

        AddSpacer(section, 5);
    }

    private static void AddCallout(
        Section section,
        string title,
        string message,
        Color accentColor)
    {
        var table = CreateTable(
            section,
            ContentWidthCentimeters);

        var cell = table.AddRow().Cells[0];

        cell.Shading.Color = SurfaceColor;
        cell.Borders.Color = accentColor;
        cell.Borders.Width = Unit.FromPoint(0.8);

        var titleParagraph =
            cell.AddParagraph(
                SafeText(title));

        titleParagraph.Format.Font.Bold = true;
        titleParagraph.Format.Font.Color = accentColor;

        titleParagraph.Format.SpaceAfter =
            Unit.FromPoint(3);

        var messageParagraph =
            cell.AddParagraph(
                SafeText(message));

        messageParagraph.Format.Font.Color = TextColor;

        AddSpacer(section, 6);
    }

    private static Table CreateTable(
        Section section,
        params double[] columnWidthsCentimeters)
    {
        var table = section.AddTable();

        table.Borders.Color = BorderColor;
        table.Borders.Width = Unit.FromPoint(0.5);

        table.Format.Font.Name = "Arial";
        table.Format.Font.Size = Unit.FromPoint(8.5);

        foreach (var width in columnWidthsCentimeters)
        {
            table.AddColumn(
                Unit.FromCentimeter(width));
        }

        return table;
    }

    private static void AddHeaderRow(
        Table table,
        params string[] headings)
    {
        var row = table.AddRow();

        row.HeadingFormat = true;

        for (var index = 0;
             index < headings.Length;
             index++)
        {
            var cell = row.Cells[index];

            cell.Shading.Color = HeadingColor;
            cell.Format.Font.Bold = true;
            cell.Format.Font.Color = Colors.White;

            cell.AddParagraph(
                SafeText(headings[index]));
        }
    }

    private static Row AddDataRow(
        Table table,
        params string[] values)
    {
        var row = table.AddRow();

        for (var index = 0;
             index < values.Length;
             index++)
        {
            var paragraph = row.Cells[index]
                .AddParagraph(
                    SafeText(values[index]));

            paragraph.Format.SpaceAfter =
                Unit.FromPoint(0);
        }

        return row;
    }

    private static void SetCellHighlight(
        Cell cell,
        Color color)
    {
        cell.Format.Font.Bold = true;
        cell.Format.Font.Size = Unit.FromPoint(15);
        cell.Format.Font.Color = color;

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
            row.Cells[index].Shading.Color = SurfaceColor;
        }
    }

    private static void ShadeAlternatingCell(
        Cell cell,
        int rowIndex)
    {
        if (rowIndex % 2 == 1)
        {
            cell.Shading.Color = SurfaceColor;
        }
    }

    private static void AddSpacer(
        Section section,
        double points)
    {
        section.AddParagraph()
            .Format.SpaceAfter =
                Unit.FromPoint(points);
    }

    // ===========================================
    // TEXT- UND DATENAUFBEREITUNG
    // ===========================================

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
        var cityLine = JoinAvailable(
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
        var values = new[] { first, second }
            .Where(value =>
                !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList();

        return values.Count == 0
            ? "Nicht hinterlegt"
            : string.Join(separator, values);
    }

    private static string BuildTaskNoteText(
        CustomerCheckupTaskComparison task)
    {
        var values = new List<string>();

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
            : string.Join(" · ", values);
    }

    private static void AddIfPresent(
        ICollection<string> values,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add(value.Trim());
        }
    }

    private static string GetAreaEvaluationText(
        CustomerCheckupAreaComparison area)
    {
        if (area.WasBeforeEvaluable
            && area.IsAfterEvaluable)
        {
            return "Vorher und nachher auswertbar";
        }

        if (!area.WasBeforeEvaluable
            && !area.IsAfterEvaluable)
        {
            return "In beiden Scans nicht "
                   + "belastbar auswertbar";
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
            return "Nicht direkt vergleichbar";
        }

        return score.Difference.Value switch
        {
            > 0 =>
                "+" + score.Difference.Value,

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
            CustomerCheckupAreaComparisonStatus.UnchangedHealthy =>
                "Technisch unverändert in Ordnung",

            CustomerCheckupAreaComparisonStatus.Improved =>
                "Technischer Befund behoben",

            CustomerCheckupAreaComparisonStatus
                .ImprovedButStillNeedsAttention =>
                    "Technisch verbessert, Restbefund vorhanden",

            CustomerCheckupAreaComparisonStatus
                .UnchangedNeedsAttention =>
                    "Technischer Befund weiterhin vorhanden",

            CustomerCheckupAreaComparisonStatus.Worsened =>
                "Technischer Zustand verschlechtert",

            CustomerCheckupAreaComparisonStatus
                .NewlyNeedsAttention =>
                    "Neuer technischer Befund",

            CustomerCheckupAreaComparisonStatus.MixedChanges =>
                CustomerCheckupReportWording
                    .GetAreaStatusText(status),

            _ =>
                "Technisch nicht direkt vergleichbar"
        };
    }

    private static Color GetAreaStatusColor(
        CustomerCheckupAreaComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupAreaComparisonStatus.UnchangedHealthy =>
                SuccessColor,

            CustomerCheckupAreaComparisonStatus.Improved =>
                SuccessColor,

            CustomerCheckupAreaComparisonStatus
                .ImprovedButStillNeedsAttention =>
                    WarningColor,

            CustomerCheckupAreaComparisonStatus
                .UnchangedNeedsAttention =>
                    WarningColor,

            CustomerCheckupAreaComparisonStatus.Worsened =>
                DangerColor,

            CustomerCheckupAreaComparisonStatus
                .NewlyNeedsAttention =>
                    DangerColor,

            CustomerCheckupAreaComparisonStatus.MixedChanges =>
                WarningColor,

            _ =>
                MutedTextColor
        };
    }

    private static int GetAreaStatusOrder(
        CustomerCheckupAreaComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupAreaComparisonStatus.Worsened => 0,

            CustomerCheckupAreaComparisonStatus
                .NewlyNeedsAttention => 1,

            CustomerCheckupAreaComparisonStatus.MixedChanges => 2,

            CustomerCheckupAreaComparisonStatus
                .UnchangedNeedsAttention => 3,

            CustomerCheckupAreaComparisonStatus
                .ImprovedButStillNeedsAttention => 4,

            CustomerCheckupAreaComparisonStatus.NotComparable => 5,

            CustomerCheckupAreaComparisonStatus.Improved => 6,

            _ => 7
        };
    }

    private static string GetFindingStatusText(
        CustomerCheckupFindingComparisonStatus status)
    {
        return CustomerCheckupReportWording
            .GetFindingStatusText(status);
    }

    private static Color GetFindingStatusColor(
        CustomerCheckupFindingComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupFindingComparisonStatus.Resolved =>
                SuccessColor,

            CustomerCheckupFindingComparisonStatus.StillOpen =>
                WarningColor,

            CustomerCheckupFindingComparisonStatus.NewlyDetected =>
                DangerColor,

            CustomerCheckupFindingComparisonStatus.Informational =>
                MutedTextColor,

            _ =>
                MutedTextColor
        };
    }

    private static int GetFindingStatusOrder(
        CustomerCheckupFindingComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupFindingComparisonStatus.NewlyDetected => 0,
            CustomerCheckupFindingComparisonStatus.StillOpen => 1,
            CustomerCheckupFindingComparisonStatus.NotReevaluatable => 2,
            CustomerCheckupFindingComparisonStatus.Resolved => 3,
            CustomerCheckupFindingComparisonStatus.Informational => 4,
            _ => 5
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
            FindingSeverity.Critical => 4,
            FindingSeverity.Warning => 3,
            FindingSeverity.Recommendation => 2,
            FindingSeverity.Information => 1,
            _ => 0
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
            CustomerCheckupTaskComparisonStatus.Completed =>
                "Abgeschlossen",

            CustomerCheckupTaskComparisonStatus
                .CompletedButStillDetected =>
                    "Abgeschlossen",

            CustomerCheckupTaskComparisonStatus.StillOpen =>
                "Aufgabe weiterhin offen",

            CustomerCheckupTaskComparisonStatus.NoLongerDetected =>
                "Abgeschlossen – Befund nicht mehr erkannt",

            CustomerCheckupTaskComparisonStatus.Skipped =>
                "Übersprungen",

            CustomerCheckupTaskComparisonStatus.NotFeasible =>
                "Nicht durchführbar",

            CustomerCheckupTaskComparisonStatus.NewlyDetected =>
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
            CustomerCheckupTaskComparisonStatus.Completed =>
                SuccessColor,

            CustomerCheckupTaskComparisonStatus.NoLongerDetected =>
                SuccessColor,

            CustomerCheckupTaskComparisonStatus
                .CompletedButStillDetected =>
                    SuccessColor,

            CustomerCheckupTaskComparisonStatus.StillOpen =>
                WarningColor,

            CustomerCheckupTaskComparisonStatus.NewlyDetected =>
                DangerColor,

            CustomerCheckupTaskComparisonStatus.NotFeasible =>
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
            CustomerCheckupTaskComparisonStatus.NewlyDetected => 0,
            CustomerCheckupTaskComparisonStatus.StillOpen => 1,

            CustomerCheckupTaskComparisonStatus
                .CompletedButStillDetected => 2,

            CustomerCheckupTaskComparisonStatus.NotFeasible => 3,
            CustomerCheckupTaskComparisonStatus.NotReevaluatable => 4,
            CustomerCheckupTaskComparisonStatus.Skipped => 5,
            CustomerCheckupTaskComparisonStatus.Completed => 6,

            _ => 7
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

    private static int GetTaskStatusOrder(
        CustomerCheckupTaskComparisonStatus status,
        bool unused)
    {
        return GetTaskStatusOrder(status);
    }

    private static int GetTaskPriorityOrder(
        CheckupTaskPriority priority)
    {
        return priority switch
        {
            CheckupTaskPriority.Required => 3,
            CheckupTaskPriority.Recommended => 2,
            _ => 1
        };
    }

    private static string FormatFindingCount(int count)
    {
        return count switch
        {
            0 => "Keine",
            1 => "1 Befund",
            _ => count + " Befunde"
        };
    }

    private static string FormatScore(int? score)
    {
        return score.HasValue
            ? score.Value + " / 100"
            : "Nicht verfügbar";
    }

    private static string FormatDate(
        DateTime? date)
    {
        return date.HasValue
            ? date.Value.ToString("dd.MM.yyyy")
            : "Nicht festgelegt";
    }

    private static string FormatDateTime(
        DateTime? dateTime)
    {
        return dateTime.HasValue
            ? dateTime.Value.ToString("dd.MM.yyyy HH:mm")
              + " Uhr"
            : "Nicht verfügbar";
    }

    private static string FormatDateTimeOffset(
        DateTimeOffset? dateTime)
    {
        return dateTime.HasValue
            ? dateTime.Value.LocalDateTime
                .ToString("dd.MM.yyyy HH:mm")
              + " Uhr"
            : "Nicht dokumentiert";
    }

    private static string SafeText(
        string? value,
        string fallback = "Nicht verfügbar")
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static string NormalizePdfFilePath(
        string filePath)
    {
        var normalizedPath =
            Path.GetFullPath(filePath.Trim());

        return string.Equals(
                Path.GetExtension(normalizedPath),
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
            Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(
                directoryPath);
        }
    }
}
