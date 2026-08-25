using System.Windows;
using System.Windows.Controls;

namespace WeberIT.Checkup.App.Views.Controls.Checkup;

public partial class CollapsibleCheckupSection :
    UserControl
{
    public static readonly DependencyProperty
        TitleProperty =
            DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(CollapsibleCheckupSection),
                new PropertyMetadata(
                    string.Empty));

    public static readonly DependencyProperty
        SectionContentProperty =
            DependencyProperty.Register(
                nameof(SectionContent),
                typeof(object),
                typeof(CollapsibleCheckupSection),
                new PropertyMetadata(
                    null));

    public static readonly DependencyProperty
        IsExpandedProperty =
            DependencyProperty.Register(
                nameof(IsExpanded),
                typeof(bool),
                typeof(CollapsibleCheckupSection),
                new PropertyMetadata(
                    false));

    public CollapsibleCheckupSection()
    {
        InitializeComponent();
    }

    public string Title
    {
        get =>
            (string)GetValue(
                TitleProperty);

        set =>
            SetValue(
                TitleProperty,
                value);
    }

    public object? SectionContent
    {
        get =>
            GetValue(
                SectionContentProperty);

        set =>
            SetValue(
                SectionContentProperty,
                value);
    }

    public bool IsExpanded
    {
        get =>
            (bool)GetValue(
                IsExpandedProperty);

        set =>
            SetValue(
                IsExpandedProperty,
                value);
    }
}