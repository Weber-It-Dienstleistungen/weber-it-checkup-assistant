using System.IO;
using System.Windows;

namespace WeberIT.Checkup.App.Infrastructure.Persistence;

public class DatabaseInitializer
{
    private readonly DatabasePaths _databasePaths;
    private readonly DatabaseConnectionFactory _connectionFactory;

    public DatabaseInitializer(
        DatabasePaths databasePaths,
        DatabaseConnectionFactory connectionFactory)
    {
        _databasePaths = databasePaths;
        _connectionFactory = connectionFactory;
    }

    public void Initialize()
    {

        Directory.CreateDirectory(
            _databasePaths.DataDirectoryPath);

        using var connection =
            _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();

        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Customers
            (
                Id TEXT NOT NULL PRIMARY KEY,
                CustomerNumber TEXT NOT NULL UNIQUE,
                FirstName TEXT NOT NULL,
                LastName TEXT NOT NULL,
                Email TEXT NOT NULL,
                Phone TEXT NOT NULL,
                Street TEXT NOT NULL,
                PostalCode TEXT NOT NULL,
                City TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS CustomerDevices
            (
                Id TEXT NOT NULL PRIMARY KEY,
                CustomerId TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                ComputerName TEXT NOT NULL,
                Manufacturer TEXT NOT NULL,
                Model TEXT NOT NULL,
                SerialNumber TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NULL,
                ScanDate TEXT NULL,
                CheckupSessionJson TEXT NOT NULL,

                FOREIGN KEY (CustomerId)
                    REFERENCES Customers(Id)
                    ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS
                IX_CustomerDevices_CustomerId
            ON CustomerDevices(CustomerId);
            """;

        command.ExecuteNonQuery();
    }
}