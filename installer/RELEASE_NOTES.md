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

**macOS note:** Hearthlens is not notarized by Apple. If macOS says it cannot be opened, right-click it, choose **Open**, then **Open** again (only the first time).

**Phones and tablets:** there is no mobile version yet. iPhone and iPad do not allow apps to read Hearthstone's data or draw over it, so iOS is not possible. On Android 11 and later, reading Hearthstone's logs needs extra setup (Shizuku or root); an Android version is planned.

## What's in this version

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
