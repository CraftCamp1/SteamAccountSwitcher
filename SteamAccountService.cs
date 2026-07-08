using System.Diagnostics;
using Microsoft.Win32;

namespace SteamAccountSwitcher;

public sealed class SteamAccountService
{
    private const string SteamRegistryPath = @"Software\Valve\Steam";

    public SteamPaths Paths { get; } = SteamPaths.Discover();

    public IReadOnlyList<SteamAccount> LoadAccounts()
    {
        if (!File.Exists(Paths.LoginUsersFile))
        {
            throw new FileNotFoundException("Steam loginusers.vdf was not found.", Paths.LoginUsersFile);
        }

        return LoginUsersVdf.Parse(File.ReadAllText(Paths.LoginUsersFile));
    }

    public async Task SwitchToAsync(SteamAccount account, SwitchOptions options, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(options.FastLaunch ? "Fast-closing Steam..." : "Closing Steam...");
        await StopSteamAsync(options.FastLaunch, cancellationToken);

        progress?.Report("Backing up loginusers.vdf...");
        var originalContent = await File.ReadAllTextAsync(Paths.LoginUsersFile, cancellationToken);
        BackupFile(Paths.LoginUsersFile);

        progress?.Report("Updating selected account...");
        var updatedContent = LoginUsersVdf.SelectAccount(originalContent, account.SteamId);
        await File.WriteAllTextAsync(Paths.LoginUsersFile, updatedContent, cancellationToken);

        progress?.Report("Disabling Steam account picker...");
        await DisableSteamAccountPickerAsync(cancellationToken);

        progress?.Report("Updating Steam registry state...");
        SetRegistryAutologin(account);

        if (!options.StartSteam)
        {
            progress?.Report($"Prepared {account.AccountName}. Steam was left closed.");
            return;
        }

        progress?.Report("Starting Steam...");
        StartSteam(account, options);
        progress?.Report($"Started Steam as {account.AccountName}.");
    }

    public async Task LoginWithCredentialsAsync(CredentialLoginRequest request, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ArgumentException("Username is required.", nameof(request));
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            throw new ArgumentException("Password is required.", nameof(request));
        }

        progress?.Report(request.FastLaunch ? "Fast-closing Steam..." : "Closing Steam...");
        await StopSteamAsync(request.FastLaunch, cancellationToken);

        progress?.Report("Preparing Steam login state...");
        PrepareCredentialLoginRegistry();

        progress?.Report("Disabling Steam account picker...");
        await DisableSteamAccountPickerAsync(cancellationToken);

