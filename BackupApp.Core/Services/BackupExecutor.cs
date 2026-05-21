using BackupApp.Core.Models;
using BackupApp.Core.Storage;

namespace BackupApp.Core.Services;

public class BackupExecutor
{
    private readonly BackupLayout _layout;
    private readonly FileScanner _scanner;

    public BackupExecutor(BackupLayout layout, FileScanner scanner)
    {
        _layout = layout;
        _scanner = scanner;
    }

    public BackupManifest ExecuteOneWay(BackupTask task, ScanResult scan, BackupManifest? previousManifest)
    {
        var timestamp = DateTime.Now;
        var dataDir = _layout.GetDataDir(task.DestPath, task.Name, timestamp);
        Directory.CreateDirectory(dataDir);

        foreach (var entry in scan.Files.Where(f => f.Status != FileStatus.Deleted))
        {
            var destPath = Path.Combine(dataDir, entry.RelativePath);
            var destDir = Path.GetDirectoryName(destPath)!;
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            var sourcePath = FindSourcePath(task.SourcePaths, entry.RelativePath);
            if (sourcePath == null) continue;

            File.Copy(sourcePath, destPath, overwrite: true);
            File.SetLastWriteTimeUtc(destPath, entry.LastModified);
        }

        return CreateManifest(task, scan, timestamp, dataDir);
    }

    public BackupManifest ExecuteIncremental(BackupTask task, ScanResult scan, BackupManifest? previousManifest)
    {
        var timestamp = DateTime.Now;
        var dataDir = _layout.GetDataDir(task.DestPath, task.Name, timestamp);
        Directory.CreateDirectory(dataDir);

        var prevDataDir = previousManifest != null
            ? _layout.GetDataDir(task.DestPath, task.Name, previousManifest.Timestamp)
            : null;

        foreach (var entry in scan.Files)
        {
            var destPath = Path.Combine(dataDir, entry.RelativePath);
            var destDir = Path.GetDirectoryName(destPath)!;
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            switch (entry.Status)
            {
                case FileStatus.Unchanged when prevDataDir != null:
                    var prevFilePath = Path.Combine(prevDataDir, entry.RelativePath);
                    if (File.Exists(prevFilePath))
                    {
                        HardLinkHelper.CreateHardLink(prevFilePath, destPath);
                    }
                    break;

                case FileStatus.Added:
                case FileStatus.Modified:
                    var sourcePath = FindSourcePath(task.SourcePaths, entry.RelativePath);
                    if (sourcePath == null) continue;
                    File.Copy(sourcePath, destPath, overwrite: true);
                    File.SetLastWriteTimeUtc(destPath, entry.LastModified);
                    break;
            }
        }

        return CreateManifest(task, scan, timestamp, dataDir);
    }

    private BackupManifest CreateManifest(BackupTask task, ScanResult scan, DateTime timestamp, string dataDir)
    {
        var manifest = new BackupManifest
        {
            TaskId = task.Id,
            Timestamp = timestamp,
            Files = scan.Files.Where(f => f.Status != FileStatus.Deleted).ToList(),
            Mode = task.BackupMode,
            Status = BackupStatus.Success
        };
        return manifest;
    }

    private static string? FindSourcePath(List<string> sourcePaths, string relativePath)
    {
        foreach (var source in sourcePaths)
        {
            var fullPath = Path.Combine(source, relativePath);
            if (File.Exists(fullPath)) return fullPath;
        }
        return null;
    }
}
