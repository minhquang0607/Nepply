# 📝 Nepply changelog

**English** · [Tiếng Việt](CHANGELOG.md) · [Home](README.md)

## 2.0.0-rc.1 — Unreleased

Consolidated **17 October 2026**. This is a summary of development changes, not a download announcement. The version is finalized; the summary date is not a publication date, and does not imply signed binaries are available.

### Desktop & interaction

- Open from the screen edge or a hotkey, and pin when needed. The default hotkey is Ctrl + Alt + Space, leaving Ctrl + Space to code completion and input methods. A hotkey saved by an earlier build is kept.
- Search/open Desktop items with the Windows context menu.
- Opening a shortcut to a disconnected network drive no longer freezes the panel.
- Choose columns × rows and screen-relative panel dimensions.
- Avoid automatic edge activation when a game hides the pointer for character control.
- A pinned panel stays open while you type in another app (when Windows hides the pointer).
- The Desktop item context menu closes when you click elsewhere, like the tray menu.
- A hotkey saved in the settings file must include Ctrl or Alt; a Shift-only combination falls back to the default so it cannot block normal typing.

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
