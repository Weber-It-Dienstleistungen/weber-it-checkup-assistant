using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using WeberIT.Checkup.App.Infrastructure.Persistence;
using WeberIT.Checkup.App.Models;
using WeberIT.Checkup.App.Repositories.Interfaces;

namespace WeberIT.Checkup.App.Repositories;

public class SQLiteCustomerRepository : ICustomerRepository
{
    private readonly DatabaseConnectionFactory _connectionFactory;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public SQLiteCustomerRepository(
        DatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;

        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public IEnumerable<Customer> GetAll()
    {
        var customers = new List<Customer>();

        using var connection =
            _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Id,
                CustomerNumber,
                FirstName,
                LastName,
                Email,
                Phone,
                Street,
                PostalCode,
                City,
                CreatedAt,
                UpdatedAt
            FROM Customers
            ORDER BY
                LastName,
                FirstName,
                CustomerNumber;
            """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            customers.Add(ReadCustomer(reader));
        }

        reader.Close();

        foreach (var customer in customers)
        {
            customer.Devices = GetDevices(
                connection,
                customer.Id);
        }

        return customers;
    }

    public Customer? GetById(Guid customerId)
    {
        using var connection =
            _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Id,
                CustomerNumber,
                FirstName,
                LastName,
                Email,
                Phone,
                Street,
                PostalCode,
                City,
                CreatedAt,
                UpdatedAt
            FROM Customers
            WHERE Id = $customerId;
            """;

        command.Parameters.AddWithValue(
            "$customerId",
            customerId.ToString());

        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        var customer = ReadCustomer(reader);

        reader.Close();

        customer.Devices = GetDevices(
            connection,
            customer.Id);

        return customer;
    }

    public void Add(Customer customer)
    {
        using var connection =
            _connectionFactory.CreateOpenConnection();

        using var transaction =
            connection.BeginTransaction();

        try
        {
            InsertCustomer(
                connection,
                transaction,
                customer);

            SynchronizeDevices(
                connection,
                transaction,
                customer);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void Update(Customer customer)
    {
        using var connection =
            _connectionFactory.CreateOpenConnection();

        using var transaction =
            connection.BeginTransaction();

        try
        {
            using var command = connection.CreateCommand();

            command.Transaction = transaction;

            command.CommandText =
                """
                UPDATE Customers
                SET
                    CustomerNumber = $customerNumber,
                    FirstName = $firstName,
                    LastName = $lastName,
                    Email = $email,
                    Phone = $phone,
                    Street = $street,
                    PostalCode = $postalCode,
                    City = $city,
                    CreatedAt = $createdAt,
                    UpdatedAt = $updatedAt
                WHERE Id = $id;
                """;

            AddCustomerParameters(
                command,
                customer);

            command.ExecuteNonQuery();

            SynchronizeDevices(
                connection,
                transaction,
                customer);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void Delete(Guid customerId)
    {
        using var connection =
            _connectionFactory.CreateOpenConnection();

        using var command = connection.CreateCommand();

        command.CommandText =
            """
            DELETE FROM Customers
            WHERE Id = $customerId;
            """;

        command.Parameters.AddWithValue(
            "$customerId",
            customerId.ToString());

        command.ExecuteNonQuery();
    }

    private void InsertCustomer(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Customer customer)
    {
        using var command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
            """
            INSERT INTO Customers
            (
                Id,
                CustomerNumber,
                FirstName,
                LastName,
                Email,
                Phone,
                Street,
                PostalCode,
                City,
                CreatedAt,
                UpdatedAt
            )
            VALUES
            (
                $id,
                $customerNumber,
                $firstName,
                $lastName,
                $email,
                $phone,
                $street,
                $postalCode,
                $city,
                $createdAt,
                $updatedAt
            );
            """;

        AddCustomerParameters(
            command,
            customer);

        command.ExecuteNonQuery();
    }

    private void AddCustomerParameters(
        SqliteCommand command,
        Customer customer)
    {
        command.Parameters.AddWithValue(
            "$id",
            customer.Id.ToString());

        command.Parameters.AddWithValue(
            "$customerNumber",
            customer.CustomerNumber);

        command.Parameters.AddWithValue(
            "$firstName",
            customer.FirstName);

        command.Parameters.AddWithValue(
            "$lastName",
            customer.LastName);

        command.Parameters.AddWithValue(
            "$email",
            customer.Email);

        command.Parameters.AddWithValue(
            "$phone",
            customer.Phone);

        command.Parameters.AddWithValue(
            "$street",
            customer.Street);

        command.Parameters.AddWithValue(
            "$postalCode",
            customer.PostalCode);

        command.Parameters.AddWithValue(
            "$city",
            customer.City);

        command.Parameters.AddWithValue(
            "$createdAt",
            FormatDateTime(customer.CreatedAt));

        command.Parameters.AddWithValue(
            "$updatedAt",
            customer.UpdatedAt is null
                ? DBNull.Value
                : FormatDateTime(customer.UpdatedAt.Value));
    }

    private void SynchronizeDevices(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Customer customer)
    {
        DeleteRemovedDevices(
            connection,
            transaction,
            customer);

        foreach (var device in customer.Devices)
        {
            SaveDevice(
                connection,
                transaction,
                customer.Id,
                device);
        }
    }

    private void DeleteRemovedDevices(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Customer customer)
    {
        if (customer.Devices.Count == 0)
        {
            using var deleteAllCommand =
                connection.CreateCommand();

            deleteAllCommand.Transaction = transaction;

            deleteAllCommand.CommandText =
                """
                DELETE FROM CustomerDevices
                WHERE CustomerId = $customerId;
                """;

            deleteAllCommand.Parameters.AddWithValue(
                "$customerId",
                customer.Id.ToString());

            deleteAllCommand.ExecuteNonQuery();

            return;
        }

        var parameterNames = new List<string>();

        using var command = connection.CreateCommand();

        command.Transaction = transaction;

        command.Parameters.AddWithValue(
            "$customerId",
            customer.Id.ToString());

        for (var index = 0;
             index < customer.Devices.Count;
             index++)
        {
            var parameterName = $"$deviceId{index}";

            parameterNames.Add(parameterName);

            command.Parameters.AddWithValue(
                parameterName,
                customer.Devices[index].Id.ToString());
        }

        command.CommandText =
            $"""
             DELETE FROM CustomerDevices
             WHERE CustomerId = $customerId
               AND Id NOT IN ({string.Join(", ", parameterNames)});
             """;

        command.ExecuteNonQuery();
    }

    private void SaveDevice(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid customerId,
        CustomerDevice device)
    {
        var checkupSessionJson =
            JsonSerializer.Serialize(
                device.CheckupSession,
                _jsonSerializerOptions);

        using var command = connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
            """
            INSERT INTO CustomerDevices
            (
                Id,
                CustomerId,
                DisplayName,
                ComputerName,
                Manufacturer,
                Model,
                SerialNumber,
                CreatedAt,
                UpdatedAt,
                ScanDate,
                CheckupSessionJson
            )
            VALUES
            (
                $id,
                $customerId,
                $displayName,
                $computerName,
                $manufacturer,
                $model,
                $serialNumber,
                $createdAt,
                $updatedAt,
                $scanDate,
                $checkupSessionJson
            )
            ON CONFLICT(Id)
            DO UPDATE SET
                CustomerId = excluded.CustomerId,
                DisplayName = excluded.DisplayName,
                ComputerName = excluded.ComputerName,
                Manufacturer = excluded.Manufacturer,
                Model = excluded.Model,
                SerialNumber = excluded.SerialNumber,
                CreatedAt = excluded.CreatedAt,
                UpdatedAt = excluded.UpdatedAt,
                ScanDate = excluded.ScanDate,
                CheckupSessionJson = excluded.CheckupSessionJson;
            """;

        command.Parameters.AddWithValue(
            "$id",
            device.Id.ToString());

        command.Parameters.AddWithValue(
            "$customerId",
            customerId.ToString());

        command.Parameters.AddWithValue(
            "$displayName",
            device.DisplayName);

        /*
         * Die Detailwerte liegen vollständig im JSON.
         * Die zusätzlichen Spalten werden vorerst leer gehalten,
         * bis wir im nächsten Schritt den exakten Ist-Zustand
         * von DeviceInformation geprüft haben.
         */
        command.Parameters.AddWithValue(
            "$computerName",
            string.Empty);

        command.Parameters.AddWithValue(
            "$manufacturer",
            string.Empty);

        command.Parameters.AddWithValue(
            "$model",
            string.Empty);

        command.Parameters.AddWithValue(
            "$serialNumber",
            string.Empty);

        command.Parameters.AddWithValue(
            "$createdAt",
            FormatDateTime(device.CreatedAt));

        command.Parameters.AddWithValue(
            "$updatedAt",
            device.UpdatedAt is null
                ? DBNull.Value
                : FormatDateTime(device.UpdatedAt.Value));

        command.Parameters.AddWithValue(
            "$scanDate",
            device.CheckupSession.ScanDate is null
                ? DBNull.Value
                : FormatDateTime(
                    device.CheckupSession.ScanDate.Value));

        command.Parameters.AddWithValue(
            "$checkupSessionJson",
            checkupSessionJson);

        command.ExecuteNonQuery();
    }

    private List<CustomerDevice> GetDevices(
        SqliteConnection connection,
        Guid customerId)
    {
        var devices = new List<CustomerDevice>();

        using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Id,
                DisplayName,
                CreatedAt,
                UpdatedAt,
                CheckupSessionJson
            FROM CustomerDevices
            WHERE CustomerId = $customerId
            ORDER BY
                DisplayName,
                CreatedAt;
            """;

        command.Parameters.AddWithValue(
            "$customerId",
            customerId.ToString());

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            devices.Add(ReadDevice(reader));
        }

        return devices;
    }

    private Customer ReadCustomer(
        SqliteDataReader reader)
    {
        return new Customer
        {
            Id = Guid.Parse(
                reader.GetString(
                    reader.GetOrdinal("Id"))),

            CustomerNumber = reader.GetString(
                reader.GetOrdinal("CustomerNumber")),

            FirstName = reader.GetString(
                reader.GetOrdinal("FirstName")),

            LastName = reader.GetString(
                reader.GetOrdinal("LastName")),

            Email = reader.GetString(
                reader.GetOrdinal("Email")),

            Phone = reader.GetString(
                reader.GetOrdinal("Phone")),

            Street = reader.GetString(
                reader.GetOrdinal("Street")),

            PostalCode = reader.GetString(
                reader.GetOrdinal("PostalCode")),

            City = reader.GetString(
                reader.GetOrdinal("City")),

            CreatedAt = ParseDateTime(
                reader.GetString(
                    reader.GetOrdinal("CreatedAt"))),

            UpdatedAt = ReadNullableDateTime(
                reader,
                "UpdatedAt")
        };
    }

    private CustomerDevice ReadDevice(
        SqliteDataReader reader)
    {
        var checkupSessionJson =
            reader.GetString(
                reader.GetOrdinal("CheckupSessionJson"));

        var checkupSession =
            JsonSerializer.Deserialize<CheckupSession>(
                checkupSessionJson,
                _jsonSerializerOptions)
            ?? new CheckupSession();

        return new CustomerDevice
        {
            Id = Guid.Parse(
                reader.GetString(
                    reader.GetOrdinal("Id"))),

            DisplayName = reader.GetString(
                reader.GetOrdinal("DisplayName")),

            CreatedAt = ParseDateTime(
                reader.GetString(
                    reader.GetOrdinal("CreatedAt"))),

            UpdatedAt = ReadNullableDateTime(
                reader,
                "UpdatedAt"),

            CheckupSession = checkupSession
        };
    }

    private static string FormatDateTime(
        DateTime value)
    {
        return value.ToString(
            "O",
            CultureInfo.InvariantCulture);
    }

    private static DateTime ParseDateTime(
        string value)
    {
        return DateTime.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
    }

    private static DateTime? ReadNullableDateTime(
        SqliteDataReader reader,
        string columnName)
    {
        var ordinal =
            reader.GetOrdinal(columnName);

        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return ParseDateTime(
            reader.GetString(ordinal));
    }
}