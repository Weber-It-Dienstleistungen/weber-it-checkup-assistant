using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Views.Dialogs;

public partial class CustomerCheckupCompletionDialog :
    Window,
    INotifyPropertyChanged
{
    private readonly CustomerCheckupComparison _comparison;

    private string _technicianSummary =
        string.Empty;

    private string _nextSteps =
        string.Empty;

    private DateTime? _nextCheckupDate;

    private string _validationMessage =
        string.Empty;

    private DatePicker? _nextCheckupDatePicker;

    public CustomerCheckupCompletionDialog(
        string deviceDisplayName,
        CustomerCheckupVisit customerCheckupVisit,
        CustomerCheckupComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(
            customerCheckupVisit);

        ArgumentNullException.ThrowIfNull(
            comparison);

        if (comparison.CustomerCheckupVisitId
            != customerCheckupVisit.Id)
        {
            throw new ArgumentException(
                "Der Vergleich gehört nicht zum übergebenen "
                + "Kundencheckup.",
                nameof(comparison));
        }

        _comparison =
            comparison;

        DeviceDisplayName =
            string.IsNullOrWhiteSpace(
                deviceDisplayName)
                ? "Ausgewähltes Kundengerät"
                : deviceDisplayName.Trim();

        TechnicianSummary =
            customerCheckupVisit.TechnicianSummary;

        NextSteps =
            customerCheckupVisit.NextSteps;

        NextCheckupDate =
            customerCheckupVisit.NextCheckupDate;

        AreaItems =
            BuildAreaItems(
                comparison);

        ImportantFindingItems =
            BuildImportantFindingItems(
                comparison);

        ComparisonNotes =
            (comparison.ComparisonNotes
             ?? new List<string>())
            .Where(
                note =>
                    !string.IsNullOrWhiteSpace(
                        note))
            .Select(
                note =>
                    note.Trim())
            .ToList();

        InitializeComponent();

        DataContext =
            this;

        Loaded +=
            CustomerCheckupCompletionDialog_OnLoaded;

        Closed +=
            CustomerCheckupCompletionDialog_OnClosed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CustomerCheckupCompletionDraft? CompletionDraft
    {
        get;
        private set;
    }

    public string DeviceDisplayName { get; }

    public IReadOnlyList<CustomerCheckupAreaPreviewItem>
        AreaItems
    { get; }

    public IReadOnlyList<CustomerCheckupFindingPreviewItem>
        ImportantFindingItems
    { get; }

    public IReadOnlyList<string> ComparisonNotes { get; }

    public bool HasImportantFindings =>
        ImportantFindingItems.Count > 0;

    public bool HasComparisonNotes =>
        ComparisonNotes.Count > 0;

    public string TechnicianSummary
    {
        get =>
            _technicianSummary;

        set
        {
            var normalizedValue =
                value
                ?? string.Empty;

            if (_technicianSummary
                == normalizedValue)
            {
                return;
            }

            _technicianSummary =
                normalizedValue;

            ValidationMessage =
                string.Empty;

            OnPropertyChanged();
        }
    }

    public string NextSteps
    {
        get =>
            _nextSteps;

        set
        {
            var normalizedValue =
                value
                ?? string.Empty;

            if (_nextSteps
                == normalizedValue)
            {
                return;
            }

            _nextSteps =
                normalizedValue;

            ValidationMessage =
                string.Empty;

            OnPropertyChanged();
        }
    }

    public DateTime? NextCheckupDate
    {
        get =>
            _nextCheckupDate;

        set
        {
            var normalizedValue =
                value?.Date;

            if (_nextCheckupDate
                == normalizedValue)
            {
                return;
            }

            _nextCheckupDate =
                normalizedValue;

            ValidationMessage =
                string.Empty;

            OnPropertyChanged();
        }
    }

    public string ValidationMessage
    {
        get =>
            _validationMessage;

        private set
        {
            if (_validationMessage
                == value)
            {
                return;
            }

            _validationMessage =
                value;

            OnPropertyChanged();
        }
    }

    public string BeforeScanDateText =>
        FormatDateTime(
            _comparison.BeforeScanDate);

    public string AfterScanDateText =>
        FormatDateTime(
            _comparison.AfterScanDate);

    public string ResolvedFindingCountText =>
        _comparison.ResolvedFindingCount
            .ToString();

    public string StillOpenFindingCountText =>
        _comparison.StillOpenFindingCount
            .ToString();

    public string NewlyDetectedFindingCountText =>
        _comparison.NewlyDetectedFindingCount
            .ToString();

    public string NotReevaluatableFindingCountText =>
        _comparison.NotReevaluatableFindingCount
            .ToString();

    public string SystemScoreText =>
        BuildScoreText(
            _comparison.SystemScore);

    public string SystemScoreReasonText =>
        BuildScoreReasonText(
            _comparison.SystemScore);

    public string HardwareScoreText =>
        BuildScoreText(
            _comparison.HardwareScore);

    public string HardwareScoreReasonText =>
        BuildScoreReasonText(
            _comparison.HardwareScore);

    public string ActionSummaryText =>
        BuildActionSummaryText(
            _comparison);

    public string RestartRequirementText =>
        _comparison.HasRestartRequirement
            ? "Mindestens eine technische Aktion meldet "
              + "Neustartbedarf."
            : "Keine dokumentierte technische Aktion meldet "
              + "Neustartbedarf.";

    private void CustomerCheckupCompletionDialog_OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -=
            CustomerCheckupCompletionDialog_OnLoaded;

        _nextCheckupDatePicker =
            FindVisualChild<DatePicker>(
                this);

        if (_nextCheckupDatePicker is null)
        {
            return;
        }

        _nextCheckupDatePicker.IsDropDownOpen =
            false;

        _nextCheckupDatePicker.ToolTip =
            "Datum in einem gut lesbaren Dialog auswählen";

        _nextCheckupDatePicker.PreviewMouseLeftButtonDown +=
            NextCheckupDatePicker_OnPreviewMouseLeftButtonDown;

        _nextCheckupDatePicker.PreviewKeyDown +=
            NextCheckupDatePicker_OnPreviewKeyDown;

        var datePickerTextBox =
            FindVisualChild<DatePickerTextBox>(
                _nextCheckupDatePicker);

        if (datePickerTextBox is not null)
        {
            datePickerTextBox.IsReadOnly =
                true;

            datePickerTextBox.Cursor =
                Cursors.Hand;

            datePickerTextBox.ToolTip =
                "Datum auswählen";
        }
    }

    private void CustomerCheckupCompletionDialog_OnClosed(
        object? sender,
        EventArgs e)
    {
        Closed -=
            CustomerCheckupCompletionDialog_OnClosed;

        if (_nextCheckupDatePicker is null)
        {
            return;
        }

        _nextCheckupDatePicker.PreviewMouseLeftButtonDown -=
            NextCheckupDatePicker_OnPreviewMouseLeftButtonDown;

        _nextCheckupDatePicker.PreviewKeyDown -=
            NextCheckupDatePicker_OnPreviewKeyDown;

        _nextCheckupDatePicker =
            null;
    }

    private void NextCheckupDatePicker_OnPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        e.Handled =
            true;

        if (sender is DatePicker datePicker)
        {
            datePicker.IsDropDownOpen =
                false;
        }

        ShowNextCheckupDateDialog();
    }

    private void NextCheckupDatePicker_OnPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        var shouldOpenDialog =
            e.Key == Key.Enter
            || e.Key == Key.Space
            || e.Key == Key.F4
            || (e.Key == Key.Down
                && Keyboard.Modifiers.HasFlag(
                    ModifierKeys.Alt));

        if (!shouldOpenDialog)
        {
            return;
        }

        e.Handled =
            true;

        if (sender is DatePicker datePicker)
        {
            datePicker.IsDropDownOpen =
                false;
        }

        ShowNextCheckupDateDialog();
    }

    private void ShowNextCheckupDateDialog()
    {
        var dialog =
            new CustomerCheckupDateInputDialog(
                NextCheckupDate,
                _comparison.AfterScanDate)
            {
                Owner =
                    this
            };

        var result =
            dialog.ShowDialog();

        if (result != true
            || !dialog.SelectedDate.HasValue)
        {
            return;
        }

        NextCheckupDate =
            dialog.SelectedDate.Value.Date;
    }

    private void SaveDraftButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        var validationMessage =
            ValidateInput();

        if (!string.IsNullOrWhiteSpace(
                validationMessage))
        {
            ValidationMessage =
                validationMessage;

            return;
        }

        CompletionDraft =
            new CustomerCheckupCompletionDraft
            {
                TechnicianSummary =
                    TechnicianSummary.Trim(),

                NextSteps =
                    NextSteps.Trim(),

                NextCheckupDate =
                    NextCheckupDate!.Value.Date
            };

        DialogResult =
            true;

        Close();
    }

    private string ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(
                TechnicianSummary))
        {
            return
                "Bitte erfassen Sie eine verständliche "
                + "Technikerzusammenfassung.";
        }

        if (string.IsNullOrWhiteSpace(
                NextSteps))
        {
            return
                "Bitte dokumentieren Sie die nächsten Schritte "
                + "für den Kunden.";
        }

        if (!NextCheckupDate.HasValue)
        {
            return
                "Bitte legen Sie den nächsten "
                + "Checkup-Termin fest.";
        }

        if (_comparison.AfterScanDate.HasValue
            && NextCheckupDate.Value.Date
                <= _comparison.AfterScanDate.Value.Date)
        {
            return
                "Der nächste Checkup-Termin muss nach dem "
                + "Nachher-Scan liegen.";
        }

        return
            string.Empty;
    }

    private static IReadOnlyList<CustomerCheckupAreaPreviewItem>
        BuildAreaItems(
            CustomerCheckupComparison comparison)
    {
        return (comparison.Areas
                ?? new List<CustomerCheckupAreaComparison>())
            .Select(
                area =>
                    new CustomerCheckupAreaPreviewItem
                    {
                        Title =
                            area.Title,

                        BeforeFindingText =
                            BuildFindingCountText(
                                area.BeforeActionableFindingCount),

                        AfterFindingText =
                            BuildFindingCountText(
                                area.AfterActionableFindingCount),

                        EvaluationText =
                            BuildAreaEvaluationText(
                                area),

                        Status =
                            area.Status,

                        StatusText =
                            GetAreaStatusText(
                                area.Status)
                    })
            .OrderBy(
                item =>
                    GetAreaStatusOrder(
                        item.Status))
            .ThenBy(
                item =>
                    item.Title,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<CustomerCheckupFindingPreviewItem>
        BuildImportantFindingItems(
            CustomerCheckupComparison comparison)
    {
        var areaTitles =
            (comparison.Areas
             ?? new List<CustomerCheckupAreaComparison>())
            .Where(
                area =>
                    !string.IsNullOrWhiteSpace(
                        area.AreaCode))
            .GroupBy(
                area =>
                    area.AreaCode,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group =>
                    group.Key,
                group =>
                    group.First().Title,
                StringComparer.OrdinalIgnoreCase);

        return (comparison.Findings
                ?? new List<CustomerCheckupFindingComparison>())
            .Where(
                finding =>
                    finding.Status
                    != CustomerCheckupFindingComparisonStatus
                        .Resolved)
            .Select(
                finding =>
                    new CustomerCheckupFindingPreviewItem
                    {
                        Title =
                            finding.Title,

                        AreaText =
                            areaTitles.TryGetValue(
                                finding.AreaCode,
                                out var areaTitle)
                                    ? areaTitle
                                    : finding.AreaCode,

                        SeverityText =
                            GetSeverityText(
                                finding.AfterSeverity
                                ?? finding.BeforeSeverity),

                        Status =
                            finding.Status,

                        StatusText =
                            GetFindingStatusText(
                                finding.Status)
                    })
            .OrderBy(
                item =>
                    GetFindingStatusOrder(
                        item.Status))
            .ThenBy(
                item =>
                    item.Title,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string BuildScoreText(
        CustomerCheckupScoreComparison score)
    {
        var beforeText =
            score.BeforeScore.HasValue
                ? score.BeforeScore.Value
                    .ToString()
                : "nicht verfügbar";

        var afterText =
            score.AfterScore.HasValue
                ? score.AfterScore.Value
                    .ToString()
                : "nicht verfügbar";

        if (!score.IsComparable
            || !score.Difference.HasValue)
        {
            return
                $"{beforeText} → {afterText} · "
                + "nicht direkt vergleichbar";
        }

        var differenceText =
            score.Difference.Value switch
            {
                > 0 =>
                    $"+{score.Difference.Value}",

                < 0 =>
                    score.Difference.Value.ToString(),

                _ =>
                    "±0"
            };

        return
            $"{beforeText} → {afterText} "
            + $"({differenceText})";
    }

    private static string BuildScoreReasonText(
        CustomerCheckupScoreComparison score)
    {
        return string.IsNullOrWhiteSpace(
                score.ComparisonReason)
            ? "Keine zusätzliche Einordnung verfügbar."
            : score.ComparisonReason.Trim();
    }

    private static string BuildActionSummaryText(
        CustomerCheckupComparison comparison)
    {
        var parts =
            new List<string>
            {
                comparison.SuccessfulActionCount == 1
                    ? "1 erfolgreiche Aktion"
                    : $"{comparison.SuccessfulActionCount} "
                      + "erfolgreiche Aktionen",

                comparison.FailedActionCount == 1
                    ? "1 fehlgeschlagene Aktion"
                    : $"{comparison.FailedActionCount} "
                      + "fehlgeschlagene Aktionen",

                comparison.CancelledActionCount == 1
                    ? "1 abgebrochene Aktion"
                    : $"{comparison.CancelledActionCount} "
                      + "abgebrochene Aktionen"
            };

        return string.Join(
            " · ",
            parts);
    }

    private static string BuildAreaEvaluationText(
        CustomerCheckupAreaComparison area)
    {
        if (area.WasBeforeEvaluable
            && area.IsAfterEvaluable)
        {
            return
                "Vorher und nachher auswertbar";
        }

        if (!area.WasBeforeEvaluable
            && !area.IsAfterEvaluable)
        {
            return
                "In beiden Scans nicht belastbar auswertbar";
        }

        return area.IsAfterEvaluable
            ? "Erst im Nachher-Scan belastbar auswertbar"
            : "Im Nachher-Scan nicht erneut auswertbar";
    }

    private static string BuildFindingCountText(
        int count)
    {
        return count switch
        {
            0 =>
                "Keine",

            1 =>
                "1 Befund",

            _ =>
                $"{count} Befunde"
        };
    }

    private static string GetAreaStatusText(
        CustomerCheckupAreaComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupAreaComparisonStatus.UnchangedHealthy =>
                "Unverändert in Ordnung",

            CustomerCheckupAreaComparisonStatus.Improved =>
                "Behoben",

            CustomerCheckupAreaComparisonStatus
                .ImprovedButStillNeedsAttention =>
                    "Verbessert, weiter prüfen",

            CustomerCheckupAreaComparisonStatus
                .UnchangedNeedsAttention =>
                    "Unverändert auffällig",

            CustomerCheckupAreaComparisonStatus.Worsened =>
                "Verschlechtert",

            CustomerCheckupAreaComparisonStatus
                .NewlyNeedsAttention =>
                    "Neu auffällig",

            _ =>
                "Nicht direkt vergleichbar"
        };
    }

    private static string GetFindingStatusText(
        CustomerCheckupFindingComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupFindingComparisonStatus.StillOpen =>
                "Weiterhin offen",

            CustomerCheckupFindingComparisonStatus.NewlyDetected =>
                "Neu erkannt",

            CustomerCheckupFindingComparisonStatus
                .NotReevaluatable =>
                    "Nicht erneut auswertbar",

            _ =>
                "Behoben"
        };
    }

    private static string GetSeverityText(
        FindingSeverity? severity)
    {
        return severity switch
        {
            FindingSeverity.Critical =>
                "Kritisch",

            FindingSeverity.Warning =>
                "Warnung",

            FindingSeverity.Recommendation =>
                "Empfehlung",

            FindingSeverity.Information =>
                "Information",

            _ =>
                "Nicht eingestuft"
        };
    }

    private static int GetAreaStatusOrder(
        CustomerCheckupAreaComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupAreaComparisonStatus.Worsened =>
                0,

            CustomerCheckupAreaComparisonStatus
                .NewlyNeedsAttention =>
                    1,

            CustomerCheckupAreaComparisonStatus
                .UnchangedNeedsAttention =>
                    2,

            CustomerCheckupAreaComparisonStatus
                .ImprovedButStillNeedsAttention =>
                    3,

            CustomerCheckupAreaComparisonStatus.NotComparable =>
                4,

            CustomerCheckupAreaComparisonStatus.Improved =>
                5,

            _ =>
                6
        };
    }

    private static int GetFindingStatusOrder(
        CustomerCheckupFindingComparisonStatus status)
    {
        return status switch
        {
            CustomerCheckupFindingComparisonStatus.NewlyDetected =>
                0,

            CustomerCheckupFindingComparisonStatus.StillOpen =>
                1,

            CustomerCheckupFindingComparisonStatus
                .NotReevaluatable =>
                    2,

            _ =>
                3
        };
    }

    private static string FormatDateTime(
        DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToString(
                "dd.MM.yyyy HH:mm")
              + " Uhr"
            : "Nicht verfügbar";
    }

    private static T? FindVisualChild<T>(
        DependencyObject parent)
        where T : DependencyObject
    {
        var childCount =
            VisualTreeHelper.GetChildrenCount(
                parent);

        for (var index = 0;
             index < childCount;
             index++)
        {
            var child =
                VisualTreeHelper.GetChild(
                    parent,
                    index);

            if (child is T matchingChild)
            {
                return matchingChild;
            }

            var nestedChild =
                FindVisualChild<T>(
                    child);

            if (nestedChild is not null)
            {
                return nestedChild;
            }
        }

        return null;
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}

public sealed class CustomerCheckupAreaPreviewItem
{
    public string Title { get; set; } =
        string.Empty;

    public string BeforeFindingText { get; set; } =
        string.Empty;

    public string AfterFindingText { get; set; } =
        string.Empty;

    public string EvaluationText { get; set; } =
        string.Empty;

    public CustomerCheckupAreaComparisonStatus Status { get; set; }

    public string StatusText { get; set; } =
        string.Empty;
}

public sealed class CustomerCheckupFindingPreviewItem
{
    public string Title { get; set; } =
        string.Empty;

    public string AreaText { get; set; } =
        string.Empty;

    public string SeverityText { get; set; } =
        string.Empty;

    public CustomerCheckupFindingComparisonStatus Status { get; set; }

    public string StatusText { get; set; } =
        string.Empty;
}

internal sealed class CustomerCheckupDateInputDialog :
    Window
{
    private static readonly CultureInfo GermanCulture =
        CultureInfo.GetCultureInfo(
            "de-DE");

    private static readonly string[] SupportedDateFormats =
    {
        "d.M.yyyy",
        "dd.MM.yyyy",
        "d.MM.yyyy",
        "dd.M.yyyy"
    };

    private readonly DateTime? _minimumExclusiveDate;
    private readonly TextBox _dateTextBox;
    private readonly TextBlock _validationTextBlock;

    public CustomerCheckupDateInputDialog(
        DateTime? selectedDate,
        DateTime? minimumExclusiveDate)
    {
        _minimumExclusiveDate =
            minimumExclusiveDate?.Date;

        Title =
            "Nächsten Checkup-Termin auswählen";

        Width =
            520;

        Height =
            330;

        MinWidth =
            520;

        MinHeight =
            330;

        WindowStartupLocation =
            WindowStartupLocation.CenterOwner;

        ResizeMode =
            ResizeMode.NoResize;

        ShowInTaskbar =
            false;

        Background =
            FindBrush(
                "BackgroundBrush",
                new SolidColorBrush(
                    Color.FromRgb(
                        15,
                        23,
                        42)));

        Foreground =
            FindBrush(
                "TextPrimaryBrush",
                Brushes.White);

        var surfaceBrush =
            FindBrush(
                "SurfaceBrush",
                new SolidColorBrush(
                    Color.FromRgb(
                        24,
                        34,
                        53)));

        var surfaceSecondaryBrush =
            FindBrush(
                "SurfaceSecondaryBrush",
                new SolidColorBrush(
                    Color.FromRgb(
                        32,
                        44,
                        64)));

        var textPrimaryBrush =
            FindBrush(
                "TextPrimaryBrush",
                Brushes.White);

        var textSecondaryBrush =
            FindBrush(
                "TextSecondaryBrush",
                new SolidColorBrush(
                    Color.FromRgb(
                        148,
                        163,
                        184)));

        var borderBrush =
            FindBrush(
                "BorderBrush",
                new SolidColorBrush(
                    Color.FromRgb(
                        51,
                        65,
                        85)));

        var accentBrush =
            FindBrush(
                "AccentBrush",
                Brushes.DodgerBlue);

        var dangerBrush =
            FindBrush(
                "DangerBrush",
                Brushes.IndianRed);

        var rootGrid =
            new Grid
            {
                Background =
                    Background
            };

        rootGrid.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        rootGrid.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        rootGrid.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
            });

        var headerBorder =
            new Border
            {
                Padding =
                    new Thickness(
                        24,
                        20,
                        24,
                        18),

                Background =
                    surfaceBrush,

                BorderBrush =
                    borderBrush,

                BorderThickness =
                    new Thickness(
                        0,
                        0,
                        0,
                        1)
            };

        var headerPanel =
            new StackPanel();

        headerPanel.Children.Add(
            new TextBlock
            {
                Text =
                    "Nächsten Checkup-Termin auswählen",

                FontSize =
                    20,

                FontWeight =
                    FontWeights.SemiBold,

                Foreground =
                    textPrimaryBrush
            });

        headerPanel.Children.Add(
            new TextBlock
            {
                Margin =
                    new Thickness(
                        0,
                        5,
                        0,
                        0),

                Text =
                    "Das Datum wird im Format TT.MM.JJJJ eingegeben.",

                Foreground =
                    textSecondaryBrush,

                TextWrapping =
                    TextWrapping.Wrap
            });

        headerBorder.Child =
            headerPanel;

        Grid.SetRow(
            headerBorder,
            0);

        rootGrid.Children.Add(
            headerBorder);

        var contentPanel =
            new StackPanel
            {
                Margin =
                    new Thickness(
                        24,
                        22,
                        24,
                        18)
            };

        contentPanel.Children.Add(
            new TextBlock
            {
                Text =
                    "Nächster Checkup-Termin",

                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        7),

                FontWeight =
                    FontWeights.SemiBold,

                Foreground =
                    textPrimaryBrush
            });

        _dateTextBox =
            new TextBox
            {
                Height =
                    42,

                Padding =
                    new Thickness(
                        12,
                        0,
                        12,
                        0),

                VerticalContentAlignment =
                    VerticalAlignment.Center,

                HorizontalContentAlignment =
                    HorizontalAlignment.Center,

                FontSize =
                    16,

                FontWeight =
                    FontWeights.SemiBold,

                MaxLength =
                    10,

                Background =
                    surfaceSecondaryBrush,

                Foreground =
                    textPrimaryBrush,

                CaretBrush =
                    textPrimaryBrush,

                SelectionBrush =
                    accentBrush,

                BorderBrush =
                    borderBrush,

                BorderThickness =
                    new Thickness(
                        1),

                Text =
                    selectedDate.HasValue
                        ? selectedDate.Value.ToString(
                            "dd.MM.yyyy",
                            GermanCulture)
                        : string.Empty
            };

        _dateTextBox.KeyDown +=
            DateTextBox_OnKeyDown;

        contentPanel.Children.Add(
            _dateTextBox);

        var helperGrid =
            new Grid
            {
                Margin =
                    new Thickness(
                        0,
                        8,
                        0,
                        0)
            };

        helperGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        helperGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });

        helperGrid.Children.Add(
            new TextBlock
            {
                Text =
                    "Beispiel: 29.07.2027",

                Foreground =
                    textSecondaryBrush,

                FontSize =
                    11,

                VerticalAlignment =
                    VerticalAlignment.Center
            });

        var quickDateButton =
            CreateButton(
                "12 Monate nach Kontrollscan",
                false,
                surfaceSecondaryBrush,
                textPrimaryBrush,
                borderBrush,
                accentBrush);

        quickDateButton.Margin =
            new Thickness(
                12,
                0,
                0,
                0);

        quickDateButton.Click +=
            QuickDateButton_OnClick;

        Grid.SetColumn(
            quickDateButton,
            1);

        helperGrid.Children.Add(
            quickDateButton);

        contentPanel.Children.Add(
            helperGrid);

        _validationTextBlock =
            new TextBlock
            {
                Margin =
                    new Thickness(
                        0,
                        14,
                        0,
                        0),

                Foreground =
                    dangerBrush,

                TextWrapping =
                    TextWrapping.Wrap,

                Visibility =
                    Visibility.Collapsed
            };

        contentPanel.Children.Add(
            _validationTextBlock);

        Grid.SetRow(
            contentPanel,
            1);

        rootGrid.Children.Add(
            contentPanel);

        var footerBorder =
            new Border
            {
                Padding =
                    new Thickness(
                        24,
                        16,
                        24,
                        16),

                Background =
                    surfaceBrush,

                BorderBrush =
                    borderBrush,

                BorderThickness =
                    new Thickness(
                        0,
                        1,
                        0,
                        0)
            };

        var footerPanel =
            new StackPanel
            {
                Orientation =
                    Orientation.Horizontal,

                HorizontalAlignment =
                    HorizontalAlignment.Right
            };

        var cancelButton =
            CreateButton(
                "Abbrechen",
                false,
                surfaceSecondaryBrush,
                textPrimaryBrush,
                borderBrush,
                accentBrush);

        cancelButton.Width =
            120;

        cancelButton.IsCancel =
            true;

        cancelButton.Click +=
            CancelButton_OnClick;

        var acceptButton =
            CreateButton(
                "Übernehmen",
                true,
                surfaceSecondaryBrush,
                textPrimaryBrush,
                borderBrush,
                accentBrush);

        acceptButton.Width =
            130;

        acceptButton.Margin =
            new Thickness(
                12,
                0,
                0,
                0);

        acceptButton.IsDefault =
            true;

        acceptButton.Click +=
            AcceptButton_OnClick;

        footerPanel.Children.Add(
            cancelButton);

        footerPanel.Children.Add(
            acceptButton);

        footerBorder.Child =
            footerPanel;

        Grid.SetRow(
            footerBorder,
            2);

        rootGrid.Children.Add(
            footerBorder);

        Content =
            rootGrid;

        Loaded +=
            CustomerCheckupDateInputDialog_OnLoaded;
    }

    public DateTime? SelectedDate
    {
        get;
        private set;
    }

    private void CustomerCheckupDateInputDialog_OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -=
            CustomerCheckupDateInputDialog_OnLoaded;

        _dateTextBox.Focus();

        _dateTextBox.SelectAll();
    }

    private void DateTextBox_OnKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled =
            true;

        TryAcceptDate();
    }

    private void QuickDateButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        var referenceDate =
            _minimumExclusiveDate
            ?? DateTime.Today;

        _dateTextBox.Text =
            referenceDate
                .AddYears(
                    1)
                .ToString(
                    "dd.MM.yyyy",
                    GermanCulture);

        _validationTextBlock.Visibility =
            Visibility.Collapsed;

        _validationTextBlock.Text =
            string.Empty;

        _dateTextBox.Focus();

        _dateTextBox.SelectAll();
    }

    private void CancelButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult =
            false;

        Close();
    }

    private void AcceptButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        TryAcceptDate();
    }

    private void TryAcceptDate()
    {
        if (!DateTime.TryParseExact(
                _dateTextBox.Text.Trim(),
                SupportedDateFormats,
                GermanCulture,
                DateTimeStyles.None,
                out var parsedDate))
        {
            ShowValidationMessage(
                "Bitte geben Sie ein gültiges Datum im "
                + "Format TT.MM.JJJJ ein.");

            return;
        }

        parsedDate =
            parsedDate.Date;

        if (_minimumExclusiveDate.HasValue
            && parsedDate
                <= _minimumExclusiveDate.Value)
        {
            ShowValidationMessage(
                "Der nächste Checkup-Termin muss nach dem "
                + "Nachher-Scan vom "
                + _minimumExclusiveDate.Value.ToString(
                    "dd.MM.yyyy",
                    GermanCulture)
                + " liegen.");

            return;
        }

        SelectedDate =
            parsedDate;

        DialogResult =
            true;

        Close();
    }

    private void ShowValidationMessage(
        string message)
    {
        _validationTextBlock.Text =
            message;

        _validationTextBlock.Visibility =
            Visibility.Visible;

        _dateTextBox.Focus();

        _dateTextBox.SelectAll();
    }

    private static Button CreateButton(
        string text,
        bool isAccentButton,
        Brush secondaryBackground,
        Brush textBrush,
        Brush borderBrush,
        Brush accentBrush)
    {
        return new Button
        {
            Content =
                text,

            MinWidth =
                100,

            Height =
                38,

            Padding =
                new Thickness(
                    14,
                    0,
                    14,
                    0),

            Background =
                isAccentButton
                    ? accentBrush
                    : secondaryBackground,

            Foreground =
                isAccentButton
                    ? Brushes.White
                    : textBrush,

            BorderBrush =
                isAccentButton
                    ? accentBrush
                    : borderBrush,

            BorderThickness =
                new Thickness(
                    1),

            FontWeight =
                FontWeights.SemiBold,

            Cursor =
                Cursors.Hand
        };
    }

    private static Brush FindBrush(
        string resourceKey,
        Brush fallbackBrush)
    {
        return Application.Current?
                   .TryFindResource(
                       resourceKey)
               as Brush
               ?? fallbackBrush;
    }
}