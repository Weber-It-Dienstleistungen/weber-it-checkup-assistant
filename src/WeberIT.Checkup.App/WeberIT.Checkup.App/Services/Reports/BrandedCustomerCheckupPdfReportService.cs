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
    private const double HeaderHeightPoints =
        44;

    private const double FooterHeightPoints =
        44;

    private const double HorizontalMarginPoints =
        42;

    private const string LogoRelativePath =
        "Resources\\Branding\\WeberIT-Logo.png";

    private static readonly XColor HeaderFooterColor =
        XColor.FromArgb(
            15,
            23,
            42);

    private static readonly XColor AccentColor =
        XColor.FromArgb(
            37,
            99,
            235);

    private static readonly XColor SecondaryTextColor =
        XColor.FromArgb(
            203,
            213,
            225);

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
        ArgumentNullException.ThrowIfNull(
            customer);

        ArgumentNullException.ThrowIfNull(
            device);

        ArgumentNullException.ThrowIfNull(
            customerCheckupVisit);

        if (string.IsNullOrWhiteSpace(
                filePath))
        {
            throw new ArgumentException(
                "Für den Kundencheckup-Bericht wurde "
                + "kein gültiger Zielpfad angegeben.",
                nameof(filePath));
        }

        var finalFilePath =
            NormalizePdfFilePath(
                filePath);

        EnsureTargetDirectoryExists(
            finalFilePath);

        var originalLogoFilePath =
            ResolveLogoFilePath();

        var preparedLogoFilePath =
            PrepareLogoForRendering(
                originalLogoFilePath);

        var workingDirectory =
            Path.GetDirectoryName(
                finalFilePath)
            ?? Path.GetTempPath();

        var fileNameWithoutExtension =
            Path.GetFileNameWithoutExtension(
                finalFilePath);

        var operationId =
            Guid.NewGuid()
                .ToString(
                    "N");

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
             * Der bestehende PDF-Dienst erzeugt weiterhin
             * ausschließlich den fachlichen Kundenbericht.
             */
            _baseReportService.Export(
                customer,
                device,
                customerCheckupVisit,
                basePdfPath);

            /*
             * Danach werden das lokal mitgelieferte Logo
             * und die Unternehmensdaten ergänzt.
             *
             * Vor dem Rendern werden transparente Ränder
             * des Original-Logos automatisch entfernt,
             * damit der sichtbare Logoinhalt sauber und
             * groß im Kopf erscheint.
             */
            ApplyBranding(
                basePdfPath,
                brandedPdfPath,
                preparedLogoFilePath,
                customer);

            ValidateBrandedReport(
                brandedPdfPath);

            /*
             * Erst die vollständig erzeugte PDF wird
             * an den endgültigen Zielpfad verschoben.
             */
            File.Move(
                brandedPdfPath,
                finalFilePath,
                overwrite: true);
        }
        finally
        {
            TryDeleteFile(
                basePdfPath);

            TryDeleteFile(
                brandedPdfPath);

            if (!string.Equals(
                    preparedLogoFilePath,
                    originalLogoFilePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(
                    preparedLogoFilePath);
            }
        }
    }

    private static string ResolveLogoFilePath()
    {
        var logoFilePath =
            Path.Combine(
                AppContext.BaseDirectory,
                LogoRelativePath);

        if (!File.Exists(
                logoFilePath))
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
            new FileInfo(
                logoFilePath);

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
                File.OpenRead(
                    originalLogoFilePath);

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
                GetVisibleBounds(
                    bitmap);

            if (visibleBounds.IsEmpty
                || visibleBounds.Width <= 0
                || visibleBounds.Height <= 0)
            {
                return originalLogoFilePath;
            }

            /*
             * Wenn nahezu das gesamte Bild schon Inhalt ist,
             * sparen wir uns eine temporäre Datei.
             */
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
                    + Guid.NewGuid()
                        .ToString(
                            "N")
                    + ".png");

            var encoder =
                new PngBitmapEncoder();

            encoder.Frames.Add(
                BitmapFrame.Create(
                    croppedBitmap));

            using var outputStream =
                File.Create(
                    preparedLogoPath);

            encoder.Save(
                outputStream);

            return preparedLogoPath;
        }
        catch
        {
            /*
             * Falls das automatische Freistellen aus irgendeinem
             * Grund fehlschlägt, verwenden wir lieber weiterhin
             * das Original-Logo statt den Bericht zu blockieren.
             */
            return originalLogoFilePath;
        }
    }

    private static Int32Rect GetVisibleBounds(
        BitmapSource bitmap)
    {
        var width =
            bitmap.PixelWidth;

        var height =
            bitmap.PixelHeight;

        if (width <= 0
            || height <= 0)
        {
            return Int32Rect.Empty;
        }

        var bytesPerPixel =
            (bitmap.Format.BitsPerPixel + 7)
            / 8;

        var stride =
            width * bytesPerPixel;

        var pixels =
            new byte[stride * height];

        bitmap.CopyPixels(
            pixels,
            stride,
            0);

        var minX =
            width;

        var minY =
            height;

        var maxX =
            -1;

        var maxY =
            -1;

        const byte alphaThreshold =
            8;

        for (var y = 0;
             y < height;
             y++)
        {
            var rowOffset =
                y * stride;

            for (var x = 0;
                 x < width;
                 x++)
            {
                var pixelOffset =
                    rowOffset
                    + (x * bytesPerPixel);

                var alpha =
                    bytesPerPixel >= 4
                        ? pixels[pixelOffset + 3]
                        : (byte)255;

                if (alpha <= alphaThreshold)
                {
                    continue;
                }

                if (x < minX)
                {
                    minX = x;
                }

                if (y < minY)
                {
                    minY = y;
                }

                if (x > maxX)
                {
                    maxX = x;
                }

                if (y > maxY)
                {
                    maxY = y;
                }
            }
        }

        if (maxX < minX
            || maxY < minY)
        {
            return Int32Rect.Empty;
        }

        const int padding =
            2;

        minX =
            Math.Max(
                0,
                minX - padding);

        minY =
            Math.Max(
                0,
                minY - padding);

        maxX =
            Math.Min(
                width - 1,
                maxX + padding);

        maxY =
            Math.Min(
                height - 1,
                maxY + padding);

        return new Int32Rect(
            minX,
            minY,
            maxX - minX + 1,
            maxY - minY + 1);
    }

    private static void ApplyBranding(
        string sourcePdfPath,
        string targetPdfPath,
        string logoFilePath,
        Customer customer)
    {
        if (!File.Exists(
                sourcePdfPath))
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
            XImage.FromFile(
                logoFilePath);

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
            new XSolidBrush(
                HeaderFooterColor);

        var accentBrush =
            new XSolidBrush(
                AccentColor);

        var secondaryTextBrush =
            new XSolidBrush(
                SecondaryTextColor);

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
                pdfDocument.Pages[
                    pageIndex];

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

        pdfDocument.Save(
            targetPdfPath);
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

        DrawLogo(
            graphics,
            logo);

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
        const double maximumWidth =
            235;

        const double maximumHeight =
            28;

        var originalWidth =
            logo.PointWidth;

        var originalHeight =
            logo.PointHeight;

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
            pageHeight
            - FooterHeightPoints;

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

    private static void ValidateBrandedReport(
        string filePath)
    {
        var file =
            new FileInfo(
                filePath);

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

    private static void TryDeleteFile(
        string filePath)
    {
        try
        {
            if (File.Exists(
                    filePath))
            {
                File.Delete(
                    filePath);
            }
        }
        catch
        {
            /*
             * Temporäre Aufräumfehler dürfen einen bereits
             * erfolgreich erzeugten Bericht nicht
             * nachträglich ungültig machen.
             */
        }
    }
}