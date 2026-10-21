# Hearthlens

Hearthstone Deck Tracker–style companion (WPF, `net10.0-windows`): in-game overlay, Battlegrounds hero/comp/shop advisor, match history, read-only memory reads (MMR).

## Layout
- `src/Hearthlens.Core` — log parsing (`Power/PowerParser`), tracking, `Battlegrounds/BattlegroundsAdvisor.cs`, `HistoryStore` (SQLite), settings, memory reader.
- `src/Hearthlens.App` — WPF UI. `Main/` dashboard window, `Overlay/` overlay window + `MovablePanel`, `Themes/Theme.xaml` (fantasy gold palette, brushes/styles), `Controls/` (`Ornament`, `RatingChart`), `DevRender.cs`.
- `tests/Hearthlens.Tests` — xUnit; advisor tests replay a real BG log.
- `tools/Hearthlens.Probe` — diagnostics CLI.

## Commands
- Build app: `dotnet build src/Hearthlens.App/Hearthlens.App.csproj -nologo -v q` (kill `Hearthlens.exe` first or the copy step fails).
- Tests: `dotnet test tests/Hearthlens.Tests/Hearthlens.Tests.csproj` (Core and tests target plain `net10.0`, so they run on Windows, macOS and Linux).
- Build the WPF app from Linux/macOS (compile check only): add `-p:EnableWindowsTargeting=true`.
- Offscreen UI render (verify visuals without touching the screen): `src\Hearthlens.App\bin\Debug\net10.0-windows\Hearthlens.exe --render-ui <folder>` — writes `main-*.png` and `overlay-pick/game/layout.png` from the real latest BG log + history.
- Memory probe: `dotnet run --project tools/Hearthlens.Probe -- mem`

## Data
- App data (`settings.json`, `history.db`, `hearthlens.log`, `cache/meta`, `cache/images`): `%APPDATA%\Hearthlens` on Windows, `~/Library/Application Support/Hearthlens` on macOS, `~/.local/share/Hearthlens` on Linux; `HEARTHLENS_DATA` overrides it.
- Hearthstone install/logs: `D:\Hearthstone` on the dev machine. `HearthstoneInstall` also finds `/Applications/Hearthstone` (macOS), Wine/Lutris/Bottles/Proton prefixes (Linux) and the Android client's files; `log.config` location is per platform (`LogConfigPathFor`).
- Memory reads (MMR, ranks, lobby tribes) are Windows-only (`HearthstoneMemory.IsSupported`); elsewhere those values are just absent.
- Meta stats from Firestone, card data from HearthstoneJSON — keep attribution in the UI.

## Rules
- No mock or fabricated data anywhere; the UI shows real data or an empty state.
- Fair play only: memory access is read-only; never show hidden information; never bypass HDT's paid tiers.
- Never screenshot the user's screen or call `SetForegroundWindow` (it stole focus from their game). Verify UI only via `--render-ui`.
- Never edit files with PowerShell `Get-Content`/`Set-Content` (it corrupts UTF-8 to mojibake). Use the Edit tool or Python.
- Overlay panel positions persist in `AppSettings.Panels` (id → X, Y, Scale); layout mode is Ctrl+Shift+L, hide overlay is Ctrl+Shift+H.
