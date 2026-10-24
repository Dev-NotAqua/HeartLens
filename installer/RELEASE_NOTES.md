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

**Hearthlens has its own setup wizard now.** The Windows installer is part of the app instead of a stock wizard: bone paper on a bottle-green spine, Fraunces and Manrope, the same colours as Hearthlens itself.

- It installs for your user only, with no administrator prompt, into `%LOCALAPPDATA%\Programs\Hearthlens`.
- Choose the folder, and whether you want a desktop shortcut, a Start menu entry and Hearthlens at sign-in.
- It closes a running Hearthlens before it writes, so the files it replaces are never locked.
- Your settings and match history are left alone, and **Settings → Apps** removes the app if you ever want it gone.
- Updates still install themselves: the new setup runs silently when you close the app.

Card data and art: HearthstoneJSON and hsbg.cards. Not affiliated with Blizzard Entertainment.

