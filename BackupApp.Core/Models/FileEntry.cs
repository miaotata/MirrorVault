namespace BackupApp.Core.Models;

public class FileEntry
{
    public string RelativePath { get; set; } = "";
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string Sha256Hash { get; set; } = "";
    public FileStatus Status { get; set; }
}
