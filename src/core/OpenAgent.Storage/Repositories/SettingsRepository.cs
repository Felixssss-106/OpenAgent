using Microsoft.Data.Sqlite;
using OpenAgent.Core.Domain;

namespace OpenAgent.Storage.Repositories;

/// <summary>
/// Local-first application settings. Secrets are never stored here — only a
/// reference into OS secure storage (spec sections 59, 60).
/// </summary>
public sealed class SettingsRepository
{
    private readonly SqliteDatabase _database;

    public SettingsRepository(SqliteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public string? Get(string key)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key=$key LIMIT 1";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    public void Set(string key, string value, DateTimeOffset at)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO settings (key, value, updated_at_utc) VALUES ($key, $value, $at)
            ON CONFLICT(key) DO UPDATE SET value=excluded.value, updated_at_utc=excluded.updated_at_utc
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$at", SqliteDatabase.ToStorage(at));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<SettingRecord> All()
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM settings ORDER BY key";

        using var reader = command.ExecuteReader();
        var results = new List<SettingRecord>();
        while (reader.Read())
        {
            results.Add(new SettingRecord
            {
                Key = reader.GetString(reader.GetOrdinal("key")),
                Value = reader.GetString(reader.GetOrdinal("value")),
                UpdatedAtUtc = SqliteDatabase.FromStorage(reader["updated_at_utc"]) ?? DateTimeOffset.MinValue,
            });
        }

        return results;
    }
}
