# 🛠️ Troubleshooting

**English** · [Tiếng Việt](TROUBLESHOOTING.md) · [Home](../README.md) · [User guide](USER_GUIDE.en.md)

## The panel will not open

1. Check the tray, including hidden icons, to confirm Nepply is running.
2. Try Ctrl + Space unless you changed it. If another app uses that shortcut, choose a different one in Settings.
3. Check the selected edge and display. After disconnecting a monitor, select an available display again.
4. If a game hides the pointer for character control, preventing edge activation is intentional. Try with the pointer visible in a menu.

## The panel collapses while I type in another app

Windows hides the pointer while you type. An unpinned panel treats that as another app taking the pointer and collapses. Pin the panel to keep it open. If a pinned panel still collapses while you type, report it with your Nepply version.

## A Desktop item is missing

Clear the search and check hidden-file/system-icon filters. Nepply lists items directly on the Desktop, not all contents of every subfolder. After many file changes, try Refresh.

An externally deleted or renamed item cannot open at its old path. Check File Explorer before reporting the issue.

## Temperature or GPU readings are missing

Not all hardware exposes every sensor. Check monitoring/sensor options and the selected GPU. Usage and temperature may come from different sources; a usage reading does not guarantee a temperature reading.

### Why does CPU temperature show "unavailable"?

This is a known limitation, not an installation fault. On many Intel and AMD CPUs, CPU temperature is only readable through a kernel driver (LibreHardwareMonitor uses PawnIO), which usually requires administrator rights. Nepply runs as a normal user and **does not install drivers**, so on these machines the CPU card still shows usage and clock but no temperature.

- Some AMD machines with integrated Radeon graphics (APUs) can read it through AMD ADL; the card details mark that source as *fallback*.
- GPU temperature usually comes from the graphics driver's API, so it can be present while CPU temperature is missing.
- Do not run Nepply as administrator or install unfamiliar drivers just to get a temperature. Use your motherboard or CPU vendor's tool if you need to monitor CPU temperature.

Do not disable Vanguard, antivirus or Windows protection, or install drivers to force sensor access. Report the CPU/GPU model, Windows version and missing field. Motherboard temperature and RPM are not supported.

## Network rates are zero or differ from a download app

Select the adapter carrying the traffic, especially with VPNs or multiple adapters. Readings cover the whole adapter over a sampling interval; different units and intervals can differ from a downloader's display.

## Effects or motion feel slow

Try disabling neon/metallic effects or reducing motion, increasing the monitoring interval and pausing monitoring while hidden. If it persists, include the action, screen resolution, Windows scaling, GPU and a short privacy-safe recording.

## Desktop icons did not return after a crash

Nepply has a recovery helper, but recovery is not guaranteed if that process is also stopped or Explorer is unavailable.

1. Reopen Nepply and choose Exit from the tray.
2. If still hidden, right-click the Desktop → View → Show desktop icons.
3. Do not remove recovery markers or app data while the Desktop remains in the wrong state. Report recurring failures.

## Windows blocks the file, or updates fail

There are no official signed downloads yet. Do not disable SmartScreen, App Control or antivirus to bypass a block. Verify the source and information on [Releases](https://github.com/minhquang0607/Nepply/releases).

For update-check errors, check connectivity and retry. A private repository or absence of suitable releases can also prevent updates. Production automatic installation and portable self-replacement are not currently enabled.

## Reset settings

In Settings choose Defaults, review the options, then Apply or OK. Choose Cancel if you do not want to save.

To back up first, exit and copy `%APPDATA%\Nepply\settings.json` elsewhere. Deleting only this file may restore a `.bak` or import legacy HoverBox settings instead of resetting. If Settings cannot open, report the problem before deleting recovery files.

Logs are in `%LOCALAPPDATA%\Nepply\Logs`; deleting them is not necessary for a visual reset. The legacy recovery marker is `%LOCALAPPDATA%\HoverBox\desktop-visibility.lease`; do not remove it before icons are restored. See the [privacy policy](legal/PRIVACY.md).

## Report an issue safely

[Open an issue](https://github.com/minhquang0607/Nepply/issues) with:

- Nepply/Windows version and Setup or portable.
- Reproduction steps, expected and actual behavior, and frequency.
- Screenshots/video if useful; resolution/scaling for display issues and hardware models for monitoring issues.

Redact account names, personal paths, private filenames and other sensitive data. Do not upload entire configuration/log folders by default. Report vulnerabilities through the [private security channel](legal/SECURITY.md).
