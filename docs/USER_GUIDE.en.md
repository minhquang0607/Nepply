# 📖 Using Nepply

**English** · [Tiếng Việt](USER_GUIDE.md) · [Home](../README.md) · [Troubleshooting](TROUBLESHOOTING.en.md)

For **Nepply 2.0.0-rc.2**, a release candidate that is not code-signed. 2.0.0 will be the first signed release.

## 🖱️ Open and close the panel

Setup asks for a language, preselecting your Windows language, and the app uses it on its first run. The portable build uses your Windows language. Change it on the welcome screen or later in **Settings → Appearance**.

Move to your selected screen edge, or open the panel from the tray icon. Nepply sets no hotkey for this, because easy combinations usually belong to another app; choose your own in **Settings → Shortcuts**.

- Pin the panel to keep it open, including while you type in another app.
- **Ctrl + F** focuses search.
- **Esc** leaves the current interaction or clears/closes search first; otherwise it collapses the panel.
- Collapsing is not quitting. Choose **Exit** from the tray icon to quit completely; check Windows' hidden tray icons if necessary.

For an in-app guide, choose **Guide** from the tray icon or in **Settings → Application**. It shows the shortcuts you currently use.

While a game hides the pointer for character control, Nepply avoids opening automatically from the edge. When the pointer returns, such as in a game menu, the panel can be used again. Behavior depends on how the game manages its pointer.

An open, unpinned panel collapses when another app hides the pointer, including when Windows hides it while you type (Windows' hide-pointer-while-typing option). Pin the panel to keep it open.

## ⌨️ Shortcuts

Every shortcut can be changed in **Settings → Shortcuts**. Click a key box and press the new combination; ↺ restores the default and × removes the shortcut.

| Action | Default | Works |
| --- | --- | --- |
| Show / hide panel | Not set | Anywhere |
| Open panel and search | Not set | Anywhere |
| Hide / show desktop icons | Not set | Anywhere |
| Pin / unpin panel | Not set | Anywhere |
| Open settings | Not set | Anywhere |
| Search desktop | Ctrl + F | While the panel is open |
| Refresh desktop | Not set | While the panel is open |

- Shortcuts that work anywhere need Ctrl or Alt, so they cannot block typing in other apps.
- Panel shortcuts may be F1–F12 on their own; letters and digits still need Ctrl or Alt because the panel has a search box.
- Two actions cannot share a key; Settings tells you which action already uses it.
- Esc is fixed: it leaves the current interaction (layout editing, search), and pressing it again collapses the panel. To hide the panel with another key, use the show/hide shortcut.
- If another app already owns a key, Nepply says so on the panel; choose another key in Settings → Shortcuts.

## 📐 Desktop items and grid

Your files remain on the Desktop. Search by name, open with your configured click mode, or right-click for the Windows menu. Delete/rename commands affect the actual item, not a copy.

In **Settings**, use the dot grid to choose **columns × rows**:

- Left/right panel: up to 5 columns.
- Top/bottom panel: up to 3 rows.
- Scroll to reach items outside the visible grid.

Panel dimensions adjust relative to the screen. Choose a comfortable size for reading and clicking; the panel need not fill your screen to hold all items.

## 🎨 Preview before saving

Change edge, dimensions, grid, colors, theme and effects in Settings. Supported preview options appear on the panel before you save.

| Action | Result |
| --- | --- |
| Apply | Save and keep Settings open. |
| OK | Save and close Settings. |
| Cancel / close button | Discard changes since the most recent save. |

Cancel does not undo an earlier Apply. System changes such as startup registration are applied when saving, not as a visual preview.

The look follows Windows' light or dark mode by default. Neon and metallic effects can be toggled. Reduce or disable motion for a static interface. Layout export/import saves presentation preferences, not a full backup of settings or Desktop files.

## 📊 System monitoring

Cards show CPU, RAM, GPU/iGPU and network rates. Temperature and clock readings require suitable system data; on many Intel/AMD machines CPU temperature is unavailable because Nepply installs no driver ([why](TROUBLESHOOTING.en.md#why-does-cpu-temperature-show-unavailable)). Motherboard temperature and fan RPM are not supported in this version.

Disable monitoring or network rates when not needed. Advanced options control sampling interval and pausing while the panel is hidden. Select the appropriate network adapter, especially with VPNs or multiple connections.

Upload/download figures describe adapter traffic, not an Internet speed test. Do not disable antivirus/anti-cheat or install unfamiliar drivers to obtain extra readings.

## ⚡ Start with Windows

Setup offers **Start with Windows**, ticked by default, on the first install; untick it if you prefer. After that, change it in Settings and choose Apply or OK. Task Manager and Windows Settings have their own switch for this entry. If that switch is off, Nepply says so under the checkbox and does not turn it back on; turn it on in **Task Manager → Startup apps** and the warning goes away. Keep a portable installation in a fixed location; after moving it, open the app from the new location and save the startup preference again.

## 🔄 Updates

Open **Settings → Application → Check for updates**. A suitable release shows its version and a summary of what changed since your version, in the language Nepply uses, so you can update or defer. With **Check for updates daily**, Nepply shows a tray notification when a new version exists; the option is on by default, can be turned off on the same page and never downloads anything by itself. If two checks in a row fail while the network is up, Nepply shows one tray notification; click it to open Settings and check again.

**Current limit:** automatic EXE installation is not enabled for public release until signing configuration and acceptance testing are complete. Portable cannot replace its own files. In those cases the update window offers **Open download page**, linking to the official release so you can download the new installer yourself. Internal builds are not an official update channel; check [Releases](https://github.com/minhquang0607/Nepply/releases) when downloads are published.

## 🗑️ Uninstall and data

- **Setup:** exit Nepply, confirm Desktop icons have returned if the app hid them, then uninstall through Windows Installed apps.
- **Portable:** disable startup and save, exit, check the Desktop, then remove only the extracted application folder.

When you uninstall the Setup edition, Nepply asks whether to remove its settings and logs too. Choose **No** if you plan to reinstall and want to keep your settings. For the portable edition, or to reset data yourself, see [Troubleshooting](TROUBLESHOOTING.en.md). Do not delete recovery markers while Desktop icons are still hidden.
