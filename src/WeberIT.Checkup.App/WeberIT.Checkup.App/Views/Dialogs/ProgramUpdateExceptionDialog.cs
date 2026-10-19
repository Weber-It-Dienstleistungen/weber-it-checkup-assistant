
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Views.Dialogs;

/// <summary>
/// Erfasst eine begründete Technikerentscheidung
/// zu einer einzelnen WinGet-Aktualisierung.
///
/// Der Dialog verändert keine gespeicherten Daten.
/// Er liefert ausschließlich eine vorbereitete
/// Entscheidung an den aufrufenden Code zurück.
/// </summary>
public sealed class ProgramUpdateExceptionDialog : Window
{
    private static readonly Brush DialogBackgroundBrush =
        new SolidColorBrush(Color.FromRgb(10, 19, 38));

    private static readonly Brush CardBackgroundBrush =
        new SolidColorBrush(Color.FromRgb(24, 37, 62));

    private static readonly Brush InputBackgroundBrush =
        new SolidColorBrush(Color.FromRgb(32, 48, 78));

    private static readonly Brush BorderBrushValue =
        new SolidColorBrush(Color.FromRgb(60, 88, 135));

    private static readonly Brush PrimaryTextBrush =
        new SolidColorBrush(Color.FromRgb(245, 248, 255));

    private static readonly Brush SecondaryTextBrush =
        new SolidColorBrush(Color.FromRgb(180, 196, 226));

    private static readonly Brush MutedTextBrush =
        new SolidColorBrush(Color.FromRgb(150, 168, 198));

    private static readonly Brush CancelButtonBackgroundBrush =
        new SolidColorBrush(Color.FromRgb(34, 49, 80));

    private static readonly Brush CancelButtonBorderBrush =
        new SolidColorBrush(Color.FromRgb(80, 108, 158));

    private readonly CustomerCheckupVisit _visit;

    private readonly ProgramUpdateInformation
        _programUpdateInformation;

    private readonly ListBox _updateSelector = new();

    private readonly ListBox _existingExceptionsList = new();

    private readonly TextBox _reasonTextBox = new();

    private readonly TextBox _technicianNoteTextBox = new();

    public ProgramUpdateExceptionDialog(
        CustomerCheckupVisit visit,
        ProgramUpdateInformation programUpdateInformation)
    {
        ArgumentNullException.ThrowIfNull(visit);
        ArgumentNullException.ThrowIfNull(
            programUpdateInformation);

        if (!visit.IsInProgress
            || visit.IsCompletionPrepared)
        {
            throw new InvalidOperationException(
                "Programmupdate-Ausnahmen können nur "
                + "während eines laufenden, noch nicht "
                + "für den Abschluss vorbereiteten "
                + "Kundencheckups bearbeitet werden.");
        }

        if (programUpdateInformation.IsWingetAvailable
                != true
            || !programUpdateInformation.IsAnalysisPerformed
            || !programUpdateInformation.IsAnalysisSuccessful)
        {
            throw new InvalidOperationException(
                "Für die Dokumentation von "
                + "Programmupdate-Ausnahmen muss eine "
                + "erfolgreiche WinGet-Analyse vorliegen.");
        }

        _visit = visit;
        _programUpdateInformation =
            programUpdateInformation;

        Title =
            "Programmupdate-Ausnahmen dokumentieren";

        Width = 760;
        Height = 730;

        MinWidth = 600;
        MinHeight = 560;

        WindowStartupLocation =
            WindowStartupLocation.CenterOwner;

        ResizeMode =
            ResizeMode.CanResize;

        ShowInTaskbar = false;

        Background = DialogBackgroundBrush;
        Foreground = PrimaryTextBrush;

        BuildInterface();
        PopulateUpdates();
        PopulateExistingExceptions();
    }

    public ProgramUpdateExceptionDialogAction
        SelectedAction
    { get; private set; } =
            ProgramUpdateExceptionDialogAction.None;

    public AvailableProgramUpdate? SelectedUpdate
    {
        get;
        private set;
    }

    public string Reason { get; private set; } =
        string.Empty;

    public string TechnicianNote { get; private set; } =
        string.Empty;

    public Guid? ExceptionIdToRevoke
    {
        get;
        private set;
    }

