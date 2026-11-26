# 📝 Nepply changelog

**English** · [Tiếng Việt](CHANGELOG.md) · [Home](README.md)

## 2.0.0-rc.3 — not yet released

Third release candidate, not code-signed. The release date is added when it reaches the Releases page.

### Upgrading

- Upgrading from `2.0.0-rc.2` installs directly over the existing setup (exit rc.2 first or let Setup wait for it to close). If you are still on `2.0.0-rc.1`, uninstall rc.1 before installing this build.

### Security

- Updated the bundled .NET runtime to 10.0.15 (from 10.0.14), which includes Microsoft's November 2026 security fixes.
- Changed **Check for updates daily** to opt-in (**off by default**) and blocked background update checks before first-run onboarding completes, so no network request runs without explicit consent.
- Packaged `TERMS.md` alongside Setup and portable builds, and added quick-open buttons for Terms/Security in Settings and Terms/Privacy on the welcome screen.

### Updates

- **What's new** in the update window is now a short summary in the language Nepply uses, covering every version newer than yours. Releases made before summaries existed still show their notes as before. Takes effect from the release after this one.

### Settings & interface

- The display selector in Settings shows friendly Windows monitor names instead of technical names like `DISPLAY1` or `DISPLAY2`, appending the device code only when two displays share a name.
- Settings and automatic network selection now share one filter: hiding disabled, loopback and internal virtual switches (`vEthernet`, Hyper-V, Wi-Fi Direct, VirtualBox host-only, VMware, Kernel Debug, Teredo/ISATAP/6to4), preferring physical adapters with a gateway and falling back to active VPN connections.
- The layout save failure prompt now opens with the panel as owner, the `Nepply` title and a warning icon.
- Repositioning the panel across mixed-DPI monitors now calls `SetWindowPos` directly in physical coordinates once the window handle exists, avoiding an intermediate logical jump.

### Desktop & performance

- Stopped watching `LastWrite` file-content events in `DesktopWatcherService` so modifying an existing file on the Desktop no longer triggers background rescans every 2 seconds.
- A single oversized icon exceeding dimension limits no longer exhausts the decoded image budget for remaining Desktop icons.
- The Desktop item Shell context menu now supports `Shift + right-click` for extended verbs (such as *Copy as path* / *Open in Terminal*) and passes `CMIC_MASK_UNICODE`, `CMIC_MASK_PTINVOKE` and `ptInvoke` when invoking commands.
- Reduced temporary string allocations when reading `.lnk` shortcut destinations.

### Documentation & community testing

- Added bilingual [Terms of use](docs/legal/TERMS.md), the community [Testing guide](docs/TESTING.en.md), a `test-report` issue form and clearer Desktop icon hide/recovery explanations in the [User guide](docs/USER_GUIDE.en.md).
- Added English architecture documentation with process boundary and Desktop icon recovery diagrams.

## 2.0.0-rc.2 — 10 November 2026

Second release candidate, published **10 November 2026**, not code-signed.

### Upgrading from 2.0.0-rc.1

- **Uninstall `2.0.0-rc.1` before installing this version.** The session locks and the Desktop icon recovery marker now use the Nepply name, so this version cannot see a running rc.1 or icons that rc.1 hid. Settings in `%APPDATA%\Nepply` carry over unless you choose to remove data during uninstall.
- Removed migration from the previous product name: its settings, startup entries, shortcuts and layout files are no longer imported.

### Microsoft Store edition

- Preparing a Microsoft Store edition: Microsoft signs the package after certification and the Store updates it. This edition still hides Desktop icons, but it hides the icon window instead of changing a Windows setting, so removing the app leaves nothing behind. It does not check GitHub for updates. Setup and portable are unchanged.

### Security

- Updated the bundled .NET runtime to 10.0.14, which includes Microsoft's October 2026 security fixes. rc.1 shipped 10.0.12.

### Reliability

- A failed daily update check retries after 12 hours instead of waiting a day while the network is up. After two failures in a row Nepply says so once in the notification area; click it to open the update settings. When Windows reports no network, Nepply sends nothing and does not count a failure.
- Two sensors that share a hardware ID no longer break the readings: Nepply keeps the first one.
- If the process that protects hidden Desktop icons stops unexpectedly, the panel now says so and the log records it with the exit code. Failed attempts to hide or restore icons are logged as well.

