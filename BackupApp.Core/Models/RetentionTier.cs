namespace BackupApp.Core.Models;

public class RetentionTier
{
    public string Name { get; set; } = "";
    public int MaxAgeHours { get; set; } = -1;
    public int KeepIntervalMinutes { get; set; } = 0;
}
