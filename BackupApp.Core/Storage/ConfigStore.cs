using System.Text.Json;
using Microsoft.Data.Sqlite;
using BackupApp.Core.Models;

namespace BackupApp.Core.Storage;

public class ConfigStore : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public ConfigStore(string? dbPath = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _dbPath = dbPath ?? Path.Combine(appData, "BackupApp", "config.db");
        var dir = Path.GetDirectoryName(_dbPath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        _connectionString = $"Data Source={_dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS tasks (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                source_paths TEXT NOT NULL,
                dest_path TEXT NOT NULL,
                dest_type INTEGER NOT NULL DEFAULT 0,
                backup_mode INTEGER NOT NULL DEFAULT 1,
                schedule_enabled INTEGER NOT NULL DEFAULT 0,
                schedule_cron TEXT NOT NULL DEFAULT '',
                schedule_next_run TEXT,
                retention_tiers TEXT NOT NULL DEFAULT '[]',
                filters TEXT NOT NULL DEFAULT '{}',
                options TEXT NOT NULL DEFAULT '{}',
                enabled INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            )";
        cmd.ExecuteNonQuery();
    }

    public string GetSetting(string key, string defaultValue = "")
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = @key";
        cmd.Parameters.AddWithValue("@key", key);
        var result = cmd.ExecuteScalar();
        return result?.ToString() ?? defaultValue;
    }

    public void SetSetting(string key, string value)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO settings (key, value) VALUES (@key, @value)";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.ExecuteNonQuery();
    }

    public List<BackupTask> ListTasks()
    {
        var tasks = new List<BackupTask>();
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tasks ORDER BY created_at DESC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tasks.Add(ReadTask(reader));
        }
        return tasks;
    }

    public BackupTask? GetTask(Guid id)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tasks WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        using var reader = cmd.ExecuteReader();
        if (reader.Read()) return ReadTask(reader);
        return null;
    }

    public void SaveTask(BackupTask task)
    {
        task.UpdatedAt = DateTime.Now;
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO tasks (id, name, source_paths, dest_path, dest_type, backup_mode,
                schedule_enabled, schedule_cron, schedule_next_run, retention_tiers, filters, options,
                enabled, created_at, updated_at)
            VALUES (@id, @name, @source_paths, @dest_path, @dest_type, @backup_mode,
                @schedule_enabled, @schedule_cron, @schedule_next_run, @retention_tiers, @filters, @options,
                @enabled, @created_at, @updated_at)";
        BindParameters(cmd, task);
        cmd.ExecuteNonQuery();
    }

    public void DeleteTask(Guid id)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM tasks WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        cmd.ExecuteNonQuery();
    }

    private static BackupTask ReadTask(SqliteDataReader reader)
    {
        return new BackupTask
        {
            Id = Guid.Parse(reader.GetString(0)),
            Name = reader.GetString(1),
            SourcePaths = JsonSerializer.Deserialize<List<string>>(reader.GetString(2)) ?? new(),
            DestPath = reader.GetString(3),
            DestType = (DestType)reader.GetInt32(4),
            BackupMode = (BackupMode)reader.GetInt32(5),
            Schedule = new ScheduleConfig
            {
                Enabled = reader.GetInt32(6) == 1,
                CronExpression = reader.GetString(7),
                NextRun = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8))
            },
            RetentionTiers = JsonSerializer.Deserialize<List<RetentionTier>>(reader.GetString(9)) ?? new(),
            Filters = JsonSerializer.Deserialize<FileFilter>(reader.GetString(10)) ?? new(),
            Options = JsonSerializer.Deserialize<TaskOptions>(reader.GetString(11)) ?? new(),
            Enabled = reader.GetInt32(12) == 1,
            CreatedAt = DateTime.Parse(reader.GetString(13)),
            UpdatedAt = DateTime.Parse(reader.GetString(14))
        };
    }

    private static void BindParameters(SqliteCommand cmd, BackupTask task)
    {
        cmd.Parameters.AddWithValue("@id", task.Id.ToString());
        cmd.Parameters.AddWithValue("@name", task.Name);
        cmd.Parameters.AddWithValue("@source_paths", JsonSerializer.Serialize(task.SourcePaths));
        cmd.Parameters.AddWithValue("@dest_path", task.DestPath);
        cmd.Parameters.AddWithValue("@dest_type", (int)task.DestType);
        cmd.Parameters.AddWithValue("@backup_mode", (int)task.BackupMode);
        cmd.Parameters.AddWithValue("@schedule_enabled", task.Schedule.Enabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@schedule_cron", task.Schedule.CronExpression);
        cmd.Parameters.AddWithValue("@schedule_next_run", (object?)task.Schedule.NextRun?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@retention_tiers", JsonSerializer.Serialize(task.RetentionTiers));
        cmd.Parameters.AddWithValue("@filters", JsonSerializer.Serialize(task.Filters));
        cmd.Parameters.AddWithValue("@options", JsonSerializer.Serialize(task.Options));
        cmd.Parameters.AddWithValue("@enabled", task.Enabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@created_at", task.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@updated_at", task.UpdatedAt.ToString("o"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
    }
}
