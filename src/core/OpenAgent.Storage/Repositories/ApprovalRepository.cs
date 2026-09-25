using Microsoft.Data.Sqlite;
using OpenAgent.Core;
using OpenAgent.Core.Domain;

namespace OpenAgent.Storage.Repositories;

public sealed class ApprovalRepository
{
    private readonly SqliteDatabase _database;

    public ApprovalRepository(SqliteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public void Insert(ApprovalRecord approval)
    {
        ArgumentNullException.ThrowIfNull(approval);

        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO approvals (id, task_id, tool_id, risk, reversible, message, arguments_summary,
                                   affected_paths, status, created_at_utc, expires_at_utc,
                                   resolved_at_utc, resolved_by)
            VALUES ($id, $task, $tool, $risk, $reversible, $message, $args, $paths, $status,
                    $created, $expires, $resolved, $by)
            """;
        BindAll(command, approval);
        command.ExecuteNonQuery();
    }

    public void Update(ApprovalRecord approval)
    {
        ArgumentNullException.ThrowIfNull(approval);

        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE approvals SET status=$status, resolved_at_utc=$resolved, resolved_by=$by
            WHERE id=$id
            """;
        command.Parameters.AddWithValue("$id", approval.Id);
        command.Parameters.AddWithValue("$status", approval.Status.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue(
            "$resolved",
            approval.ResolvedAtUtc is { } at ? SqliteDatabase.ToStorage(at) : DBNull.Value);
        command.Parameters.AddWithValue("$by", (object?)approval.ResolvedBy ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public ApprovalRecord? Get(string id)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM approvals WHERE id=$id LIMIT 1";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    /// <summary>
    /// Approvals are time-boxed: a stale pending request must never be executed
    /// after the user walked away (spec section 98).
    /// </summary>
    public IReadOnlyList<ApprovalRecord> Pending(DateTimeOffset asOf)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM approvals
            WHERE status = 'pending' AND expires_at_utc > $now
            ORDER BY created_at_utc ASC
            """;
        command.Parameters.AddWithValue("$now", SqliteDatabase.ToStorage(asOf));

        using var reader = command.ExecuteReader();
        var results = new List<ApprovalRecord>();
        while (reader.Read())
        {
            results.Add(Map(reader));
        }

        return results;
    }

    /// <summary>Flips expired pending approvals so nothing can approve them later.</summary>
    public int Expire(DateTimeOffset asOf)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE approvals SET status='expired' WHERE status='pending' AND expires_at_utc <= $now";
        command.Parameters.AddWithValue("$now", SqliteDatabase.ToStorage(asOf));
        return command.ExecuteNonQuery();
    }

    private static void BindAll(SqliteCommand command, ApprovalRecord approval)
    {
        command.Parameters.AddWithValue("$id", approval.Id);
        command.Parameters.AddWithValue("$task", approval.TaskId);
        command.Parameters.AddWithValue("$tool", approval.ToolId);
        command.Parameters.AddWithValue("$risk", approval.Risk.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$reversible", approval.Reversible ? 1 : 0);
        command.Parameters.AddWithValue("$message", approval.Message);
        command.Parameters.AddWithValue("$args", approval.ArgumentsSummary);
        command.Parameters.AddWithValue("$paths", (object?)approval.AffectedPaths ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", approval.Status.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$created", SqliteDatabase.ToStorage(approval.CreatedAtUtc));
        command.Parameters.AddWithValue("$expires", SqliteDatabase.ToStorage(approval.ExpiresAtUtc));
        command.Parameters.AddWithValue(
            "$resolved",
            approval.ResolvedAtUtc is { } at ? SqliteDatabase.ToStorage(at) : DBNull.Value);
        command.Parameters.AddWithValue("$by", (object?)approval.ResolvedBy ?? DBNull.Value);
    }

    private static ApprovalRecord Map(SqliteDataReader reader)
    {
        Enum.TryParse<ApprovalStatus>(reader.GetString(reader.GetOrdinal("status")), true, out var status);
        Enum.TryParse<RiskLevel>(reader.GetString(reader.GetOrdinal("risk")), true, out var risk);

        return new ApprovalRecord
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            TaskId = reader.GetString(reader.GetOrdinal("task_id")),
            ToolId = reader.GetString(reader.GetOrdinal("tool_id")),
            Risk = risk,
            Reversible = reader.GetInt32(reader.GetOrdinal("reversible")) == 1,
            Message = reader.GetString(reader.GetOrdinal("message")),
            ArgumentsSummary = reader.GetString(reader.GetOrdinal("arguments_summary")),
            AffectedPaths = TaskRepository.NullableString(reader, "affected_paths"),
            Status = status,
            CreatedAtUtc = SqliteDatabase.FromStorage(reader["created_at_utc"]) ?? DateTimeOffset.MinValue,
            ExpiresAtUtc = SqliteDatabase.FromStorage(reader["expires_at_utc"]) ?? DateTimeOffset.MinValue,
            ResolvedAtUtc = SqliteDatabase.FromStorage(reader["resolved_at_utc"]),
            ResolvedBy = TaskRepository.NullableString(reader, "resolved_by"),
        };
    }
}
