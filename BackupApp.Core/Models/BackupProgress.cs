namespace BackupApp.Core.Models;

public class BackupProgress
{
    public Guid TaskId { get; set; }
    public string TaskName { get; set; } = "";
    public string Phase { get; set; } = "";
    public int ProcessedFiles { get; set; }
    public int TotalFiles { get; set; }
    public int Percent => TotalFiles > 0 ? ProcessedFiles * 100 / TotalFiles : 0;
}
