namespace SteamAccountSwitcher;

public static class SteamConfigVdf
{
    private const string AccountChooserKey = "\"AlwaysShowUserChooser\"";

    public static string DisableAccountChooser(string content, out bool changed)
    {
        changed = false;
        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains(AccountChooserKey, StringComparison.Ordinal))
            {
                continue;
            }

            var currentValue = ExtractLastQuotedValue(lines[i]);
            if (currentValue == "0")
            {
                return content;
            }

            var indent = lines[i][..(lines[i].Length - lines[i].TrimStart().Length)];
            lines[i] = $"{indent}{AccountChooserKey}\t\t\"0\"";
            changed = true;
            return string.Join(Environment.NewLine, lines);
        }

        return content;
    }

    private static string? ExtractLastQuotedValue(string line)
    {
        var lastQuote = line.LastIndexOf('"');
        if (lastQuote <= 0)
        {
            return null;
        }

        var previousQuote = line.LastIndexOf('"', lastQuote - 1);
        return previousQuote < 0 ? null : line[(previousQuote + 1)..lastQuote];
    }
}
