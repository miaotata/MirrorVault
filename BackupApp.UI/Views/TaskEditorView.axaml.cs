using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using BackupApp.Core.Models;
using BackupApp.Core.Storage;

namespace BackupApp.UI.Views;

public partial class TaskEditorView : UserControl
{
    private readonly ConfigStore _configStore;
    private readonly BackupTask _task;

    public event Action? Done;

    public TaskEditorView(ConfigStore configStore, BackupTask? existingTask)
    {
        _configStore = configStore;
        _task = existingTask ?? new BackupTask();
        InitializeComponent();
        LoadTask();
        BindEvents();
    }

    private void BindEvents()
    {
        BtnSave.Click += (_, _) => SaveTask();
        BtnCancel.Click += (_, _) => Done?.Invoke();
        BtnBrowseSource.Click += async (_, _) => await BrowseFolder(TxtNewSourcePath);
        BtnBrowseDest.Click += async (_, _) => await BrowseFolder(TxtDestPath);
        BtnAddSource.Click += (_, _) => AddSourcePath();
        BtnRemoveSource.Click += (_, _) => RemoveSourcePath();
        BtnHourly.Click += (_, _) => TxtCron.Text = "0 * * * *";
        BtnDaily.Click += (_, _) => TxtCron.Text = "0 2 * * *";
        BtnWeekly.Click += (_, _) => TxtCron.Text = "0 3 * * 1";
        BtnAddTier.Click += (_, _) => AddTier();
        BtnRemoveTier.Click += (_, _) => RemoveTier();
        BtnPresetDaily.Click += (_, _) => ApplyPreset(new[] { new RetentionTier { Name = "1天内", MaxAgeHours = 24, KeepIntervalMinutes = 0 } });
        BtnPreset3Day.Click += (_, _) => ApplyPreset(new[] {
            new RetentionTier { Name = "1天内", MaxAgeHours = 24, KeepIntervalMinutes = 0 },
            new RetentionTier { Name = "1-3天", MaxAgeHours = 72, KeepIntervalMinutes = 1440 }
        });
        BtnPreset7Day.Click += (_, _) => ApplyPreset(new[] {
            new RetentionTier { Name = "1天内", MaxAgeHours = 24, KeepIntervalMinutes = 0 },
            new RetentionTier { Name = "1-3天", MaxAgeHours = 72, KeepIntervalMinutes = 1440 },
            new RetentionTier { Name = "3-7天", MaxAgeHours = 168, KeepIntervalMinutes = 4320 }
        });
        BtnPreset30Day.Click += (_, _) => ApplyPreset(new[] {
            new RetentionTier { Name = "1天内", MaxAgeHours = 24, KeepIntervalMinutes = 0 },
            new RetentionTier { Name = "1-3天", MaxAgeHours = 72, KeepIntervalMinutes = 1440 },
            new RetentionTier { Name = "3-7天", MaxAgeHours = 168, KeepIntervalMinutes = 4320 },
            new RetentionTier { Name = "7-30天", MaxAgeHours = 720, KeepIntervalMinutes = 10080 }
        });
        BtnPreset90Day.Click += (_, _) => ApplyPreset(new[] {
            new RetentionTier { Name = "1天内", MaxAgeHours = 24, KeepIntervalMinutes = 0 },
            new RetentionTier { Name = "1-3天", MaxAgeHours = 72, KeepIntervalMinutes = 1440 },
            new RetentionTier { Name = "3-7天", MaxAgeHours = 168, KeepIntervalMinutes = 4320 },
            new RetentionTier { Name = "7-30天", MaxAgeHours = 720, KeepIntervalMinutes = 10080 },
            new RetentionTier { Name = "30-90天", MaxAgeHours = 2160, KeepIntervalMinutes = 20160 }
        });
        ChkEncryption.IsCheckedChanged += (_, _) =>
            PanelPassword.IsVisible = ChkEncryption.IsChecked == true;
    }

