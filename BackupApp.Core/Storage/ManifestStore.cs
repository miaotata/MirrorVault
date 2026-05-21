using BackupApp.Core.Models;

namespace BackupApp.Core.Storage;

public class ManifestStore
{
    private readonly BackupLayout _layout;

    public ManifestStore(BackupLayout layout)
    {
        _layout = layout;
    }

    public List<BackupManifest> ListManifests(string destPath, string taskName)
    {
        var dir = _layout.GetManifestDir(destPath, taskName);
        if (!Directory.Exists(dir)) return new List<BackupManifest>();

        var manifests = new List<BackupManifest>();
        foreach (var file in Directory.GetFiles(dir, "*.json").OrderByDescending(f => f))
        {
            var manifest = BackupManifest.Load(file);
            if (manifest != null) manifests.Add(manifest);
        }
        return manifests;
    }

    public BackupManifest? GetManifest(string destPath, string taskName, Guid runId)
    {
        return ListManifests(destPath, taskName).FirstOrDefault(m => m.RunId == runId);
    }

    public void SaveManifest(string destPath, string taskName, BackupManifest manifest)
    {
        var path = _layout.GetManifestPath(destPath, taskName, manifest.Timestamp);
        var dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        manifest.Save(path);
    }

    public void DeleteManifest(string destPath, string taskName, Guid runId)
    {
        var manifests = ListManifests(destPath, taskName);
        var manifest = manifests.FirstOrDefault(m => m.RunId == runId);
        if (manifest == null) return;

        var manifestPath = _layout.GetManifestPath(destPath, taskName, manifest.Timestamp);
        if (File.Exists(manifestPath)) File.Delete(manifestPath);

        var dataDir = _layout.GetDataDir(destPath, taskName, manifest.Timestamp);
        if (Directory.Exists(dataDir)) Directory.Delete(dataDir, recursive: true);
    }

    public BackupManifest? GetLatestManifest(string destPath, string taskName)
    {
        return ListManifests(destPath, taskName).FirstOrDefault();
    }
}
