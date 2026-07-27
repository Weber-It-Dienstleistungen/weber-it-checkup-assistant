using WeberIT.Checkup.App.Models;

namespace WeberIT.Checkup.App.Services.Interfaces;

public interface IDiagnosticPdfReportService
{
    void Export(
        CheckupSession checkupSession,
        string filePath);
}