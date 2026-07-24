# Steam Account Switcher

Lightweight Windows desktop app for switching between Steam accounts saved on the current Windows profile.

## Features

- Reads switchable accounts that Steam has remembered from `config\loginusers.vdf`.
- Switches the selected account to `MostRecent` and `AllowAutoLogin`.
- Disables Steam's account picker flag when switching.
- Updates Steam registry autologin state.
- Fast-closes Steam, writes the local state, then restarts Steam.
- Optional credential login popup for adding an account.
- Does not save passwords in the app.
- Creates timestamped backups in `Steam\config\SteamAccountSwitcherBackups`.

## Limits

- Steam must be able to remember the account locally.
- Steam Guard, CAPTCHA, invalid passwords, or session invalidation still require normal Steam interaction.
- Credential login uses Steam's own `-login` launch flow, then waits for Steam to write `loginusers.vdf`.

## Build

Requires the .NET 8 SDK on Windows.

```powershell
dotnet publish -c Release
```

The single-file self-contained executable is published to:

```text
dist\SteamAccountSwitcher.exe
```
