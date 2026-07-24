namespace SteamAccountSwitcher;

public enum CredentialLoginStatus
{
    SignedIn,
    InvalidCredentials,
    SteamGuardRequired,
    Pending
}

public sealed record CredentialLoginResult(CredentialLoginStatus Status, SteamAccount? Account = null);

public sealed record CredentialLoginSession(string AccountName, long LoginLogOffset, long ConnectionLogOffset);
