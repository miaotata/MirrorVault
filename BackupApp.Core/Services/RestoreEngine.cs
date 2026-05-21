using BackupApp.Core.Models;
using BackupApp.Core.Storage;

namespace BackupApp.Core.Services;

public class RestoreEngine
{
    private readonly BackupLayout _layout;
    private readonly ManifestStore _manifestStore;
    private readonly EncryptionService _encryption;
    private readonly CompressionService _compression;

    public RestoreEngine(BackupLayout layout, ManifestStore manifestStore,
        EncryptionService encryption, CompressionService compression)
    {
        _layout = layout;
        _manifestStore = manifestStore;
        _encryption = encryption;
        _compression = compression;
    }

    public List<BackupManifest> GetTimeline(string destPath, string taskName)
    {
        return _manifestStore.ListManifests(destPath, taskName)
            .OrderByDescending(m => m.Timestamp)
            .ToList();
    }

    public BackupManifest? GetSnapshot(string destPath, string taskName, Guid runId)
    {
        return _manifestStore.GetManifest(destPath, taskName, runId);
    }

    public RestoreResult RestoreFile(BackupTask task, Guid runId, string relativePath, string restoreTo, string? password = null)
    {
        var manifest = _manifestStore.GetManifest(task.DestPath, task.Name, runId)
            ?? throw new InvalidOperationException("Manifest not found");

        var entry = manifest.Files.FirstOrDefault(f => f.RelativePath == relativePath)
            ?? throw new InvalidOperationException("File not found in snapshot");

        var dataDir = _layout.GetDataDir(task.DestPath, task.Name, manifest.Timestamp);
        var sourceFile = Path.Combine(dataDir, relativePath);

        if (!File.Exists(sourceFile))
            throw new FileNotFoundException($"Backup file not found: {sourceFile}");

        var destDir = Path.GetDirectoryName(restoreTo)!;
        if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

        if (task.Options.EncryptionEnabled && !string.IsNullOrEmpty(password))
            _encryption.DecryptFile(sourceFile, restoreTo, password);
        else
            File.Copy(sourceFile, restoreTo, overwrite: true);

        return new RestoreResult { RestoredFiles = 1, TotalFiles = 1 };
    }

    public RestoreResult RestoreAll(BackupTask task, Guid runId, string restoreRoot, string? password = null,
        IProgress<int>? progress = null)
    {
        var manifest = _manifestStore.GetManifest(task.DestPath, task.Name, runId)
            ?? throw new InvalidOperationException("Manifest not found");

        var dataDir = _layout.GetDataDir(task.DestPath, task.Name, manifest.Timestamp);
        var result = new RestoreResult { TotalFiles = manifest.Files.Count };

        for (int i = 0; i < manifest.Files.Count; i++)
        {
            var entry = manifest.Files[i];
            var sourceFile = Path.Combine(dataDir, entry.RelativePath);
            var destFile = Path.Combine(restoreRoot, entry.RelativePath);
            var destDir = Path.GetDirectoryName(destFile)!;

            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            if (File.Exists(sourceFile))
            {
                if (task.Options.EncryptionEnabled && !string.IsNullOrEmpty(password))
                    _encryption.DecryptFile(sourceFile, destFile, password);
                else
                    File.Copy(sourceFile, destFile, overwrite: true);
                result.RestoredFiles++;
            }
            else
            {
                result.FailedFiles.Add(entry.RelativePath);
            }

            progress?.Report((i + 1) * 100 / manifest.Files.Count);
        }

        return result;
    }
}

public class RestoreResult
{
    public int RestoredFiles { get; set; }
    public int TotalFiles { get; set; }
    public List<string> FailedFiles { get; set; } = new();
    public bool Success => FailedFiles.Count == 0;
}
