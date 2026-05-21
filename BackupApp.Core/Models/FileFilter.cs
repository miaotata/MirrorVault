namespace BackupApp.Core.Models;

public class FileFilter
{
    public List<string> IncludePatterns { get; set; } = new();
    public List<string> ExcludePatterns { get; set; } = new();
    public List<string> ExcludeDirectories { get; set; } = new();
    public long MaxFileSizeBytes { get; set; } = 0;
}
