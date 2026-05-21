using System.Text.RegularExpressions;
using BackupApp.Core.Models;

namespace BackupApp.Core.Storage;

public class BackupLayout
{
    private static readonly Regex InvalidPathChars = new(@"[/\\:\*\?""<>\|]", RegexOptions.Compiled);

    public string GetTaskRoot(string destPath, string taskName)
    {
        var safeName = InvalidPathChars.Replace(taskName, "_");
        return Path.Combine(destPath, safeName);
    }

    public string GetDataDir(string destPath, string taskName, DateTime timestamp)
    {
        var root = GetTaskRoot(destPath, taskName);
        return Path.Combine(root, "data", timestamp.ToString("yyyy-MM-dd_HH-mm-ss-fff"));
    }

    public string GetManifestDir(string destPath, string taskName)
    {
        var root = GetTaskRoot(destPath, taskName);
        return Path.Combine(root, "manifests");
    }

    public string GetManifestPath(string destPath, string taskName, DateTime timestamp)
    {
        var dir = GetManifestDir(destPath, taskName);
        return Path.Combine(dir, $"{timestamp:yyyy-MM-dd_HH-mm-ss-fff}.json");
    }

    public string GetSyncStatePath(string destPath, string taskName)
    {
        var root = GetTaskRoot(destPath, taskName);
        return Path.Combine(root, "sync_state.json");
    }

    public string GetTaskConfigCopyPath(string destPath, string taskName)
    {
        var root = GetTaskRoot(destPath, taskName);
        return Path.Combine(root, "task_config.json");
    }

    public void EnsureDataStructure(string destPath, string taskName)
    {
        var root = GetTaskRoot(destPath, taskName);
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "data"));
        Directory.CreateDirectory(Path.Combine(root, "manifests"));
    }
}
