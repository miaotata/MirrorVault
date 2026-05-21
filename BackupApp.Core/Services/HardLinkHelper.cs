using System.Runtime.InteropServices;

namespace BackupApp.Core.Services;

public static class HardLinkHelper
{
    public static void CreateHardLink(string targetPath, string linkPath)
    {
        if (File.Exists(linkPath))
            File.Delete(linkPath);

        var dir = Path.GetDirectoryName(linkPath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!CreateHardLinkWindows(linkPath, targetPath, IntPtr.Zero))
                throw new IOException($"Failed to create hard link: {linkPath} -> {targetPath}");
        }
        else
        {
            if (link(targetPath, linkPath) != 0)
                throw new IOException($"Failed to create hard link: {linkPath} -> {targetPath}");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateHardLinkWindows(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    [DllImport("libc", SetLastError = true)]
    private static extern int link(string oldpath, string newpath);
}
