using System.Globalization;
using System.Text.RegularExpressions;

namespace SteamAccountSwitcher;

public static partial class LoginUsersVdf
{
    public static IReadOnlyList<SteamAccount> Parse(string content)
    {
        var accounts = new List<SteamAccount>();

        foreach (Match blockMatch in AccountBlockRegex().Matches(content))
        {
            var steamId = blockMatch.Groups["id"].Value;
            var body = blockMatch.Groups["body"].Value;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match valueMatch in KeyValueRegex().Matches(body))
            {
                values[valueMatch.Groups["key"].Value] = valueMatch.Groups["value"].Value;
            }

            if (!values.TryGetValue("AccountName", out var accountName) ||
                string.IsNullOrWhiteSpace(accountName))
            {
                continue;
            }

            accounts.Add(new SteamAccount(
                steamId,
                accountName,
                values.GetValueOrDefault("PersonaName", string.Empty),
                IsEnabled(values.GetValueOrDefault("RememberPassword")),
                IsEnabled(values.GetValueOrDefault("MostRecent")),
                IsEnabled(values.GetValueOrDefault("AllowAutoLogin")),
                ParseTimestamp(values.GetValueOrDefault("Timestamp"))));
        }

        return accounts
            .OrderByDescending(account => account.MostRecent)
            .ThenByDescending(account => account.LastUsed)
            .ThenBy(account => account.AccountName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string SelectAccount(string content, string targetSteamId)
    {
        var accounts = Parse(content);
        if (accounts.All(account => account.SteamId != targetSteamId))
        {
            throw new InvalidOperationException("The selected account was not found in loginusers.vdf.");
        }

        return SerializeSelectedAccounts(accounts, targetSteamId);
    }

    public static string SelectAccountByName(string content, string targetAccountName, out SteamAccount selectedAccount)
    {
        var accounts = Parse(content);
        selectedAccount = accounts.FirstOrDefault(account =>
            string.Equals(account.AccountName, targetAccountName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The logged-in account was not found in loginusers.vdf yet.");

        return SerializeSelectedAccounts(accounts, selectedAccount.SteamId);
    }

    private static string SerializeSelectedAccounts(IReadOnlyList<SteamAccount> accounts, string targetSteamId)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        writer.WriteLine("\"users\"");
        writer.WriteLine("{");

        foreach (var account in accounts.OrderByDescending(account => account.SteamId == targetSteamId).ThenBy(account => account.AccountName, StringComparer.OrdinalIgnoreCase))
        {
            var isTarget = account.SteamId == targetSteamId;
            writer.WriteLine($"\t\"{Escape(account.SteamId)}\"");
            writer.WriteLine("\t{");
            writer.WriteLine($"\t\t\"AccountName\"\t\t\"{Escape(account.AccountName)}\"");
            writer.WriteLine($"\t\t\"PersonaName\"\t\t\"{Escape(account.PersonaName)}\"");
            writer.WriteLine("\t\t\"RememberPassword\"\t\t\"1\"");
            writer.WriteLine("\t\t\"WantsOfflineMode\"\t\t\"0\"");
            writer.WriteLine("\t\t\"SkipOfflineModeWarning\"\t\t\"0\"");
            writer.WriteLine($"\t\t\"AllowAutoLogin\"\t\t\"{(isTarget ? "1" : "0")}\"");
            writer.WriteLine($"\t\t\"MostRecent\"\t\t\"{(isTarget ? "1" : "0")}\"");
            writer.WriteLine($"\t\t\"Timestamp\"\t\t\"{(isTarget ? now : ToUnixTimestamp(account.LastUsed))}\"");
            writer.WriteLine("\t}");
        }

        writer.WriteLine("}");
        return writer.ToString();
    }

    private static string ToUnixTimestamp(DateTimeOffset? value)
    {
        return (value?.ToUniversalTime().ToUnixTimeSeconds() ?? 0).ToString(CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static bool IsEnabled(string? value)
    {
        return string.Equals(value, "1", StringComparison.Ordinal);
    }

    private static DateTimeOffset? ParseTimestamp(string? value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime()
            : null;
    }

    [GeneratedRegex("""(?ms)^\s*"(?<id>\d{17})"\s*\{\s*(?<body>.*?)^\s*\}""", RegexOptions.CultureInvariant)]
    private static partial Regex AccountBlockRegex();

    [GeneratedRegex("""(?m)^\s*"(?<key>[^"]+)"\s*"(?<value>[^"]*)"\s*$""", RegexOptions.CultureInvariant)]
    private static partial Regex KeyValueRegex();
}

file static class StringExtensions
{
    public static string Also(this string value, Action<string> action)
    {
        action(value);
        return value;
    }
}
