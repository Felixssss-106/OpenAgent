using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAgent.Storage.Schema;

namespace OpenAgent.Storage;

/// <summary>
/// Owns the SQLite file. One short-lived connection per operation keeps the
/// Agent's background work from racing the UI's reads (spec section 163).
/// </summary>
public sealed class SqliteDatabase
{
    private readonly DatabasePaths _paths;
    private readonly ILogger _logger;

    public SqliteDatabase(DatabasePaths paths, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
        _logger = logger ?? NullLogger.Instance;
    }

    public DatabasePaths Paths => _paths;

    public string DatabaseFile => _paths.DatabaseFile;

    public int Initialize()
    {
        _paths.EnsureCreated();
        var migrator = new SchemaMigrator(_paths.DatabaseFile, _paths.BackupsDirectory, _logger);
        return migrator.Apply();
    }

    public SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _paths.DatabaseFile,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 30,
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    public static string ToStorage(DateTimeOffset value) => value.ToUniversalTime().ToString("O");

    public static DateTimeOffset? FromStorage(object? value) =>
        value is null or DBNull
            ? null
            : DateTimeOffset.TryParse((string)value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : null;
}
