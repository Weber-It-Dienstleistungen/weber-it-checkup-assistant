using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Services.Interfaces;
using WeberIT.Checkup.App.Services.Storage;

namespace WeberIT.Checkup.App.Services.Scanners;

public class StorageInformationScanner : IStorageInformationScanner
{
    private readonly IStorageInformationProvider _storageInformationProvider;

    public StorageInformationScanner(IStorageInformationProvider storageInformationProvider)
    {
        _storageInformationProvider = storageInformationProvider;
    }

    public ScanResult<StorageInformation> Scan()
    {
        var storageInformation = new StorageInformation
        {
            PhysicalDrives = _storageInformationProvider.GetPhysicalDrives(),
            Volumes = _storageInformationProvider.GetVolumes()
        };

        return new ScanResult<StorageInformation>
        {
            IsSuccessful = true,
            Data = storageInformation
        };
    }
}