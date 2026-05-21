using BackupApp.Core.Models;
using BackupApp.Core.Storage;

namespace BackupApp.Core.Services;

public class SyncEngine
{
    private readonly BackupLayout _layout;
    private readonly FileScanner _scanner;

    public SyncEngine(BackupLayout layout, FileScanner scanner)
    {
        _layout = layout;
        _scanner = scanner;
    }

    public SyncResult ExecuteSync(BackupTask task)
    {
        var result = new SyncResult();
        var syncStatePath = _layout.GetSyncStatePath(task.DestPath, task.Name);
        var syncState = SyncState.Load(syncStatePath);

        var sourceScan = _scanner.Scan(task.SourcePaths, task.Filters, null);
        var destScan = _scanner.Scan(new List<string> { task.DestPath }, task.Filters, null);

        var sourceDict = sourceScan.Files.ToDictionary(f => f.RelativePath);
        var destDict = destScan.Files.ToDictionary(f => f.RelativePath);
        var allPaths = new HashSet<string>(sourceDict.Keys.Concat(destDict.Keys));

        foreach (var path in allPaths)
        {
            var inSource = sourceDict.TryGetValue(path, out var s);
            var inDest = destDict.TryGetValue(path, out var d);
            var inState = syncState.Files.TryGetValue(path, out var state);

            if (inSource && !inDest)
            {
                CopyFromSource(task, path);
                result.FilesSynced++;
                var hash = _scanner.ComputeHash(Path.Combine(task.SourcePaths[0], path));
                syncState.Files[path] = new SyncFileEntry { RelativePath = path, Sha256Hash = hash, Size = s!.Size, LastModified = s.LastModified };
            }
            else if (!inSource && inDest)
            {
                CopyFromDest(task, path);
                result.FilesSynced++;
                syncState.Files.Remove(path);
            }
            else if (inSource && inDest)
            {
                var sourceHash = _scanner.ComputeHash(Path.Combine(task.SourcePaths[0], path));
                var destHash = _scanner.ComputeHash(Path.Combine(task.DestPath, path));
                var sourceChanged = state == null || sourceHash != state.Sha256Hash;
                var destChanged = state == null || destHash != state.Sha256Hash;

                if (sourceChanged && destChanged && sourceHash != destHash)
                {
                    result.Conflicts.Add(new ConflictFile
                    {
                        RelativePath = path,
                        SourceModified = s!.LastModified,
                        SourceSize = s.Size,
                        DestModified = d!.LastModified,
                        DestSize = d.Size,
                        SourceHash = sourceHash,
                        DestHash = destHash
                    });
                }
                else if (sourceChanged && !destChanged)
                {
                    CopyFromSource(task, path);
                    result.FilesSynced++;
                    syncState.Files[path] = new SyncFileEntry { RelativePath = path, Sha256Hash = sourceHash, Size = s!.Size, LastModified = s.LastModified };
                }
                else if (!sourceChanged && destChanged)
                {
                    CopyFromDest(task, path);
                    result.FilesSynced++;
                    syncState.Files[path] = new SyncFileEntry { RelativePath = path, Sha256Hash = destHash, Size = d!.Size, LastModified = d.LastModified };
                }
            }
        }

        syncState.Save(syncStatePath);
        return result;
    }

    public void ApplyConflictResolutions(BackupTask task, SyncResult result)
    {
        foreach (var conflict in result.Conflicts)
        {
            switch (conflict.Resolution)
            {
                case ConflictResolution.KeepLocal:
                    CopyFromSource(task, conflict.RelativePath);
                    break;
                case ConflictResolution.KeepRemote:
                    CopyFromDest(task, conflict.RelativePath);
                    break;
                case ConflictResolution.KeepBoth:
                    var ext = Path.GetExtension(conflict.RelativePath);
                    var baseName = conflict.RelativePath[..^ext.Length];
                    var backupName = $"{baseName}_backup_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                    var destBackup = Path.Combine(task.DestPath, backupName);
                    var srcExisting = Path.Combine(task.SourcePaths[0], conflict.RelativePath);
                    File.Copy(srcExisting, destBackup, overwrite: true);
                    CopyFromDest(task, conflict.RelativePath);
                    break;
            }
        }
    }

    private static void CopyFromSource(BackupTask task, string relativePath)
    {
        var srcPath = Path.Combine(task.SourcePaths[0], relativePath);
        var destPath = Path.Combine(task.DestPath, relativePath);
        var destDir = Path.GetDirectoryName(destPath)!;
        if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
        File.Copy(srcPath, destPath, overwrite: true);
    }

    private static void CopyFromDest(BackupTask task, string relativePath)
    {
        var srcPath = Path.Combine(task.DestPath, relativePath);
        var destPath = Path.Combine(task.SourcePaths[0], relativePath);
        var destDir = Path.GetDirectoryName(destPath)!;
        if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
        File.Copy(srcPath, destPath, overwrite: true);
    }
}

public class SyncResult
{
    public int FilesSynced { get; set; }
    public List<ConflictFile> Conflicts { get; set; } = new();
    public bool HasConflicts => Conflicts.Count > 0;
}
