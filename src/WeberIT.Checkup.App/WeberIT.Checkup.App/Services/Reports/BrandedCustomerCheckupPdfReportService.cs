
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Reports;

public sealed class BrandedCustomerCheckupPdfReportService :
    ICustomerCheckupPdfReportService
{
    private const double HeaderHeightPoints = 44;
    private const double FooterHeightPoints = 44;
    private const double HorizontalMarginPoints = 42;

    private const string LogoRelativePath =
        "Resources\\Branding\\WeberIT-Logo.png";

    private static readonly XColor HeaderFooterColor =
        XColor.FromArgb(15, 23, 42);

    private static readonly XColor AccentColor =
        XColor.FromArgb(37, 99, 235);

    private static readonly XColor SecondaryTextColor =
        XColor.FromArgb(203, 213, 225);

    private readonly CustomerCheckupPdfReportService
        _baseReportService;

    public BrandedCustomerCheckupPdfReportService(
        CustomerCheckupPdfReportService baseReportService)
    {
        _baseReportService =
            baseReportService
            ?? throw new ArgumentNullException(
                nameof(baseReportService));
    }

    public void Export(
        Customer customer,
        CustomerDevice device,
        CustomerCheckupVisit customerCheckupVisit,
        string filePath)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(customerCheckupVisit);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "Für den Kundencheckup-Bericht wurde "
                + "kein gültiger Zielpfad angegeben.",
                nameof(filePath));
        }

        var finalFilePath =
            NormalizePdfFilePath(filePath);

        EnsureTargetDirectoryExists(finalFilePath);

        var originalLogoFilePath =
            ResolveLogoFilePath();

        var preparedLogoFilePath =
            PrepareLogoForRendering(
                originalLogoFilePath);

        var workingDirectory =
            Path.GetDirectoryName(finalFilePath)
            ?? Path.GetTempPath();

        var fileNameWithoutExtension =
            Path.GetFileNameWithoutExtension(
                finalFilePath);

        var operationId =
            Guid.NewGuid().ToString("N");

        var basePdfPath =
            Path.Combine(
                workingDirectory,
                "."
                + fileNameWithoutExtension
                + "."
                + operationId
                + ".base.pdf");

        var brandedPdfPath =
            Path.Combine(
                workingDirectory,
                "."
                + fileNameWithoutExtension
                + "."
                + operationId
                + ".branded.pdf");

        try
        {
            /*
             * Für abgeschlossene ältere Checkups
             * erzeugen wir bei Bedarf eine korrigierte
             * Berichtssicht.
             *
             * Die gespeicherten historischen Prüfdaten
             * und Vergleichsergebnisse bleiben unverändert.
             */
            var reportVisit =
                PrepareHistoricalVisitForReport(
                    customerCheckupVisit);

            _baseReportService.Export(
                customer,
                device,
                reportVisit,
                basePdfPath);

            ApplyBranding(
                basePdfPath,
                brandedPdfPath,
                preparedLogoFilePath,
                customer);

            ValidateBrandedReport(
                brandedPdfPath);

            File.Move(
                brandedPdfPath,
                finalFilePath,
                overwrite: true);
        }
        finally
        {
            TryDeleteFile(basePdfPath);
            TryDeleteFile(brandedPdfPath);

            if (!string.Equals(
                preparedLogoFilePath,
                originalLogoFilePath,
                StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(preparedLogoFilePath);
            }
        }
    }

    // =====================================================
    // V1.1 – KOMPATIBILITÄT HISTORISCHER BEFUNDE
    // =====================================================

    /// <summary>
    /// Erstellt bei Bedarf eine separate Berichtssicht
    /// für einen abgeschlossenen Kundencheckup.
    ///
    /// In alten Vergleichsergebnissen können reine
    /// Informationsbefunde fälschlich als offene
    /// technische Probleme gespeichert sein.
    ///
    /// Die Originaldaten werden nicht verändert.
    /// </summary>
    private static CustomerCheckupVisit
        PrepareHistoricalVisitForReport(
            CustomerCheckupVisit source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.IsCompleted
            || source.Comparison is null)
        {
            return source;
        }

        var originalComparison =
            source.Comparison;

        var originalFindings =
            originalComparison.Findings;

        if (originalFindings is null
            || originalFindings.Count == 0)
        {
            return source;
        }

        var requiresNormalization =
            originalFindings.Any(
                IsLegacyNeutralInformation);

        if (!requiresNormalization)
        {
            return source;
        }

        /*
         * Sämtliche Befundobjekte werden kopiert,
         * damit kein gespeichertes Statusfeld
         * verändert werden kann.
         */
        var reportFindings =
            originalFindings
                .Select(CreateReportFindingCopy)
                .ToList();

        /*
         * Die übrigen Vergleichsdaten werden
         * ausschließlich gelesen.
         *
         * Scores, Aufgaben, Aktionen und technische
         * Bereichsauswertungen bleiben unverändert.
         */
        var reportComparison =
            new CustomerCheckupComparison
            {
                ComparisonModelVersion =
                    originalComparison.ComparisonModelVersion,

                CustomerCheckupVisitId =
                    originalComparison.CustomerCheckupVisitId,

                CreatedAt =
                    originalComparison.CreatedAt,

                BeforeScanDate =
                    originalComparison.BeforeScanDate,

                AfterScanDate =
                    originalComparison.AfterScanDate,

                BeforeScoringVersion =
                    originalComparison.BeforeScoringVersion,

                AfterScoringVersion =
                    originalComparison.AfterScoringVersion,

                BeforeTaskListVersion =
                    originalComparison.BeforeTaskListVersion,

                WorkingTaskListVersion =
                    originalComparison.WorkingTaskListVersion,

                AfterTaskListVersion =
                    originalComparison.AfterTaskListVersion,

                SystemScore =
                    originalComparison.SystemScore,

                HardwareScore =
                    originalComparison.HardwareScore,

                Areas =
                    originalComparison.Areas,

                Findings =
                    reportFindings,

                Tasks =
                    originalComparison.Tasks,

                Actions =
                    originalComparison.Actions,

                ComparisonNotes =
                    originalComparison.ComparisonNotes
            };

        /*
         * Auch der Checkup-Vorgang erhält für die
         * PDF-Ausgabe eine separate Objektinstanz.
         *
         * Die historischen Scans und alle
         * Technikerangaben bleiben unverändert.
         */
        return new CustomerCheckupVisit
        {
            VisitModelVersion =
                source.VisitModelVersion,

            Id =
                source.Id,

            StartedAt =
                source.StartedAt,

            CompletedAt =
                source.CompletedAt,

            Status =
                source.Status,

            BeforeCheckup =
                source.BeforeCheckup,

            AfterCheckup =
                source.AfterCheckup,

            Comparison =
                reportComparison,

            TechnicianSummary =
                source.TechnicianSummary,

            NextSteps =
                source.NextSteps,

            NextCheckupDate =
                source.NextCheckupDate,

            CancellationReason =
                source.CancellationReason
        };
    }

    /// <summary>
    /// Erkennt einen älteren neutralen Befund,
    /// der noch einen problembezogenen Status hat.
    ///
    /// V1.1:
    /// Die fachliche Einstufung erfolgt anhand
    /// der Schweregrade, unabhängig davon,
    /// welcher Bewertungskategorie der Befund
    /// zugeordnet ist.
    ///
    /// AssessmentTarget bestimmt, ob ein Befund
    /// beispielsweise in die System- oder
    /// Hardwarebewertung einfließt.
    ///
    /// Es ist kein verlässliches Kriterium dafür,
    /// ob tatsächlich Handlungsbedarf besteht.
    /// </summary>
    private static bool IsLegacyNeutralInformation(
        CustomerCheckupFindingComparison finding)
    {
        if (finding.Status ==
            CustomerCheckupFindingComparisonStatus.Informational)
        {
            return false;
        }

        /*
         * Es muss mindestens ein gespeicherter
         * Schweregrad vorhanden sein.
         */
        if (!finding.BeforeSeverity.HasValue
            && !finding.AfterSeverity.HasValue)
        {
            return false;
        }

        /*
         * Beide Seiten des Vergleichs dürfen
         * ausschließlich Informationsbefunde
         * enthalten.
         *
         * Fehlt ein Befund auf einer Seite,
         * ist dies für sich genommen kein
         * Grund für eine Warnung.
         */
        if (finding.BeforeSeverity.HasValue
            && finding.BeforeSeverity.Value
                != FindingSeverity.Information)
        {
            return false;
        }

        if (finding.AfterSeverity.HasValue
            && finding.AfterSeverity.Value
                != FindingSeverity.Information)
        {
            return false;
        }

        /*
         * Wichtig:
         *
         * Keine Prüfung des AssessmentTarget.
         *
         * Auch ein Befund mit
         * AssessmentTarget.SystemCondition oder
         * AssessmentTarget.HardwareCondition kann
         * eine neutrale Information sein.
         *
         * Beispiele:
         * - Virenschutz aktiv
         * - Secure Boot aktiv
         * - TPM 2.0 aktiv
         * - Windows 11 erkannt
         */
        return true;
    }

    /// <summary>
    /// Kopiert einen Vergleichsbefund vollständig.
    ///
    /// Lediglich sein Status kann für die
    /// temporäre Berichtsausgabe korrigiert werden.
    /// </summary>
    private static CustomerCheckupFindingComparison
        CreateReportFindingCopy(
            CustomerCheckupFindingComparison source)
    {
        var reportStatus =
            IsLegacyNeutralInformation(source)
                ? CustomerCheckupFindingComparisonStatus
                    .Informational
                : source.Status;

        return new CustomerCheckupFindingComparison
        {
            MatchKey =
                source.MatchKey,

            MatchBasis =
                source.MatchBasis,

            AreaCode =
                source.AreaCode,

            Code =
                source.Code,

            CauseGroup =
                source.CauseGroup,

            Title =
                source.Title,

            BeforeDescription =
                source.BeforeDescription,

            AfterDescription =
                source.AfterDescription,

            Category =
                source.Category,

            AssessmentTarget =
                source.AssessmentTarget,

            BeforeSeverity =
                source.BeforeSeverity,

            AfterSeverity =
                source.AfterSeverity,

            BeforeOccurrenceCount =
                source.BeforeOccurrenceCount,

            AfterOccurrenceCount =
                source.AfterOccurrenceCount,

            WasBeforeAreaEvaluable =
                source.WasBeforeAreaEvaluable,

            IsAfterAreaEvaluable =
                source.IsAfterAreaEvaluable,

            Status =
                reportStatus
        };
    }

    // =====================================================
    // WEBER-IT-LOGO
    // =====================================================

    private static string ResolveLogoFilePath()
    {
        var logoFilePath =
            Path.Combine(
                AppContext.BaseDirectory,
                LogoRelativePath);

        if (!File.Exists(logoFilePath))
        {
            throw new FileNotFoundException(
                "Das Original-Logo von Weber IT ist "
                + "nicht im Programmverzeichnis vorhanden."
                + Environment.NewLine
                + Environment.NewLine
                + "Erwarteter Pfad:"
                + Environment.NewLine
                + logoFilePath,
                logoFilePath);
        }

        var fileInfo =
            new FileInfo(logoFilePath);

        if (fileInfo.Length == 0)
        {
            throw new IOException(
                "Die mitgelieferte Weber-IT-Logodatei "
                + "ist leer.");
        }

        return logoFilePath;
    }

    private static string PrepareLogoForRendering(
        string originalLogoFilePath)
    {
        try
        {
            using var stream =
                File.OpenRead(originalLogoFilePath);

            var decoder =
                BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count == 0)
            {
                return originalLogoFilePath;
            }

            BitmapSource bitmap =
                decoder.Frames[0];

            if (bitmap.Format != PixelFormats.Bgra32)
            {
                bitmap =
                    new FormatConvertedBitmap(
                        bitmap,
                        PixelFormats.Bgra32,
                        null,
                        0);
            }

            var visibleBounds =
                GetVisibleBounds(bitmap);

            if (visibleBounds.IsEmpty
                || visibleBounds.Width <= 0
                || visibleBounds.Height <= 0)
            {
                return originalLogoFilePath;
            }

            if (visibleBounds.X == 0
                && visibleBounds.Y == 0
                && visibleBounds.Width == bitmap.PixelWidth
                && visibleBounds.Height == bitmap.PixelHeight)
            {
                return originalLogoFilePath;
            }

            var croppedBitmap =
                new CroppedBitmap(
                    bitmap,
                    visibleBounds);

            var preparedLogoPath =
                Path.Combine(
                    Path.GetTempPath(),
                    "WeberIT-Logo-Prepared-"
                    + Guid.NewGuid().ToString("N")
                    + ".png");

            var encoder =
                new PngBitmapEncoder();

            encoder.Frames.Add(
                BitmapFrame.Create(croppedBitmap));

            using var outputStream =
                File.Create(preparedLogoPath);

            encoder.Save(outputStream);

            return preparedLogoPath;
        }
        catch
        {
            /*
             * Ein Fehler bei der automatischen
             * Logoaufbereitung soll die
             * PDF-Erstellung nicht blockieren.
             */
            return originalLogoFilePath;
        }
    }

    private static Int32Rect GetVisibleBounds(
        BitmapSource bitmap)
    {
        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;

        if (width <= 0 || height <= 0)
        {
            return Int32Rect.Empty;
        }

        var bytesPerPixel =
            (bitmap.Format.BitsPerPixel + 7) / 8;

        var stride =
            width * bytesPerPixel;

        var pixels =
            new byte[stride * height];

        bitmap.CopyPixels(
            pixels,
            stride,
            0);

        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;

        const byte alphaThreshold = 8;

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * stride;

            for (var x = 0; x < width; x++)
            {
                var pixelOffset =
                    rowOffset + (x * bytesPerPixel);

                var alpha =
                    bytesPerPixel >= 4
                        ? pixels[pixelOffset + 3]
                        : (byte)255;

                if (alpha <= alphaThreshold)
                {
                    continue;
                }

                if (x < minX)
                    minX = x;

                if (y < minY)
                    minY = y;

                if (x > maxX)
                    maxX = x;

                if (y > maxY)
                    maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return Int32Rect.Empty;
        }

        const int padding = 2;

        minX = Math.Max(0, minX - padding);
        minY = Math.Max(0, minY - padding);

        maxX = Math.Min(width - 1, maxX + padding);
        maxY = Math.Min(height - 1, maxY + padding);

        return new Int32Rect(
            minX,
            minY,
            maxX - minX + 1,
            maxY - minY + 1);
    }

    // =====================================================
    // PDF-BRANDING
    // =====================================================

    private static void ApplyBranding(
        string sourcePdfPath,
        string targetPdfPath,
        string logoFilePath,
        Customer customer)
    {
        if (!File.Exists(sourcePdfPath))
        {
            throw new FileNotFoundException(
                "Der technische Kundenbericht ist "
                + "für das Branding nicht verfügbar.",
                sourcePdfPath);
        }

        using var pdfDocument =
            PdfReader.Open(
                sourcePdfPath,
                PdfDocumentOpenMode.Modify);

        if (pdfDocument.PageCount == 0)
        {
            throw new InvalidOperationException(
                "Der technische Kundenbericht enthält "
                + "keine PDF-Seiten.");
        }

        using var logo =
            XImage.FromFile(logoFilePath);

        var headerTitleFont =
            new XFont(
                "Arial",
                7.5,
                XFontStyleEx.Bold);

        var companyFont =
            new XFont(
                "Arial",
                6.2,
                XFontStyleEx.Bold);

        var contactFont =
            new XFont(
                "Arial",
                5.8,
                XFontStyleEx.Regular);

        var pageFont =
            new XFont(
                "Arial",
                5.8,
                XFontStyleEx.Regular);

        var headerFooterBrush =
            new XSolidBrush(HeaderFooterColor);

        var accentBrush =
            new XSolidBrush(AccentColor);

        var secondaryTextBrush =
            new XSolidBrush(SecondaryTextColor);

        var whiteBrush =
            XBrushes.White;

        var customerNumber =
            string.IsNullOrWhiteSpace(
                customer.CustomerNumber)
                ? "ohne Kundennummer"
                : customer.CustomerNumber.Trim();

        for (var pageIndex = 0;
             pageIndex < pdfDocument.PageCount;
             pageIndex++)
        {
            var page =
                pdfDocument.Pages[pageIndex];

            var pageWidth =
                page.Width.Point;

            var pageHeight =
                page.Height.Point;

            using var graphics =
                XGraphics.FromPdfPage(
                    page,
                    XGraphicsPdfPageOptions.Append);

            DrawHeader(
                graphics,
                logo,
                pageWidth,
                headerTitleFont,
                headerFooterBrush,
                accentBrush,
                whiteBrush);

            DrawFooter(
                graphics,
                pageWidth,
                pageHeight,
                pageIndex + 1,
                pdfDocument.PageCount,
                customerNumber,
                companyFont,
                contactFont,
                pageFont,
                headerFooterBrush,
                accentBrush,
                secondaryTextBrush,
                whiteBrush);
        }

        pdfDocument.Save(targetPdfPath);
    }

    private static void DrawHeader(
        XGraphics graphics,
        XImage logo,
        double pageWidth,
        XFont headerTitleFont,
        XBrush headerFooterBrush,
        XBrush accentBrush,
        XBrush whiteBrush)
    {
        graphics.DrawRectangle(
            headerFooterBrush,
            0,
            0,
            pageWidth,
            HeaderHeightPoints);

        DrawLogo(graphics, logo);

        graphics.DrawString(
            "KUNDENCHECKUP",
            headerTitleFont,
            whiteBrush,
            new XRect(
                pageWidth
                - HorizontalMarginPoints
                - 170,
                14,
                170,
                16),
            XStringFormats.TopRight);

        graphics.DrawRectangle(
            accentBrush,
            0,
            HeaderHeightPoints - 2,
            pageWidth,
            2);
    }

    private static void DrawLogo(
        XGraphics graphics,
        XImage logo)
    {
        const double maximumWidth = 235;
        const double maximumHeight = 28;

        var originalWidth = logo.PointWidth;
        var originalHeight = logo.PointHeight;

        if (originalWidth <= 0
            || originalHeight <= 0)
        {
            throw new InvalidOperationException(
                "Das Weber-IT-Logo besitzt keine "
                + "gültigen Bildabmessungen.");
        }

        var scale =
            Math.Min(
                maximumWidth / originalWidth,
                maximumHeight / originalHeight);

        var width =
            originalWidth * scale;

        var height =
            originalHeight * scale;

        var left =
            HorizontalMarginPoints;

        var top =
            (HeaderHeightPoints - height) / 2;

        graphics.DrawImage(
            logo,
            left,
            top,
            width,
            height);
    }

    private static void DrawFooter(
        XGraphics graphics,
        double pageWidth,
        double pageHeight,
        int pageNumber,
        int pageCount,
        string customerNumber,
        XFont companyFont,
        XFont contactFont,
        XFont pageFont,
        XBrush headerFooterBrush,
        XBrush accentBrush,
        XBrush secondaryTextBrush,
        XBrush whiteBrush)
    {
        var footerTop =
            pageHeight - FooterHeightPoints;

        graphics.DrawRectangle(
            headerFooterBrush,
            0,
            footerTop,
            pageWidth,
            FooterHeightPoints);

        graphics.DrawRectangle(
            accentBrush,
            0,
            footerTop,
            pageWidth,
            2);

        var availableWidth =
            pageWidth
            - (HorizontalMarginPoints * 2);

        var companyLine =
            WeberItCompanyProfile.CompanyName
            + " · Inhaber "
            + WeberItCompanyProfile.OwnerName
            + " · "
            + WeberItCompanyProfile.Street
            + " · "
            + WeberItCompanyProfile.PostalCode
            + " "
            + WeberItCompanyProfile.City
            + " · "
            + WeberItCompanyProfile.Country;

        var mainContactLine =
            "T "
            + WeberItCompanyProfile.BusinessPhone
            + " · "
            + WeberItCompanyProfile.BusinessEmail
            + " · "
            + WeberItCompanyProfile.Website;

        var ownerContactLine =
            "Mobil "
            + WeberItCompanyProfile.MobilePhone
            + " · "
            + WeberItCompanyProfile.OwnerEmail;

        graphics.DrawString(
            companyLine,
            companyFont,
            whiteBrush,
            new XRect(
                HorizontalMarginPoints,
                footerTop + 6,
                availableWidth,
                9),
            XStringFormats.TopLeft);

        graphics.DrawString(
            mainContactLine,
            contactFont,
            secondaryTextBrush,
            new XRect(
                HorizontalMarginPoints,
                footerTop + 17,
                availableWidth,
                8),
            XStringFormats.TopLeft);

        graphics.DrawString(
            ownerContactLine,
            contactFont,
            secondaryTextBrush,
            new XRect(
                HorizontalMarginPoints,
                footerTop + 28,
                availableWidth - 150,
                8),
            XStringFormats.TopLeft);

        var pageText =
            "Kunde "
            + customerNumber
            + " · Seite "
            + pageNumber
            + " / "
            + pageCount;

        graphics.DrawString(
            pageText,
            pageFont,
            secondaryTextBrush,
            new XRect(
                pageWidth
                - HorizontalMarginPoints
                - 190,
                footerTop + 28,
                190,
                8),
            XStringFormats.TopRight);
    }

    // =====================================================
    // DATEIPRÜFUNG
    // =====================================================

    private static void ValidateBrandedReport(
        string filePath)
    {
        var file =
            new FileInfo(filePath);

        if (!file.Exists
            || file.Length == 0)
        {
            throw new IOException(
                "Der gebrandete Kundencheckup-Bericht "
                + "wurde nicht als gültige PDF-Datei "
                + "gespeichert.");
        }

        using var document =
            PdfReader.Open(
                filePath,
                PdfDocumentOpenMode.Import);

        if (document.PageCount == 0)
        {
            throw new IOException(
                "Der gebrandete Kundencheckup-Bericht "
                + "enthält keine PDF-Seiten.");
        }
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

        if (!string.IsNullOrWhiteSpace(
            directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    private static void TryDeleteFile(
        string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            /*
             * Temporäre Aufräumfehler dürfen einen
             * erfolgreich erzeugten Bericht nicht
             * nachträglich ungültig machen.
             */
        }
    }
}
