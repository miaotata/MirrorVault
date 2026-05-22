using System.Threading.Tasks;
using BackupApp.Core.Models;
using BackupApp.Core.Storage;

namespace BackupApp.Core.Services;

public interface IBackupNotification
{
    void Notify(string title, string message, bool isError);
    Task<SyncResult?> OnConflictRequired(BackupTask task, SyncResult syncResult);
    Task<bool> OnPreviewReady(BackupTask task, ScanResult scan);
}

public class BackupEngine
{
    private readonly ConfigStore _configStore;
    private readonly BackupLayout _layout;
    private readonly ManifestStore _manifestStore;
    private readonly FileScanner _scanner;
    private readonly BackupExecutor _executor;
    private readonly SyncEngine _syncEngine;
    private readonly RestoreEngine _restoreEngine;
    private readonly VerificationService _verification;
    private readonly EncryptionService _encryption;
    private readonly CompressionService _compression;
    private readonly VersionManager _versionManager;
    private readonly IBackupNotification? _notification;

    public BackupEngine(
        ConfigStore configStore, BackupLayout layout, ManifestStore manifestStore,
        FileScanner scanner, BackupExecutor executor, SyncEngine syncEngine,
        RestoreEngine restoreEngine, VerificationService verification,
        EncryptionService encryption, CompressionService compression,
        VersionManager versionManager, IBackupNotification? notification = null)
    {
        _configStore = configStore;
        _layout = layout;
        _manifestStore = manifestStore;
        _scanner = scanner;
        _executor = executor;
        _syncEngine = syncEngine;
        _restoreEngine = restoreEngine;
        _verification = verification;
        _encryption = encryption;
        _compression = compression;
        _versionManager = versionManager;
        _notification = notification;
    }

    public async Task<BackupManifest?> RunBackupAsync(Guid taskId, IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default, string? password = null)
    {
        var task = _configStore.GetTask(taskId);
        if (task == null) throw new InvalidOperationException("Task not found");

        var startTime = DateTime.Now;
        var manifest = new BackupManifest { TaskId = task.Id, Mode = task.BackupMode, Timestamp = startTime };

        try
        {
            _layout.EnsureDataStructure(task.DestPath, task.Name);

            if (task.BackupMode == BackupMode.TwoWaySync)
            {
                var syncResult = await Task.Run(() => _syncEngine.ExecuteSync(task)).ConfigureAwait(true);
                if (syncResult.HasConflicts && _notification != null)
                {
                    var resolved = await _notification.OnConflictRequired(task, syncResult);
                    if (resolved != null)
                        _syncEngine.ApplyConflictResolutions(task, resolved);
                }

                manifest.Status = syncResult.HasConflicts ? BackupStatus.Partial : BackupStatus.Success;
                manifest.Files = _scanner.Scan(task.SourcePaths, task.Filters, null).Files;
                manifest.Duration = DateTime.Now - startTime;
                _manifestStore.SaveManifest(task.DestPath, task.Name, manifest);

                _notification?.Notify("双向同步完成",
                    $"同步 {syncResult.FilesSynced} 个文件，冲突 {syncResult.Conflicts.Count} 个",
                    syncResult.HasConflicts);

                _versionManager.Cleanup(task);
                return manifest;
            }

            var previousManifest = _manifestStore.GetLatestManifest(task.DestPath, task.Name);
            var scan = await Task.Run(() => _scanner.Scan(task.SourcePaths, task.Filters, previousManifest)).ConfigureAwait(true);

            if (task.Options.PreviewBeforeRun && _notification != null)
            {
                if (!await _notification.OnPreviewReady(task, scan))
                {
                    manifest.Status = BackupStatus.Failed;
                    manifest.ErrorLog.Add("User cancelled after preview");
                    return manifest;
                }
            }

            manifest = await Task.Run(() => task.BackupMode switch
            {
                BackupMode.OneWay => _executor.ExecuteOneWay(task, scan, previousManifest, progress, cancellationToken),
                BackupMode.Incremental => _executor.ExecuteIncremental(task, scan, previousManifest, progress, cancellationToken),
                _ => throw new NotSupportedException($"Mode {task.BackupMode} not supported")
            }, cancellationToken).ConfigureAwait(true);

            if (task.Options.CompressionEnabled)
            {
                await Task.Run(() =>
                {
                    var dataDir = _layout.GetDataDir(task.DestPath, task.Name, manifest.Timestamp);
                    _compression.CompressDirectory(dataDir);
                    Directory.Delete(dataDir, recursive: true);
                }).ConfigureAwait(true);
            }

            if (task.Options.VerifyAfterBackup)
            {
                var verifyResult = await Task.Run(() => _verification.Verify(task, manifest)).ConfigureAwait(true);
                if (!verifyResult.AllOk)
                {
                    manifest.Status = BackupStatus.Partial;
                    manifest.ErrorLog.AddRange(verifyResult.MismatchedFiles.Select(f => $"Mismatch: {f}"));
                    manifest.ErrorLog.AddRange(verifyResult.MissingFiles.Select(f => $"Missing: {f}"));
                }
            }

            manifest.Duration = DateTime.Now - startTime;
            _manifestStore.SaveManifest(task.DestPath, task.Name, manifest);

            var deleted = _versionManager.Cleanup(task);

            if (manifest.Status == BackupStatus.Success)
                _notification?.Notify("备份成功", $"任务 '{task.Name}' 完成，{manifest.Files.Count} 个文件，清理 {deleted} 个旧版本", false);
            else
                _notification?.Notify("备份完成（有问题）", $"任务 '{task.Name}' 部分失败，见错误日志", true);

            return manifest;
        }
        catch (Exception ex)
        {
            manifest.Status = BackupStatus.Failed;
            manifest.ErrorLog.Add(ex.ToString());
            manifest.Duration = DateTime.Now - startTime;
            _notification?.Notify("备份失败", $"任务 '{task.Name}': {ex.Message}", true);
            return manifest;
        }
    }

    public RestoreResult RestoreFile(BackupTask task, Guid runId, string relativePath, string restoreTo, string? password = null)
        => _restoreEngine.RestoreFile(task, runId, relativePath, restoreTo, password);

    public RestoreResult RestoreAll(BackupTask task, Guid runId, string restoreRoot, string? password = null, IProgress<int>? progress = null)
        => _restoreEngine.RestoreAll(task, runId, restoreRoot, password, progress);

    public List<BackupManifest> GetTimeline(string destPath, string taskName)
        => _restoreEngine.GetTimeline(destPath, taskName);

    public void DeleteVersion(string destPath, string taskName, Guid runId)
        => _manifestStore.DeleteManifest(destPath, taskName, runId);
}
