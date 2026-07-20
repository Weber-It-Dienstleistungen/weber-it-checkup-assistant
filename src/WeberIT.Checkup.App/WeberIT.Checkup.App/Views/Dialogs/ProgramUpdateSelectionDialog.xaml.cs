using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Tasks;

namespace WeberIT.Checkup.App.Views.Dialogs;

public partial class ProgramUpdateSelectionDialog :
    Window,
    INotifyPropertyChanged
{
    private readonly CheckupTask _task;
    private readonly ProgramUpdateInformation
        _programUpdateInformation;

    private string _selectionStatusText =
        "Noch kein Programmupdate ausgewählt.";

    public ProgramUpdateSelectionDialog(
        CheckupTask task,
        ProgramUpdateInformation programUpdateInformation)
    {
        ArgumentNullException.ThrowIfNull(
            task);

        ArgumentNullException.ThrowIfNull(
            programUpdateInformation);

        _task =
            task;

        _programUpdateInformation =
            programUpdateInformation;

        AvailableUpdates =
            programUpdateInformation
                .AvailableUpdates
                .OrderBy(
                    update =>
                        update.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(
                    update =>
                        update.PackageId,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        InitializeComponent();

        DataContext =
            this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<AvailableProgramUpdate>
        AvailableUpdates
    { get; }

    public string AvailableUpdateCountText =>
        AvailableUpdates.Count == 1
            ? "1 erkanntes Programmupdate"
            : $"{AvailableUpdates.Count} erkannte Programmupdates";

    public string SelectionStatusText
    {
        get =>
            _selectionStatusText;

        private set
        {
            if (_selectionStatusText == value)
            {
                return;
            }

            _selectionStatusText =
                value;

            OnPropertyChanged();
        }
    }

    private void SelectAllButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        UpdatesListBox.SelectAll();
    }

    private void ClearSelectionButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        UpdatesListBox.UnselectAll();
    }

    private void UpdatesListBox_OnSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        var selectedCount =
            UpdatesListBox.SelectedItems.Count;

        PreviewPlanButton.IsEnabled =
            selectedCount > 0;

        SelectionStatusText =
            selectedCount switch
            {
                0 =>
                    "Noch kein Programmupdate ausgewählt.",

                1 =>
                    "Ein Programmupdate ist für die "
                    + "Planvorschau ausgewählt.",

                _ =>
                    $"{selectedCount} Programmupdates sind "
                    + "für die Planvorschau ausgewählt."
            };
    }

    private void PreviewPlanButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            var selections =
                UpdatesListBox
                    .SelectedItems
                    .Cast<AvailableProgramUpdate>()
                    .Select(
                        update =>
                            new ProgramUpdateActionSelection
                            {
                                PackageId =
                                    update.PackageId,

                                Source =
                                    update.Source
                            })
                    .ToList();

            var planBuilder =
                new ProgramUpdateActionPlanBuilder();

            var plan =
                planBuilder.Build(
                    _task,
                    _programUpdateInformation,
                    selections);

            var previewDialog =
                new TaskActionPlanPreviewDialog(
                    plan)
                {
                    Owner =
                        this
                };

            previewDialog.ShowDialog();
        }
        catch (Exception exception)
        {
            var dialog =
                new MessageDialog(
                    "Aktionsplan nicht erstellt",
                    string.IsNullOrWhiteSpace(
                        exception.Message)
                        ? "Der Aktionsplan konnte nicht "
                          + "erstellt werden."
                        : exception.Message)
                {
                    Owner =
                        this
                };

            dialog.ShowDialog();
        }
    }

    private void CloseButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        Close();
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