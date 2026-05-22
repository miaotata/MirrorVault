using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using BackupApp.Core.Models;
using BackupApp.Core.Services;

namespace BackupApp.UI.Views;

public partial class PreviewView : UserControl
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public Task<bool> WaitForResultAsync() => _tcs.Task;
    public event Action? Confirmed;
    public event Action? Cancelled;

    public PreviewView(ScanResult scan, string taskName)
    {
        InitializeComponent();
        TitleText.Text = $"任务 '{taskName}' 变更预览";
        AddedText.Text = $"新增: {scan.AddedCount}";
        ModifiedText.Text = $"修改: {scan.ModifiedCount}";
        DeletedText.Text = $"删除: {scan.DeletedCount}";
        UnchangedText.Text = $"不变: {scan.UnchangedCount}";

        var items = scan.Files
            .Where(f => f.Status != FileStatus.Unchanged)
            .Select(f => $"[{f.Status}] {f.RelativePath} ({FormatSize(f.Size)})")
            .ToList();
        FileListBox.ItemsSource = items;

        BtnProceed.Click += (_, _) => { _tcs.TrySetResult(true); Confirmed?.Invoke(); };
        BtnCancel.Click += (_, _) => { _tcs.TrySetResult(false); Cancelled?.Invoke(); };
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
        return $"{bytes / 1073741824.0:F2} GB";
    }
}
