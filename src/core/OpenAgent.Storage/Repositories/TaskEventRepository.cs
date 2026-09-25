using Microsoft.Data.Sqlite;
using OpenAgent.Core.Domain;

namespace OpenAgent.Storage.Repositories;

/// <summary>
/// Timeline of one task: assistant text, tool calls, system notes, errors,
/// approvals (spec sections 37, 155).
/// </summary>
public sealed class TaskEventRepository
{
    private readonly SqliteDatabase _database;

    public TaskEventRepository(SqliteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public void Insert(TaskEventRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO task_events (id, task_id, kind, text, data_json, timestamp_utc)
            VALUES ($id, $task, $kind, $text, $data, $ts)
            """;
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$task", record.TaskId);
        command.Parameters.AddWithValue("$kind", record.Kind);
        command.Parameters.AddWithValue("$text", record.Text);
        command.Parameters.AddWithValue("$data", (object?)record.DataJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$ts", SqliteDatabase.ToStorage(record.TimestampUtc));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<TaskEventRecord> ForTask(string taskId)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM task_events WHERE task_id=$task ORDER BY timestamp_utc ASC";
        command.Parameters.AddWithValue("$task", taskId);

        using var reader = command.ExecuteReader();
        var results = new List<TaskEventRecord>();
        while (reader.Read())
        {
            results.Add(new TaskEventRecord
            {
                Id = reader.GetString(reader.GetOrdinal("id")),
                TaskId = reader.GetString(reader.GetOrdinal("task_id")),
                Kind = reader.GetString(reader.GetOrdinal("kind")),
                Text = reader.GetString(reader.GetOrdinal("text")),
                DataJson = TaskRepository.NullableString(reader, "data_json"),
                TimestampUtc = SqliteDatabase.FromStorage(reader["timestamp_utc"]) ?? DateTimeOffset.MinValue,
            });
        }

        return results;
    }

    public int DeleteForTask(string taskId)
    {
        using var connection = _database.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM task_events WHERE task_id=$task";
        command.Parameters.AddWithValue("$task", taskId);
        return command.ExecuteNonQuery();
    }
}
