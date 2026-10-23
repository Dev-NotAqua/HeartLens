# Hearthlens

<img src="installer/assets/hearthlens.png" width="96" align="right" alt="Hearthlens logo">

A Hearthstone companion in the spirit of Hearthstone Deck Tracker: an in-game overlay, a deck tracker, a Battlegrounds
advisor (heroes, comps, shop, trinkets, Discover picks, combat odds) and your match history.

It runs on **Windows, macOS and Linux**. Hearthlens only reads the log files Hearthstone writes (plus, on Windows, a few
read-only values from game memory such as your rating). It never changes the game and never shows hidden information.

## Install

Download the installer for your system from the [latest release](../../releases/latest):

- **Windows:** `Hearthlens-Setup-…-win-x64.exe`, a setup wizard with desktop and Start menu shortcuts and an uninstaller.
- **macOS:** `…-macOS-arm64.pkg` (Apple silicon) or `…-macOS-x64.pkg` (Intel); `.dmg` and `.zip` are also available.
- **Linux:** `.deb` (Ubuntu/Debian), `.rpm` (Fedora/openSUSE), `.AppImage`, or the `.tar.gz` with `install.sh`.

Restart Hearthstone once after the first start: Hearthlens turns on the game's logging the first time it runs.

Phones and tablets: there is no mobile version yet. iOS does not let other apps read Hearthstone or draw over it; on
Android 11 and later, reading Hearthstone's logs needs extra setup (Shizuku or root).

## Project layout

| Folder | What it is |
|---|---|
| `src/Hearthlens.Core` | Log parsing, game tracking, Battlegrounds advisor and combat simulator, history (SQLite), settings, install discovery for every OS, Windows memory reader. Plain `net10.0`. |
| `src/Hearthlens.UI` | Cross-platform UI (Avalonia): dashboard, overlay surface, theme, view models, `AppHost` startup logic. |
| `src/Hearthlens.Desktop` | Desktop app for Windows, macOS and Linux: windows, tray, overlay window and the per-OS overlay layer (Win32, macOS CoreGraphics/AppKit, X11). |
| `src/Hearthlens.App` | The original Windows app (WPF), shipped by the Windows setup wizard. |
| `tests/Hearthlens.Tests` | xUnit tests (run on any OS). |
| `tools/Hearthlens.Probe` | Diagnostics CLI. |
| `installer/` | Installer scripts and assets for every platform. |

## Build

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
dotnet test tests/Hearthlens.Tests/Hearthlens.Tests.csproj     # tests
dotnet run --project src/Hearthlens.Desktop                     # cross-platform app
dotnet run --project src/Hearthlens.Desktop -- --render-ui out  # render the UI to PNGs, nothing shown on screen
dotnet build src/Hearthlens.App/Hearthlens.App.csproj           # Windows app (add -p:EnableWindowsTargeting=true elsewhere)
```

### Installers

```sh
installer/windows/build.sh 0.3.0            # needs NSIS (makensis): setup wizard + portable preview
installer/linux/build.sh 0.3.0 x64          # .deb, .rpm, .tar.gz (and .AppImage with appimagetool)
installer/macos/build.sh 0.3.0 arm64 x64    # .zip anywhere; .dmg and .pkg on a Mac
```

Output goes to `dist/`. Pushing a tag such as `v0.3.0` runs `.github/workflows/release.yml`, which builds every
installer (macOS ones on a Mac runner) and publishes them as a GitHub Release.

## Data and credits

- Your data stays on your computer: `%APPDATA%\Hearthlens` (Windows), `~/Library/Application Support/Hearthlens` (macOS),
  `~/.local/share/Hearthlens` (Linux).
- Community statistics: Firestone (zerotoheroes.com). Card data and art: HearthstoneJSON.
- Fonts: Fraunces and Manrope (SIL Open Font License 1.1), Material Design Icons (Pictogrammers, Apache 2.0).

Hearthlens is not affiliated with or endorsed by Blizzard Entertainment.
