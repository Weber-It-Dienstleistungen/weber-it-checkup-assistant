using Microsoft.Data.Sqlite;

namespace WeberIT.Checkup.App.Infrastructure.Persistence;

public class DatabaseConnectionFactory
{
    private readonly DatabasePaths _databasePaths;

    public DatabaseConnectionFactory(DatabasePaths databasePaths)
    {
        _databasePaths = databasePaths;
    }

    public SqliteConnection CreateConnection()
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePaths.DatabaseFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        return new SqliteConnection(
            connectionStringBuilder.ToString());
    }

    public SqliteConnection CreateOpenConnection()
    {
        var connection = CreateConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();

        return connection;
    }
}