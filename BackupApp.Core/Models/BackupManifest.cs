using System.Text.Json;

namespace BackupApp.Core.Models;

public class BackupManifest
{
    public Guid RunId { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public List<FileEntry> Files { get; set; } = new();
    public BackupMode Mode { get; set; }
    public TimeSpan Duration { get; set; }
    public BackupStatus Status { get; set; }
    public List<string> ErrorLog { get; set; } = new();

    public void Save(string path)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static BackupManifest? Load(string path)
    {
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<BackupManifest>(json);
    }
}
