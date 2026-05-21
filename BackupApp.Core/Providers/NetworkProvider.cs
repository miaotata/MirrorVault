namespace BackupApp.Core.Providers;

public class NetworkProvider : IStorageProvider
{
    public string Name => "网络共享";

    public async Task<Stream> ReadFileAsync(string path)
    {
        return await Task.Run(() => File.OpenRead(path));
    }

    public async Task WriteFileAsync(string path, Stream data)
    {
        await Task.Run(async () =>
        {
            var dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            await using var fs = File.Create(path);
            await data.CopyToAsync(fs);
        });
    }

    public Task DeleteFileAsync(string path)
    {
        return Task.Run(() =>
        {
            if (File.Exists(path)) File.Delete(path);
        });
    }

    public Task<List<string>> ListFilesAsync(string directory)
    {
        return Task.Run(() =>
        {
            if (!Directory.Exists(directory)) return new List<string>();
            return Directory.GetFiles(directory, "*", SearchOption.AllDirectories).ToList();
        });
    }

    public Task CreateDirectoryAsync(string path)
    {
        return Task.Run(() => Directory.CreateDirectory(path));
    }

    public Task<bool> FileExistsAsync(string path)
    {
        return Task.Run(() => File.Exists(path));
    }

    public bool Supports(string destPath)
    {
        return destPath.StartsWith("\\\\");
    }
}
