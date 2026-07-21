using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.Services.Tasks;

namespace WeberIT.Checkup.App.Views.Dialogs;

public partial class CleanupActionSelectionDialog :
    Window,
    INotifyPropertyChanged
{
    private readonly CheckupTask _task;

    private readonly CleanupPotentialInformation
        _cleanupInformation;

    private readonly ICleanupActionPlanBuilder
        _planBuilder;

    private string _selectionStatusText =
        "Noch keine Bereinigungskategorie ausgewählt.";

    public CleanupActionSelectionDialog(
        CheckupTask task,
        CleanupPotentialInformation cleanupInformation)
    {
        ArgumentNullException.ThrowIfNull(
            task);

        ArgumentNullException.ThrowIfNull(
            cleanupInformation);

        _task =
            task;

        _cleanupInformation =
            cleanupInformation;

        _planBuilder =
            new CleanupActionPlanBuilder();

        AvailableCategories =
            _planBuilder.GetSelectableCategories(
                cleanupInformation);

        if (AvailableCategories.Count == 0)
        {
            _selectionStatusText =
                "Im gespeicherten Checkup ist keine vollständig "
                + "gemessene und sicher freigegebene "
                + "Bereinigungskategorie enthalten.";
        }

        InitializeComponent();

        DataContext =
            this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<CleanupActionCategory>
        AvailableCategories
    { get; }

    public bool HasSelectableCategories =>
        AvailableCategories.Count > 0;

    public string SelectableCategoryCountText =>
        AvailableCategories.Count switch
        {
            0 =>
                "Keine sicher auswählbare Kategorie",

            1 =>
                "1 sicher auswählbare Kategorie",

            _ =>
                $"{AvailableCategories.Count} "
                + "sicher auswählbare Kategorien"
        };

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
        CategoriesListBox.SelectAll();
    }

    private void ClearSelectionButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        CategoriesListBox.UnselectAll();
    }

    private void CategoriesListBox_OnSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        var selectedCount =
            CategoriesListBox.SelectedItems.Count;

        PreviewPlanButton.IsEnabled =
            selectedCount > 0;

        SelectionStatusText =
            selectedCount switch
            {
                0 when AvailableCategories.Count == 0 =>
                    "Im gespeicherten Checkup ist keine "
                    + "vollständig gemessene und sicher "
                    + "freigegebene Bereinigungskategorie enthalten.",

                0 =>
                    "Noch keine Bereinigungskategorie ausgewählt.",

                1 =>
                    "Eine Bereinigungskategorie ist für "
                    + "die Planvorschau ausgewählt.",

                _ =>
                    $"{selectedCount} Bereinigungskategorien "
                    + "sind für die Planvorschau ausgewählt."
            };
    }

    private void PreviewPlanButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        CheckupTaskActionPlan plan;

        try
        {
            plan =
                BuildPlan();
        }
        catch (Exception exception)
        {
            ShowMessage(
                "Aktionsplan nicht erstellt",
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Der Bereinigungsplan konnte nicht "
                      + "sicher erstellt werden."
                    : exception.Message);

            return;
        }

        var previewDialog =
            new CleanupActionPlanPreviewDialog(
                plan)
            {
                Owner =
                    this
            };

        previewDialog.ShowDialog();
    }

    private CheckupTaskActionPlan BuildPlan()
    {
        var selections =
            CategoriesListBox
                .SelectedItems
                .Cast<CleanupActionCategory>()
                .Select(
                    category =>
                        new CleanupActionSelection
                        {
                            Category =
                                category.Category
                        })
                .ToList();

        return _planBuilder.Build(
            _task,
            _cleanupInformation,
            selections);
    }

    private void CloseButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }

    private void ShowMessage(
        string title,
        string message)
    {
        var dialog =
            new MessageDialog(
                title,
                message)
            {
                Owner =
                    this
            };

        dialog.ShowDialog();
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