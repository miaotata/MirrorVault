using System.IO.Compression;

namespace BackupApp.Core.Services;

public class CompressionService
{
    public string CompressDirectory(string sourceDir, string? outputPath = null)
    {
        outputPath ??= sourceDir + ".zip";
        if (File.Exists(outputPath)) File.Delete(outputPath);
        ZipFile.CreateFromDirectory(sourceDir, outputPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        return outputPath;
    }

    public void DecompressToDirectory(string archivePath, string destDir)
    {
        if (Directory.Exists(destDir)) Directory.Delete(destDir, recursive: true);
        ZipFile.ExtractToDirectory(archivePath, destDir);
    }
}
