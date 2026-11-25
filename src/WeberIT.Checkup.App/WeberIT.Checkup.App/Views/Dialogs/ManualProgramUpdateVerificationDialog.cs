using System.Windows;
using System.Windows.Controls;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Views.Dialogs;

public sealed class ManualProgramUpdateVerificationDialog : Window
{
    private readonly ComboBox _packages = new();
    private readonly TextBox _version = new();
    private readonly TextBox _evidence = new();
    private readonly TextBlock _error = new();

    public AvailableProgramUpdate? SelectedUpdate { get; private set; }
    public string ObservedVersion { get; private set; } = string.Empty;
    public string Evidence { get; private set; } = string.Empty;

    public ManualProgramUpdateVerificationDialog(IReadOnlyList<AvailableProgramUpdate> candidates)
    {
        Title = "Manuell installierte Programmversion bestätigen";
        Width = 680; Height = 490; MinWidth = 540; MinHeight = 430;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "SurfaceSecondaryBrush");
        SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock
        {
            Text = "Bestätigen Sie nur eine tatsächlich überprüfte installierte Zielversion (z. B. im Info-Dialog des Programms). Eine manuelle Bestätigung ersetzt keine zuverlässige WinGet-Abschlussprüfung.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16)
        });
        foreach (var item in candidates)
            _packages.Items.Add(new ComboBoxItem
            {
                Content = $"{item.Name} ({item.PackageId}) · {item.InstalledVersion} → {item.AvailableVersion}",
                Tag = item
            });
        _packages.SelectedIndex = candidates.Count == 1 ? 0 : -1;
        AddField(panel, "Programm aus Eingangsscan", _packages);
        _version.MaxLength = 200;
        AddField(panel, "Tatsächlich überprüfte installierte Version (exakte Zielversion)", _version);
        _evidence.MaxLength = 1000;
        _evidence.AcceptsReturn = true;
        _evidence.TextWrapping = TextWrapping.Wrap;
        _evidence.MinHeight = 72;
        AddField(panel, "Prüfquelle / nachvollziehbare Begründung", _evidence);
        _error.TextWrapping = TextWrapping.Wrap;
        _error.SetResourceReference(TextBlock.ForegroundProperty, "WarningBrush");
        panel.Children.Add(_error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = "Abbrechen", IsCancel = true, Padding = new Thickness(12, 8, 12, 8) };
        var save = new Button { Content = "Bestätigung speichern", IsDefault = true,
            Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(10, 0, 0, 0) };
        save.SetResourceReference(FrameworkElement.StyleProperty, "AccentButtonStyle");
        save.Click += (_, _) =>
        {
            if (_packages.SelectedItem is not ComboBoxItem { Tag: AvailableProgramUpdate update }
                || !string.Equals(_version.Text.Trim(), update.AvailableVersion.Trim(), StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(_evidence.Text))
            {
                _error.Text = "Bitte Programm, exakte Zielversion und Prüfquelle angeben.";
                return;
            }
            SelectedUpdate = update;
            ObservedVersion = _version.Text.Trim();
            Evidence = _evidence.Text.Trim();
            DialogResult = true;
        };
        buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private static void AddField(StackPanel panel, string label, Control field)
    {
        panel.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 4) });
        field.Margin = new Thickness(0, 0, 0, 8);
        panel.Children.Add(field);
    }
}
