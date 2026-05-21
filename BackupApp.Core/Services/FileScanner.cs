using System.Security.Cryptography;
using BackupApp.Core.Models;

namespace BackupApp.Core.Services;

public class FileScanner
{
    public ScanResult Scan(List<string> sourcePaths, FileFilter filter, BackupManifest? previousManifest)
    {
        var result = new ScanResult();
        var previousFiles = previousManifest?.Files.ToDictionary(f => f.RelativePath) ?? new();

        foreach (var sourcePath in sourcePaths)
        {
            if (!Directory.Exists(sourcePath)) continue;
            ScanDirectory(sourcePath, sourcePath, filter, previousFiles, result);
        }

        MarkDeletedFiles(previousFiles, result);
        return result;
    }

    private void ScanDirectory(string basePath, string currentPath, FileFilter filter,
        Dictionary<string, FileEntry> previousFiles, ScanResult result)
    {
        var files = Directory.GetFiles(currentPath);
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(basePath, file);
            var fileInfo = new FileInfo(file);

            if (!ShouldInclude(fileInfo, relativePath, filter)) continue;

            var entry = new FileEntry
            {
                RelativePath = relativePath,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc,
                Sha256Hash = ""
            };

            if (previousFiles.TryGetValue(relativePath, out var prev))
            {
                if (prev.Size == entry.Size && prev.LastModified == entry.LastModified)
                    entry.Status = FileStatus.Unchanged;
                else
                    entry.Status = FileStatus.Modified;
            }
            else
            {
                entry.Status = FileStatus.Added;
            }

            result.Files.Add(entry);
        }

        foreach (var dir in Directory.GetDirectories(currentPath))
        {
            var dirName = Path.GetFileName(dir);
            if (filter.ExcludeDirectories.Any(d => MatchesPattern(dirName, d))) continue;
            ScanDirectory(basePath, dir, filter, previousFiles, result);
        }
    }

    private static void MarkDeletedFiles(Dictionary<string, FileEntry> previousFiles, ScanResult result)
    {
        foreach (var (path, entry) in previousFiles)
        {
            if (!result.Files.Any(f => f.RelativePath == path))
            {
                result.Files.Add(new FileEntry
                {
                    RelativePath = path,
                    Size = entry.Size,
                    LastModified = entry.LastModified,
                    Status = FileStatus.Deleted
                });
            }
        }
    }

    private static bool ShouldInclude(FileInfo fileInfo, string relativePath, FileFilter filter)
    {
        if (filter.MaxFileSizeBytes > 0 && fileInfo.Length > filter.MaxFileSizeBytes)
            return false;

        if (filter.ExcludePatterns.Count > 0)
        {
            foreach (var pattern in filter.ExcludePatterns)
            {
                if (MatchesPattern(fileInfo.Name, pattern)) return false;
            }
        }

        if (filter.IncludePatterns.Count > 0)
        {
            bool matched = false;
            foreach (var pattern in filter.IncludePatterns)
            {
                if (MatchesPattern(fileInfo.Name, pattern)) { matched = true; break; }
            }
            if (!matched) return false;
        }

        return true;
    }

    private static bool MatchesPattern(string name, string pattern)
    {
        if (pattern.StartsWith("*."))
        {
            var ext = pattern[1..];
            return name.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
        }
        if (pattern.StartsWith("*") && pattern.EndsWith("*"))
        {
            var middle = pattern.Trim('*');
            return name.Contains(middle, StringComparison.OrdinalIgnoreCase);
        }
        if (pattern.StartsWith("*"))
            return name.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);
        if (pattern.EndsWith("*"))
            return name.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);
        return name.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    public string ComputeHash(string filePath)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public class ScanResult
{
    public List<FileEntry> Files { get; set; } = new();
    public long TotalSize => Files.Sum(f => f.Size);
    public int AddedCount => Files.Count(f => f.Status == FileStatus.Added);
    public int ModifiedCount => Files.Count(f => f.Status == FileStatus.Modified);
    public int DeletedCount => Files.Count(f => f.Status == FileStatus.Deleted);
    public int UnchangedCount => Files.Count(f => f.Status == FileStatus.Unchanged);
    public bool HasChanges => AddedCount > 0 || ModifiedCount > 0 || DeletedCount > 0;
}
