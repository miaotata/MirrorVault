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

    public async Task<SyncResult?> OnConflictRequired(BackupTask task, SyncResult syncResult)
    {
        if (_mainWindow == null) return null;

        var tcs = new TaskCompletionSource<SyncResult?>();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var view = new ConflictView(syncResult.Conflicts);
            _mainWindow.ShowOverlay(view);

            view.WaitForResultAsync().ContinueWith(t =>
            {
                if (t.Result)
                {
                    var resolutions = view.GetResolutions();
                    for (int i = 0; i < syncResult.Conflicts.Count && i < resolutions.Count; i++)
                        syncResult.Conflicts[i].Resolution = resolutions[i];
                    tcs.SetResult(syncResult);
                }
                else
                {
                    tcs.SetResult(null);
                }
                _mainWindow.HideOverlay();
            });
        });
        return await tcs.Task;
    }

    public async Task<bool> OnPreviewReady(BackupTask task, ScanResult scan)
    {
        if (_mainWindow == null) return false;

        var tcs = new TaskCompletionSource<bool>();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var view = new PreviewView(scan, task.Name);
            _mainWindow.ShowOverlay(view);

            view.WaitForResultAsync().ContinueWith(t =>
            {
                tcs.SetResult(t.Result);
                _mainWindow.HideOverlay();
            });
        });
        return await tcs.Task;
    }
}
