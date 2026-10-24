# 📖 Using Nepply

**English** · [Tiếng Việt](USER_GUIDE.md) · [Home](../README.md) · [Troubleshooting](TROUBLESHOOTING.en.md)

For **Nepply 1.0.0-beta.1**, the first public beta (unsigned), currently in preparation. No official signed downloads are available yet.

## 🖱️ Open and close the panel

Move to your selected screen edge, or press **Ctrl + Space** unless you changed the hotkey.

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
| Show / hide panel | Ctrl + Space | Anywhere |
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

Neon and metallic effects can be toggled. Reduce or disable motion for a static interface. Layout export/import saves presentation preferences, not a full backup of settings or Desktop files.

## 📊 System monitoring

Cards show CPU, RAM, GPU/iGPU and network rates. Temperature and clock readings require suitable system data; on many Intel/AMD machines CPU temperature is unavailable because Nepply installs no driver ([why](TROUBLESHOOTING.en.md#why-does-cpu-temperature-show-unavailable)). Motherboard temperature and fan RPM are not supported in this version.

Disable monitoring or network rates when not needed. Advanced options control sampling interval and pausing while the panel is hidden. Select the appropriate network adapter, especially with VPNs or multiple connections.

Upload/download figures describe adapter traffic, not an Internet speed test. Do not disable antivirus/anti-cheat or install unfamiliar drivers to obtain extra readings.

## ⚡ Start with Windows

Enable startup in Settings and choose Apply or OK. Windows may manage or disable it in Startup apps. Keep a portable installation in a fixed location; after moving it, open the app from the new location and save the startup preference again.

## 🔄 Updates

Open **Settings → Application → Check for updates**. A suitable release shows its version and notes so you can update or defer. There is no scheduled background update check.

**Current limit:** automatic EXE installation is not enabled for public release until signing configuration and acceptance testing are complete. Portable cannot replace its own files. Internal builds are not an official update channel; check [Releases](https://github.com/minhquang0607/Nepply/releases) when downloads are published.

## 🗑️ Uninstall and data

- **Setup:** exit Nepply, confirm Desktop icons have returned if the app hid them, then uninstall through Windows Installed apps.
- **Portable:** disable startup and save, exit, check the Desktop, then remove only the extracted application folder.

Settings may remain for reinstallation. See [Troubleshooting](TROUBLESHOOTING.en.md) to reset them. Do not delete recovery markers while Desktop icons are still hidden.
