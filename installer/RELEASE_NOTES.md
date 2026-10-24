## Download

| System | File | How to install |
|---|---|---|
| **Windows 10/11** | `Hearthlens-Setup-<version>-win-x64.exe` | Run the setup wizard. It adds a desktop and Start menu shortcut, installs without administrator rights, and can be removed from **Settings → Apps**. |
| Windows, portable | `Hearthlens-Portable-<version>-win-x64.zip` | Unzip anywhere and run `Hearthlens.exe`. Settings and history stay in the `data` folder next to it. It tells you about new versions but does not update itself. |
| Windows (try the new cross-platform app) | `Hearthlens-Preview-<version>-win-x64.zip` | Unzip anywhere and run `Hearthlens.exe`. |
| **macOS** (Apple silicon: M1/M2/M3/M4) | `Hearthlens-<version>-macOS-arm64.pkg` | Open the package and follow the wizard. Adds Hearthlens to Applications and a Desktop shortcut. |
| **macOS** (Intel) | `Hearthlens-<version>-macOS-x64.pkg` | Same as above. |
| macOS, portable | `Hearthlens-Portable-<version>-macOS-arm64.zip` (`x64` for Intel) | Unzip anywhere and open `Hearthlens.app`. Settings and history stay in the `Hearthlens Data` folder next to it. It tells you about new versions but does not update itself. |
| macOS, other formats | `.dmg` (drag to Applications) or `.zip` (double-click **Install Hearthlens**) | |
| **Ubuntu, Debian, Mint, Pop!_OS** | `hearthlens_<version>_amd64.deb` (`arm64` for ARM) | Double-click, or `sudo apt install ./hearthlens_<version>_amd64.deb` |
| **Fedora, openSUSE, RHEL** | `hearthlens-<version>-1.x86_64.rpm` (`aarch64` for ARM) | `sudo dnf install ./hearthlens-<version>-1.x86_64.rpm` |
| **Any Linux** | `Hearthlens-<version>-linux-x64.AppImage` | Make it executable and run it. |
| Any Linux, portable | `Hearthlens-Portable-<version>-linux-x64.tar.gz` (`arm64` for ARM) | Unpack anywhere and run `./Hearthlens`. Settings and history stay in the `data` folder next to it. It tells you about new versions but does not update itself. |
| Any Linux (installer script) | `Hearthlens-<version>-linux-x64.tar.gz` | Unpack, run `./install.sh`; remove with `hearthlens-uninstall`. |

Every installer adds a desktop shortcut and can be uninstalled cleanly; your match history is kept unless you choose to delete it. (The AppImage and the Windows preview zip are portable: nothing to install or uninstall.)
`SHA256SUMS.txt` lists the checksums of all files.

**You only have to do this once.** Hearthlens 0.5.0 and newer keep themselves up to date: they check GitHub in the background and install the new version when you close the app. *Before that first update, install 0.5.0 by hand — 0.4.0 and older do not know how to update themselves.*

**macOS note:** Hearthlens is not notarized by Apple. If macOS says it cannot be opened, right-click it, choose **Open**, then **Open** again (only the first time).

**Phones and tablets:** there is no mobile version yet. iPhone and iPad do not allow apps to read Hearthstone's data or draw over it, so iOS is not possible. On Android 11 and later, reading Hearthstone's logs needs extra setup (Shizuku or root); an Android version is planned.

## What's in this version

**The overlay follows Night edition.** Turn on **Settings → Appearance → Night edition** and the in-game overlay switches to deep green paper with bone ink as well, straight away, in both the Windows app and the cross-platform app. Before, it stayed on light paper whatever you picked.

## What was new in 0.12.0

**A new look, inside and out.** Hearthlens drops the dark wood and gold for something calmer and easier to read: bone paper, a bottle-green sidebar, oxblood ink and one marigold mark for the pick that matters.

- New type: Fraunces for headings and Manrope for everything else, both easier to read at small sizes.
- The in-game overlay is a set of paper notes at the edges of the board. Best picks get a marigold "Best pick" tag, tier chips are square, and labels are in normal case ("Buy", "In plan", "Next") instead of shouting in capitals.
- No more glowing bars, scalloped badges or gloss on the buttons.

**Night edition.** Prefer a dark app? Turn on **Settings → Appearance → Night edition** for deep green paper with bone ink. It switches straight away, no restart. The overlay keeps its light paper notes either way, because they read best over any board.

The seven colour themes and the custom colour sliders are gone: the app now has one look, in a day and a night edition. If you used a dark theme before, switch on Night edition once after updating.

**Fixes**

- Switching to Night edition in the cross-platform app now recolours tier badges, win and loss colours and the rating chart straight away, instead of keeping the old colours until the page was rebuilt.
- In the cross-platform app, the overlay no longer picks up dark colours for its tier chips when the app is in Night edition.

Card data and art: HearthstoneJSON and hsbg.cards. Not affiliated with Blizzard Entertainment.

