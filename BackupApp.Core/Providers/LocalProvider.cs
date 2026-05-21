namespace BackupApp.Core.Providers;

public class LocalProvider : IStorageProvider
{
    public string Name => "本地磁盘";

    public Task<Stream> ReadFileAsync(string path)
    {
        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    public async Task WriteFileAsync(string path, Stream data)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        await using var fs = File.Create(path);
        await data.CopyToAsync(fs);
    }

    public Task DeleteFileAsync(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<List<string>> ListFilesAsync(string directory)
    {
        if (!Directory.Exists(directory)) return Task.FromResult(new List<string>());
        return Task.FromResult(Directory.GetFiles(directory, "*", SearchOption.AllDirectories).ToList());
    }

    public Task CreateDirectoryAsync(string path)
    {
        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }

    public Task<bool> FileExistsAsync(string path)
    {
        return Task.FromResult(File.Exists(path));
    }

    public bool Supports(string destPath)
    {
        return Directory.Exists(Path.GetPathRoot(destPath)) || !destPath.StartsWith("\\\\");
    }
}
