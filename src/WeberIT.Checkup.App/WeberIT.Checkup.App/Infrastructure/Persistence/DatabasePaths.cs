using System.IO;

namespace WeberIT.Checkup.App.Infrastructure.Persistence;

public class DatabasePaths
{
    private const string DataDirectoryName = "Data";
    private const string DatabaseFileName = "weber-it-checkup.db";

    public string DataDirectoryPath { get; }

    public string DatabaseFilePath { get; }

    public DatabasePaths()
    {
        DataDirectoryPath = Path.Combine(
            AppContext.BaseDirectory,
            DataDirectoryName);

        DatabaseFilePath = Path.Combine(
            DataDirectoryPath,
            DatabaseFileName);
    }
}