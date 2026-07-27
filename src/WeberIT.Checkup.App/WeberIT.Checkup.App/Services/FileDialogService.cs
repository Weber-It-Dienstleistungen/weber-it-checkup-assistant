using Microsoft.Win32;
using System.Windows;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services;

public sealed class FileDialogService :
    IFileDialogService
{
    public string? SelectPdfSavePath(
        string suggestedFileName)
    {
        var normalizedFileName =
            string.IsNullOrWhiteSpace(
                suggestedFileName)
                ? "Weber-IT-Diagnosebericht.pdf"
                : suggestedFileName.Trim();

        var dialog =
            new SaveFileDialog
            {
                Title =
                    "Diagnosebericht als PDF speichern",

                Filter =
                    "PDF-Dokument (*.pdf)|*.pdf",

                DefaultExt =
                    ".pdf",

                AddExtension =
                    true,

                OverwritePrompt =
                    true,

                CheckPathExists =
                    true,

                ValidateNames =
                    true,

                FileName =
                    normalizedFileName,

                InitialDirectory =
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.MyDocuments)
            };

        var owner =
            Application.Current?.MainWindow;

        var result =
            owner is null
                ? dialog.ShowDialog()
                : dialog.ShowDialog(
                    owner);

        return result == true
            ? dialog.FileName
            : null;
    }
}