    private void BuildInterface()
    {
        var rootGrid = new Grid
        {
            Background = DialogBackgroundBrush
        };

        rootGrid.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        rootGrid.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility =
                ScrollBarVisibility.Auto,

            HorizontalScrollBarVisibility =
                ScrollBarVisibility.Disabled,

            Background = DialogBackgroundBrush
        };

        var contentBorder = new Border
        {
            Padding = new Thickness(24),
            Background = DialogBackgroundBrush
        };

        var contentPanel = new StackPanel();

        // ===============================================
        // EINLEITUNG
        // ===============================================

        var introCard = CreateCardPanel();

        introCard.Children.Add(
            CreateTitle(
                "Begründete Programmupdate-Ausnahmen"));

        introCard.Children.Add(
            CreateDescription(
                "Hier können einzelne ausstehende "
                + "Programmaktualisierungen nach "
                + "fachlicher Prüfung bewusst "
                + "ausgenommen werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Die Aktualisierungen bleiben weiterhin "
                + "als technische Befunde dokumentiert. "
                + "Eine Ausnahme ist keine Bestätigung, "
                + "dass das betreffende Programm "
                + "frei von Sicherheitslücken ist."));

        contentPanel.Children.Add(introCard);

        // ===============================================
        // PROGRAMMAUSWAHL
        // ===============================================

        var selectionCard = CreateCardPanel();

        selectionCard.Margin =
            new Thickness(0, 16, 0, 0);

        selectionCard.Children.Add(
            CreateSectionTitle(
                "1. Programm auswählen"));

        selectionCard.Children.Add(
            CreateDescription(
                "Wählen Sie die Aktualisierung aus, "
                + "die im aktuellen Kundencheckup "
                + "bewusst zurückgestellt werden soll."));

        /*
         * Statt einer nativen ComboBox nutzen wir
         * eine reguläre ListBox.
         *
         * Dadurch bleiben Hintergrund und Schrift
         * unabhängig vom Windows-ComboBox-Theme
         * zuverlässig lesbar.
         *
         * Die Liste unterstützt weiterhin
         * Tastaturbedienung und Scrollen.
         */
        _updateSelector.Margin =
            new Thickness(0, 10, 0, 0);

        _updateSelector.MinHeight = 100;
        _updateSelector.Height = 170;

        _updateSelector.SelectionMode =
            SelectionMode.Single;

        _updateSelector
            .HorizontalContentAlignment =
                HorizontalAlignment.Stretch;

        _updateSelector
            .SetValue(
                ScrollViewer.VerticalScrollBarVisibilityProperty,
                ScrollBarVisibility.Auto);

        _updateSelector
            .SetValue(
                ScrollViewer.HorizontalScrollBarVisibilityProperty,
                ScrollBarVisibility.Disabled);

        ApplyInputStyle(_updateSelector);

        selectionCard.Children.Add(
            _updateSelector);

        contentPanel.Children.Add(selectionCard);

        // ===============================================
        // FACHLICHE BEGRÜNDUNG
        // ===============================================

        var reasonCard = CreateCardPanel();

        reasonCard.Margin =
            new Thickness(0, 16, 0, 0);

        reasonCard.Children.Add(
            CreateSectionTitle(
                "2. Fachliche Begründung"));

        reasonCard.Children.Add(
            CreateDescription(
                "Warum darf oder soll dieses Programm "
                + "im aktuellen Kundencheckup "
                + "nicht aktualisiert werden?"));

        _reasonTextBox.Margin =
            new Thickness(0, 10, 0, 0);

        _reasonTextBox.MinHeight = 90;
        _reasonTextBox.MaxLength = 2000;
        _reasonTextBox.AcceptsReturn = true;

        _reasonTextBox.TextWrapping =
            TextWrapping.Wrap;

        _reasonTextBox.VerticalScrollBarVisibility =
            ScrollBarVisibility.Auto;

        _reasonTextBox.ToolTip =
            "Pflichtfeld: fachliche Begründung "
            + "für die Entscheidung.";

        ApplyInputStyle(_reasonTextBox);

        reasonCard.Children.Add(
            _reasonTextBox);

        contentPanel.Children.Add(reasonCard);

        // ===============================================
        // TECHNIKERNOTIZ
        // ===============================================

