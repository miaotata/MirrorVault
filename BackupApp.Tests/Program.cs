using System.Collections.Generic;
using System.Linq;
using BackupApp.Core.Models;
using BackupApp.Core.Services;
using BackupApp.Core.Storage;

var testDir = Path.Combine(Path.GetTempPath(), "BackupApp_Tests_" + DateTime.Now.ToString("yyyyMMddHHmmss"));
var srcDir = Path.Combine(testDir, "src");
var dstDir = Path.Combine(testDir, "dst");
var restoreDir = Path.Combine(testDir, "restore");

Directory.CreateDirectory(srcDir);
Directory.CreateDirectory(dstDir);
Directory.CreateDirectory(restoreDir);

Console.WriteLine($"Test directory: {testDir}\n");

int passed = 0, failed = 0;

void Assert(string name, bool condition, string detail = "")
{
    if (condition) { Console.WriteLine($"  [PASS] {name}"); passed++; }
    else { Console.WriteLine($"  [FAIL] {name} - {detail}"); failed++; }
}

// ========== Setup ==========
var configStore = new ConfigStore(Path.Combine(testDir, "config.db"));
var layout = new BackupLayout();
var manifestStore = new ManifestStore(layout);
var scanner = new FileScanner();
var executor = new BackupExecutor(layout, scanner);
var syncEngine = new SyncEngine(layout, scanner);
var encryption = new EncryptionService();
var compression = new CompressionService();
var verification = new VerificationService(scanner);
var versionManager = new VersionManager(manifestStore, layout);
var restoreEngine = new RestoreEngine(layout, manifestStore, encryption, compression);
var engine = new BackupEngine(configStore, layout, manifestStore, scanner, executor,
    syncEngine, restoreEngine, verification, encryption, compression, versionManager);

// Create test files
File.WriteAllText(Path.Combine(srcDir, "file1.txt"), "Hello World " + Guid.NewGuid());
File.WriteAllText(Path.Combine(srcDir, "file2.txt"), "Test Content " + Guid.NewGuid());
var subDir = Path.Combine(srcDir, "subdir");
Directory.CreateDirectory(subDir);
File.WriteAllText(Path.Combine(subDir, "file3.txt"), "Sub File " + Guid.NewGuid());
File.WriteAllText(Path.Combine(srcDir, "excluded.tmp"), "Should be excluded");

// ========== Test 1: Task CRUD ==========
Console.WriteLine("--- Test 1: Task Config CRUD ---");
var task = new BackupTask
{
    Name = "Test Task",
    SourcePaths = new List<string> { srcDir },
    DestPath = dstDir,
    DestType = DestType.Local,
    BackupMode = BackupMode.OneWay,
    Filters = new FileFilter { ExcludePatterns = new List<string> { "*.tmp" } },
    Options = new TaskOptions { PreviewBeforeRun = false }
};
configStore.SaveTask(task);
var loaded = configStore.GetTask(task.Id);
Assert("Save/Load task", loaded != null);
Assert("Task name preserved", loaded?.Name == "Test Task");
Assert("Source path preserved", loaded?.SourcePaths[0] == srcDir);

// ========== Test 2: One-Way Backup ==========
Console.WriteLine("--- Test 2: One-Way Backup ---");
var manifest = await engine.RunBackupAsync(task.Id);
Assert("One-way backup succeeds", manifest?.Status == BackupStatus.Success, manifest?.Status.ToString() ?? "null");
Assert("3 files backed up (no .tmp)", manifest?.Files.Count == 3);

// ========== Test 3: File Filtering ==========
Console.WriteLine("--- Test 3: File Filtering ---");
bool hasTmp = manifest!.Files.Any(f => f.RelativePath.Contains(".tmp"));
Assert("Excluded .tmp files", !hasTmp);

// ========== Test 4: Verification (before modifying source) ==========
Console.WriteLine("--- Test 4: Verification ---");
var verifyResult = verification.Verify(task, manifest);
Console.WriteLine($"  [DEBUG] Verification: allOk={verifyResult.AllOk}, mismatched={string.Join(", ", verifyResult.MismatchedFiles)}, missing={string.Join(", ", verifyResult.MissingFiles)}");
Assert("Verification passes for fresh backup", verifyResult.AllOk, string.Join(", ", verifyResult.MismatchedFiles));

// ========== Test 5: Incremental Backup ==========
Console.WriteLine("--- Test 5: Incremental Backup ---");
task.BackupMode = BackupMode.Incremental;
configStore.SaveTask(task);

Thread.Sleep(50); // ensure distinct timestamp

// Modify a file
File.AppendAllText(Path.Combine(srcDir, "file1.txt"), " MODIFIED");
// Add a new file
File.WriteAllText(Path.Combine(srcDir, "file4.txt"), "New file");

var manifest2 = await engine.RunBackupAsync(task.Id);
Console.WriteLine($"  [DEBUG] Incremental: status={manifest2?.Status}, files={manifest2?.Files.Count}, errors={manifest2?.ErrorLog.Count ?? 0}");
foreach (var e in manifest2?.ErrorLog ?? new()) Console.WriteLine($"    Error: {e}");
Assert("Incremental backup succeeds", manifest2?.Status == BackupStatus.Success);

var changedCount = manifest2!.Files.Count(f => f.Status == FileStatus.Modified);
var addedCount = manifest2.Files.Count(f => f.Status == FileStatus.Added);
var unchangedCount = manifest2.Files.Count(f => f.Status == FileStatus.Unchanged);
Console.WriteLine($"  [DEBUG] changed={changedCount}, added={addedCount}, unchanged={unchangedCount}");
Assert("Detects modified files", changedCount == 1, $"got {changedCount}");
Assert("Detects new files", addedCount == 1, $"got {addedCount}");
Assert("Unchanged files tracked", unchangedCount >= 2, $"got {unchangedCount}");

