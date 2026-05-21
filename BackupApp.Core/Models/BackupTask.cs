namespace BackupApp.Core.Models;

public class BackupTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public List<string> SourcePaths { get; set; } = new();
    public string DestPath { get; set; } = "";
    public DestType DestType { get; set; } = DestType.Local;
    public BackupMode BackupMode { get; set; } = BackupMode.Incremental;
    public ScheduleConfig Schedule { get; set; } = new();
    public List<RetentionTier> RetentionTiers { get; set; } = new();
    public FileFilter Filters { get; set; } = new();
    public TaskOptions Options { get; set; } = new();
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public bool IsCron(string expression)
    {
        try
        {
            var parts = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 5;
        }
        catch
        {
            return false;
        }
    }
}
