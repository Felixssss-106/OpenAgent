using Microsoft.Data.Sqlite;
using OpenAgent.Core.Domain;

namespace OpenAgent.Storage.Repositories;

public sealed class DeviceRepository
{
    private readonly SqliteDatabase _database;

    public DeviceRepository(SqliteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public void Upsert(DeviceRecord device)
    {
        ArgumentNullException.ThrowIfNull(device);

        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO devices (id, name, device_type, operating_system, app_version, trust_state,
                                 created_at_utc, last_seen_utc, key_fingerprint)
            VALUES ($id, $name, $type, $os, $version, $trust, $created, $seen, $fingerprint)
            ON CONFLICT(id) DO UPDATE SET
                name=excluded.name,
                device_type=excluded.device_type,
                operating_system=excluded.operating_system,
                app_version=excluded.app_version,
                trust_state=excluded.trust_state,
                last_seen_utc=excluded.last_seen_utc,
                key_fingerprint=excluded.key_fingerprint
            """;
        command.Parameters.AddWithValue("$id", device.Id);
        command.Parameters.AddWithValue("$name", device.Name);
        command.Parameters.AddWithValue("$type", device.DeviceType);
        command.Parameters.AddWithValue("$os", device.OperatingSystem);
        command.Parameters.AddWithValue("$version", device.AppVersion);
        command.Parameters.AddWithValue("$trust", device.TrustState);
        command.Parameters.AddWithValue("$created", SqliteDatabase.ToStorage(device.CreatedAtUtc));
        command.Parameters.AddWithValue("$seen", SqliteDatabase.ToStorage(device.LastSeenUtc));
        command.Parameters.AddWithValue("$fingerprint", (object?)device.KeyFingerprint ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<DeviceRecord> All()
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM devices ORDER BY last_seen_utc DESC";

        using var reader = command.ExecuteReader();
        var results = new List<DeviceRecord>();
        while (reader.Read())
        {
            results.Add(new DeviceRecord
            {
                Id = reader.GetString(reader.GetOrdinal("id")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                DeviceType = reader.GetString(reader.GetOrdinal("device_type")),
                OperatingSystem = reader.GetString(reader.GetOrdinal("operating_system")),
                AppVersion = reader.GetString(reader.GetOrdinal("app_version")),
                TrustState = reader.GetString(reader.GetOrdinal("trust_state")),
                CreatedAtUtc = SqliteDatabase.FromStorage(reader["created_at_utc"]) ?? DateTimeOffset.MinValue,
                LastSeenUtc = SqliteDatabase.FromStorage(reader["last_seen_utc"]) ?? DateTimeOffset.MinValue,
                KeyFingerprint = TaskRepository.NullableString(reader, "key_fingerprint"),
            });
        }

        return results;
    }

    /// <summary>Revoking never deletes the row — it must stay impossible to reuse.</summary>
    public void Revoke(string deviceId, DateTimeOffset at)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE devices SET trust_state=$state, last_seen_utc=$seen WHERE id=$id";
        command.Parameters.AddWithValue("$state", TrustStates.Revoked);
        command.Parameters.AddWithValue("$seen", SqliteDatabase.ToStorage(at));
        command.Parameters.AddWithValue("$id", deviceId);
        command.ExecuteNonQuery();
    }
}
