using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using BackupApp.Core.Services;
using BackupApp.Core.Storage;

namespace BackupApp.UI.Views;

public partial class SettingsView : UserControl
{
    private readonly List<ThemeOption> _themes;
    private readonly ConfigStore _configStore;

    public SettingsView(ConfigStore configStore)
    {
        _configStore = configStore;
        InitializeComponent();

        _themes = new List<ThemeOption>
        {
            new() { Key = "claude", Name = "Claude 暖调人文", Desc = "米白底色 · 珊瑚橙 · 衬线标题", ColorHex = "#cc785c" },
            new() { Key = "apple", Name = "Apple 干净极简", Desc = "纯白背景 · 蓝色强调 · SF 字体", ColorHex = "#0066cc" },
            new() { Key = "vercel", Name = "Vercel 几何开发者", Desc = "黑白对比 · 锐利边框 · 几何感", ColorHex = "#171717" },
            new() { Key = "stripe", Name = "Stripe 金融商务", Desc = "靛蓝主色 · 轻薄阴影 · 产品化", ColorHex = "#533afd" },
            new() { Key = "spotify", Name = "Spotify 深色沉浸", Desc = "深黑底色 · 绿色点缀 · 圆润", ColorHex = "#1ed760" },
        };

        BuildThemeList();
        LoadSettings();
        BindSettingsEvents();
    }

    private void LoadSettings()
    {
        ChkAutoStart.IsChecked = _configStore.GetSetting("autostart") == "1";
        ChkMinimizeTray.IsChecked = _configStore.GetSetting("minimize_tray", "1") == "1";
        ChkNotify.IsChecked = _configStore.GetSetting("notify", "1") == "1";
    }

    private void BindSettingsEvents()
    {
        ChkAutoStart.IsCheckedChanged += (_, _) =>
            _configStore.SetSetting("autostart", ChkAutoStart.IsChecked == true ? "1" : "0");
        ChkMinimizeTray.IsCheckedChanged += (_, _) =>
            _configStore.SetSetting("minimize_tray", ChkMinimizeTray.IsChecked == true ? "1" : "0");
        ChkNotify.IsCheckedChanged += (_, _) =>
            _configStore.SetSetting("notify", ChkNotify.IsChecked == true ? "1" : "0");
    }

    private IBrush Res(string key, string fallbackHex)
    {
        return Application.Current!.Resources.TryGetValue(key, out var val) && val is IBrush b
            ? b
            : Brush.Parse(fallbackHex);
    }

    private void BuildThemeList()
    {
        ThemePanel.Children.Clear();
        var currentTheme = App.ThemeManager.CurrentTheme;

        foreach (var t in _themes)
        {
            var isActive = t.Key == currentTheme;

            var border = new Border
            {
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 3),
                Padding = new Thickness(14, 12),
                BorderThickness = new Thickness(isActive ? 1.5 : 0.5),
                BorderBrush = isActive ? Brush.Parse("#0066cc") : Res("AppHairlineBrush", "#e0e0e0"),
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };

            var swatch = new Border
            {
                Width = 32, Height = 32,
                CornerRadius = new CornerRadius(8),
                Background = Brush.Parse(t.ColorHex),
                Margin = new Thickness(0, 0, 12, 0)
            };
            grid.Children.Add(swatch);
            Grid.SetColumn(swatch, 0);

            var info = new StackPanel { Spacing = 2, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            info.Children.Add(new TextBlock
            {
                Text = t.Name, FontSize = 13, FontWeight = FontWeight.SemiBold,
                Foreground = Res("AppInkBrush", "#1d1d1f")
            });
            info.Children.Add(new TextBlock
            {
                Text = t.Desc, FontSize = 11,
                Foreground = Res("AppMutedBrush", "#8e8e93")
            });
            grid.Children.Add(info);
            Grid.SetColumn(info, 1);

            if (isActive)
            {
                grid.Children.Add(new TextBlock
                {
                    Text = "\u2713", FontSize = 16, Foreground = Brush.Parse("#0066cc"),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                });
                Grid.SetColumn(grid.Children[grid.Children.Count - 1], 2);
            }

            border.Child = grid;
            var capturedTheme = t;
            border.PointerPressed += (_, _) =>
            {
                App.SwitchTheme(capturedTheme.Key);
                BuildThemeList();
            };

            ThemePanel.Children.Add(border);
        }
    }
}

public class ThemeOption
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Desc { get; set; } = "";
    public string ColorHex { get; set; } = "#ccc";
    public bool Selected { get; set; }
}
