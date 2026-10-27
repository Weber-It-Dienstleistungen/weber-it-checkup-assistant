
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Reports;

/// <summary>
/// Erzeugt eine halbkreisförmige Zustandsanzeige
/// für den Weber-IT-Kundencheckup.
///
/// Die Darstellung erfolgt ausschließlich
/// im Arbeitsspeicher.
///
/// Es werden keine Bilddateien geschrieben,
/// keine Kundendaten verändert und keine
/// technischen Bewertungen neu berechnet.
///
/// Der äußere Bogen zeigt den aktuellen Score.
/// Die Farbe richtet sich ausschließlich nach
/// der aktuellen Punktzahl:
///
/// 0 bis 49:   Rot
/// 50 bis 79:  Orange
/// 80 bis 100: Grün
///
/// Der innere hellblaue Bogen zeigt den
/// Eingangswert, jedoch ausschließlich
/// bei einem fachlich zulässigen Vergleich.
///
/// Die Richtung der Punktveränderung wird
/// unabhängig von der aktuellen Scorefarbe
/// dargestellt.
/// </summary>
internal static class CustomerCheckupGaugeImage
{
    private const int ImageWidth = 560;
    private const int ImageHeight = 310;

    private const double CenterX = 280;
    private const double CenterY = 222;

    private const double OuterRadius = 171;
    private const double InnerRadius = 140;

    private const double OuterStroke = 19;
    private const double InnerStroke = 13;

    private const int RedScoreMaximum = 49;
    private const int OrangeScoreMaximum = 79;

    private static readonly CultureInfo GermanCulture =
        CultureInfo.GetCultureInfo("de-DE");

    private static readonly Brush RedScoreBrush =
        CreateBrush(185, 28, 28);

    private static readonly Brush OrangeScoreBrush =
        CreateBrush(180, 83, 9);

    private static readonly Brush GreenScoreBrush =
        CreateBrush(21, 128, 61);

    private static readonly Brush PreviousBrush =
        CreateBrush(147, 197, 253);

    private static readonly Brush TrackBrush =
        CreateBrush(226, 232, 240);

    private static readonly Brush HeadingBrush =
        CreateBrush(15, 23, 42);

    private static readonly Brush MutedBrush =
        CreateBrush(71, 85, 105);

    private static readonly Brush SuccessBrush =
        CreateBrush(21, 128, 61);

    private static readonly Brush DangerBrush =
        CreateBrush(185, 28, 28);

