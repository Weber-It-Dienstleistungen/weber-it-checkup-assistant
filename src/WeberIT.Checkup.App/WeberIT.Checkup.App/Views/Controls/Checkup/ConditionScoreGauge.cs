
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WeberIT.Checkup.App.Views.Controls.Checkup;

/// <summary>
/// Statische, wiederverwendbare WPF-Zustandsanzeige.
///
/// Der Tacho zeigt ausschließlich einen bereits
/// berechneten System- oder Hardware-Score.
///
/// Die Bewertung selbst wird hier nicht berechnet.
///
/// Ampelfarben:
/// 0–49   = Rot
/// 50–79  = Orange
/// 80–100 = Grün
///
/// Fehlende oder ungültige Werte werden neutral
/// dargestellt.
///
/// Für den Eingangsscan wird bewusst kein
/// Vorher-/Nachher-Vergleich angezeigt.
///
/// Eine spätere Animation kann auf dieser
/// Komponente aufbauen, ohne das Bewertungsmodell
/// oder den Scanner verändern zu müssen.
/// </summary>
public sealed class ConditionScoreGauge : FrameworkElement
{
    private const double DrawingWidth = 180;
    private const double DrawingHeight = 124;

    private const double CenterX = 90;
    private const double CenterY = 91;
    private const double Radius = 70;

    private const double StrokeWidth = 12;

    private const int RedMaximum = 49;
    private const int OrangeMaximum = 79;

    private static readonly CultureInfo GermanCulture =
        CultureInfo.GetCultureInfo("de-DE");

    private static readonly Brush RedBrush =
        CreateBrush(185, 28, 28);

    private static readonly Brush OrangeBrush =
        CreateBrush(180, 83, 9);

    private static readonly Brush GreenBrush =
        CreateBrush(21, 128, 61);

    private static readonly Brush TrackBrush =
        CreateBrush(
            148,
            163,
            184,
            100);

    private static readonly Brush NeutralBrush =
        CreateBrush(100, 116, 139);

    private static readonly Typeface ScoreTypeface =
        new(
            new FontFamily("Segoe UI"),
            FontStyles.Normal,
            FontWeights.Bold,
            FontStretches.Normal);

    private static readonly Typeface CaptionTypeface =
        new(
            new FontFamily("Segoe UI"),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);

    /// <summary>
    /// Bindbare Punktzahl.
    ///
    /// Durch AffectsRender zeichnet WPF den Tacho
    /// automatisch neu, wenn sich die Bewertung
    /// durch einen neuen Scan verändert.
    /// </summary>
    public static readonly DependencyProperty ScoreProperty =
        DependencyProperty.Register(
            nameof(Score),
            typeof(int?),
            typeof(ConditionScoreGauge),
            new FrameworkPropertyMetadata(
                default(int?),
                FrameworkPropertyMetadataOptions
                    .AffectsRender));

    public int? Score
    {
        get => (int?)GetValue(ScoreProperty);

        set => SetValue(
            ScoreProperty,
            value);
    }

    public ConditionScoreGauge()
    {
        SnapsToDevicePixels = true;
        Focusable = false;
    }

    protected override Size MeasureOverride(
        Size availableSize)
    {
        return new Size(
            DrawingWidth,
            DrawingHeight);
    }

    protected override void OnRender(
        DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (ActualWidth <= 0
            || ActualHeight <= 0)
        {
            return;
        }

        /*
         * Die Zeichnung besitzt ein festes
         * internes Koordinatensystem.
         *
         * Dadurch kann das Steuerelement
         * auf unterschiedliche Größen skaliert
         * werden, ohne die Geometrie zu verändern.
         */
        var scale =
            Math.Min(
                ActualWidth / DrawingWidth,
                ActualHeight / DrawingHeight);

        if (!double.IsFinite(scale)
            || scale <= 0)
        {
            return;
        }

        var offsetX =
            (ActualWidth - DrawingWidth * scale)
            / 2d;

        var offsetY =
            (ActualHeight - DrawingHeight * scale)
            / 2d;

        drawingContext.PushTransform(
            new TranslateTransform(
                offsetX,
                offsetY));

        drawingContext.PushTransform(
            new ScaleTransform(
                scale,
                scale));

        try
        {
            DrawGauge(
                drawingContext);
        }
        finally
        {
            drawingContext.Pop();
            drawingContext.Pop();
        }
    }

