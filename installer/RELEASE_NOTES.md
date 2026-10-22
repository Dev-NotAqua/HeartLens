## Download

| System | File | How to install |
|---|---|---|
| **Windows 10/11** | `Hearthlens-Setup-<version>-win-x64.exe` | Run the setup wizard. It adds a desktop and Start menu shortcut, installs without administrator rights, and can be removed from **Settings → Apps**. |
| Windows (try the new cross-platform app) | `Hearthlens-Preview-<version>-win-x64.zip` | Unzip anywhere and run `Hearthlens.exe`. |
| **macOS** (Apple silicon: M1/M2/M3/M4) | `Hearthlens-<version>-macOS-arm64.pkg` | Open the package and follow the wizard. Adds Hearthlens to Applications and a Desktop shortcut. |
| **macOS** (Intel) | `Hearthlens-<version>-macOS-x64.pkg` | Same as above. |
| macOS, other formats | `.dmg` (drag to Applications) or `.zip` (double-click **Install Hearthlens**) | |
| **Ubuntu, Debian, Mint, Pop!_OS** | `hearthlens_<version>_amd64.deb` (`arm64` for ARM) | Double-click, or `sudo apt install ./hearthlens_<version>_amd64.deb` |
| **Fedora, openSUSE, RHEL** | `hearthlens-<version>-1.x86_64.rpm` (`aarch64` for ARM) | `sudo dnf install ./hearthlens-<version>-1.x86_64.rpm` |
| **Any Linux** | `Hearthlens-<version>-linux-x64.AppImage` | Make it executable and run it. |
| Any Linux (installer script) | `Hearthlens-<version>-linux-x64.tar.gz` | Unpack, run `./install.sh`; remove with `hearthlens-uninstall`. |

Every installer adds a desktop shortcut and can be uninstalled cleanly; your match history is kept unless you choose to delete it. (The AppImage and the Windows preview zip are portable: nothing to install or uninstall.)
`SHA256SUMS.txt` lists the checksums of all files.

**You only have to do this once.** Hearthlens 0.5.0 and newer keep themselves up to date: they check GitHub in the background and install the new version when you close the app. *Before that first update, install 0.5.0 by hand — 0.4.0 and older do not know how to update themselves.*

**macOS note:** Hearthlens is not notarized by Apple. If macOS says it cannot be opened, right-click it, choose **Open**, then **Open** again (only the first time).

**Phones and tablets:** there is no mobile version yet. iPhone and iPad do not allow apps to read Hearthstone's data or draw over it, so iOS is not possible. On Android 11 and later, reading Hearthstone's logs needs extra setup (Shizuku or root); an Android version is planned.

## What's in this version

**Nothing can leave the overlay frozen on an old turn any more.** If tracking stops following the game — the overlay keeps showing the board from an earlier round — press **Ctrl+Shift+R**, or choose **Hard refresh (re-read the current game)** from the tray menu. Hearthlens replays the current game from Hearthstone's log and rebuilds the board, turn, tavern tier, odds and game plan at the round you are actually on. It also re-shows the overlay if it had been hidden and puts it back on top of the game.

**Hearthlens now notices a stall by itself.** A watchdog checks the log pipeline every two seconds: if the reader thread ever dies, it is restarted; if the game's log keeps growing while nothing is being read, the current game is replayed automatically. The freeze you had to fix by hand repairs itself.

**Windows 10 overlay hardening**
- The overlay re-asserts itself as the topmost window every few seconds, so another topmost window (a media overlay, a capture tool, the game itself) can no longer silently push it behind Hearthstone.
- New tray option **Software overlay rendering**: some Windows 10 GPUs and drivers never draw the transparent overlay in hardware, so it is invisible while the app works. Turn this on and restart Hearthlens.
- Pressing **Ctrl+Shift+H** (hide overlay) now shows a tray balloon telling you how to bring it back, so a hidden overlay is never a mystery.

**Smaller and lighter:** attaching to a session that has been running for hours no longer loads the whole Power.log into memory. The last game is found by scanning backwards in blocks, which matters on a PC with little RAM.

