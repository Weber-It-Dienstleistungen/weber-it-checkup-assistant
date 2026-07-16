using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;

namespace WeberIT.Checkup.App.Services.Scanners;

public class CleanupPotentialScanner :
    ICleanupPotentialScanner
{
    private readonly ICleanupPotentialProvider
        _cleanupPotentialProvider;

    public CleanupPotentialScanner(
        ICleanupPotentialProvider cleanupPotentialProvider)
    {
        _cleanupPotentialProvider =
            cleanupPotentialProvider;
    }

    public ScanResult<CleanupPotentialInformation> Scan(
        StorageInformation storageInformation)
    {
        try
        {
            var information =
                _cleanupPotentialProvider.Analyze(
                    storageInformation);

            return new ScanResult<CleanupPotentialInformation>
            {
                IsSuccessful =
                    information.AnalysisStatus
                        is CleanupMeasurementStatus.Measured
                        or CleanupMeasurementStatus.PartiallyMeasured,

                Message =
                    information.AnalysisMessage,

                Data =
                    information
            };
        }
        catch
        {
            var information =
                new CleanupPotentialInformation
                {
                    AnalysisDate =
                        DateTime.Now,

                    AnalysisStatus =
                        CleanupMeasurementStatus.NotEvaluable,

                    AnalysisMessage =
                        "Die Analyse des Bereinigungspotenzials "
                        + "konnte nicht ausgeführt werden."
                };

            return new ScanResult<CleanupPotentialInformation>
            {
                IsSuccessful =
                    false,

                Message =
                    information.AnalysisMessage,

                Data =
                    information
            };
        }
    }
}