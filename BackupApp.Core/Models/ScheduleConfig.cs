namespace BackupApp.Core.Models;

public class ScheduleConfig
{
    public bool Enabled { get; set; }
    public string CronExpression { get; set; } = "";
    public DateTime? NextRun { get; set; }
}