    /// <summary>
    /// Erzeugt eine für MigraDoc nutzbare
    /// Base64-PNG-Bildquelle.
    ///
    /// Die Rückgabe kann unmittelbar an
    /// AddImage(...) übergeben werden.
    ///
    /// Es entstehen keine temporären Dateien.
    /// </summary>
    public static string CreateBase64Png(
        CustomerCheckupScoreComparison score)
    {
        ArgumentNullException.ThrowIfNull(score);

        var hasBeforeScore =
            IsValidScore(score.BeforeScore);

        var hasAfterScore =
            IsValidScore(score.AfterScore);

        var isComparable =
            score.IsComparable
            && score.Difference.HasValue
            && hasBeforeScore
            && hasAfterScore
            && score.AfterScore!.Value
               - score.BeforeScore!.Value
               == score.Difference.Value;

        /*
         * Schritt 55.4:
         *
         * Die Ampelfarbe wird nur anhand
         * des gültigen aktuellen Scores
         * bestimmt.
         *
         * Sie hängt ausdrücklich NICHT
         * von einer Verbesserung oder
         * Verschlechterung ab.
         *
         * Bei ungültigen oder fehlenden
         * Punktwerten bleibt die Darstellung
         * neutral.
         */
        var currentScoreBrush =
            GetCurrentScoreBrush(
                score.AfterScore);

        var visual = new DrawingVisual();

        using (var drawingContext = visual.RenderOpen())
        {
            DrawGaugeTracks(
                drawingContext);

            if (isComparable)
            {
                DrawProgressArc(
                    drawingContext,
                    CenterX,
                    CenterY,
                    InnerRadius,
                    InnerStroke,
                    score.BeforeScore!.Value,
                    PreviousBrush);
            }

            if (hasAfterScore)
            {
                DrawProgressArc(
                    drawingContext,
                    CenterX,
                    CenterY,
                    OuterRadius,
                    OuterStroke,
                    score.AfterScore!.Value,
                    currentScoreBrush);
            }

            DrawCenterScore(
                drawingContext,
                score.AfterScore,
                currentScoreBrush);

            DrawComparisonLabels(
                drawingContext,
                score,
                isComparable);
        }

        var bitmap = new RenderTargetBitmap(
            ImageWidth,
            ImageHeight,
            96,
            96,
            PixelFormats.Pbgra32);

        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();

        encoder.Frames.Add(
            BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();

        encoder.Save(stream);

        return "base64:"
               + Convert.ToBase64String(
                   stream.ToArray());
    }

    /// <summary>
    /// Ordnet einem gültigen aktuellen
    /// Punktwert eine Ampelfarbe zu.
    ///
    /// 0–49   = Rot
    /// 50–79  = Orange
    /// 80–100 = Grün
    ///
    /// Fehlende oder ungültige Werte
    /// werden neutral dargestellt.
    /// </summary>
    private static Brush GetCurrentScoreBrush(
        int? score)
    {
        if (!IsValidScore(score))
        {
            return HeadingBrush;
        }

        var value = score!.Value;

        if (value <= RedScoreMaximum)
        {
            return RedScoreBrush;
        }

        if (value <= OrangeScoreMaximum)
        {
            return OrangeScoreBrush;
        }

        return GreenScoreBrush;
    }

    private static void DrawGaugeTracks(
        DrawingContext context)
    {
        DrawSemicircle(
            context,
            CenterX,
            CenterY,
            OuterRadius,
            OuterStroke,
            TrackBrush);

        DrawSemicircle(
            context,
            CenterX,
            CenterY,
            InnerRadius,
            InnerStroke,
            TrackBrush);
    }

    private static void DrawSemicircle(
        DrawingContext context,
        double centerX,
        double centerY,
        double radius,
        double strokeWidth,
        Brush brush)
    {
        var geometry = CreateArcGeometry(
            centerX,
            centerY,
            radius,
            180d);

        var pen = CreatePen(
            brush,
            strokeWidth);

        context.DrawGeometry(
            null,
            pen,
            geometry);
    }

    private static void DrawProgressArc(
        DrawingContext context,
        double centerX,
        double centerY,
        double radius,
        double strokeWidth,
        int score,
        Brush brush)
    {
        var boundedScore =
            Math.Clamp(score, 0, 100);

        if (boundedScore <= 0)
        {
            return;
        }

        var sweepAngle =
            180d * boundedScore / 100d;

        var geometry = CreateArcGeometry(
            centerX,
            centerY,
            radius,
            sweepAngle);

        var pen = CreatePen(
            brush,
            strokeWidth);

        context.DrawGeometry(
            null,
            pen,
            geometry);
    }

    /// <summary>
    /// Ein Halbkreis verläuft vom linken
    /// Scheitel über die obere Kreishälfte
    /// zum rechten Scheitel.
    ///
    /// Der farbige Abschnitt wächst
    /// entsprechend der Punktzahl.
    /// </summary>
    private static Geometry CreateArcGeometry(
        double centerX,
        double centerY,
        double radius,
        double sweepAngle)
    {
        var boundedAngle =
            Math.Clamp(sweepAngle, 0d, 180d);

        var start = new Point(
            centerX - radius,
            centerY);

        var endAngleRadians =
            (180d + boundedAngle)
            * Math.PI / 180d;

        var end = new Point(
            centerX
            + Math.Cos(endAngleRadians) * radius,
            centerY
            + Math.Sin(endAngleRadians) * radius);

        var figure = new PathFigure
        {
            StartPoint = start,
            IsClosed = false,
            IsFilled = false
        };

        figure.Segments.Add(
            new ArcSegment(
                end,
                new Size(radius, radius),
                0d,
                false,
                SweepDirection.Clockwise,
                true));

        var geometry = new PathGeometry();

        geometry.Figures.Add(figure);

        geometry.Freeze();

        return geometry;
    }

    private static Pen CreatePen(
        Brush brush,
        double width)
    {
        var pen = new Pen(
            brush,
            width)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        pen.Freeze();

        return pen;
    }

    /// <summary>
    /// Die große Punktzahl und der äußere
    /// Fortschrittsbogen erhalten dieselbe
    /// Ampelfarbe.
    ///
    /// Die Beschriftung bleibt bewusst
    /// neutral für gute Lesbarkeit.
    /// </summary>
    private static void DrawCenterScore(
        DrawingContext context,
        int? score,
        Brush scoreBrush)
    {
        var scoreText =
            IsValidScore(score)
                ? score!.Value.ToString(
                    GermanCulture)
                : "–";

        DrawCenteredText(
            context,
            scoreText,
            CenterX,
            112,
            78,
            scoreBrush,
            bold: true);

        DrawCenteredText(
            context,
            "von 100 Punkten",
            CenterX,
            200,
            20,
            MutedBrush,
            bold: false);
    }

    /// <summary>
    /// Der Trend beschreibt ausschließlich
    /// die Veränderung zwischen zwei
    /// vergleichbaren Messungen.
    ///
    /// Deshalb kann beispielsweise ein
    /// roter Tacho eine grüne Verbesserung
    /// anzeigen, wenn der Systemzustand
    /// zwar besser geworden ist, aber
    /// weiterhin einen niedrigen
    /// absoluten Punktwert besitzt.
    /// </summary>
    private static void DrawComparisonLabels(
        DrawingContext context,
        CustomerCheckupScoreComparison score,
        bool isComparable)
    {
        if (!isComparable)
        {
            DrawCenteredText(
                context,
                "Kein zuverlässiger Vorher-/Nachher-Vergleich",
                CenterX,
                249,
                15,
                MutedBrush,
                bold: false);

            return;
        }

        var beforeScore =
            score.BeforeScore!.Value;

        var afterScore =
            score.AfterScore!.Value;

        DrawCenteredText(
            context,
            $"Vorher {beforeScore}  ·  Nachher {afterScore}",
            CenterX,
            245,
            18,
            MutedBrush,
            bold: false);

        var difference =
            score.Difference!.Value;

        var differenceText =
            difference switch
            {
                > 0 =>
                    $"+{difference} Punkte",

                < 0 =>
                    $"{difference} Punkte",

                _ =>
                    "Unverändert"
            };

        var differenceBrush =
            difference switch
            {
                > 0 => SuccessBrush,
                < 0 => DangerBrush,
                _ => MutedBrush
            };

        DrawCenteredText(
            context,
            differenceText,
            CenterX,
            275,
            20,
            differenceBrush,
            bold: true);
    }

    private static void DrawCenteredText(
        DrawingContext context,
        string text,
        double centerX,
        double top,
        double fontSize,
        Brush brush,
        bool bold)
    {
        var typeface = new Typeface(
            new FontFamily("Segoe UI"),
            FontStyles.Normal,
            bold
                ? FontWeights.Bold
                : FontWeights.Normal,
            FontStretches.Normal);

        var formattedText = new FormattedText(
            text,
            GermanCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            brush,
            1d);

        var left =
            centerX
            - formattedText.WidthIncludingTrailingWhitespace / 2d;

        context.DrawText(
            formattedText,
            new Point(
                left,
                top));
    }

    private static bool IsValidScore(
        int? score)
    {
        return score.HasValue
               && score.Value >= 0
               && score.Value <= 100;
    }

    private static Brush CreateBrush(
        byte red,
        byte green,
        byte blue)
    {
        var brush = new SolidColorBrush(
            Color.FromRgb(
                red,
                green,
                blue));

        brush.Freeze();

        return brush;
    }
}
