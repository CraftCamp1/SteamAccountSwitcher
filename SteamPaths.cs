using Microsoft.Win32;

namespace SteamAccountSwitcher;

public sealed record SteamPaths(string SteamExe, string SteamDirectory, string LoginUsersFile, string ConfigFile)
{
    public static SteamPaths Discover()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        var steamExe = NormalizePath(key?.GetValue("SteamExe") as string);
        var steamPath = NormalizePath(key?.GetValue("SteamPath") as string);

        if (string.IsNullOrWhiteSpace(steamExe))
        {
            steamExe = @"C:\Program Files (x86)\Steam\steam.exe";
        }

        if (string.IsNullOrWhiteSpace(steamPath))
        {
            steamPath = Path.GetDirectoryName(steamExe) ?? @"C:\Program Files (x86)\Steam";
        }

        return new SteamPaths(
            steamExe,
            steamPath,
            Path.Combine(steamPath, "config", "loginusers.vdf"),
            Path.Combine(steamPath, "config", "config.vdf"));
    }

    private static string NormalizePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('/', '\\');
    }
}
