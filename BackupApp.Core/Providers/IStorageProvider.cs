namespace BackupApp.Core.Providers;

public interface IStorageProvider
{
    string Name { get; }
    Task<Stream> ReadFileAsync(string path);
    Task WriteFileAsync(string path, Stream data);
    Task DeleteFileAsync(string path);
    Task<List<string>> ListFilesAsync(string directory);
    Task CreateDirectoryAsync(string path);
    Task<bool> FileExistsAsync(string path);
    bool Supports(string destPath);
}
