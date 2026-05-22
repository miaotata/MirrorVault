using Avalonia;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace BackupApp.UI;

class Program
{
    private const string AppMutexName = "MirrorVault_SingleInstance";
    private const int HWND_BROADCAST = 0xFFFF;
    private static readonly int WmMirrorVaultActivate;

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern IntPtr RegisterWindowMessage(string lpString);

    static Program()
    {
        if (OperatingSystem.IsWindows())
            WmMirrorVaultActivate = (int)RegisterWindowMessage("MirrorVault_Activate");
    }

    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(true, AppMutexName, out bool createdNew);
        if (!createdNew)
        {
            // Another instance is running — activate it and exit
            if (OperatingSystem.IsWindows())
                PostMessage((IntPtr)HWND_BROADCAST, (uint)WmMirrorVaultActivate, IntPtr.Zero, IntPtr.Zero);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static int WmActivate => WmMirrorVaultActivate;

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
