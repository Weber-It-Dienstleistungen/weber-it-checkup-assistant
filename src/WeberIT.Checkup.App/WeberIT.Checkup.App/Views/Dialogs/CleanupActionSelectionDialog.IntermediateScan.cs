
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Cleanup;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.ViewModels;

namespace WeberIT.Checkup.App.Views.Dialogs;

/// <summary>
/// Sprint 55.14.34f
///
/// Ergänzt den bestehenden Bereinigungsdialog
/// um eine gezielte, lesende Zwischenprüfung.
///
/// Der bisherige Bereinigungsablauf
/// bleibt unverändert.
///
/// Es wird ausschließlich die ausgewählte
/// Bereinigungskategorie neu gemessen.
///
/// Die ursprüngliche Eingangsmessung bleibt
/// unverändert und wird als Vergleichsbasis
/// verwendet.
///
/// Technische Löschaktionen werden in diesem
/// Schritt noch nicht automatisch zugeordnet.
/// </summary>
public partial class CleanupActionSelectionDialog
{
    private bool _cleanupIntermediateInitialized;

    private bool _cleanupIntermediateRunning;

    private ICheckupTaskActionExecutionCoordinator?
        _cleanupIntermediateCoordinator;

    // =============================================
    // DIALOG INITIALISIEREN
    // =============================================

    protected override void OnContentRendered(
        EventArgs e)
    {
        base.OnContentRendered(e);

        if (_cleanupIntermediateInitialized)
        {
            return;
        }

        _cleanupIntermediateInitialized = true;

        RefreshCleanupButton.Click +=
            RefreshCleanupButton_OnClick;

        CategoriesListBox.SelectionChanged +=
            IntermediateCategorySelectionChanged;

        if (Application.Current is App application)
        {
            _cleanupIntermediateCoordinator =
                application.Services.GetRequiredService<
                    ICheckupTaskActionExecutionCoordinator>();

            _cleanupIntermediateCoordinator.StateChanged +=
                CleanupIntermediateCoordinator_OnStateChanged;
        }

        UpdateIntermediateControls();

        UpdateIntermediateResultForSelection();
    }

    // =============================================
    // WÄHREND EINER MESSUNG NICHT SCHLIESSEN
    // =============================================

