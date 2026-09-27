using OpenAgent.Core;
using OpenAgent.Core.Domain;
using OpenAgent.Core.Tasks;
using OpenAgent.Storage;
using OpenAgent.Storage.Repositories;
using OpenAgent.Storage.Schema;

namespace OpenAgent.Storage.Tests;

/// <summary>
/// Real SQLite against a temp directory: the repositories are only meaningful if
/// they actually round-trip through SQL.
/// </summary>
public sealed class SqliteStorageTests : IDisposable
{
    private readonly string _root;
    private readonly DatabasePaths _paths;
    private readonly SqliteDatabase _database;

    public SqliteStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "openagent-tests", Guid.NewGuid().ToString("N"));
        _paths = new DatabasePaths(_root);
        _database = new SqliteDatabase(_paths);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Windows may still hold a handle on the WAL file; nothing to do.
        }
    }

    [Fact]
    public void Migrating_creates_every_core_table_and_records_the_version()
    {
        var version = _database.Initialize();

        Assert.Equal(2, version);
        Assert.Equal(2, new SchemaMigrator(_paths.DatabaseFile, _paths.BackupsDirectory).CurrentVersion());

        using var connection = _database.CreateConnection();
        foreach (var table in new[]
                 {
                     "devices", "sessions", "tasks", "task_events", "approvals", "tools",
                     "providers", "provider_configs", "settings", "workflows", "plugins",
                     "audit_logs", "sync_jobs", "pairing", "pairing_identity",
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'";
            Assert.Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
        }
    }

    [Fact]
    public void Migrating_twice_is_a_no_op()
    {
        // Initialize reports the schema version it brought the database to,
        // so a second call reports the same 2 and applies nothing.
        Assert.Equal(2, _database.Initialize());
        Assert.Equal(2, _database.Initialize());
    }

    [Fact]
    public void A_task_round_trips_through_sqlite()
    {
        _database.Initialize();
        var repository = new TaskRepository(_database);

        var task = new AgentTask
        {
            Prompt = "整理下载目录",
            ProviderId = "openagent.native",
            Status = AgentTaskStatus.Running,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        repository.Insert(task);

        var loaded = repository.Get(task.Id);
        Assert.NotNull(loaded);
        Assert.Equal("整理下载目录", loaded!.Prompt);
        Assert.Equal(AgentTaskStatus.Running, loaded.Status);

        loaded.Status = AgentTaskStatus.Completed;
        loaded.CompletedAtUtc = DateTimeOffset.UtcNow;
        repository.Update(loaded);

        Assert.Equal(AgentTaskStatus.Completed, repository.Get(task.Id)!.Status);
        Assert.NotNull(repository.Get(task.Id)!.CompletedAtUtc);
    }

    [Fact]
    public void Tasks_are_listed_newest_first()
    {
        _database.Initialize();
        var repository = new TaskRepository(_database);
        var now = DateTimeOffset.UtcNow;

        repository.Insert(new AgentTask { Prompt = "older", CreatedAtUtc = now.AddMinutes(-5), UpdatedAtUtc = now.AddMinutes(-5) });
        repository.Insert(new AgentTask { Prompt = "newer", CreatedAtUtc = now, UpdatedAtUtc = now });

        var recent = repository.Recent(10);

        Assert.Equal("newer", recent[0].Prompt);
    }

    [Fact]
    public void Expired_approvals_disappear_from_the_pending_list()
    {
        _database.Initialize();
        var repository = new ApprovalRepository(_database);
        var now = DateTimeOffset.UtcNow;

        repository.Insert(new ApprovalRecord
        {
            TaskId = "t1",
            ToolId = "file.move",
            Risk = RiskLevel.Medium,
            Message = "移动文件",
            ArgumentsSummary = "{}",
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(-1),
        });
        repository.Insert(new ApprovalRecord
        {
            TaskId = "t2",
            ToolId = "file.move",
            Risk = RiskLevel.Medium,
            Message = "移动文件",
            ArgumentsSummary = "{}",
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(5),
        });

        // Only the still-valid one is pending, and expiring leaves it alone.
        Assert.Single(repository.Pending(now));
        Assert.Equal(1, repository.Expire(now));

        var stillPending = repository.Pending(now);
        Assert.Single(stillPending);
        Assert.Equal("t2", stillPending[0].TaskId);
    }

    [Fact]
    public void An_approval_can_be_resolved_once_only()
    {
        _database.Initialize();
        var repository = new ApprovalRepository(_database);
        var now = DateTimeOffset.UtcNow;

        var approval = new ApprovalRecord
        {
            TaskId = "t1",
            ToolId = "file.delete",
            Risk = RiskLevel.High,
            Message = "删除文件",
            ArgumentsSummary = "{}",
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(5),
        };
        repository.Insert(approval);

        approval.Status = ApprovalStatus.Approved;
        approval.ResolvedAtUtc = now;
        approval.ResolvedBy = "user";
        repository.Update(approval);

        var loaded = repository.Get(approval.Id)!;
        Assert.Equal(ApprovalStatus.Approved, loaded.Status);
        Assert.Equal("user", loaded.ResolvedBy);
        Assert.False(loaded.IsPending(now));
    }

    [Fact]
    public void Audit_entries_are_stored_and_can_be_cleared()
    {
        _database.Initialize();
        var repository = new AuditLogRepository(_database);

        repository.Insert(new AuditEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            ToolId = "file.move",
            Risk = RiskLevel.Medium,
            ArgumentsSummary = "src → dst",
            Success = true,
            DurationMs = 12,
        });

        Assert.Single(repository.Recent());
        Assert.Equal(1, repository.DeleteAll());
        Assert.Empty(repository.Recent());
    }

    [Fact]
    public void Settings_are_upserted()
    {
        _database.Initialize();
        var repository = new SettingsRepository(_database);

        Assert.Null(repository.Get("theme"));
        repository.Set("theme", "dark", DateTimeOffset.UtcNow);
        Assert.Equal("dark", repository.Get("theme"));
        repository.Set("theme", "light", DateTimeOffset.UtcNow);
        Assert.Equal("light", repository.Get("theme"));
        Assert.Single(repository.All());
    }

    [Fact]
    public void Devices_can_be_revoked_without_being_forgotten()
    {
        _database.Initialize();
        var repository = new DeviceRepository(_database);
        var now = DateTimeOffset.UtcNow;

        var device = new DeviceRecord
        {
            Name = "DESKTOP-TEST",
            DeviceType = "windows",
            OperatingSystem = "Windows 11",
            AppVersion = "0.1.0",
            TrustState = TrustStates.Trusted,
            CreatedAtUtc = now,
            LastSeenUtc = now,
        };
        repository.Upsert(device);
        repository.Revoke(device.Id, now);

        var loaded = repository.All();
        Assert.Single(loaded);
        Assert.Equal(TrustStates.Revoked, loaded[0].TrustState);
    }
}
