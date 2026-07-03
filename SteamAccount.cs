namespace SteamAccountSwitcher;

public sealed record SteamAccount(
    string SteamId,
    string AccountName,
    string PersonaName,
    bool RememberPassword,
    bool MostRecent,
    bool AllowAutoLogin,
    DateTimeOffset? LastUsed)
{
    public string DisplayName
    {
        get
        {
            var persona = string.IsNullOrWhiteSpace(PersonaName) ? "No persona" : PersonaName;
            return $"{AccountName}  ({persona})";
        }
    }
}
