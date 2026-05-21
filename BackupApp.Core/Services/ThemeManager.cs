using BackupApp.Core.Storage;

namespace BackupApp.Core.Services;

public class ThemeManager
{
    public const string DefaultTheme = "claude";
    public const string ThemeConfigKey = "app.theme";

    public static readonly Dictionary<string, string> Themes = new()
    {
        ["claude"] = "Claude 暖调人文",
        ["apple"] = "Apple 干净极简",
        ["vercel"] = "Vercel 几何开发者",
        ["stripe"] = "Stripe 金融商务",
        ["spotify"] = "Spotify 深色沉浸"
    };

    public string CurrentTheme { get; private set; } = DefaultTheme;

    public event Action<string>? ThemeChanged;

    public void LoadTheme(ConfigStore configStore)
    {
        // Theme preference stored as a simple key in the config DB
        // For now, use a file-based approach since ConfigStore is task-specific
        var settingsPath = Path.Combine(GetSettingsDir(), "theme.txt");
        if (File.Exists(settingsPath))
        {
            var theme = File.ReadAllText(settingsPath).Trim();
            if (Themes.ContainsKey(theme)) CurrentTheme = theme;
        }
    }

    public void SetTheme(string themeKey, ConfigStore? configStore = null)
    {
        if (!Themes.ContainsKey(themeKey)) return;
        CurrentTheme = themeKey;
        var settingsDir = GetSettingsDir();
        if (!Directory.Exists(settingsDir)) Directory.CreateDirectory(settingsDir);
        File.WriteAllText(Path.Combine(settingsDir, "theme.txt"), themeKey);
        ThemeChanged?.Invoke(themeKey);
    }

    private static string GetSettingsDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "BackupApp");
    }
}