    protected override void OnClosing(
        CancelEventArgs e)
    {
        if (_cleanupIntermediateRunning)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(
        EventArgs e)
    {
        if (_cleanupIntermediateCoordinator is not null)
        {
            _cleanupIntermediateCoordinator.StateChanged -=
                CleanupIntermediateCoordinator_OnStateChanged;
        }

        base.OnClosed(e);
    }

    // =============================================
    // KUNDENKONTEXT PRÜFEN
    // =============================================

    private bool TryResolveCleanupIntermediateContext(
        out Customer customer,
        out CustomerDevice device,
        out CustomerCheckupVisit visit)
    {
        customer = null!;
        device = null!;
        visit = null!;

        if (Owner?.DataContext
            is not CustomerDevicesViewModel viewModel)
        {
            return false;
        }

        var selectedCustomer =
            viewModel.SelectedCustomer;

        var selectedDevice =
            viewModel.SelectedDevice;

        if (selectedCustomer is null
            || selectedDevice is null
            || viewModel.IsScanRunning)
        {
            return false;
        }

        if (!selectedCustomer.Devices.Any(
                storedDevice =>
                    ReferenceEquals(
                        storedDevice,
                        selectedDevice)))
        {
            return false;
        }

        var session =
            selectedDevice.CheckupSession;

        /*
         * Der geöffnete Dialog muss weiterhin
         * zu genau der Aufgabenliste und der
         * Bereinigungsanalyse gehören, aus
         * denen er ursprünglich geöffnet wurde.
         */
        if (!ReferenceEquals(
                session.TaskList,
                _taskList))
        {
            return false;
        }

        if (!ReferenceEquals(
                session.CleanupPotentialInformation,
                _cleanupInformation))
        {
            return false;
        }

        if (!_taskList.Tasks.Any(
                storedTask =>
                    ReferenceEquals(
                        storedTask,
                        _task)))
        {
            return false;
        }

        var currentVisit =
            session.CurrentCustomerCheckupVisit;

        if (currentVisit is null
            || !currentVisit.IsInProgress
            || currentVisit.IsCompletionPrepared)
        {
            return false;
        }

        if (!session.CustomerCheckupVisits.Any(
                storedVisit =>
                    ReferenceEquals(
                        storedVisit,
                        currentVisit)))
        {
            return false;
        }

        customer =
            selectedCustomer;

        device =
            selectedDevice;

        visit =
            currentVisit;

        return true;
    }

    // =============================================
    // UNTERSTÜTZTE KATEGORIEN
    // =============================================

    private static bool IsIntermediateCategorySupported(
        CleanupCategoryType category)
    {
        return category is
            CleanupCategoryType.UserTemporaryFiles
            or CleanupCategoryType.WindowsTemporaryFiles
            or CleanupCategoryType.DirectXShaderCache
            or CleanupCategoryType.ThumbnailCache
            or CleanupCategoryType.BrowserCache;
    }

    // =============================================
    // AUSGEWÄHLTE KATEGORIE SICHER ERMITTELN
    // =============================================

    private bool TryGetSelectedIntermediateCategory(
        out CleanupCategoryType category)
    {
        category = default;

        if (CategoriesListBox.SelectedItem
            is not CleanupActionCategory selectedCategory)
        {
            return false;
        }

        /*
         * SCHRITT 55.14.34f – KORREKTUR
         *
         * CleanupActionCategory.Category
         * besitzt den Typ CleanupCategoryType?.
         *
         * Ein fehlender Kategoriecode darf
         * weder implizit umgewandelt noch
         * als gültige Auswahl behandelt werden.
         *
         * Das Pattern liefert nur dann
         * einen konkreten CleanupCategoryType,
         * wenn tatsächlich ein Wert vorhanden ist.
         */
        if (selectedCategory.Category
            is not CleanupCategoryType selectedType)
        {
            return false;
        }

        if (!IsIntermediateCategorySupported(
                selectedType))
        {
            return false;
        }

        category =
            selectedType;

        return true;
    }

    // =============================================
    // ZWISCHENPRÜFUNG AUSFÜHREN
    // =============================================

    private async void RefreshCleanupButton_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        if (_cleanupIntermediateRunning)
        {
            return;
        }

        if (!TryGetSelectedIntermediateCategory(
                out var category))
        {
            ShowMessage(
                "Zwischenprüfung nicht verfügbar",
                "Bitte wählen Sie zuerst eine "
                + "unterstützte Bereinigungskategorie.");

            return;
        }

        if (!TryResolveCleanupIntermediateContext(
                out var customer,
                out var device,
                out _))
        {
            ShowMessage(
                "Zwischenprüfung nicht verfügbar",
                "Der aktuelle Kundencheckup konnte "
                + "nicht eindeutig zugeordnet werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Die Zwischenprüfung ist nur "
                + "während eines laufenden, noch "
                + "nicht zum Abschluss vorbereiteten "
                + "Kundencheckups möglich.");

            return;
        }

        _cleanupIntermediateRunning = true;

        IntermediateScanStatusTextBlock.Text =
            "Die ausgewählte Bereinigungskategorie "
            + "wird erneut gemessen."
            + Environment.NewLine
            + Environment.NewLine
            + "Die Prüfung verändert keine Dateien. "
            + "Der bisherige Eingangsscan "
            + "bleibt erhalten.";

        UpdateIntermediateControls();

        try
        {
            var application =
                Application.Current as App
                ?? throw new InvalidOperationException(
                    "Der zentrale Anwendungsdienst "
                    + "ist nicht verfügbar.");

            var service =
                application.Services.GetRequiredService<
                    CleanupPotentialIntermediateScanService>();

            var result =
                await service.RunAsync(
                    customer,
                    device,
                    category);

            /*
             * Der Dienst sichert den Zwischenbefund
             * in einer unabhängigen Vorgangskopie.
             *
             * Nach dem Speichern muss der aktuelle
             * Kundencheckup deshalb neu aufgelöst
             * werden.
             *
             * SCHRITT 55.14.34f – KORREKTUR
             *
             * Die Methode besitzt genau drei
             * out-Parameter:
             *
             * Customer, CustomerDevice und Visit.
             */
            if (!TryResolveCleanupIntermediateContext(
                    out _,
                    out _,
                    out var updatedVisit))
            {
                IntermediateScanStatusTextBlock.Text =
                    "Die Zwischenprüfung wurde ausgeführt, "
                    + "der aktuelle Kundencheckup konnte "
                    + "anschließend jedoch nicht mehr "
                    + "eindeutig zugeordnet werden."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Bitte prüfen Sie den gespeicherten "
                    + "Kundencheckup, bevor Sie fortfahren.";

                return;
            }

            var storedResult =
                updatedVisit.CleanupPotentialIntermediateScans
                    .FirstOrDefault(
                        scan =>
                            scan.Id == result.Id
                            && scan.CustomerCheckupVisitId
                                == updatedVisit.Id
                            && scan.Category == category);

            if (storedResult is null)
            {
                IntermediateScanStatusTextBlock.Text =
                    "Der Messdienst hat ein Ergebnis "
                    + "zurückgegeben, das im aktuellen "
                    + "Kundencheckup nicht eindeutig "
                    + "wiedergefunden werden konnte."
                    + Environment.NewLine
                    + Environment.NewLine
                    + "Die Messung wird daher nicht "
                    + "als bestätigter Zwischenstand "
                    + "angezeigt.";

                return;
            }

            ShowIntermediateResult(
                storedResult);
        }
        catch (Exception exception)
        {
            IntermediateScanStatusTextBlock.Text =
                "Die Zwischenprüfung konnte nicht "
                + "erfolgreich abgeschlossen werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Die bisherigen Eingangswerte "
                + "bleiben unverändert."
                + Environment.NewLine
                + Environment.NewLine
                + "Ein neuer Messstand wurde nicht "
                + "als bestätigt angezeigt.";

            ShowMessage(
                "Bereinigungs-Zwischenprüfung fehlgeschlagen",
                string.IsNullOrWhiteSpace(
                    exception.Message)
                    ? "Die Zwischenprüfung konnte "
                      + "nicht ausgeführt werden."
                    : exception.Message);
        }
        finally
        {
            _cleanupIntermediateRunning = false;

            UpdateIntermediateControls();
        }
    }

