using System.Windows;
using System.Windows.Controls;

namespace WeberIT.Checkup.App.Views.Controls.Shared;

public partial class InfoRow : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label),
            typeof(string),
            typeof(InfoRow),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(string),
            typeof(InfoRow),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty LabelWidthProperty =
        DependencyProperty.Register(
            nameof(LabelWidth),
            typeof(GridLength),
            typeof(InfoRow),
            new PropertyMetadata(new GridLength(130)));

    public static readonly DependencyProperty RowMarginProperty =
        DependencyProperty.Register(
            nameof(RowMargin),
            typeof(Thickness),
            typeof(InfoRow),
            new PropertyMetadata(new Thickness(0, 0, 0, 10)));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public GridLength LabelWidth
    {
        get => (GridLength)GetValue(LabelWidthProperty);
        set => SetValue(LabelWidthProperty, value);
    }

    public Thickness RowMargin
    {
        get => (Thickness)GetValue(RowMarginProperty);
        set => SetValue(RowMarginProperty, value);
    }

    public InfoRow()
    {
        InitializeComponent();
    }
}