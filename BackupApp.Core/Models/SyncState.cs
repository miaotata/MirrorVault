using System.Text.Json;

namespace BackupApp.Core.Models;

public class SyncState
{
    public Dictionary<string, SyncFileEntry> Files { get; set; } = new();

    public static SyncState Load(string path)
    {
        if (!File.Exists(path)) return new SyncState();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SyncState>(json) ?? new SyncState();
    }

    public void Save(string path)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}

public class SyncFileEntry
{
    public string RelativePath { get; set; } = "";
    public string Sha256Hash { get; set; } = "";
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
}