    private void DrawGauge(
        DrawingContext context)
    {
        var hasValidScore =
            Score.HasValue
            && Score.Value >= 0
            && Score.Value <= 100;

        var currentBrush =
            hasValidScore
                ? GetScoreBrush(Score!.Value)
                : NeutralBrush;

        /*
         * Hintergrundbogen:
         * immer sichtbar, auch wenn aktuell
         * keine Bewertung verfügbar ist.
         */
        DrawArc(
            context,
            TrackBrush,
            180d);

        /*
         * Der farbige Bogen zeigt die
         * tatsächliche Punktzahl.
         *
         * Es erfolgt keine Animation und
         * keine künstliche Fortschrittsanzeige.
         */
        if (hasValidScore
            && Score!.Value > 0)
        {
            var angle =
                180d * Score.Value / 100d;

            DrawArc(
                context,
                currentBrush,
                angle);
        }

        var scoreText =
            hasValidScore
                ? Score!.Value.ToString(
                    GermanCulture)
                : "–";

        DrawCenteredText(
            context,
            scoreText,
            51,
            40,
            currentBrush,
            ScoreTypeface);

        var caption =
            hasValidScore
                ? "von 100 Punkten"
                : "Nicht bewertet";

        var captionBrush =
            TryFindResource("TextSecondaryBrush")
                as Brush
            ?? NeutralBrush;

        DrawCenteredText(
            context,
            caption,
            100,
            11,
            captionBrush,
            CaptionTypeface);
    }

    /// <summary>
    /// Zeichnet einen Bogen vom linken über
    /// den oberen zum rechten Kreispunkt.
    ///
    /// Der komplette Halbkreis entspricht
    /// 100 Punkten.
    /// </summary>
    private static void DrawArc(
        DrawingContext context,
        Brush brush,
        double sweepAngle)
    {
        var boundedAngle =
            Math.Clamp(
                sweepAngle,
                0d,
                180d);

        if (boundedAngle <= 0)
        {
            return;
        }

        var start =
            new Point(
                CenterX - Radius,
                CenterY);

        var radians =
            (180d + boundedAngle)
            * Math.PI / 180d;

        var end =
            new Point(
                CenterX
                + Math.Cos(radians) * Radius,
                CenterY
                + Math.Sin(radians) * Radius);

        var geometry =
            new StreamGeometry();

        using (var geometryContext =
               geometry.Open())
        {
            geometryContext.BeginFigure(
                start,
                false,
                false);

            geometryContext.ArcTo(
                end,
                new Size(
                    Radius,
                    Radius),
                0d,
                false,
                SweepDirection.Clockwise,
                true,
                false);
        }

        geometry.Freeze();

        var pen = new Pen(
            brush,
            StrokeWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        pen.Freeze();

        context.DrawGeometry(
            null,
            pen,
            geometry);
    }

    private void DrawCenteredText(
        DrawingContext context,
        string value,
        double top,
        double fontSize,
        Brush brush,
        Typeface typeface)
    {
        var pixelsPerDip =
            VisualTreeHelper.GetDpi(this)
                .PixelsPerDip;

        var text =
            new FormattedText(
                value,
                GermanCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                brush,
                pixelsPerDip);

        var left =
            CenterX
            - text.WidthIncludingTrailingWhitespace
            / 2d;

        context.DrawText(
            text,
            new Point(
                left,
                top));
    }

    private static Brush GetScoreBrush(
        int score)
    {
        if (score <= RedMaximum)
        {
            return RedBrush;
        }

        if (score <= OrangeMaximum)
        {
            return OrangeBrush;
        }

        return GreenBrush;
    }

    private static Brush CreateBrush(
        byte red,
        byte green,
        byte blue,
        byte alpha = 255)
    {
        var brush =
            new SolidColorBrush(
                Color.FromArgb(
                    alpha,
                    red,
                    green,
                    blue));

        brush.Freeze();

        return brush;
    }
}
