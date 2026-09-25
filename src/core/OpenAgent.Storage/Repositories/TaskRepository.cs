using Microsoft.Data.Sqlite;
using OpenAgent.Core.Domain;
using OpenAgent.Core.Tasks;

namespace OpenAgent.Storage.Repositories;

public sealed class TaskRepository
{
    private readonly SqliteDatabase _database;

    public TaskRepository(SqliteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public void Insert(AgentTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tasks (id, prompt, provider_id, status, working_directory, summary, error,
                               created_at_utc, updated_at_utc, completed_at_utc)
            VALUES ($id, $prompt, $provider, $status, $wd, $summary, $error, $created, $updated, $completed)
            """;
        BindAll(command, task);
        command.ExecuteNonQuery();
    }

    public void Update(AgentTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE tasks SET prompt=$prompt, provider_id=$provider, status=$status,
                             working_directory=$wd, summary=$summary, error=$error,
                             updated_at_utc=$updated, completed_at_utc=$completed
            WHERE id=$id
            """;
        BindAll(command, task);
        command.ExecuteNonQuery();
    }

    public AgentTask? Get(string id)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM tasks WHERE id=$id LIMIT 1";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public IReadOnlyList<AgentTask> Recent(int limit = 50)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM tasks ORDER BY created_at_utc DESC LIMIT $limit";
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        var results = new List<AgentTask>();
        while (reader.Read())
        {
            results.Add(Map(reader));
        }

        return results;
    }

    private static void BindAll(SqliteCommand command, AgentTask task)
    {
        command.Parameters.AddWithValue("$id", task.Id);
        command.Parameters.AddWithValue("$prompt", task.Prompt);
        command.Parameters.AddWithValue("$provider", task.ProviderId);
        command.Parameters.AddWithValue("$status", task.Status.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$wd", (object?)task.WorkingDirectory ?? DBNull.Value);
        command.Parameters.AddWithValue("$summary", (object?)task.Summary ?? DBNull.Value);
        command.Parameters.AddWithValue("$error", (object?)task.Error ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", SqliteDatabase.ToStorage(task.CreatedAtUtc));
        command.Parameters.AddWithValue("$updated", SqliteDatabase.ToStorage(task.UpdatedAtUtc));
        command.Parameters.AddWithValue(
            "$completed",
            task.CompletedAtUtc is { } done ? SqliteDatabase.ToStorage(done) : DBNull.Value);
    }

    private static AgentTask Map(SqliteDataReader reader)
    {
        var statusString = reader.GetString(reader.GetOrdinal("status"));
        Enum.TryParse<AgentTaskStatus>(statusString, ignoreCase: true, out var status);

        return new AgentTask
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            Prompt = reader.GetString(reader.GetOrdinal("prompt")),
            ProviderId = reader.GetString(reader.GetOrdinal("provider_id")),
            Status = status,
            WorkingDirectory = NullableString(reader, "working_directory"),
            Summary = NullableString(reader, "summary"),
            Error = NullableString(reader, "error"),
            CreatedAtUtc = SqliteDatabase.FromStorage(reader["created_at_utc"]) ?? DateTimeOffset.MinValue,
            UpdatedAtUtc = SqliteDatabase.FromStorage(reader["updated_at_utc"]) ?? DateTimeOffset.MinValue,
            CompletedAtUtc = SqliteDatabase.FromStorage(reader["completed_at_utc"]),
        };
    }

    internal static string? NullableString(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