    private async Task BrowseFolder(TextBox targetBox)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions { Title = "选择文件夹" });

            if (folders.Count > 0)
            {
                targetBox.Text = folders[0].Path.LocalPath;
            }
        }
        catch
        {
            // Folder picker not available — user types manually
        }
    }

    private void ApplyPreset(RetentionTier[] tiers)
    {
        _task.RetentionTiers.Clear();
        _task.RetentionTiers.AddRange(tiers);
        RefreshTierList();
    }

    // ===== Time formatting =====

    private static string FormatMinutes(int minutes)
    {
        if (minutes == 0) return "全部";
        if (minutes < 60) return $"{minutes}min";
        if (minutes % 1440 == 0) return $"{minutes / 1440}天";
        if (minutes % 60 == 0) return $"{minutes / 60}h";
        return $"{minutes}min";
    }

    private static string FormatHours(int hours)
    {
        if (hours == 0) return "全部";
        if (hours < 24) return $"{hours}h";
        if (hours % 24 == 0) return $"{hours / 24}天";
        return $"{hours}h";
    }

    // ===== Tier list =====

    private void RefreshTierList()
    {
        TierListBox.ItemsSource = _task.RetentionTiers.Select(t =>
            $"{t.Name}: {FormatHours(t.MaxAgeHours)}内, 每{FormatMinutes(t.KeepIntervalMinutes)}保留1份").ToList();
    }

    private void LoadTask()
    {
        TxtName.Text = _task.Name;
        ChkEnabled.IsChecked = _task.Enabled;
        SourcePathList.ItemsSource = new List<string>(_task.SourcePaths);
        TxtDestPath.Text = _task.DestPath;
        CmbDestType.SelectedIndex = (int)_task.DestType;

        RbOneWay.IsChecked = _task.BackupMode == BackupMode.OneWay;
        RbIncremental.IsChecked = _task.BackupMode == BackupMode.Incremental;
        RbTwoWaySync.IsChecked = _task.BackupMode == BackupMode.TwoWaySync;

        ChkSchedule.IsChecked = _task.Schedule.Enabled;
        TxtCron.Text = _task.Schedule.CronExpression;

        TxtInclude.Text = string.Join(";", _task.Filters.IncludePatterns);
        TxtExclude.Text = string.Join(";", _task.Filters.ExcludePatterns);
        TxtExcludeDirs.Text = string.Join(";", _task.Filters.ExcludeDirectories);
        NumMaxSize.Value = (decimal)_task.Filters.MaxFileSizeBytes / 1048576;

        RefreshTierList();

        ChkEncryption.IsChecked = _task.Options.EncryptionEnabled;
        ChkCompression.IsChecked = _task.Options.CompressionEnabled;
        ChkVerify.IsChecked = _task.Options.VerifyAfterBackup;
        ChkPreview.IsChecked = _task.Options.PreviewBeforeRun;
        PanelPassword.IsVisible = _task.Options.EncryptionEnabled;
    }

    private void AddSourcePath()
    {
        var path = TxtNewSourcePath.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(path))
        {
            _task.SourcePaths.Add(path);
            SourcePathList.ItemsSource = new List<string>(_task.SourcePaths);
            TxtNewSourcePath.Text = "";
        }
    }

    private void RemoveSourcePath()
    {
        if (SourcePathList.SelectedItem is string path)
        {
            _task.SourcePaths.Remove(path);
            SourcePathList.ItemsSource = new List<string>(_task.SourcePaths);
        }
    }

    private void AddTier()
    {
        _task.RetentionTiers.Add(new RetentionTier { Name = "新层", MaxAgeHours = 168, KeepIntervalMinutes = 1440 });
        RefreshTierList();
    }

    private void RemoveTier()
    {
        if (TierListBox.SelectedIndex >= 0 && TierListBox.SelectedIndex < _task.RetentionTiers.Count)
        {
            _task.RetentionTiers.RemoveAt(TierListBox.SelectedIndex);
            RefreshTierList();
        }
    }

    private void SaveTask()
    {
        _task.Name = TxtName.Text ?? "未命名任务";
        _task.Enabled = ChkEnabled.IsChecked == true;
        _task.DestPath = TxtDestPath.Text ?? "";
        _task.DestType = (DestType)CmbDestType.SelectedIndex;

        if (RbOneWay.IsChecked == true) _task.BackupMode = BackupMode.OneWay;
        else if (RbIncremental.IsChecked == true) _task.BackupMode = BackupMode.Incremental;
        else if (RbTwoWaySync.IsChecked == true) _task.BackupMode = BackupMode.TwoWaySync;

        _task.Schedule.Enabled = ChkSchedule.IsChecked == true;
        _task.Schedule.CronExpression = TxtCron.Text ?? "";

        _task.Filters.IncludePatterns = (TxtInclude.Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        _task.Filters.ExcludePatterns = (TxtExclude.Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        _task.Filters.ExcludeDirectories = (TxtExcludeDirs.Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        _task.Filters.MaxFileSizeBytes = (long)(NumMaxSize.Value ?? 0) * 1048576;

        _task.Options.EncryptionEnabled = ChkEncryption.IsChecked == true;
        _task.Options.CompressionEnabled = ChkCompression.IsChecked == true;
        _task.Options.VerifyAfterBackup = ChkVerify.IsChecked == true;
        _task.Options.PreviewBeforeRun = ChkPreview.IsChecked == true;

        _configStore.SaveTask(_task);
        Done?.Invoke();
    }
}
