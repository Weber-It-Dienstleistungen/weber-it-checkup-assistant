using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Pdf;
using System.IO;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Reports;

public sealed class DiagnosticPdfReportService :
    IDiagnosticPdfReportService
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
        CheckupSession checkupSession,
        string filePath)
    {
        ArgumentNullException.ThrowIfNull(
            checkupSession);

        if (!checkupSession.ScanDate.HasValue)
        {
            throw new InvalidOperationException(
                "Der Diagnosebericht kann nur für einen "
                + "abgeschlossenen Systemscan erstellt werden.");
        }

        if (string.IsNullOrWhiteSpace(
                filePath))
        {
            throw new ArgumentException(
                "Für den PDF-Export wurde kein gültiger "
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
                checkupSession);

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
    }

    private static Document CreateDocument(
        CheckupSession checkupSession)
    {
        var document =
            new Document();

        document.Info.Title =
            "Weber IT Windows-PC-Diagnosebericht";

        document.Info.Author =
            "Weber IT-Dienstleistungen";

        document.Info.Subject =
            "Kundenunspezifischer Windows-PC-Diagnosescan";

        document.Info.Keywords =
            "Windows, Diagnose, Checkup, Hardware, "
            + "Systemzustand, Weber IT";

        ConfigureStyles(
            document);

        var section =
            document.AddSection();

        ConfigurePage(
            section);

        AddHeaderAndFooter(
            section);

        AddReportTitle(
            section,
            checkupSession);

        AddDeviceOverview(
            section,
            checkupSession);

        AddAssessmentOverview(
            section,
            checkupSession);

        AddActionableFindings(
            section,
            checkupSession);

        AddTasks(
            section,
            checkupSession);

        AddHardwareInformation(
            section,
            checkupSession);

        AddStorageInformation(
            section,
            checkupSession);

        AddInformationalFindings(
            section,
            checkupSession);

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

        var headingOneStyle =
            document.Styles[
                StyleNames.Heading1]!;

        headingOneStyle.Font.Name =
            "Arial";

        headingOneStyle.Font.Size =
            Unit.FromPoint(
                15);

        headingOneStyle.Font.Bold =
            true;

        headingOneStyle.Font.Color =
            HeadingColor;

        headingOneStyle.ParagraphFormat.SpaceBefore =
            Unit.FromPoint(
                12);

        headingOneStyle.ParagraphFormat.SpaceAfter =
            Unit.FromPoint(
                7);

        headingOneStyle.ParagraphFormat.KeepWithNext =
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
        Section section)
    {
        var header =
            section.Headers.Primary
                .AddParagraph(
                    "Weber IT-Dienstleistungen · "
                    + "Windows-PC-Diagnose");

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
            "Kundenunspezifischer Diagnosebericht · Seite ");

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

    private static void AddReportTitle(
        Section section,
        CheckupSession checkupSession)
    {
        var title =
            section.AddParagraph(
                "Windows-PC-Diagnosebericht");

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
                "Technische Bestandsaufnahme und "
                + "Bewertung des aktuellen Systemzustands");

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
            "Kundenunspezifischer Diagnosescan",
            "Dieser Bericht dokumentiert einen eigenständigen "
            + "lesenden Diagnosescan. Er enthält keine "
            + "Kundendaten und keine Vorher-/Nachher-Bewertung.",
            AccentColor,
            SecondarySurfaceColor);

        AddKeyValueTable(
            section,
            new[]
            {
                (
                    "Gescanntes Gerät",
                    SafeText(
                        checkupSession
                            .DeviceInformation
                            .Name,
                        "Unbekanntes Gerät")),

                (
                    "Scanzeitpunkt",
                    FormatDateTime(
                        checkupSession.ScanDate)),

                (
                    "Bericht erstellt",
                    FormatDateTime(
                        DateTime.Now))
            });
    }

    private static void AddDeviceOverview(
        Section section,
        CheckupSession checkupSession)
    {
        AddSectionHeading(
            section,
            "1. Geräte- und Systemübersicht");

        var device =
            checkupSession.DeviceInformation;

        var operatingSystem =
            checkupSession.OperatingSystemInformation;

        AddKeyValueTable(
            section,
            new[]
            {
                (
                    "Computername",
                    SafeText(
                        device.Name)),

                (
                    "Gerätetyp",
                    SafeText(
                        device.DeviceType)),

                (
                    "Hersteller",
                    SafeText(
                        device.Manufacturer)),

                (
                    "Modell",
                    SafeText(
                        device.Model)),

                (
                    "Seriennummer",
                    SafeText(
                        device.SerialNumber)),

                (
                    "Betriebssystem",
                    SafeText(
                        operatingSystem.Name)),

                (
                    "Windows-Version",
                    SafeText(
                        operatingSystem.Version)),

                (
                    "Windows-Build",
                    SafeText(
                        operatingSystem.BuildNumber)),

                (
                    "Architektur",
                    SafeText(
                        operatingSystem.Architecture))
            });
    }

    private static void AddAssessmentOverview(
        Section section,
        CheckupSession checkupSession)
    {
        AddSectionHeading(
            section,
            "2. Zustandsbewertung");

        var assessment =
            checkupSession.Assessment;

        AddKeyValueTable(
            section,
            new[]
            {
                (
                    "Systemzustand",
                    assessment
                        .SystemCondition
                        .ScoreText
                    + " · "
                    + assessment
                        .SystemCondition
                        .RatingText),

                (
                    "Datengrundlage System",
                    assessment
                        .SystemCondition
                        .DataQualityText
                    + " · "
                    + assessment
                        .SystemCondition
                        .CoverageText),

                (
                    "Systembewertung",
                    assessment
                        .SystemCondition
                        .SummaryText),

                (
                    "Hardwarezustand",
                    assessment
                        .HardwareCondition
                        .ScoreText
                    + " · "
                    + assessment
                        .HardwareCondition
                        .RatingText),

                (
                    "Datengrundlage Hardware",
                    assessment
                        .HardwareCondition
                        .DataQualityText
                    + " · "
                    + assessment
                        .HardwareCondition
                        .CoverageText),

                (
                    "Hardwarebewertung",
                    assessment
                        .HardwareCondition
                        .SummaryText),

                (
                    "Planungshorizont",
                    assessment
                        .HardwarePlanningHorizonText),

                (
                    "Planungshinweis",
                    assessment
                        .HardwarePlanningSummaryText)
            });
    }

    private static void AddActionableFindings(
        Section section,
        CheckupSession checkupSession)
    {
        AddSectionHeading(
            section,
            "3. Handlungsrelevante Befunde");

        var findings =
            checkupSession
                .Assessment
                .ActionableFindings;

        if (findings.Count == 0)
        {
            AddCallout(
                section,
                "Kein unmittelbarer Handlungsbedarf erkannt",
                "Die Bewertung enthält derzeit keine Empfehlung, "
                + "Warnung oder kritische Auffälligkeit.",
                SuccessColor,
                SurfaceColor);

            return;
        }

        var table =
            CreateTable(
                section,
                2.8,
                5.0,
                10.0);

        AddHeaderRow(
            table,
            "Priorität",
            "Befund",
            "Technische Beschreibung");

        var rowIndex =
            0;

        foreach (var finding in findings)
        {
            var row =
                AddDataRow(
                    table,
                    GetSeverityText(
                        finding.Severity),
                    SafeText(
                        finding.Title),
                    SafeText(
                        finding.Description));

            SetCellFontColor(
                row.Cells[0],
                GetSeverityColor(
                    finding.Severity));

            if (rowIndex % 2 == 1)
            {
                ShadeRow(
                    row,
                    SurfaceColor);
            }

            rowIndex++;
        }

        AddSpacer(
            section,
            5);
    }

    private static void AddTasks(
        Section section,
        CheckupSession checkupSession)
    {
        AddSectionHeading(
            section,
            "4. Abgeleitete Prüf- und Wartungsaufgaben");

        var taskList =
            checkupSession.TaskList;

        if (!taskList.IsAvailable)
        {
            AddCallout(
                section,
                "Aufgabenliste nicht verfügbar",
                taskList.AvailabilityText,
                WarningColor,
                SurfaceColor);

            return;
        }

        if (!taskList.HasTasks)
        {
            AddCallout(
                section,
                "Keine Aufgaben abgeleitet",
                taskList.AvailabilityText,
                SuccessColor,
                SurfaceColor);

            return;
        }

        var tasks =
            (taskList.Tasks
             ?? new List<CheckupTask>())
                .OrderByDescending(
                    task =>
                        GetTaskPriorityOrder(
                            task.Priority))
                .ThenBy(
                    task =>
                        task.CategoryText,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(
                    task =>
                        task.Title,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        var summary =
            section.AddParagraph(
                $"{taskList.TotalTaskCount} Aufgaben insgesamt · "
                + $"{taskList.OpenTaskCount} offen · "
                + $"{taskList.RequiredOpenTaskCount} "
                + "offene Pflichtaufgaben");

        summary.Format.Font.Color =
            MutedTextColor;

        summary.Format.SpaceAfter =
            Unit.FromPoint(
                6);

        var table =
            CreateTable(
                section,
                2.3,
                3.4,
                4.7,
                7.4);

        AddHeaderRow(
            table,
            "Priorität",
            "Kategorie",
            "Aufgabe",
            "Beschreibung");

        var rowIndex =
            0;

        foreach (var task in tasks)
        {
            var row =
                AddDataRow(
                    table,
                    task.PriorityText,
                    task.CategoryText,
                    SafeText(
                        task.Title),
                    SafeText(
                        task.Description));

            SetCellFontColor(
                row.Cells[0],
                GetTaskPriorityColor(
                    task.Priority));

            if (rowIndex % 2 == 1)
            {
                ShadeRow(
                    row,
                    SurfaceColor);
            }

            rowIndex++;
        }

        AddSpacer(
            section,
            5);
    }

    private static void AddHardwareInformation(
        Section section,
        CheckupSession checkupSession)
    {
        AddSectionHeading(
            section,
            "5. Technische Hardwaredaten");

        var hardware =
            checkupSession.HardwareInformation;

        var graphicsCards =
            hardware.GraphicsCards is
            {
                Count: > 0
            }
                ? string.Join(
                    ", ",
                    hardware.GraphicsCards)
                : "Nicht verfügbar";

        AddKeyValueTable(
            section,
            new[]
            {
                (
                    "Prozessor",
                    SafeText(
                        hardware.ProcessorName)),

                (
                    "Arbeitsspeicher",
                    SafeText(
                        hardware.InstalledMemory)),

                (
                    "Mainboard-Hersteller",
                    SafeText(
                        hardware.MainboardManufacturer)),

                (
                    "Mainboard-Modell",
                    SafeText(
                        hardware.MainboardProduct)),

                (
                    "BIOS-Hersteller",
                    SafeText(
                        hardware.BiosManufacturer)),

                (
                    "BIOS-Version",
                    SafeText(
                        hardware.BiosVersion)),

                (
                    "Grafikkarte",
                    graphicsCards),

                (
                    "TPM-Status",
                    SafeText(
                        hardware.TpmStatus)),

                (
                    "TPM-Version",
                    SafeText(
                        hardware.TpmVersion))
            });
    }

    private static void AddStorageInformation(
        Section section,
        CheckupSession checkupSession)
    {
        AddSectionHeading(
            section,
            "6. Datenträger und Volumes");

        var storage =
            checkupSession.StorageInformation;

        if (!storage.IsAnalysisSuccessful)
        {
            AddCallout(
                section,
                "Datenträgeranalyse eingeschränkt",
                SafeText(
                    storage.AnalysisMessage,
                    "Die Datenträgerinformationen konnten "
                    + "nicht vollständig ausgewertet werden."),
                WarningColor,
                SurfaceColor);
        }

        var physicalDrives =
            storage.PhysicalDrives
            ?? new List<PhysicalDriveInformation>();

        if (physicalDrives.Count > 0)
        {
            var driveHeading =
                section.AddParagraph(
                    "Physische Datenträger");

            driveHeading.Format.Font.Bold =
                true;

            driveHeading.Format.Font.Size =
                Unit.FromPoint(
                    10);

            driveHeading.Format.SpaceAfter =
                Unit.FromPoint(
                    5);

            var driveTable =
                CreateTable(
                    section,
                    5.6,
                    2.1,
                    2.2,
                    3.1,
                    4.8);

            AddHeaderRow(
                driveTable,
                "Modell",
                "Medium",
                "Bus",
                "Kapazität",
                "Gesundheit / Rolle");

            var rowIndex =
                0;

            foreach (var drive in physicalDrives)
            {
                var healthAndRole =
                    drive.HealthStatusText
                    + " · "
                    + drive.RoleText;

                var row =
                    AddDataRow(
                        driveTable,
                        SafeText(
                            drive.Model),
                        drive.MediaTypeText,
                        drive.BusTypeText,
                        SafeText(
                            drive.Capacity),
                        healthAndRole);

                if (rowIndex % 2 == 1)
                {
                    ShadeRow(
                        row,
                        SurfaceColor);
                }

                rowIndex++;
            }

            AddSpacer(
                section,
                7);
        }
        else
        {
            AddCallout(
                section,
                "Keine physischen Datenträgerdaten",
                "Der Scan enthält keine auswertbaren "
                + "Informationen zu physischen Datenträgern.",
                WarningColor,
                SurfaceColor);
        }

        var volumes =
            storage.Volumes
            ?? new List<VolumeInformation>();

        if (volumes.Count > 0)
        {
            var volumeHeading =
                section.AddParagraph(
                    "Volumes und Laufwerksbuchstaben");

            volumeHeading.Format.Font.Bold =
                true;

            volumeHeading.Format.Font.Size =
                Unit.FromPoint(
                    10);

            volumeHeading.Format.SpaceAfter =
                Unit.FromPoint(
                    5);

            var volumeTable =
                CreateTable(
                    section,
                    2.0,
                    2.6,
                    3.2,
                    5.0,
                    5.0);

            AddHeaderRow(
                volumeTable,
                "Laufwerk",
                "Dateisystem",
                "Typ",
                "Gesamtgröße",
                "Freier Speicher");

            var rowIndex =
                0;

            foreach (var volume in volumes)
            {
                var row =
                    AddDataRow(
                        volumeTable,
                        SafeText(
                            volume.DriveLetter),
                        SafeText(
                            volume.FileSystem),
                        SafeText(
                            volume.DriveType),
                        SafeText(
                            volume.TotalSize),
                        SafeText(
                            volume.FreeSpace));

                if (rowIndex % 2 == 1)
                {
                    ShadeRow(
                        row,
                        SurfaceColor);
                }

                rowIndex++;
            }

            AddSpacer(
                section,
                5);
        }
    }

    private static void AddInformationalFindings(
        Section section,
        CheckupSession checkupSession)
    {
        var findings =
            checkupSession
                .Assessment
                .InformationalFindings;

        if (findings.Count == 0)
        {
            return;
        }

        AddSectionHeading(
            section,
            "7. Weitere Systeminformationen");

        var introduction =
            section.AddParagraph(
                "Die folgenden Befunde dienen der technischen "
                + "Vollständigkeit. Aus ihnen wurde kein "
                + "unmittelbarer Handlungsbedarf abgeleitet.");

        introduction.Format.Font.Color =
            MutedTextColor;

        introduction.Format.SpaceAfter =
            Unit.FromPoint(
                6);

        var table =
            CreateTable(
                section,
                5.3,
                12.5);

        AddHeaderRow(
            table,
            "Information",
            "Beschreibung");

        var rowIndex =
            0;

        foreach (var finding in findings)
        {
            var row =
                AddDataRow(
                    table,
                    SafeText(
                        finding.Title),
                    SafeText(
                        finding.Description));

            if (rowIndex % 2 == 1)
            {
                ShadeRow(
                    row,
                    SurfaceColor);
            }

            rowIndex++;
        }

        AddSpacer(
            section,
            5);
    }

    private static void AddReportNotice(
        Section section)
    {
        AddSectionHeading(
            section,
            "Berichtshinweis");

        AddCallout(
            section,
            "Technische Momentaufnahme",
            "Der Bericht dokumentiert den zum Scanzeitpunkt "
            + "aus Windows auslesbaren Zustand. Er ersetzt keine "
            + "Herstellergarantie, keine Datensicherung und keine "
            + "physische Laborprüfung. Nicht auswertbare Bereiche "
            + "sind entsprechend gekennzeichnet. Kundenspezifische "
            + "Vorher-/Nachher-Berichte werden getrennt erstellt.",
            MutedTextColor,
            SurfaceColor);
    }

    private static void AddSectionHeading(
        Section section,
        string text)
    {
        var paragraph =
            section.AddParagraph(
                text);

        paragraph.Style =
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

            SetCellFontBold(
                row.Cells[0]);

            row.Cells[0].Shading.Color =
                SecondarySurfaceColor;

            if (rowIndex % 2 == 1)
            {
                row.Cells[1].Shading.Color =
                    SurfaceColor;
            }

            rowIndex++;
        }

        AddSpacer(
            section,
            5);
    }

    private static void AddCallout(
        Section section,
        string title,
        string message,
        Color accentColor,
        Color backgroundColor)
    {
        var table =
            CreateTable(
                section,
                ContentWidthCentimeters);

        var row =
            table.AddRow();

        var cell =
            row.Cells[0];

        cell.Shading.Color =
            backgroundColor;

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

            AddCellText(
                cell,
                headings[index],
                true,
                Colors.White);
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
            AddCellText(
                row.Cells[index],
                values[index]);
        }

        return row;
    }

    private static void AddCellText(
        Cell cell,
        string text,
        bool isBold = false,
        Color? fontColor = null)
    {
        cell.Format.Font.Bold =
            isBold;

        if (fontColor.HasValue)
        {
            cell.Format.Font.Color =
                fontColor.Value;
        }

        var paragraph =
            cell.AddParagraph(
                SafeText(
                    text));

        paragraph.Format.SpaceAfter =
            Unit.FromPoint(
                0);
    }

    private static void SetCellFontBold(
        Cell cell)
    {
        cell.Format.Font.Bold =
            true;
    }

    private static void SetCellFontColor(
        Cell cell,
        Color color)
    {
        cell.Format.Font.Color =
            color;
    }

    private static void ShadeRow(
        Row row,
        Color color)
    {
        for (var index = 0;
             index < row.Cells.Count;
             index++)
        {
            row.Cells[index].Shading.Color =
                color;
        }
    }

    private static void AddSpacer(
        Section section,
        double points)
    {
        var paragraph =
            section.AddParagraph();

        paragraph.Format.SpaceAfter =
            Unit.FromPoint(
                points);
    }

    private static string GetSeverityText(
        FindingSeverity severity)
    {
        return severity switch
        {
            FindingSeverity.Critical =>
                "Kritisch",

            FindingSeverity.Warning =>
                "Warnung",

            FindingSeverity.Recommendation =>
                "Empfehlung",

            _ =>
                "Information"
        };
    }

    private static Color GetSeverityColor(
        FindingSeverity severity)
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

    private static string FormatDateTime(
        DateTime? dateTime)
    {
        return dateTime.HasValue
            ? dateTime.Value.ToString(
                "dd.MM.yyyy HH:mm")
              + " Uhr"
            : "Nicht verfügbar";
    }

    private static string SafeText(
        string? value,
        string fallback =
            "Nicht verfügbar")
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

        if (string.IsNullOrWhiteSpace(
                directoryPath))
        {
            return;
        }

        Directory.CreateDirectory(
            directoryPath);
    }
}