        progress?.Report("Starting Steam with credentials...");
        StartSteamWithCredentials(request);
        progress?.Report($"Started Steam login for {request.Username}.");
    }

    private async Task StopSteamAsync(bool fastMode, CancellationToken cancellationToken)
    {
        if (!IsSteamRunning())
        {
            return;
        }

        TrySteamShutdownCommand();
        await WaitForSteamExitAsync(fastMode ? TimeSpan.FromMilliseconds(1200) : TimeSpan.FromSeconds(4), cancellationToken);

        if (!IsSteamRunning())
        {
            return;
        }

        if (fastMode)
        {
            await KillSteamProcessesAsync(cancellationToken);
            return;
        }

        foreach (var process in Process.GetProcessesByName("steam"))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                }
            }
            catch
            {
                // Process may exit while being inspected.
            }
        }

        await WaitForSteamExitAsync(TimeSpan.FromSeconds(3), cancellationToken);

        await KillSteamProcessesAsync(cancellationToken);
    }

    private static bool IsSteamRunning()
    {
        return Process.GetProcessesByName("steam").Length > 0
               || Process.GetProcessesByName("steamwebhelper").Length > 0;
    }

    private static async Task KillSteamProcessesAsync(CancellationToken cancellationToken)
    {
        foreach (var process in Process.GetProcessesByName("steam")
                     .Concat(Process.GetProcessesByName("steamwebhelper")))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(cancellationToken);
                }
            }
            catch
            {
                // Best-effort cleanup; file write will fail if Steam still owns the file.
            }
        }

        await WaitForSteamExitAsync(TimeSpan.FromSeconds(2), cancellationToken);
    }

    private void TrySteamShutdownCommand()
    {
        if (!File.Exists(Paths.SteamExe))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Paths.SteamExe,
                Arguments = "-shutdown",
                WorkingDirectory = Paths.SteamDirectory,
                UseShellExecute = true
            });
        }
        catch
        {
            // Fall back to window close / force kill below.
        }
    }

    private static async Task WaitForSteamExitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsSteamRunning())
            {
                return;
            }

            await Task.Delay(100, cancellationToken);
        }
    }

    private async Task DisableSteamAccountPickerAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(Paths.ConfigFile))
        {
            return;
        }

        var originalContent = await File.ReadAllTextAsync(Paths.ConfigFile, cancellationToken);
        var updatedContent = SteamConfigVdf.DisableAccountChooser(originalContent, out var changed);
        if (!changed)
        {
            return;
        }

        BackupFile(Paths.ConfigFile);
        await File.WriteAllTextAsync(Paths.ConfigFile, updatedContent, cancellationToken);
    }

    public async Task<SteamAccount?> WaitForCredentialLoginToPersistAsync(string accountName, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
        var backedUp = false;
        var lastStatus = DateTimeOffset.MinValue;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryMarkLoggedInAccountRemembered(accountName, ref backedUp, out var account))
            {
                SetRegistryAutologin(account);
                return account;
            }

            if (DateTimeOffset.UtcNow - lastStatus > TimeSpan.FromSeconds(5))
            {
                progress?.Report($"Waiting for Steam Guard / Steam to finish {accountName}...");
                lastStatus = DateTimeOffset.UtcNow;
            }

            await Task.Delay(1000, cancellationToken);
        }

        return null;
    }

    private bool TryMarkLoggedInAccountRemembered(string accountName, ref bool backedUp, out SteamAccount account)
    {
        account = default!;

        if (!File.Exists(Paths.LoginUsersFile))
        {
            return false;
        }

        try
        {
            var originalContent = File.ReadAllText(Paths.LoginUsersFile);
            var updatedContent = LoginUsersVdf.SelectAccountByName(originalContent, accountName, out account);
            if (!IsAuthorizedActiveAccount(account))
            {
                return false;
            }

            if (!string.Equals(originalContent, updatedContent, StringComparison.Ordinal))
            {
                if (!backedUp)
                {
                    BackupFile(Paths.LoginUsersFile);
                    backedUp = true;
                }

                File.WriteAllText(Paths.LoginUsersFile, updatedContent);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void BackupFile(string path)
    {
        var backupDirectory = Path.Combine(Path.GetDirectoryName(path)!, "SteamAccountSwitcherBackups");
        Directory.CreateDirectory(backupDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var backupPath = Path.Combine(backupDirectory, $"{fileName}.{timestamp}{extension}");
        File.Copy(path, backupPath, overwrite: false);

        foreach (var oldBackup in Directory
                     .EnumerateFiles(backupDirectory, $"{fileName}.*{extension}")
                     .OrderByDescending(File.GetCreationTimeUtc)
                     .Skip(25))
        {
            File.Delete(oldBackup);
        }
    }

    private static void SetRegistryAutologin(SteamAccount account)
    {
        SetRegistryAutologin(account.AccountName);

        if (!TryGetAccountId(account.SteamId, out var accountId))
        {
            return;
        }

        using var activeProcess = Registry.CurrentUser.CreateSubKey($@"{SteamRegistryPath}\ActiveProcess");
        activeProcess.SetValue("ActiveUser", unchecked((int)accountId), RegistryValueKind.DWord);

        using var key = Registry.CurrentUser.CreateSubKey(SteamRegistryPath);
        key.SetValue("ActiveUser", unchecked((int)accountId), RegistryValueKind.DWord);
    }

    private static void SetRegistryAutologin(string accountName)
    {
        using var key = Registry.CurrentUser.CreateSubKey(SteamRegistryPath);
        key.SetValue("AutoLoginUser", accountName, RegistryValueKind.String);
        key.SetValue("RememberPassword", 1, RegistryValueKind.DWord);

        using var activeProcess = Registry.CurrentUser.CreateSubKey($@"{SteamRegistryPath}\ActiveProcess");
        activeProcess.SetValue("ActiveUser", 0, RegistryValueKind.DWord);

        key.SetValue("ActiveUser", 0, RegistryValueKind.DWord);
    }

    private static void PrepareCredentialLoginRegistry()
    {
        using var key = Registry.CurrentUser.CreateSubKey(SteamRegistryPath);
        key.DeleteValue("AutoLoginUser", throwOnMissingValue: false);
        key.SetValue("RememberPassword", 1, RegistryValueKind.DWord);
        key.SetValue("ActiveUser", 0, RegistryValueKind.DWord);

        using var activeProcess = Registry.CurrentUser.CreateSubKey($@"{SteamRegistryPath}\ActiveProcess");
        activeProcess.SetValue("ActiveUser", 0, RegistryValueKind.DWord);
    }

    private static bool IsAuthorizedActiveAccount(SteamAccount account)
    {
        if (!TryGetAccountId(account.SteamId, out var expectedAccountId))
        {
            return false;
        }

        return RegistryDWordEquals($@"{SteamRegistryPath}\ActiveProcess", "ActiveUser", expectedAccountId)
               || RegistryDWordEquals(SteamRegistryPath, "ActiveUser", expectedAccountId);
    }

    private static bool RegistryDWordEquals(string path, string name, uint expectedValue)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        var value = key?.GetValue(name);
        return value is int intValue && unchecked((uint)intValue) == expectedValue;
    }

    private static bool TryGetAccountId(string steamId, out uint accountId)
    {
        accountId = 0;
        const ulong steamId64Base = 76561197960265728UL;
        if (!ulong.TryParse(steamId, out var steamId64) || steamId64 < steamId64Base)
        {
            return false;
        }

        var value = steamId64 - steamId64Base;
        if (value > uint.MaxValue)
        {
            return false;
        }

        accountId = (uint)value;
        return true;
    }

    private void StartSteam(SteamAccount account, SwitchOptions options)
    {
        if (!File.Exists(Paths.SteamExe))
        {
            throw new FileNotFoundException("Steam executable was not found.", Paths.SteamExe);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = Paths.SteamExe,
            Arguments = string.Empty,
            WorkingDirectory = Paths.SteamDirectory,
            UseShellExecute = true
        });
    }

    private void StartSteamWithCredentials(CredentialLoginRequest request)
    {
        if (!File.Exists(Paths.SteamExe))
        {
            throw new FileNotFoundException("Steam executable was not found.", Paths.SteamExe);
        }

        var arguments = $"-login {QuoteArgument(request.Username)} {QuoteArgument(request.Password)} -remember_password";

        Process.Start(new ProcessStartInfo
        {
            FileName = Paths.SteamExe,
            Arguments = arguments,
            WorkingDirectory = Paths.SteamDirectory,
            UseShellExecute = true
        });
    }

    private static string QuoteArgument(string value)
    {
        return value.Contains(' ') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
    }
}
