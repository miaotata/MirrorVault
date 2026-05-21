using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using BackupApp.Core.Models;
using BackupApp.Core.Services;

namespace BackupApp.UI.Views;

public partial class RestoreWizardView : UserControl
{
    private readonly BackupEngine _engine;
    private readonly BackupTask _task;
    private int _step;
    private List<BackupManifest> _timeline = new();
    private BackupManifest? _selected;
    private string _selectedFile = "";

    public event Action? Done;

    public RestoreWizardView(BackupEngine engine, BackupTask task)
    {
        _engine = engine;
        _task = task;
        InitializeComponent();
        BtnBack.Click += (_, _) => GoBack();
        BtnNext.Click += (_, _) => GoNext();
        BtnCancel.Click += (_, _) => Done?.Invoke();
        ShowStep1();
    }

    private void ShowStep1()
    {
        StepTitle.Text = "步骤 1/4: 选择备份任务";
        _timeline = _engine.GetTimeline(_task.DestPath, _task.Name);
        var sp = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(10) };
        sp.Children.Add(new TextBlock { Text = $"任务: {_task.Name}", FontWeight = FontWeight.Bold });
        sp.Children.Add(new TextBlock { Text = $"共 {_timeline.Count} 个备份版本" });

        var lb = new ListBox { Height = 250 };
        lb.ItemsSource = _timeline.Select(m => $"{m.Timestamp:yyyy-MM-dd HH:mm:ss} | {m.Files.Count} 个文件 | {m.Status}");
        lb.SelectionChanged += (_, _) =>
        {
            if (lb.SelectedIndex >= 0) _selected = _timeline[lb.SelectedIndex];
        };
        sp.Children.Add(lb);
        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(sp);
        UpdateButtons(false, true);
    }

    private void ShowStep2()
    {
        StepTitle.Text = "步骤 2/4: 确认时间点";
        var sp = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(10) };
        sp.Children.Add(new TextBlock { Text = $"时间点: {_selected?.Timestamp:yyyy-MM-dd HH:mm:ss}", FontWeight = FontWeight.Bold });
        sp.Children.Add(new TextBlock { Text = $"备份文件数: {_selected?.Files.Count}" });
        sp.Children.Add(new TextBlock { Text = $"备份模式: {_selected?.Mode}" });
        sp.Children.Add(new TextBlock { Text = $"耗时: {_selected?.Duration.TotalSeconds:F1} 秒" });
        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(sp);
        UpdateButtons(true, true);
    }

    private void ShowStep3()
    {
        StepTitle.Text = "步骤 3/4: 选择要还原的文件";
        var sp = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(10) };

        var lb = new ListBox { Height = 300 };
        var files = _selected?.Files.OrderBy(f => f.RelativePath).Select(f => f.RelativePath).ToList() ?? new();
        lb.ItemsSource = files;
        lb.SelectionChanged += (_, _) =>
        {
            if (lb.SelectedItem is string path) _selectedFile = path;
        };
        sp.Children.Add(new TextBlock { Text = "选择文件还原（或下一步全量还原）" });
        sp.Children.Add(lb);

        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(sp);
        UpdateButtons(true, true);
    }

    private void ShowStep4()
    {
        StepTitle.Text = "步骤 4/4: 还原执行";
        BtnNext.IsEnabled = false;

        var sp = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(10) };
        var progress = new TextBlock { Text = "正在还原..." };
        sp.Children.Add(progress);

        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(sp);

        if (_selected == null) return;

        try
        {
            RestoreResult result;
            if (!string.IsNullOrEmpty(_selectedFile))
            {
                var restoreTo = Path.Combine(_task.SourcePaths[0], _selectedFile);
                result = _engine.RestoreFile(_task, _selected.RunId, _selectedFile, restoreTo);
            }
            else
            {
                result = _engine.RestoreAll(_task, _selected.RunId, _task.SourcePaths[0]);
            }

            progress.Text = result.Success
                ? $"还原完成: {result.RestoredFiles}/{result.TotalFiles} 个文件"
                : $"还原完成（有失败）: {result.RestoredFiles}/{result.TotalFiles}, 失败 {result.FailedFiles.Count} 个";
        }
        catch (Exception ex)
        {
            progress.Text = $"还原失败: {ex.Message}";
        }

        BtnBack.Content = "关闭";
        BtnBack.IsEnabled = true;
        BtnNext.IsEnabled = false;
    }

    private void GoBack()
    {
        _step--;
        switch (_step)
        {
            case 0: ShowStep1(); break;
            case 1: ShowStep2(); break;
            case 2: ShowStep3(); break;
        }
    }

    private void GoNext()
    {
        _step++;
        switch (_step)
        {
            case 1: ShowStep2(); break;
            case 2: ShowStep3(); break;
            case 3: ShowStep4(); break;
        }
    }

    private void UpdateButtons(bool back, bool next)
    {
        BtnBack.IsEnabled = back;
        BtnNext.IsEnabled = next;
        BtnBack.Content = "上一步";
    }
}
