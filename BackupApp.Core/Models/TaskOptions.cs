namespace BackupApp.Core.Models;

public class TaskOptions
{
    public bool EncryptionEnabled { get; set; }
    public string EncryptedPassword { get; set; } = "";
    public bool CompressionEnabled { get; set; }
    public bool VerifyAfterBackup { get; set; }
    public bool PreviewBeforeRun { get; set; }
}
