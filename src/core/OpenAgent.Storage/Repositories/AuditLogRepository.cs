using Microsoft.Data.Sqlite;
using OpenAgent.Core;
using OpenAgent.Core.Domain;

namespace OpenAgent.Storage.Repositories;

public sealed class AuditLogRepository
{
    private readonly SqliteDatabase _database;

    public AuditLogRepository(SqliteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public void Insert(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO audit_logs (id, timestamp_utc, task_id, device_id, provider_id, tool_id, risk,
                                    arguments_summary, success, result, approval_id, duration_ms)
            VALUES ($id, $ts, $task, $device, $provider, $tool, $risk, $args, $ok, $result, $approval, $ms)
            """;
        command.Parameters.AddWithValue("$id", entry.Id);
        command.Parameters.AddWithValue("$ts", SqliteDatabase.ToStorage(entry.TimestampUtc));
        command.Parameters.AddWithValue("$task", (object?)entry.TaskId ?? DBNull.Value);
        command.Parameters.AddWithValue("$device", (object?)entry.DeviceId ?? DBNull.Value);
        command.Parameters.AddWithValue("$provider", entry.ProviderId);
        command.Parameters.AddWithValue("$tool", entry.ToolId);
        command.Parameters.AddWithValue("$risk", entry.Risk.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$args", entry.ArgumentsSummary);
        command.Parameters.AddWithValue("$ok", entry.Success ? 1 : 0);
        command.Parameters.AddWithValue("$result", (object?)entry.Result ?? DBNull.Value);
        command.Parameters.AddWithValue("$approval", (object?)entry.ApprovalId ?? DBNull.Value);
        command.Parameters.AddWithValue("$ms", entry.DurationMs);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<AuditEntry> Recent(int limit = 100)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM audit_logs ORDER BY timestamp_utc DESC LIMIT $limit";
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        var results = new List<AuditEntry>();
        while (reader.Read())
        {
            Enum.TryParse<RiskLevel>(reader.GetString(reader.GetOrdinal("risk")), true, out var risk);
            results.Add(new AuditEntry
            {
                Id = reader.GetString(reader.GetOrdinal("id")),
                TimestampUtc = SqliteDatabase.FromStorage(reader["timestamp_utc"]) ?? DateTimeOffset.MinValue,
                TaskId = TaskRepository.NullableString(reader, "task_id"),
                DeviceId = TaskRepository.NullableString(reader, "device_id"),
                ProviderId = reader.GetString(reader.GetOrdinal("provider_id")),
                ToolId = reader.GetString(reader.GetOrdinal("tool_id")),
                Risk = risk,
                ArgumentsSummary = reader.GetString(reader.GetOrdinal("arguments_summary")),
                Success = reader.GetInt32(reader.GetOrdinal("success")) == 1,
                Result = TaskRepository.NullableString(reader, "result"),
                ApprovalId = TaskRepository.NullableString(reader, "approval_id"),
                DurationMs = reader.GetInt64(reader.GetOrdinal("duration_ms")),
            });
        }

        return results;
    }

    /// <summary>Used by "clear logs" (spec section 209).</summary>
    public int DeleteAll()
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM audit_logs";
        return command.ExecuteNonQuery();
    }
}