    // =============================================
    // GESPEICHERTE MESSUNG ANZEIGEN
    // =============================================

    private void UpdateIntermediateResultForSelection()
    {
        if (_cleanupIntermediateRunning)
        {
            return;
        }

        if (!TryGetSelectedIntermediateCategory(
                out var category))
        {
            IntermediateScanStatusTextBlock.Text =
                "Wählen Sie eine Bereinigungskategorie, "
                + "um vorhandene Zwischenprüfungen "
                + "anzuzeigen oder eine neue Messung "
                + "durchzuführen.";

            return;
        }

        if (!TryResolveCleanupIntermediateContext(
                out _,
                out _,
                out var visit))
        {
            IntermediateScanStatusTextBlock.Text =
                "Für die ausgewählte Kategorie "
                + "ist hier keine neue Messung verfügbar."
                + Environment.NewLine
                + Environment.NewLine
                + "Eine Zwischenprüfung benötigt "
                + "einen laufenden Kundencheckup "
                + "mit eindeutiger Gerätezuordnung.";

            return;
        }

        var latestScan =
            visit.CleanupPotentialIntermediateScans
                .Where(
                    scan =>
                        scan.CustomerCheckupVisitId
                            == visit.Id
                        && scan.Category == category)
                .OrderByDescending(
                    scan => scan.RecordedAt)
                .FirstOrDefault();

        if (latestScan is null)
        {
            IntermediateScanStatusTextBlock.Text =
                "Für diese Kategorie wurde noch "
                + "keine Zwischenprüfung gespeichert."
                + Environment.NewLine
                + Environment.NewLine
                + "Die oben angezeigten Größen "
                + "stammen weiterhin aus dem "
                + "ursprünglichen Eingangsscan.";

            return;
        }

        ShowIntermediateResult(
            latestScan);
    }

