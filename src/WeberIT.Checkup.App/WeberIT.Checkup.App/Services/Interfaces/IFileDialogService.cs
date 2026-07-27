namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IFileDialogService
{
    string? SelectPdfSavePath(
        string suggestedFileName);
}