namespace BackupApp.Core.Models;

public class ConflictFile
{
    public string RelativePath { get; set; } = "";
    public DateTime SourceModified { get; set; }
    public long SourceSize { get; set; }
    public DateTime DestModified { get; set; }
    public long DestSize { get; set; }
    public string SourceHash { get; set; } = "";
    public string DestHash { get; set; } = "";
    public ConflictResolution Resolution { get; set; } = ConflictResolution.KeepLocal;
}
