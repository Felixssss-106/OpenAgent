namespace OpenAgent.Storage.Schema;

public sealed record SchemaMigration(int Version, string Name, string Sql);

/// <summary>
/// Ordered, append-only. Existing migrations must never be edited — a change
/// means a new version (spec sections 64, 261).
/// </summary>
public static class SchemaMigrations
{
    public static IReadOnlyList<SchemaMigration> All { get; } = new[]
    {
        new SchemaMigration(1, "initial", InitialSql),
    };

    private const string InitialSql = """
    CREATE TABLE IF NOT EXISTS schema_version (
        version INTEGER NOT NULL,
        name TEXT NOT NULL,
        applied_at_utc TEXT NOT NULL
    );

    CREATE TABLE IF NOT EXISTS devices (
        id TEXT PRIMARY KEY,
        name TEXT NOT NULL,
        device_type TEXT NOT NULL,
        operating_system TEXT NOT NULL,
        app_version TEXT NOT NULL,
        trust_state TEXT NOT NULL,
        created_at_utc TEXT NOT NULL,
        last_seen_utc TEXT NOT NULL,
        key_fingerprint TEXT NULL
    );

    CREATE TABLE IF NOT EXISTS sessions (
        id TEXT PRIMARY KEY,
        provider TEXT NOT NULL,
        task_id TEXT NULL,
        working_directory TEXT NULL,
        permission_mode TEXT NOT NULL,
        status TEXT NOT NULL,
        created_at_utc TEXT NOT NULL,
        updated_at_utc TEXT NOT NULL,
        summary TEXT NULL
    );

    CREATE TABLE IF NOT EXISTS tasks (
        id TEXT PRIMARY KEY,
        prompt TEXT NOT NULL,
        provider_id TEXT NOT NULL,
        status TEXT NOT NULL,
        working_directory TEXT NULL,
        summary TEXT NULL,
        error TEXT NULL,
        created_at_utc TEXT NOT NULL,
        updated_at_utc TEXT NOT NULL,
        completed_at_utc TEXT NULL
    );
    CREATE INDEX IF NOT EXISTS idx_tasks_status ON tasks(status);

    CREATE TABLE IF NOT EXISTS task_events (
        id TEXT PRIMARY KEY,
        task_id TEXT NOT NULL,
        kind TEXT NOT NULL,
        text TEXT NOT NULL,
        data_json TEXT NULL,
        timestamp_utc TEXT NOT NULL
    );
    CREATE INDEX IF NOT EXISTS idx_task_events_task ON task_events(task_id, timestamp_utc);

    CREATE TABLE IF NOT EXISTS approvals (
        id TEXT PRIMARY KEY,
        task_id TEXT NOT NULL,
        tool_id TEXT NOT NULL,
        risk TEXT NOT NULL,
        reversible INTEGER NOT NULL,
        message TEXT NOT NULL,
        arguments_summary TEXT NOT NULL,
        affected_paths TEXT NULL,
        status TEXT NOT NULL,
        created_at_utc TEXT NOT NULL,
        expires_at_utc TEXT NOT NULL,
        resolved_at_utc TEXT NULL,
        resolved_by TEXT NULL
    );
    CREATE INDEX IF NOT EXISTS idx_approvals_status ON approvals(status);

    CREATE TABLE IF NOT EXISTS tools (
        id TEXT PRIMARY KEY,
        name TEXT NOT NULL,
        description TEXT NULL,
        risk TEXT NOT NULL,
        requires_approval INTEGER NOT NULL,
        reversible INTEGER NOT NULL,
        input_schema_json TEXT NOT NULL,
        permissions TEXT NULL
    );

    CREATE TABLE IF NOT EXISTS providers (
        id TEXT PRIMARY KEY,
        display_name TEXT NOT NULL,
        kind TEXT NOT NULL,
        capabilities TEXT NULL,
        status TEXT NOT NULL,
        executable_path TEXT NULL,
        version TEXT NULL,
        last_checked_utc TEXT NULL
    );

    CREATE TABLE IF NOT EXISTS provider_configs (
        id TEXT PRIMARY KEY,
        provider_id TEXT NOT NULL,
        display_name TEXT NOT NULL,
        api_base_url TEXT NULL,
        model TEXT NULL,
        temperature REAL NULL,
        timeout_seconds INTEGER NULL,
        enabled INTEGER NOT NULL,
        secret_reference TEXT NULL
    );

    CREATE TABLE IF NOT EXISTS settings (
        key TEXT PRIMARY KEY,
        value TEXT NOT NULL,
        updated_at_utc TEXT NOT NULL
    );

    CREATE TABLE IF NOT EXISTS workflows (
        id TEXT PRIMARY KEY,
        name TEXT NOT NULL,
        definition_json TEXT NOT NULL,
        enabled INTEGER NOT NULL,
        created_at_utc TEXT NOT NULL
    );

    CREATE TABLE IF NOT EXISTS plugins (
        id TEXT PRIMARY KEY,
        name TEXT NOT NULL,
        version TEXT NOT NULL,
        api_version INTEGER NOT NULL,
        permissions TEXT NOT NULL,
        enabled INTEGER NOT NULL,
        installed_at_utc TEXT NOT NULL
    );

    CREATE TABLE IF NOT EXISTS audit_logs (
        id TEXT PRIMARY KEY,
        timestamp_utc TEXT NOT NULL,
        task_id TEXT NULL,
        device_id TEXT NULL,
        provider_id TEXT NOT NULL,
        tool_id TEXT NOT NULL,
        risk TEXT NOT NULL,
        arguments_summary TEXT NOT NULL,
        success INTEGER NOT NULL,
        result TEXT NULL,
        approval_id TEXT NULL,
        duration_ms INTEGER NOT NULL
    );
    CREATE INDEX IF NOT EXISTS idx_audit_logs_time ON audit_logs(timestamp_utc);

    CREATE TABLE IF NOT EXISTS sync_jobs (
        id TEXT PRIMARY KEY,
        provider TEXT NOT NULL,
        last_run_utc TEXT NULL,
        status TEXT NOT NULL,
        detail TEXT NULL
    );
    """;
}
