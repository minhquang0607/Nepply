# 🧪 Testing Nepply

**English** · [Tiếng Việt](TESTING.md) · [Home](../README.md) · [Troubleshooting](TROUBLESHOOTING.en.md)

Nepply 2.0.0 is in release-candidate stage and the maintainer is looking for developers and power users to try it on hardware he does not have. This page says what to install, what to try and how to report. Ten minutes on one scenario is already useful.

## Before you start

- Windows 10 version 2004 (build 19041) or later, or Windows 11, x64.
- Download only from [GitHub Releases](https://github.com/minhquang0607/Nepply/releases). Pick the newest version marked *Pre-release*.
- The build is **not code-signed**, so SmartScreen may warn you. Do not disable SmartScreen, Smart App Control or antivirus to run it; if Windows blocks it completely, that is a result worth reporting.
- If you installed `2.0.0-rc.1`, uninstall it first. Settings are kept unless you choose to remove data.
- Read the [Terms of use](legal/TERMS.md#english) and [Privacy policy](legal/PRIVACY.md#english). Nepply sends nothing automatically except update checks to GitHub, which you can turn off.

## If Desktop icons stay hidden

Reopen Nepply and choose **Exit** from the tray. If they are still hidden, right-click the Desktop → **View** → **Show desktop icons**. Do not delete `%LOCALAPPDATA%\Nepply\desktop-visibility.lease` while icons are hidden. Then report it: this is the most important kind of bug.

## What to try

Pick any rows that match your setup. Write down what happened, even when it worked.

| # | Scenario | What to look for |
| --- | --- | --- |
| 1 | Download Setup in a browser and run it | What SmartScreen or Smart App Control shows; installer language |
| 2 | First run | Language matches Windows; welcome screen is readable |
| 3 | Hide Desktop icons, then Exit from the tray | Icons return |
| 4 | Hide icons, then restart Explorer (Task Manager → Windows Explorer → Restart) | Whether icons stay hidden or come back, and whether the panel still works |
| 5 | Hide icons, then end `Nepply.exe` in Task Manager | Icons come back within a few seconds |
| 6 | Uninstall while icons are hidden | Icons return; nothing left in `%LOCALAPPDATA%\Programs\Nepply` |
| 7 | Several monitors, mixed scaling (e.g. 100 % + 150 %) | Panel opens on the chosen edge and display, at the right size |
| 8 | Sensors on your CPU/GPU (Intel, NVIDIA, AMD, laptop on battery) | Which readings appear or stay empty; **Settings → Application → Copy diagnostics** shows details |
| 9 | Edge opening during games or full-screen apps | Panel does not open while a game hides the pointer |
| 10 | Shortcuts | Setting a hotkey; conflicts with other apps |
| 11 | Leave it running for a day with icons hidden | Memory or CPU in Task Manager growing over time |
| 12 | Windows 10 22H2, non-English Windows, standard (non-admin) account | Anything that differs from Windows 11 |
| 13 | Narrator or High Contrast | Controls are announced and visible |

## How to report

- **Bug:** in Nepply, **Settings → Application → Report a problem** copies diagnostics and opens the bug form with your versions filled in. Review the diagnostics before pasting.
- **Test results (worked or not):** use the [Test report form](https://github.com/minhquang0607/Nepply/issues/new?template=test_report.yml). Results that say "worked" matter too: they tell which hardware is covered.
- **Ideas:** use the [feature request form](https://github.com/minhquang0607/Nepply/issues/new?template=feature_request.yml).
- **Security issue:** report privately under the [security policy](legal/SECURITY.md#english), never in a public issue.

Issues are public. Redact account names, personal paths and private file names from screenshots and logs.

## For developers

To build from source and run the test fixtures, see [Contributing](../CONTRIBUTING.en.md). Pull requests are welcome; please open an issue first for larger changes.