// ========== Test 6: Restore ==========
Console.WriteLine("--- Test 6: Restore ---");
var restoreTarget = Path.Combine(restoreDir, "restored_file1.txt");
engine.RestoreFile(task, manifest.RunId, "file1.txt", restoreTarget);
Assert("Single file restore", File.Exists(restoreTarget));

var fullRestoreResult = engine.RestoreAll(task, manifest.RunId, Path.Combine(restoreDir, "full_restore"));
Console.WriteLine($"  [DEBUG] Full restore: {fullRestoreResult.RestoredFiles}/{fullRestoreResult.TotalFiles}, failed: {string.Join(", ", fullRestoreResult.FailedFiles)}");
Assert("Full restore succeeds", fullRestoreResult.Success, $"restored {fullRestoreResult.RestoredFiles}/{fullRestoreResult.TotalFiles}");

// ========== Test 7: Encryption ==========
Console.WriteLine("--- Test 7: Encryption ---");
var testFile = Path.Combine(testDir, "encrypt_src.txt");
var encryptedFile = Path.Combine(testDir, "encrypted.bin");
var decryptedFile = Path.Combine(testDir, "decrypted.txt");
File.WriteAllText(testFile, "Secret Data " + Guid.NewGuid());
encryption.EncryptFile(testFile, encryptedFile, "testpassword123");

Assert("Encrypted file created", File.Exists(encryptedFile));
var origContent = File.ReadAllText(testFile);
encryption.DecryptFile(encryptedFile, decryptedFile, "testpassword123");
var decContent = File.ReadAllText(decryptedFile);
Assert("Decrypt matches original", decContent == origContent);

try
{
    encryption.DecryptFile(encryptedFile, Path.Combine(testDir, "wrong.txt"), "wrongpass");
    Assert("Wrong password throws", false);
}
catch { Assert("Wrong password throws", true); }

// ========== Test 8: Compression ==========
Console.WriteLine("--- Test 8: Compression ---");
var compressDir = Path.Combine(testDir, "compress_src");
Directory.CreateDirectory(compressDir);
File.WriteAllText(Path.Combine(compressDir, "a.txt"), new string('A', 10000));
long origSize = Directory.GetFiles(compressDir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);

var zipPath = compression.CompressDirectory(compressDir);
long zipSize = new FileInfo(zipPath).Length;
Assert("Compressed is smaller", zipSize < origSize, $"orig={origSize}, zip={zipSize}");

var decompressDir = Path.Combine(testDir, "decompress_out");
compression.DecompressToDirectory(zipPath, decompressDir);
Assert("Decompressed file exists", File.Exists(Path.Combine(decompressDir, "a.txt")));

// ========== Test 9: Version Manager ==========
Console.WriteLine("--- Test 9: Version Manager ---");
task.RetentionTiers = new List<RetentionTier>
{
    new RetentionTier { Name = "1天内", MaxAgeHours = 24, KeepIntervalMinutes = 0 },
    new RetentionTier { Name = "超过1天", MaxAgeHours = -1, KeepIntervalMinutes = 0 }
};
configStore.SaveTask(task);

int initialCount = manifestStore.ListManifests(dstDir, task.Name).Count;
Console.WriteLine($"  [DEBUG] Manifests before cleanup: {initialCount}");
Assert("Multiple manifests exist", initialCount >= 2, $"got {initialCount}");

// Create an "old" manifest
var oldManifest = new BackupManifest
{
    RunId = Guid.NewGuid(),
    TaskId = task.Id,
    Timestamp = DateTime.Now.AddHours(-25),
    Files = new List<FileEntry> { new FileEntry { RelativePath = "old.txt", Size = 100, LastModified = DateTime.Now.AddHours(-25) } },
    Mode = BackupMode.Incremental,
    Status = BackupStatus.Success
};
manifestStore.SaveManifest(dstDir, task.Name, oldManifest);

int deleted = versionManager.Cleanup(task);
Assert("Old version cleaned up", deleted >= 1, $"deleted {deleted}");

var stillExists = manifestStore.ListManifests(dstDir, task.Name).Any(m => m.RunId == oldManifest.RunId);
Assert("Old manifest removed from list", !stillExists);

// ========== Test 10: Scheduler Cron ==========
Console.WriteLine("--- Test 10: Scheduler Cron Parsing ---");
var now = new DateTime(2026, 5, 20, 14, 0, 0);
var nextRun = SchedulerService.GetNextRun("0 2 * * *", null, now);
Assert("Daily cron parses", nextRun.HasValue);
Assert("Next run is tomorrow 2 AM", nextRun?.Hour == 2 && nextRun?.Day == 21, nextRun?.ToString("yyyy-MM-dd HH:mm"));

nextRun = SchedulerService.GetNextRun("0 * * * *", null, now);
Assert("Hourly cron", nextRun.HasValue);
Assert("Next run is 15:00", nextRun?.Hour == 15, nextRun?.ToString());

// ========== Summary ==========
Console.WriteLine($"\n{new string('=', 50)}");
Console.WriteLine($"  TOTAL: {passed} passed, {failed} failed out of {passed + failed} tests");
Console.WriteLine($"  Test artifacts at: {testDir}");
Console.WriteLine($"{new string('=', 50)}");

if (failed > 0) { Console.WriteLine("\n  !!! SOME TESTS FAILED !!!"); return 1; }
return 0;
