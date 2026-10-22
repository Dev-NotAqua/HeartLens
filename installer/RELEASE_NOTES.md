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

**Combat odds are back.** The win, tie and loss chances now stay on screen for the whole fight. Hearthstone writes the entire fight to its log about a second after it starts and then plays it for another half a minute; Hearthlens took the end of the log entry as the end of the fight, so the odds disappeared before the fight was even shown.

## What was new in 0.7.5
**Pick your colours.** *Settings â†’ Theme* has seven ready-made themes: Tavern (the gold you know), Frost, Fel, Arcane, Ember, Tide and Shadow. Click one and the app and the in-game overlay switch right away. **Make your own** sets the accent colour, the background colour and how strongly the background is tinted, with a live preview. Wins, losses, tiers and card rarities keep their usual colours in every theme.

**Your last hero behind every page.** The art of the hero you played last fades in behind the page titles.

**A cleaner window.**

- Decorative rings and lines are gone; headings are just an icon and a title.
- Stat labels read normally instead of in capitals.
- One status chip instead of two: grey when Hearthstone is closed, gold when it runs, green when Hearthlens is connected.
- Hero names in History were nearly black on the dark background; they are readable again.
- The overlay's top bar keeps the lobby tribes on one row.

## What was new in 0.7.4

**A new look for the Windows app.** The whole app and the in-game overlay get a gilded tavern style:

- Page titles and the logo are gold; panels look lit from above with a gold top edge.
- Buttons and switches are polished gold metal; the selected menu item glows.
- Filter bars and text boxes sit in a recessed groove; scrollbars are gold.
- The rating chart is a smooth gold curve with a soft glow and a tag with your current rating. The curve never dips past a real value.
- Pages slide up and fade in when you open them (off when Windows animations are off).
- In game, the recommended hero, trinket and Discover option has a gold edge, a metal BEST PICK tag and a soft gold glow, drawn without heavy effects so the overlay stays fast.

## What was new in 0.7.3

**Tracking no longer stops around turn 8.** Hearthstone stops writing its game log once the file reaches 10 MB, and a Battlegrounds game passes 10 MB around turn 8. From then on the game writes nothing more until it restarts, so the overlay froze with no error anywhere. Other trackers turn this limit off; Hearthlens did not. It now sets `FileSizeLimit.Int=-1` in the `[Log]` section of `client.config` in the Hearthstone folder, keeps everything else in that file, and puts the setting back if it disappears.

**Restart Hearthstone once after installing this version.** The game only reads `client.config` when it starts. Hearthlens shows a "Restart Hearthstone" note while the old limit is still active, and another one if a game hits the limit.

**Detailed diagnostic log.** `%APPDATA%\Hearthlens\hearthlens.log` now records what every part of the app is doing, so a problem can be traced to the second it started:

- every turn, game start and game end;
- every 15 seconds: the size of Hearthstone's log and how far Hearthlens has read it, lines handled, the game and turn the tracker sees, the memory reader's state, how many updates the window showed and how long it waited for them, RAM use;
- a note when Hearthstone's log stops growing, when it stopped at the 10 MB limit, when a log pass or an overlay update is slow, and when the window does not respond;
- the Hearthstone folder, the log settings and the size limit found at startup.

The previous log is kept as `hearthlens.old.log` when the log reaches 4 MB.

## What was new in 0.7.1 and 0.7.2

**The overlay no longer rebuilds itself ten times a second.** Every update of the game replaced every list on the overlay with a new copy, even when nothing in it had changed. The window then threw away and rebuilt every row and set its card picture again, up to ten times a second. A fast PC hides that; a slower one cannot keep up and the overlay freezes. A list that did not change is now left alone.

**Other fixes from a full bug hunt:**

- **The tracker keeps going after a log line it does not understand.** Before, one unexpected line in Hearthstone's log threw away the rest of that batch, and the overlay could then show a wrong board or turn.
- **Reading the game's memory never slows the tracker down.** It runs on its own thread, every scan has a time limit, and a few rare cases where it could loop forever or crash the app are fixed.
- **No more restart loops on a slow PC.** The automatic recovery waited too little and could restart the log reader again and again while it was still working.
- **Card pictures:** a broken picture on disk is deleted and downloaded again instead of staying blank forever; error pages are never saved as pictures; the picture cache really stays small now (finished downloads kept their pictures in memory).
- **Updates:** the downloaded setup is checked against GitHub's checksum before it runs; a Windows user name with accents (for example `C:\Users\Niccolò`) or a `%` in a path no longer stops the update from installing; old setup files are deleted.

**The freeze on a slower PC was the card images.** When a card image was already on disk, Hearthlens decoded it **on the interface thread**: decoding one picture takes tens of milliseconds, and the comp panel alone asks for dozens of them at once. The window stopped repainting for seconds — the tracker was still running in the background, which is why the overlay looked stuck on an old turn while the log showed no error at all. Images are now decoded away from the interface thread, at most four at a time.

That was the last piece of the freeze hunt: the other causes (the fight simulation on the log thread, the 85 MB statistics file, the unbounded image cache, the memory reader) are already fixed in 0.7.0.

## What was new in 0.7.0

**The overlay can no longer freeze mid-game on a slower PC.** Three things were blocking the tracker, and none of them show up on a fast machine:

- The fight simulation ran on the same thread that reads Hearthstone's log. On a slow PC, simulating two big boards takes seconds — the log was not read during that time, so the overlay stopped updating in the middle of a fight. A quick estimate now appears right away and the accurate one is computed in the background.
- The community statistics file (85 MB) was parsed into a JSON document, which costs several hundred megabytes of RAM. On a small PC that is a freeze, then swapping. The file is now streamed and only the fields that are used are read.
- Card images were cached without a limit. A 256 px card is a quarter of a megabyte, so a long game filled the memory and the system started to swap. The cache now keeps 160 images and drops the oldest.

**Log reading got lighter.** The log is read in 256 KB blocks through one reused buffer instead of allocating a fresh multi-megabyte array on every pass — that churn is exactly what makes a low-memory PC stutter.

**The memory reader does far less work.** It walked the whole cache of server objects on every poll, and that map grows during a session. The address it needs is now remembered, and a scan that takes longer than a second backs off and writes it to the log.

**A restarted watcher no longer feeds the tracker twice.** When the watchdog replaces a stuck reader, the old thread stops delivering lines before it wakes up.

**More accurate advice**

- **Freeze** only when the card really cannot be bought now. It names the card, why it is worth keeping (completes a triple, core card, pair, or simply too strong to reroll away) and how much more gold is needed. Nothing is claimed when the gold is unknown.
- A **pair is bought** instead of being rolled away: a pair leads to a triple, and a triple discovers a higher tier.
- Core cards, add-ons, golden cards and the last copy of a pair are **never sold** to make room. When the board is full of cards worth keeping, the advice is to hold instead of selling your own plan.
- **Rolling** is never advised while a triple, core card or pair is on offer.
- **"Hold this turn"** now says what to save for: the tier upgrade and its cost, a full board worth keeping, or the health that makes leveling too risky.
- The **Dark Gift** hint stays quiet when the gold is unknown and says what it costs.
- **Comps no longer suggest cards that are not in this patch.** Cards that are not in the current tavern pool (and Duos-only cards in a solo game) are hidden, and a comp with no playable card left is not offered at all.
- The tavern curve is corrected: tier 2 by turn 3, tier 3 by turn 5, tier 4 by turn 7, tier 5 by turn 10, tier 6 after that.

## What was new in 0.6.0

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
