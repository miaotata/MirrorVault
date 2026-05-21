using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using BackupApp.Core.Services;
using BackupApp.Core.Storage;

namespace BackupApp.UI;

public partial class App : Application
{
    public static ThemeManager ThemeManager { get; } = new();
    private static ResourceDictionary? _currentThemeDict;

    private ConfigStore _configStore = null!;
    private BackupEngine _engine = null!;
    private SchedulerService _scheduler = null!;
    private TrayIcon? _trayIcon;
    private MainWindow? _mainWindow;

    private const string IconResourceUri = "avares://MirrorVault/Assets/icon.png";

    private static WindowIcon LoadAppIcon()
    {
        try
        {
            var uri = new Uri(IconResourceUri);
            using var stream = AssetLoader.Open(uri);
            return new WindowIcon(stream);
        }
        catch
        {
            // Fallback: try file path for non-single-file scenarios
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.png");
            if (File.Exists(path))
                return new WindowIcon(path);
            throw;
        }
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _configStore = new ConfigStore();
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
            var notification = new UINotification();

            _engine = new BackupEngine(
                _configStore, layout, manifestStore, scanner, executor,
                syncEngine, restoreEngine, verification, encryption, compression,
                versionManager, notification);

            _scheduler = new SchedulerService(_configStore, _engine);
            _scheduler.Start();

            ThemeManager.LoadTheme(_configStore);
            LoadThemeResources(ThemeManager.CurrentTheme);

            ThemeManager.ThemeChanged += (theme) =>
            {
                LoadThemeResources(theme);
            };

            _mainWindow = new MainWindow(_configStore, _engine);

            // Set window icon
            try { _mainWindow.Icon = LoadAppIcon(); }
            catch (Exception ex) { Console.WriteLine($"Window icon failed: {ex.Message}"); }

            notification.SetMainWindow(_mainWindow);
            desktop.MainWindow = _mainWindow;

            // System tray icon
            SetupTrayIcon();

            desktop.Exit += (_, _) =>
            {
                _scheduler.Dispose();
                _configStore.Dispose();
                _trayIcon?.Dispose();
            };

            _mainWindow.RefreshTaskList();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon()
    {
        try
        {
            var showItem = new NativeMenuItem("显示主窗口");
            showItem.Click += (_, _) =>
            {
                _mainWindow?.Show();
                _mainWindow?.Activate();
            };

            var exitItem = new NativeMenuItem("退出");
            exitItem.Click += (_, _) =>
            {
                _trayIcon?.Dispose();
                _scheduler?.Dispose();
                _configStore?.Dispose();
                Environment.Exit(0);
            };

            _trayIcon = new TrayIcon
            {
                Icon = LoadAppIcon(),
                ToolTipText = "MirrorVault — 自动备份工具",
                IsVisible = true,
                Menu = new NativeMenu
                {
                    showItem,
                    new NativeMenuItemSeparator(),
                    exitItem
                }
            };
            _trayIcon.Clicked += (_, _) =>
            {
                _mainWindow?.Show();
                _mainWindow?.Activate();
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Tray icon failed: {ex.Message}");
        }
    }

    public static void SwitchTheme(string themeKey)
    {
        ThemeManager.SetTheme(themeKey);
    }

    private static void LoadThemeResources(string themeKey)
    {
        var resources = Current!.Resources;
        var uri = new Uri($"avares://MirrorVault/Themes/{Capitalize(themeKey)}Theme.axaml");
        var themeDict = (ResourceDictionary)AvaloniaXamlLoader.Load(uri!);

        if (_currentThemeDict != null)
            resources.MergedDictionaries.Remove(_currentThemeDict);

        resources.MergedDictionaries.Add(themeDict);
        _currentThemeDict = themeDict;
    }

    private static string Capitalize(string s)
        => s.Length > 0 ? char.ToUpper(s[0]) + s[1..] : s;
}
