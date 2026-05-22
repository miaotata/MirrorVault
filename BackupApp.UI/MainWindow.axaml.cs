using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using BackupApp.Core.Models;
using BackupApp.Core.Services;
using BackupApp.Core.Storage;
using BackupApp.UI.Views;

namespace BackupApp.UI;

public partial class MainWindow : Window
{
    private readonly ConfigStore _configStore;
    private readonly BackupEngine _backupEngine;
    private UserControl? _currentView;
    private bool _isExiting;
    private readonly Dictionary<Guid, BackupProgress> _runningProgress = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _runningCts = new();

    public BackupProgress? RunningTaskInfo => _runningProgress.Values.FirstOrDefault();

    // Windows API for taskbar appearance and system menu
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")]
    private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
    [DllImport("user32.dll")]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string? lpNewItem);
    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private WndProcDelegate? _wndProcHook;
    private IntPtr _originalWndProc;
    private bool _wndProcHooked;

    private const int GWL_EXSTYLE = -20;
    private const int GWLP_WNDPROC = -4;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const uint WM_SYSCOMMAND = 0x0112;
    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint IDM_SHOW = 1001;
    private const uint IDM_EXIT = 1002;

    public MainWindow(ConfigStore configStore, BackupEngine backupEngine)
    {
        _configStore = configStore;
        _backupEngine = backupEngine;
        InitializeComponent();

        // Taskbar right-click menu (Avalonia approach as baseline)
        var winMenu = new NativeMenu();
        var showItem = new NativeMenuItem("显示主窗口");
        showItem.Click += (_, _) => { Show(); Activate(); };
        var exitItem = new NativeMenuItem("退出");
        exitItem.Click += (_, _) => { _isExiting = true; Close(); };
        winMenu.Add(showItem);
        winMenu.Add(new NativeMenuItemSeparator());
        winMenu.Add(exitItem);
        NativeMenu.SetMenu(this, winMenu);

        // Force taskbar visibility + system menu on Windows
        Opened += (_, _) =>
        {
            if (OperatingSystem.IsWindows() && TryGetPlatformHandle() is { } handle)
            {
                var hwnd = handle.Handle;

                // Force WS_EX_APPWINDOW so borderless window appears in taskbar
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_APPWINDOW);

                // Add items to system menu (taskbar right-click menu)
                IntPtr sysMenu = GetSystemMenu(hwnd, false);
                AppendMenu(sysMenu, MF_SEPARATOR, 0, null);
                AppendMenu(sysMenu, MF_STRING, IDM_SHOW, "显示主窗口");
                AppendMenu(sysMenu, MF_STRING, IDM_EXIT, "退出");

                // Hook WndProc to handle the custom menu commands
                if (!_wndProcHooked)
                {
                    _wndProcHook = WndProcHook;
                    IntPtr hookPtr = Marshal.GetFunctionPointerForDelegate(_wndProcHook);
                    _originalWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, hookPtr);
                    _wndProcHooked = true;
                }
            }
        };

        // Window controls
        BtnSettings.Click += (_, _) => ShowSettings();
        BtnMinimize.Click += (_, _) => WindowState = WindowState.Minimized;
        BtnMaximize.Click += (_, _) => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal : WindowState.Maximized;
        BtnClose.Click += (_, _) => Hide();

        // Edge resize
        SetUpResizeEdges();

        // Back button
        BtnBack.Click += (_, _) => NavigateBack();

        // Task actions
        BtnNewTask.Click += (_, _) => ShowTaskEditor(null);
        BtnEditTask.Click += (_, _) => EditSelectedTask();
        BtnDeleteTask.Click += (_, _) => DeleteSelectedTask();
        BtnRunNow.Click += (_, _) => RunSelectedTask();
        BtnRestore.Click += (_, _) => OpenRestoreWizard();
        BtnRefresh.Click += (_, _) => RefreshTaskList();
        BtnHistory.Click += (_, _) => ShowHistory();

        Closing += (_, e) => { if (!_isExiting) { e.Cancel = true; Hide(); } };
    }

    private IntPtr WndProcHook(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_SYSCOMMAND)
        {
            uint cmd = (uint)(wParam.ToInt64() & 0xFFF0);
            if (cmd == IDM_SHOW)
            {
                Show();
                Activate();
                return IntPtr.Zero;
            }
            if (cmd == IDM_EXIT)
            {
                _isExiting = true;
                Close();
                return IntPtr.Zero;
            }
        }
        // Handle single-instance activation from another process
        if ((int)msg == Program.WmActivate && Program.WmActivate != 0)
        {
            Show();
            Activate();
            return IntPtr.Zero;
        }
        return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }

    // ===== Edge resize =====

    private void SetUpResizeEdges()
    {
        const int edgeSize = 6;
        // Top edge
        var topEdge = new Border { Height = edgeSize, Cursor = new Cursor(StandardCursorType.TopSide) };
        topEdge.PointerPressed += (_, e) => BeginResizeDrag(WindowEdge.North, e);
        // Bottom edge
        var bottomEdge = new Border { Height = edgeSize, Cursor = new Cursor(StandardCursorType.BottomSide) };
        bottomEdge.PointerPressed += (_, e) => BeginResizeDrag(WindowEdge.South, e);
        // Left edge
        var leftEdge = new Border { Width = edgeSize, Cursor = new Cursor(StandardCursorType.LeftSide) };
        leftEdge.PointerPressed += (_, e) => BeginResizeDrag(WindowEdge.West, e);
        // Right edge
        var rightEdge = new Border { Width = edgeSize, Cursor = new Cursor(StandardCursorType.RightSide) };
        rightEdge.PointerPressed += (_, e) => BeginResizeDrag(WindowEdge.East, e);

        // Attach edges to the outer Grid as adorner-like elements
    }

    // ===== Title bar drag =====

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void ResizeGrip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginResizeDrag(WindowEdge.SouthEast, e);
    }

    // ===== Navigation =====

    private void ShowView(UserControl view)
    {
        _currentView = view;
        ViewContainer.Content = view;
        HomePanel.IsVisible = false;
        ViewContainer.IsVisible = true;
        BtnBack.IsVisible = true;
    }

    private void NavigateBack()
    {
        _currentView = null;
        ViewContainer.Content = null;
        ViewContainer.IsVisible = false;
        HomePanel.IsVisible = true;
        BtnBack.IsVisible = false;
        RefreshTaskList();
    }

    private void ShowSettings()
    {
        ShowView(new SettingsView(_configStore));
    }

    private void ShowTaskEditor(BackupTask? task)
    {
        var editor = new TaskEditorView(_configStore, task);
        editor.Done += () => NavigateBack();
        ShowView(editor);
    }

    private void ShowRestoreWizard(BackupTask task)
    {
        var wizard = new RestoreWizardView(_backupEngine, task);
        wizard.Done += () => NavigateBack();
        ShowView(wizard);
    }

    // ===== Overlay =====

    public void ShowOverlay(UserControl content)
    {
        OverlayCard.Child = content;
        Overlay.IsVisible = true;
    }

    public void HideOverlay()
    {
        Overlay.IsVisible = false;
        OverlayCard.Child = null;
    }

    // ===== Task list =====

    public void RefreshTaskList()
    {
        var tasks = _configStore.ListTasks();
        var items = new List<TaskDisplayItem>();
        foreach (var t in tasks)
        {
            var isEnabled = t.Enabled;
            var isRunning = _runningProgress.TryGetValue(t.Id, out var prog);
            items.Add(new TaskDisplayItem
            {
                Id = t.Id,
                Name = t.Name,
                Icon = GetIcon(t.BackupMode),
                ModeText = GetModeText(t.BackupMode),
                DisplayLastRun = isRunning ? "备份中…" : $"上次: {t.UpdatedAt:yyyy-MM-dd HH:mm}",
                StatusText = isRunning ? "运行中" : (isEnabled ? "已启用" : "已禁用"),
                StatusBg = isRunning ? Brush.Parse("#e3f2fd") : (isEnabled ? Brush.Parse("#e3f7e7") : Brush.Parse("#f5f5f7")),
                StatusFg = isRunning ? Brush.Parse("#1565c0") : (isEnabled ? Brush.Parse("#1e7e34") : Brush.Parse("#8e8e93")),
                IsRunning = isRunning,
                ProgressPercent = prog?.Percent ?? 0,
                ProgressText = isRunning ? $"{prog!.Phase} {prog.ProcessedFiles}/{prog.TotalFiles}" : ""
            });
        }
        TaskListBox.ItemsSource = items;
        EmptyState.IsVisible = !items.Any();
        TxtStatus.Text = tasks.Any() ? "就绪" : "暂无任务";
        var now = DateTime.Now;
        var nextRun = tasks
            .Where(t => t.Schedule.Enabled)
            .Select(t => SchedulerService.GetNextRun(t.Schedule.CronExpression, null, now))
            .Where(dt => dt.HasValue)
            .Select(dt => dt!.Value)
            .DefaultIfEmpty()
            .Min();
        TxtNextRun.Text = nextRun != default
            ? $"下次调度: {nextRun:yyyy-MM-dd HH:mm:ss}"
            : "下次调度: 无";
    }

    private void ShowSelectHint()
    {
        TxtStatus.Text = "请先选择一个任务";
        StatusDot.Fill = Brush.Parse("#F57F17");
    }

    private void ClearStatusHint()
    {
        StatusDot.Fill = Brush.Parse("#2E7D32");
    }

    private void StatusBadge_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is TaskDisplayItem item)
        {
            var task = _configStore.GetTask(item.Id);
            if (task != null)
            {
                task.Enabled = !task.Enabled;
                _configStore.SaveTask(task);
                RefreshTaskList();
            }
        }
    }

    private static string GetIcon(BackupMode mode) => mode switch
    {
        BackupMode.Incremental => "📑",
        BackupMode.TwoWaySync => "🔄",
        BackupMode.OneWay => "📤",
        _ => "📁"
    };

    private static string GetModeText(BackupMode mode) => mode switch
    {
        BackupMode.Incremental => "增量备份",
        BackupMode.TwoWaySync => "双向同步",
        BackupMode.OneWay => "单向备份",
        _ => "未知"
    };

    private void EditSelectedTask()
    {
        if (TaskListBox.SelectedItem is TaskDisplayItem item)
        {
            var task = _configStore.GetTask(item.Id);
            if (task != null) ShowTaskEditor(task);
        }
        else ShowSelectHint();
    }

    private async void DeleteSelectedTask()
    {
        if (TaskListBox.SelectedItem is TaskDisplayItem item)
        {
            var confirm = new ConfirmView($"确定要删除任务「{item.Name}」吗？\n此操作不可撤销。");
            ShowOverlay(confirm);
            var result = await confirm.WaitForResultAsync();
            HideOverlay();
            if (result)
            {
                _configStore.DeleteTask(item.Id);
                RefreshTaskList();
            }
        }
        else ShowSelectHint();
    }

    private void RunSelectedTask()
    {
        if (TaskListBox.SelectedItem is TaskDisplayItem item)
        {
            ClearStatusHint();
            TxtStatus.Text = $"执行中: {item.Name}...";

            var cts = new CancellationTokenSource();
            _runningCts[item.Id] = cts;

            var progress = new Progress<BackupProgress>(p =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _runningProgress[p.TaskId] = p;
                    TxtStatus.Text = $"执行中: {p.TaskName} — {p.Percent}%";
                    RefreshTaskList();
                });
            });

            _ = _backupEngine.RunBackupAsync(item.Id, progress, cts.Token).ContinueWith(t =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _runningProgress.Remove(item.Id);
                    _runningCts.Remove(item.Id);

                    if (t.IsCanceled)
                        TxtStatus.Text = $"已取消: {item.Name}";
                    else
                    {
                        var manifest = t.Result;
                        TxtStatus.Text = manifest?.Status == BackupStatus.Success
                            ? $"完成: {item.Name}"
                            : $"失败: {item.Name}";
                    }
                    RefreshTaskList();
                });
            });
        }
        else ShowSelectHint();
    }

    private void StopCurrentTask()
    {
        var runningId = _runningCts.Keys.FirstOrDefault();
        if (runningId != Guid.Empty && _runningCts.TryGetValue(runningId, out var cts))
            cts.Cancel();
    }

    private void StopTask_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is TaskDisplayItem item)
        {
            if (_runningCts.TryGetValue(item.Id, out var cts))
                cts.Cancel();
        }
    }

    private void OpenRestoreWizard()
    {
        if (TaskListBox.SelectedItem is TaskDisplayItem item)
        {
            var task = _configStore.GetTask(item.Id);
            if (task != null) ShowRestoreWizard(task);
        }
        else ShowSelectHint();
    }

    private void ShowHistory()
    {
        if (TaskListBox.SelectedItem is TaskDisplayItem item)
        {
            var task = _configStore.GetTask(item.Id);
            if (task != null)
            {
                var history = new HistoryView(_backupEngine, task);
                ShowView(history);
            }
        }
        else ShowSelectHint();
    }
}

public class TaskDisplayItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "📁";
    public string ModeText { get; set; } = "";
    public string DisplayLastRun { get; set; } = "";
    public string StatusText { get; set; } = "";
    public IBrush? StatusBg { get; set; }
    public IBrush? StatusFg { get; set; }
    public bool IsRunning { get; set; }
    public int ProgressPercent { get; set; }
    public string ProgressText { get; set; } = "";
}
