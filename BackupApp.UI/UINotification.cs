using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using BackupApp.Core.Models;
using BackupApp.Core.Services;
using BackupApp.UI.Views;

namespace BackupApp.UI;

public class UINotification : IBackupNotification
{
    private MainWindow? _mainWindow;

    public void SetMainWindow(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public void Notify(string title, string message, bool isError)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var msgBox = new Window
            {
                Title = title,
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(15),
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                        new Button
                        {
                            Content = "确定",
                            Width = 60,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                        }
                    }
                }
            };
            ((Button)((StackPanel)msgBox.Content).Children[1]).Click += (_, _) => msgBox.Close();
            msgBox.Show();
        });
    }

    public Task<SyncResult?> OnConflictRequired(BackupTask task, SyncResult syncResult)
    {
        if (_mainWindow == null) return Task.FromResult<SyncResult?>(null);

        var tcs = new TaskCompletionSource<SyncResult?>();
        var view = new ConflictView(syncResult.Conflicts);
        _mainWindow.ShowOverlay(view);

        view.WaitForResultAsync().ContinueWith(t =>
        {
            var confirmed = t.Result;
            if (confirmed)
            {
                var resolutions = view.GetResolutions();
                for (int i = 0; i < syncResult.Conflicts.Count && i < resolutions.Count; i++)
                    syncResult.Conflicts[i].Resolution = resolutions[i];
            }
            Dispatcher.UIThread.Post(() =>
            {
                _mainWindow.HideOverlay();
                tcs.SetResult(confirmed ? syncResult : null);
            });
        });

        return tcs.Task;
    }

    public Task<bool> OnPreviewReady(BackupTask task, ScanResult scan)
    {
        if (_mainWindow == null) return Task.FromResult(false);

        var tcs = new TaskCompletionSource<bool>();
        var view = new PreviewView(scan, task.Name);
        _mainWindow.ShowOverlay(view);

        view.Confirmed += () =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                _mainWindow.HideOverlay();
                tcs.TrySetResult(true);
            });
        };
        view.Cancelled += () =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                _mainWindow.HideOverlay();
                tcs.TrySetResult(false);
            });
        };

        return tcs.Task;
    }
}