        var noteCard = CreateCardPanel();

        noteCard.Margin =
            new Thickness(0, 16, 0, 0);

        noteCard.Children.Add(
            CreateSectionTitle(
                "3. Zusätzliche Technikernotiz"));

        noteCard.Children.Add(
            CreateDescription(
                "Optional: beispielsweise die "
                + "betroffene Kundenanwendung, "
                + "eine Herstellerfreigabe oder "
                + "Hinweise für den nächsten Checkup."));

        _technicianNoteTextBox.Margin =
            new Thickness(0, 10, 0, 0);

        _technicianNoteTextBox.MinHeight = 80;
        _technicianNoteTextBox.MaxLength = 4000;
        _technicianNoteTextBox.AcceptsReturn = true;

        _technicianNoteTextBox.TextWrapping =
            TextWrapping.Wrap;

        _technicianNoteTextBox
            .VerticalScrollBarVisibility =
                ScrollBarVisibility.Auto;

        ApplyInputStyle(_technicianNoteTextBox);

        noteCard.Children.Add(
            _technicianNoteTextBox);

        contentPanel.Children.Add(noteCard);

        // ===============================================
        // DOKUMENTIERTE AUSNAHMEN
        // ===============================================

        var existingCard = CreateCardPanel();

        existingCard.Margin =
            new Thickness(0, 16, 0, 0);

        existingCard.Children.Add(
            CreateSectionTitle(
                "Bereits dokumentierte Ausnahmen"));

        existingCard.Children.Add(
            CreateDescription(
                "Die Entscheidungen gelten ausschließlich "
                + "für diesen Kundencheckup und die "
                + "dokumentierten Programmversionen."));

        _existingExceptionsList.Margin =
            new Thickness(0, 10, 0, 0);

        _existingExceptionsList.MinHeight = 80;
        _existingExceptionsList.MaxHeight = 180;

        _existingExceptionsList
            .HorizontalContentAlignment =
                HorizontalAlignment.Stretch;

        ApplyInputStyle(_existingExceptionsList);

        existingCard.Children.Add(
            _existingExceptionsList);

        var revokeButton = new Button
        {
            Content =
                "Ausgewählte Ausnahme aufheben",

            Margin =
                new Thickness(0, 12, 0, 0),

            Padding =
                new Thickness(12, 8, 12, 8),

            MinHeight = 36,

            HorizontalAlignment =
                HorizontalAlignment.Left,

            Background =
                CancelButtonBackgroundBrush,

            Foreground =
                PrimaryTextBrush,

            BorderBrush =
                CancelButtonBorderBrush,

            BorderThickness =
                new Thickness(1)
        };

        revokeButton.Click +=
            RevokeButton_OnClick;

        existingCard.Children.Add(
            revokeButton);

        contentPanel.Children.Add(existingCard);

        // ===============================================
        // SCROLLBEREICH
        // ===============================================

        contentBorder.Child = contentPanel;

        scrollViewer.Content = contentBorder;

        Grid.SetRow(scrollViewer, 0);

        rootGrid.Children.Add(scrollViewer);

        // ===============================================
        // FUSSLEISTE
        // ===============================================

        var footer = new Border
        {
            Padding = new Thickness(24, 14, 24, 14),
            Background = CardBackgroundBrush,
            BorderBrush = BorderBrushValue,
            BorderThickness = new Thickness(0, 1, 0, 0)
        };

        var buttonPanel = new StackPanel
        {
            Orientation =
                Orientation.Horizontal,

            HorizontalAlignment =
                HorizontalAlignment.Right
        };

        var cancelButton = new Button
        {
            Content = "Abbrechen",

            MinWidth = 120,
            MinHeight = 38,

            Margin =
                new Thickness(0, 0, 10, 0),

            IsCancel = true,

            Background =
                CancelButtonBackgroundBrush,

            Foreground =
                PrimaryTextBrush,

            BorderBrush =
                CancelButtonBorderBrush,

            BorderThickness =
                new Thickness(1)
        };

        cancelButton.Click +=
            (_, _) => Close();

        var approveButton = new Button
        {
            Content =
                "Ausnahme dokumentieren",

            MinWidth = 195,
            MinHeight = 38,

            IsDefault = true
        };

