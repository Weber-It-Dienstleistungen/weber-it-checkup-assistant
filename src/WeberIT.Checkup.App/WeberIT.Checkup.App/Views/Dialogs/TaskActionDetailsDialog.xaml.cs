using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Views.Dialogs;

public partial class TaskActionDetailsDialog : Window
{
    private readonly CheckupTaskActionDefinition
        _definition;

    private readonly IGuidedTaskActionLauncher
        _guidedTaskActionLauncher;

    private readonly bool
        _guidedLaunchAvailable;

    public TaskActionDetailsDialog(
        CheckupTaskActionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        _definition =
            definition;

        _guidedTaskActionLauncher =
            ResolveGuidedTaskActionLauncher();

        _guidedLaunchAvailable =
            definition.IsGuided
            && _guidedTaskActionLauncher.CanLaunch(
                definition.ActionCode);

        InitializeComponent();

        DataContext =
            definition;

        ApplyGuidedSupportState();
    }

    private static IGuidedTaskActionLauncher
        ResolveGuidedTaskActionLauncher()
    {
        var application =
            Application.Current as App;

        if (application is null)
        {
            throw new InvalidOperationException(
                "Der zentrale Anwendungsdienst ist für die "
                + "geführte Prüfung nicht verfügbar.");
        }

        return application.Services
            .GetRequiredService<
                IGuidedTaskActionLauncher>();
    }

    private void ApplyGuidedSupportState()
    {
        if (!_guidedLaunchAvailable)
        {
            PreparationOnlyNotice.Visibility =
                Visibility.Visible;

            GuidedSupportNotice.Visibility =
                Visibility.Collapsed;

            OpenGuidedViewButton.Visibility =
                Visibility.Collapsed;

            FooterStatusTextBlock.Text =
                "Noch keine Ausführung oder Bestätigung";

            return;
        }

        PreparationOnlyNotice.Visibility =
            Visibility.Collapsed;

        GuidedSupportNotice.Visibility =
            Visibility.Visible;

        OpenGuidedViewButton.Visibility =
            Visibility.Visible;

        FooterStatusTextBlock.Text =
            "Noch keine Prüfansicht geöffnet";

        var targetDescription =
            _guidedTaskActionLauncher
                .GetTargetDescription(
                    _definition.ActionCode);

        GuidedTargetDescriptionTextBlock.Text =
            "Vorgesehene Prüfansicht: "
            + targetDescription;

        OpenGuidedViewButton.ToolTip =
            targetDescription;
    }

    private void OpenGuidedViewButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (!_guidedLaunchAvailable)
        {
            return;
        }

        try
        {
            _guidedTaskActionLauncher.Launch(
                _definition.ActionCode);

            FooterStatusTextBlock.Text =
                "Prüfansicht geöffnet – keine Behebung dokumentiert";
        }
        catch (Exception exception)
        {
            FooterStatusTextBlock.Text =
                "Prüfansicht konnte nicht geöffnet werden";

            var dialog =
                new MessageDialog(
                    "Geführte Prüfung nicht geöffnet",
                    string.IsNullOrWhiteSpace(
                        exception.Message)
                        ? "Die zugehörige Windows-Prüfansicht "
                          + "konnte nicht geöffnet werden."
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
}