    // =============================================
    // VORHER-/NACHHER-ERGEBNIS
    // =============================================

    private void ShowIntermediateResult(
        CleanupPotentialIntermediateScan scan)
    {
        var before =
            scan.BeforeMeasurement;

        var after =
            scan.AfterMeasurement;

        var categoryName =
            before.CategoryText;

        var header =
            categoryName
            + " – Zwischenprüfung vom "
            + scan.RecordedAt.ToString(
                "dd.MM.yyyy HH:mm")
            + " Uhr";

        var beforeText =
            "Eingangsscan: "
            + before.SizeText
            + ", "
            + before.FileCountText;

        var afterText =
            "Erneute Messung: "
            + after.SizeText
            + ", "
            + after.FileCountText;

        if (!scan.IsComparisonReliable)
        {
            IntermediateScanStatusTextBlock.Text =
                header
                + Environment.NewLine
                + Environment.NewLine
                + beforeText
                + Environment.NewLine
                + afterText
                + Environment.NewLine
                + Environment.NewLine
                + "Die Messwerte sind nicht vollständig "
                + "vergleichbar. Eine bestätigte "
                + "Größenänderung kann deshalb "
                + "nicht ausgewiesen werden."
                + Environment.NewLine
                + Environment.NewLine
                + "Messstatus: "
                + after.MeasurementStatusText;

            return;
        }

        IntermediateScanStatusTextBlock.Text =
            header
            + Environment.NewLine
            + Environment.NewLine
            + beforeText
            + Environment.NewLine
            + afterText
            + Environment.NewLine
            + Environment.NewLine
            + scan.ComparisonSummary
            + Environment.NewLine
            + Environment.NewLine
            + "Hinweis: Die Zwischenprüfung "
            + "ist eine eigenständige Messung. "
            + "Eine technische Löschaktion "
            + "wird hier noch nicht automatisch "
            + "zugeordnet.";
    }

    // =============================================
    // AUSWAHL GEÄNDERT
    // =============================================

    private void IntermediateCategorySelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateIntermediateControls();

        UpdateIntermediateResultForSelection();
    }

    // =============================================
    // SCHALTFLÄCHEN AKTUALISIEREN
    // =============================================

    private void UpdateIntermediateControls()
    {
        if (!_cleanupIntermediateInitialized)
        {
            return;
        }

        var hasCustomerContext =
            TryResolveCleanupIntermediateContext(
                out _,
                out _,
                out _);

        var hasSupportedSelection =
            TryGetSelectedIntermediateCategory(
                out _);

        var otherExecutionRunning =
            _cleanupIntermediateCoordinator?
                .IsExecutionRunning
            ?? false;

        RefreshCleanupButton.Visibility =
            hasCustomerContext
                ? Visibility.Visible
                : Visibility.Collapsed;

        RefreshCleanupButton.IsEnabled =
            hasCustomerContext
            && hasSupportedSelection
            && !_cleanupIntermediateRunning
            && !otherExecutionRunning;

        RefreshCleanupButton.Content =
            _cleanupIntermediateRunning
                ? "Kontrollmessung läuft …"
                : "Kategorie erneut prüfen";

        CategoriesListBox.IsEnabled =
            !_cleanupIntermediateRunning;

        /*
         * Die bisherige Bereinigung und
         * die neue Zwischenprüfung dürfen
         * nicht gleichzeitig gestartet werden.
         *
         * Die bestehende Planlogik und
         * die ursprüngliche Kategorieauswahl
         * bleiben unverändert.
         */
        PreviewPlanButton.IsEnabled =
            !_cleanupIntermediateRunning
            && !otherExecutionRunning
            && CategoriesListBox.SelectedItems.Count > 0;
    }

    // =============================================
    // AKTIVE SYSTEMAKTIONEN BEOBACHTEN
    // =============================================

    private void CleanupIntermediateCoordinator_OnStateChanged(
        object? sender,
        EventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            UpdateIntermediateControls();

            return;
        }

        Dispatcher.BeginInvoke(
            new Action(
                UpdateIntermediateControls));
    }
}