        approveButton.SetResourceReference(
            FrameworkElement.StyleProperty,
            "AccentButtonStyle");

        approveButton.Click +=
            ApproveButton_OnClick;

        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(approveButton);

        footer.Child = buttonPanel;

        Grid.SetRow(footer, 1);

        rootGrid.Children.Add(footer);

        Content = rootGrid;
    }

    // =====================================================
    // PROGRAMMLISTE
    // =====================================================

    private void PopulateUpdates()
    {
        _updateSelector.Items.Clear();

        foreach (var update in
                 _programUpdateInformation.AvailableUpdates)
        {
            var alreadyApproved =
                _visit.ProgramUpdateExceptions.Any(
                    approval =>
                        approval.AppliesTo(
                            _visit.Id,
                            update));

            var itemPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            var programTitle = new TextBlock
            {
                Text = update.Name,

                FontWeight = FontWeights.SemiBold,

                Foreground = PrimaryTextBrush,

                TextWrapping = TextWrapping.Wrap
            };

            itemPanel.Children.Add(programTitle);

            var packageDescription = new TextBlock
            {
                Text =
                    update.PackageId
                    + " · "
                    + update.InstalledVersion
                    + " → "
                    + update.AvailableVersion,

                FontSize = 11,

                Foreground = SecondaryTextBrush,

                Margin = new Thickness(0, 4, 0, 0),

                TextWrapping = TextWrapping.Wrap
            };

            itemPanel.Children.Add(
                packageDescription);

            if (alreadyApproved)
            {
                var approvedText = new TextBlock
                {
                    Text = "Bereits dokumentiert",

                    FontSize = 11,

                    Foreground = MutedTextBrush,

                    Margin = new Thickness(0, 4, 0, 0)
                };

                itemPanel.Children.Add(
                    approvedText);
            }

            var item = new ListBoxItem
            {
                Content = itemPanel,

                Tag = update,

                Padding = new Thickness(10),

                Margin = new Thickness(0, 0, 0, 3),

                Background = InputBackgroundBrush,

                Foreground = PrimaryTextBrush,

                BorderBrush = BorderBrushValue,

                BorderThickness = new Thickness(0, 0, 0, 1),

                HorizontalContentAlignment =
                    HorizontalAlignment.Stretch
            };

            _updateSelector.Items.Add(item);
        }

        if (_updateSelector.Items.Count > 0)
        {
            _updateSelector.SelectedIndex = 0;
        }
    }

    // =====================================================
    // BESTEHENDE AUSNAHMEN
    // =====================================================

    private void PopulateExistingExceptions()
    {
        _existingExceptionsList.Items.Clear();

        var exceptions =
            _visit.ProgramUpdateExceptions
                .Where(approval =>
                    approval.CheckupVisitId
                        == _visit.Id)
                .OrderBy(approval =>
                    approval.PackageName,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (exceptions.Count == 0)
        {
            _existingExceptionsList.Items.Add(
                new ListBoxItem
                {
                    Content = new TextBlock
                    {
                        Text =
                            "Noch keine Ausnahmen "
                            + "dokumentiert.",

                        TextWrapping = TextWrapping.Wrap,

                        Foreground = MutedTextBrush
                    },

                    Padding = new Thickness(8),

                    IsEnabled = false,

                    Background = InputBackgroundBrush
                });

            return;
        }

        foreach (var approval in exceptions)
        {
            var description =
                approval.PackageName
                + " ("
                + approval.PackageId
                + ")"
                + Environment.NewLine
                + approval.InstalledVersion
                + " → "
                + approval.AvailableVersion
                + Environment.NewLine
                + "Begründung: "
                + approval.Reason;

            if (!string.IsNullOrWhiteSpace(
                    approval.TechnicianNote))
            {
                description +=
                    Environment.NewLine
                    + "Technikernotiz: "
                    + approval.TechnicianNote;
            }

            var item = new ListBoxItem
            {
                Content = new TextBlock
                {
                    Text = description,

                    TextWrapping = TextWrapping.Wrap,

                    Foreground = PrimaryTextBrush
                },

                Tag = approval,

                Padding = new Thickness(8),

                Background = InputBackgroundBrush
            };

            _existingExceptionsList.Items.Add(item);
        }

        if (_existingExceptionsList.Items.Count > 0)
        {
            _existingExceptionsList.SelectedIndex = 0;
        }
    }

    // =====================================================
    // AUSNAHME DOKUMENTIEREN
    // =====================================================

    private void ApproveButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_updateSelector.SelectedItem
                is not ListBoxItem selectedItem
            || selectedItem.Tag
                is not AvailableProgramUpdate update)
        {
            ShowValidationError(
                "Bitte zuerst eine erkannte "
                + "Programmaktualisierung auswählen.");

            return;
        }

        var reason =
            _reasonTextBox.Text.Trim();

        if (reason.Length < 10)
        {
            ShowValidationError(
                "Bitte eine aussagekräftige fachliche "
                + "Begründung mit mindestens "
                + "zehn Zeichen eingeben.");

            _reasonTextBox.Focus();

            return;
        }

        SelectedUpdate = update;

        Reason = reason;

        TechnicianNote =
            _technicianNoteTextBox.Text.Trim();

        ExceptionIdToRevoke = null;

        SelectedAction =
            ProgramUpdateExceptionDialogAction.Approve;

        DialogResult = true;
    }

    // =====================================================
    // AUSNAHME WIDERRUFEN
    // =====================================================

    private void RevokeButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_existingExceptionsList.SelectedItem
                is not ListBoxItem selectedItem
            || selectedItem.Tag
                is not ProgramUpdateException approval)
        {
            ShowValidationError(
                "Bitte zuerst eine dokumentierte "
                + "Ausnahme aus der Liste auswählen.");

            return;
        }

        var confirmation =
            MessageBox.Show(
                this,
                "Soll die dokumentierte Ausnahme für "
                + approval.PackageName
                + " wirklich aufgehoben werden?"
                + Environment.NewLine
                + Environment.NewLine
                + "Das betreffende Programm wird damit "
                + "wieder regulär als ausstehende "
                + "Aktualisierung berücksichtigt.",
                "Programmupdate-Ausnahme aufheben",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        SelectedUpdate = null;

        Reason = string.Empty;
        TechnicianNote = string.Empty;

        ExceptionIdToRevoke = approval.Id;

        SelectedAction =
            ProgramUpdateExceptionDialogAction.Revoke;

        DialogResult = true;
    }

    // =====================================================
    // DARSTELLUNGSHELFER
    // =====================================================

    private static StackPanel CreateCardPanel()
    {
        return new StackPanel
        {
            Background = CardBackgroundBrush,

            Margin = new Thickness(0),

            Orientation = Orientation.Vertical
        };
    }

    private static TextBlock CreateTitle(string text)
    {
        return new TextBlock
        {
            Text = text,

            FontSize = 20,

            FontWeight = FontWeights.SemiBold,

            TextWrapping = TextWrapping.Wrap,

            Margin = new Thickness(0, 0, 0, 6),

            Foreground = PrimaryTextBrush
        };
    }

    private static TextBlock CreateSectionTitle(
        string text)
    {
        return new TextBlock
        {
            Text = text,

            FontSize = 14,

            FontWeight = FontWeights.SemiBold,

            Margin = new Thickness(0),

            Foreground = PrimaryTextBrush
        };
    }

    private static TextBlock CreateDescription(
        string text)
    {
        return new TextBlock
        {
            Text = text,

            TextWrapping = TextWrapping.Wrap,

            Margin = new Thickness(0, 8, 0, 0),

            Foreground = SecondaryTextBrush
        };
    }

    private static void ApplyInputStyle(Control control)
    {
        control.Background = InputBackgroundBrush;

        control.Foreground = PrimaryTextBrush;

        control.BorderBrush = BorderBrushValue;

        control.BorderThickness = new Thickness(1);

        control.Padding = new Thickness(10, 8, 10, 8);
    }

    private void ShowValidationError(string message)
    {
        MessageBox.Show(
            this,
            message,
            "Angaben prüfen",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}

/// <summary>
/// Ergebnis einer dokumentierten
/// Programmupdate-Technikerentscheidung.
/// </summary>
public enum ProgramUpdateExceptionDialogAction
{
    None = 0,

    Approve = 1,

    Revoke = 2
}
