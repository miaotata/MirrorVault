using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using BackupApp.Core.Models;

namespace BackupApp.UI.Views;

public partial class ConflictView : UserControl
{
    public List<ConflictItem> Items { get; } = new();
    private readonly TaskCompletionSource<bool> _tcs = new();

    public Task<bool> WaitForResultAsync() => _tcs.Task;

    public ConflictView(List<ConflictFile> conflicts)
    {
        InitializeComponent();
        foreach (var c in conflicts)
        {
            Items.Add(new ConflictItem
            {
                RelativePath = c.RelativePath,
                SourceInfo = $"本地: {c.SourceModified:MM-dd HH:mm} ({FormatSize(c.SourceSize)})",
                DestInfo = $"远程: {c.DestModified:MM-dd HH:mm} ({FormatSize(c.DestSize)})",
                Conflict = c
            });
        }
        ConflictListBox.ItemsSource = Items;
        BtnApply.Click += (_, _) => _tcs.TrySetResult(true);
        BtnCancel.Click += (_, _) => _tcs.TrySetResult(false);
    }

    public List<ConflictResolution> GetResolutions()
    {
        return Items.Select(i => (ConflictResolution)i.SelectedResolution).ToList();
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1073741824) return $"{bytes / 1073741824.0:F1} MB";
        return $"{bytes / 1073741824.0:F2} GB";
    }
}

public class ConflictItem
{
    public string RelativePath { get; set; } = "";
    public string SourceInfo { get; set; } = "";
    public string DestInfo { get; set; } = "";
    public int SelectedResolution { get; set; }
    public ConflictFile Conflict { get; set; } = null!;
}