### Desktop & interaction

- One Desktop item that cannot be read no longer blocks the whole list: Nepply skips it, shows the others and writes the error to the log. If a whole Desktop location is lost, the last list stays on screen as before.

## 2.0.0-rc.1 — 2 November 2026

First public release candidate, published **2 November 2026**, not code-signed. The list below summarizes the changes made during development.

### Desktop & interaction

- Open from the screen edge or a hotkey, and pin when needed. No hotkey is preset for opening the panel, because easy combinations clash with other apps; set one in Settings → Shortcuts and clear it like any other. A hotkey saved by an earlier build is kept.
- Search/open Desktop items with the Windows context menu.
- Opening a shortcut to a disconnected network drive no longer freezes the panel.
- Choose columns × rows and screen-relative panel dimensions.
- Avoid automatic edge activation when a game hides the pointer for character control.
- A pinned panel stays open while you type in another app (when Windows hides the pointer).
- The Desktop item context menu closes when you click elsewhere, like the tray menu.
- A hotkey saved in the settings file must include Ctrl or Alt; a Shift-only combination becomes unset so it cannot block normal typing.

### Appearance & personalization

- Light, dark and system themes (following Windows by default); colors, neon and mouse-interactive metallic effects.
- Preview before saving: Apply saves, OK saves/closes, Cancel discards unsaved changes.
- Vietnamese, English, Japanese, German, Simplified Chinese, Russian, French and Thai.
- The first run uses your Windows display language (English when Nepply lacks it), and the welcome screen lets you pick another. Existing settings keep their language; Reset does not change it.
- A **Shortcuts** section in Settings: every key is rebindable (show/hide, search, refresh…), with new global shortcuts to open the panel and search, hide/show desktop icons, pin the panel and open settings (unset by default).
- Settings, the tray menu and layout editing show feature names only; usage tips moved to a **Guide** you can reopen from the tray or Settings.
- The icon size (small, medium, large) stored in settings or an imported layout is now applied; medium remains the default.

### System monitoring

- Optional CPU, RAM, GPU/iGPU and network cards.
- GPU/iGPU classification uses system information; unavailable sensors may show no reading.
- Opening the panel shows readings right away: CPU, RAM and network appear in about 0.2 seconds, while temperatures and GPU keep their last values until fresh ones arrive. While the panel is hidden the sensor helper stays for 10 minutes without reading and starts early when the pointer nears the edge; turning off monitoring or sensors still stops it immediately.
- The panel opens when the pointer moves up the edge from the taskbar corner, and reacts sooner when the pointer reaches the edge.
- An overly long GPU, monitor or network identifier in the settings file falls back to automatic selection instead of leaving sensors stuck reconnecting.
- A missing temperature shows a muted dash that explains why on hover.
- **Copy diagnostics** (Settings → Application) copies the version, Windows and sensor details for bug reports, without paths or account names.
- The Desktop icon guardian process has a clear name in Task Manager.

### Updates & recovery

- Update window with version, notes, progress and retry states.
- **Check for updates daily** option, on by default and available on the welcome screen, shows a tray notification when a new version exists.
- When the Windows startup switch is off, Settings says so under the checkbox and shows where to turn it on; the warning goes away once you do, without reopening the app. A switch left over from an earlier install no longer turns off the new entry.
- When this build cannot install an update itself (portable or not yet signed), the update window offers **Open download page**, linking to the official GitHub release.
- Release notes in the update window appear as clean text instead of raw Markdown.
- Setup asks for a language when it opens, preselecting your Windows language: Vietnamese, English, Japanese, German, Simplified Chinese, Russian, French and Thai. The app uses that language on its first run.
- Setup has a dark look with Nepply artwork. The information page shows only the language you picked. Start with Windows is ticked on the first install, and updates leave that choice alone.
- Uninstalling asks whether to remove settings and logs too (kept by default), clears the extraction folder in Temp and drops the startup switch of the removed entry.
- **Report a problem** (Settings → App) copies diagnostics and opens the GitHub bug form with the Nepply and Windows versions filled in.
- Desktop icon recovery after abnormal termination, subject to helper/Explorer availability.

### Current limits

- No official signed distribution yet.
- Automatic EXE installation is not enabled for release; portable cannot replace its own files.
- No published Microsoft Store version.