**Fixes**
- Combat result: damage is no longer erased by a later heal or armor gain, so a fight you lost cannot become a tie in the history.
- A fight whose boards could not be captured no longer inherits the previous fight's survivor counts and shows the wrong win/loss.
- The dashboard's **ALL TIME** tile now counts every game (it stopped at 400 before).

**If the overlay never appears on a Windows 10 PC:** in Hearthstone set *Options → Graphics → Display Mode* to **Windowed (Fullscreen)**, then turn on **Software overlay rendering** in the Hearthlens tray menu and restart the app. True fullscreen hands the screen to the game exclusively, which no overlay can draw over.

## What was new in 0.5.0

**Hearthlens updates itself now.** Downloading a new setup by hand is over: shortly after it starts, and every six hours after that, Hearthlens looks at GitHub for a newer release, fetches the file your system needs, and installs it **the next time you close Hearthlens**. Nothing pops up mid-game — the update waits until the app is closed, so a running overlay is never interrupted, and your match history, settings and overlay layout stay exactly as they are.

- Windows: the new setup runs silently while Hearthlens is closed. A gold **Update ready — restart** chip appears in the top bar of the dashboard when you would rather install right away: one click closes the app, installs and comes back in the new version.
- macOS and Linux: the matching package is downloaded for you and one click hands it to your system's installer (no hunting through the releases page).
- Updates can be turned off: *Settings → **Install updates automatically***.
- One-time step: if you are on 0.4.0 or older, install 0.5.0 by hand once — older versions do not know about updates yet. From here on, every release arrives on its own.

## What was new in 0.4.0

**Tavern spells.** Spells are now part of the advice: the game plan prices them at their real cost, the shop panel marks the best spell to buy (spells need no board space), and the best pick is marked for spells too.

**Read any card you hover.** Hover a card in the overlay — a card in the tier browser, a minion on a board, or a comp card row — and it grows, with a full-size card tooltip, so a tier 6 effect is finally readable.

**Fixes**
- Fixed the overlay sometimes staying stuck "in combat" while you were back in the tavern. A stale combat state now clears itself after two minutes, and shop cards end combat reliably.
- Community stats could load completely empty: the Firestone feed carries null fields (a stat row with no turn, for example) that the reader treated as an error and dropped the whole card list. Those rows are skipped now, so minion, hero and comp statistics load again.
- Card art now falls back to a second card database (hsbg.cards) when the first source cannot be reached, instead of showing an empty tile.

## What was new in 0.3.0

**New look.** Hearthlens now wears Hearthstone's colours: dark wood browns, parchment text and gold. The selected page is a framed tile with a gold bar instead of a stretched oval, and buttons, chips and panels have a cleaner, framed shape.

**You choose your picks.** Two new settings under *Settings → In-game overlay*:
- *Pick suggestions*: **Mark the best** / **Stats only** / **Off**. *Stats only* (the default) shows the same numbers on every hero, trinket, tavern minion and Discover option and marks nothing as the best, so the choice is yours.
- *Game plan comp*: **I choose** / **Automatic**. In *I choose* (the default) Hearthlens never picks a comp for you: click one in the game plan panel, and it is kept for your next games.

**Much better combat odds.** The simulator now plays out minion effects: deathrattles (summons, buffs, damage, and effects that trigger them twice), Rally and other attack triggers, cleave and splash damage, avenge, "after a friendly minion dies", on-summon and damage triggers, and auras — 61 of the 65 Battlegrounds minions whose effects matter in combat. The "effects not simulated" note is now accurate and only counts cards whose effect is really missing. Reborn minions come back with the right stats and keywords.

**Fixes**
- Windows setup: on a PC that can't run Hearthlens (32-bit Windows, Windows older than 10, or an ARM PC without Windows 11) the setup now says so instead of installing an app that won't start.
- Windows: long pages (Settings, Battlegrounds) no longer cut off their content above the bottom bar.
- Linux: the ARM (aarch64) .rpm builds again, and .rpm packages no longer clash with other apps that bundle the same libraries.

Community stats: Firestone (zerotoheroes.com). Card data and art: HearthstoneJSON and hsbg.cards. Not affiliated with Blizzard Entertainment.
