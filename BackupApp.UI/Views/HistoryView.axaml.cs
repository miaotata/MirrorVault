using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using BackupApp.Core.Models;
using BackupApp.Core.Services;

namespace BackupApp.UI.Views;

public partial class HistoryView : UserControl
{
    private readonly BackupEngine _engine;
    private readonly BackupTask _task;
    private List<BackupManifest> _manifests = new();

    public HistoryView(BackupEngine engine, BackupTask task)
    {
        _engine = engine;
        _task = task;
        InitializeComponent();
        LoadHistory();
    }

    private void LoadHistory()
    {
        HeaderText.Text = $"备份历史 — {_task.Name}";
        _manifests = _engine.GetTimeline(_task.DestPath, _task.Name);
        SubtitleText.Text = $"共 {_manifests.Count} 次备份记录";

        HistoryPanel.Children.Clear();

        if (!_manifests.Any())
        {
            HistoryPanel.Children.Add(new TextBlock
            {
                Text = "暂无备份记录",
                FontSize = 13,
                Foreground = Res("AppMutedBrush", "#8e8e93"),
                Margin = new Avalonia.Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            return;
        }

        foreach (var m in _manifests.OrderByDescending(m => m.Timestamp))
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Avalonia.Thickness(14, 12),
                Background = Res("AppCardBgBrush", "#ffffff"),
                BorderBrush = Res("AppHairlineBrush", "#e0e0e0"),
                BorderThickness = new Avalonia.Thickness(0.5)
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };

            // Status icon
            var statusIcon = m.Status == BackupStatus.Success ? "\u2713" : "\u2717";
            var statusColor = m.Status == BackupStatus.Success ? "#2E7D32" :
                              m.Status == BackupStatus.Partial ? "#F57F17" : "#C62828";
            var icon = new Border
            {
                Width = 36, Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = Brush.Parse(m.Status == BackupStatus.Success ? "#e8f5e9" :
                               m.Status == BackupStatus.Partial ? "#fff8e1" : "#ffebee"),
                Child = new TextBlock
                {
                    Text = statusIcon, FontSize = 16,
                    Foreground = Brush.Parse(statusColor),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Margin = new Avalonia.Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(icon);
            Grid.SetColumn(icon, 0);

            // Info
            var info = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock
            {
                Text = m.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                FontSize = 13, FontWeight = FontWeight.SemiBold,
                Foreground = Res("AppInkBrush", "#1d1d1f")
            });
            info.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = $"{m.Files.Count} 个文件", FontSize = 11, Foreground = Res("AppBodyBrush", "#555") },
                    new TextBlock { Text = $"模式: {m.Mode}", FontSize = 11, Foreground = Res("AppBodyBrush", "#555") },
                    new TextBlock { Text = $"耗时: {m.Duration.TotalSeconds:F1}s", FontSize = 11, Foreground = Res("AppBodyBrush", "#555") }
                }
            });
            grid.Children.Add(info);
            Grid.SetColumn(info, 1);

            // Status badge
            var badge = new Border
            {
                Padding = new Avalonia.Thickness(8, 3),
                CornerRadius = new CornerRadius(12),
                Background = Brush.Parse(m.Status == BackupStatus.Success ? "#e8f5e9" :
                               m.Status == BackupStatus.Partial ? "#fff8e1" : "#ffebee"),
                Child = new TextBlock
                {
                    Text = m.Status == BackupStatus.Success ? "成功" :
                           m.Status == BackupStatus.Partial ? "部分成功" : "失败",
                    FontSize = 11, FontWeight = FontWeight.SemiBold,
                    Foreground = Brush.Parse(statusColor)
                },
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(badge);
            Grid.SetColumn(badge, 2);

            card.Child = grid;
            HistoryPanel.Children.Add(card);
        }
    }

    private static IBrush Res(string key, string fallbackHex)
    {
        if (Application.Current!.Resources.TryGetValue(key, out var val) && val is IBrush b)
            return b;
        return Brush.Parse(fallbackHex);
    }
}
