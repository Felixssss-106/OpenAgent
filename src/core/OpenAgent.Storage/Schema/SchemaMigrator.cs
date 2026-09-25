using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OpenAgent.Storage.Schema;

/// <summary>
/// backup → migrate → verify → commit, with rollback on any failure. The
/// database is never deleted to "fix" a failed migration (spec section 223).
/// </summary>
public sealed class SchemaMigrator
{
    private const int BackupsToKeep = 3;

    private readonly string _databasePath;
    private readonly string _backupsDirectory;
    private readonly ILogger _logger;

    public SchemaMigrator(string databasePath, string backupsDirectory, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupsDirectory);

        _databasePath = databasePath;
        _backupsDirectory = backupsDirectory;
        _logger = logger ?? NullLogger.Instance;
    }

    public int CurrentVersion()
    {
        using var connection = Open();

        using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='schema_version'";
        if (Convert.ToInt64(exists.ExecuteScalar()) == 0)
        {
            return 0;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(version) FROM schema_version";
        var value = command.ExecuteScalar();
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    public int Apply()
    {
        Directory.CreateDirectory(_backupsDirectory);

        var before = CurrentVersion();
        var pending = SchemaMigrations.All.Where(m => m.Version > before).OrderBy(m => m.Version).ToArray();
        if (pending.Length == 0)
        {
            return before;
        }

        var backup = CreateBackup();

        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var migration in pending)
            {
                _logger.LogInformation("Applying schema migration {Version} ({Name}).", migration.Version, migration.Name);

                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = migration.Sql;
                command.ExecuteNonQuery();

                using var record = connection.CreateCommand();
                record.Transaction = transaction;
                record.CommandText = "INSERT INTO schema_version (version, name, applied_at_utc) VALUES ($v, $n, $t)";
                record.Parameters.AddWithValue("$v", migration.Version);
                record.Parameters.AddWithValue("$n", migration.Name);
                record.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToString("O"));
                record.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Schema migration failed, rolled back. Backup kept at {Backup}.", backup);
            throw;
        }

        var after = CurrentVersion();
        if (after < pending[^1].Version)
        {
            throw new InvalidOperationException(
                $"Schema migration ended at version {after}, expected {pending[^1].Version}.");
        }

        PruneBackups();
        return after;
    }

    private SqliteConnection Open()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    private string? CreateBackup()
    {
        if (!File.Exists(_databasePath))
        {
            return null;
        }

        var target = Path.Combine(_backupsDirectory, $"openagent-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.sqlite");
        File.Copy(_databasePath, target, overwrite: false);
        return target;
    }

    private void PruneBackups()
    {
        var files = Directory.GetFiles(_backupsDirectory, "openagent-*.sqlite")
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .Skip(BackupsToKeep)
            .ToArray();

        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not prune old backup {File}.", file);
            }
        }
    }
}
