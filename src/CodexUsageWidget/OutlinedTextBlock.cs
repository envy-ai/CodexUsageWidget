using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Size = System.Windows.Size;
using Point = System.Windows.Point;
using Pen = System.Windows.Media.Pen;
using Brushes = System.Windows.Media.Brushes;

namespace CodexUsageWidget;

public sealed class OutlinedTextBlock : System.Windows.Controls.Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register(
        nameof(TextAlignment), typeof(TextAlignment), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(TextAlignment.Left, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TextTrimmingProperty = DependencyProperty.Register(
        nameof(TextTrimming), typeof(TextTrimming), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(TextTrimming.None, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public TextAlignment TextAlignment { get => (TextAlignment)GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }
    public TextTrimming TextTrimming { get => (TextTrimming)GetValue(TextTrimmingProperty); set => SetValue(TextTrimmingProperty, value); }

    protected override Size MeasureOverride(Size constraint)
    {
        var text = CreateText();
        return new Size(Math.Min(constraint.Width, text.WidthIncludingTrailingWhitespace + 2), text.Height + 2);
    }

    private FormattedText CreateText() => new(
        Text ?? string.Empty, CultureInfo.CurrentCulture, FlowDirection,
        new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize,
        Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (string.IsNullOrEmpty(Text) || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        var text = CreateText();
        text.MaxTextWidth = Math.Max(1, ActualWidth - 2);
        text.MaxLineCount = 1;
        text.Trimming = TextTrimming;
        text.TextAlignment = TextAlignment;

        // A centered 2-pixel stroke leaves a 1-pixel outline outside the glyph.
        var geometry = text.BuildGeometry(new Point(1, 1));
        drawingContext.DrawGeometry(null, new Pen(Brushes.Black, 2), geometry);
        drawingContext.DrawGeometry(Foreground, null, geometry);
    }
}
