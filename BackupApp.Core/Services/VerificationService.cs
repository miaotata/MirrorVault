using BackupApp.Core.Models;

namespace BackupApp.Core.Services;

public class VerificationService
{
    private readonly FileScanner _scanner;

    public VerificationService(FileScanner scanner)
    {
        _scanner = scanner;
    }

    public VerificationResult Verify(BackupTask task, BackupManifest manifest)
    {
        var result = new VerificationResult();
        foreach (var entry in manifest.Files)
        {
            string? sourcePath = null;
            foreach (var src in task.SourcePaths)
            {
                var candidate = Path.Combine(src, entry.RelativePath);
                if (File.Exists(candidate)) { sourcePath = candidate; break; }
            }

            if (sourcePath == null)
            {
                result.MissingFiles.Add(entry.RelativePath);
                continue;
            }

            var sourceHash = _scanner.ComputeHash(sourcePath);
            entry.Sha256Hash = sourceHash;

            if (entry.LastModified != File.GetLastWriteTimeUtc(sourcePath) ||
                entry.Size != new FileInfo(sourcePath).Length)
            {
                result.MismatchedFiles.Add(entry.RelativePath);
            }
        }

        result.AllOk = result.MismatchedFiles.Count == 0 && result.MissingFiles.Count == 0;
        return result;
    }
}

public class VerificationResult
{
    public bool AllOk { get; set; }
    public List<string> MismatchedFiles { get; set; } = new();
    public List<string> MissingFiles { get; set; } = new();
}
