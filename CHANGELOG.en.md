# 📝 Nepply changelog

**English** · [Tiếng Việt](CHANGELOG.md) · [Home](README.md)

## 2.0.0-rc.1 — Unreleased

Consolidated **17 October 2026**. This is a summary of development changes, not a download announcement. The version is finalized; the summary date is not a publication date, and does not imply signed binaries are available.

### Desktop & interaction

- Open from the screen edge or a hotkey, and pin when needed.
- Search/open Desktop items with the Windows context menu.
- Choose columns × rows and screen-relative panel dimensions.
- Avoid automatic edge activation when a game hides the pointer for character control.
- A pinned panel stays open while you type in another app (when Windows hides the pointer).
- The Desktop item context menu closes when you click elsewhere, like the tray menu.
- A hotkey saved in the settings file must include Ctrl or Alt; a Shift-only combination falls back to Ctrl so it cannot block normal typing.

### Appearance & personalization

- Light, dark and system themes; colors, neon and mouse-interactive metallic effects.
- Preview before saving: Apply saves, OK saves/closes, Cancel discards unsaved changes.
- Vietnamese, English, Japanese, German, Simplified Chinese, Russian, French and Thai.
- The first run uses your Windows display language (English when Nepply lacks it), and the welcome screen lets you pick another. Existing settings keep their language; Reset does not change it.
- A **Shortcuts** section in Settings: every key is rebindable (show/hide, search, refresh…), with new global shortcuts to open the panel and search, hide/show desktop icons, pin the panel and open settings (unset by default).
- Settings, the tray menu and layout editing show feature names only; usage tips moved to a **Guide** you can reopen from the tray or Settings.
- The icon size (small, medium, large) stored in settings or an imported layout is now applied; medium remains the default.

### System monitoring

- Optional CPU, RAM, GPU/iGPU and network cards.
- GPU/iGPU classification uses system information; unavailable sensors may show no reading.
- After the panel hides, the sensor helper is kept for 60 seconds (without reading), so reopening shows readings right away instead of restarting it; turning off monitoring or sensors still stops it immediately.
- An overly long GPU, monitor or network identifier in the settings file falls back to automatic selection instead of leaving sensors stuck reconnecting.
- A missing temperature shows a muted dash that explains why on hover.
- **Copy diagnostics** (Settings → Application) copies the version, Windows and sensor details for bug reports, without paths or account names.
- The Desktop icon guardian process has a clear name in Task Manager.

### Updates & recovery

- Update window with version, notes, progress and retry states.
- When this build cannot install an update itself (portable or not yet signed), the update window offers **Open download page**, linking to the official GitHub release.
- Release notes in the update window appear as clean text instead of raw Markdown.
- Setup follows your Windows language: Vietnamese, English, Japanese, German, Simplified Chinese, Russian, French and Thai.
- **Report a problem** (Settings → App) copies diagnostics and opens the GitHub bug form with the Nepply and Windows versions filled in.
- Desktop icon recovery after abnormal termination, subject to helper/Explorer availability.

### Current limits

- No official signed distribution yet.
- Automatic EXE installation is not enabled for release; portable cannot replace its own files.
- No published Microsoft Store version.
