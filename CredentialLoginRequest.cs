namespace SteamAccountSwitcher;

public sealed record CredentialLoginRequest(string Username, string Password, bool FastLaunch);
