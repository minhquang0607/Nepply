# Nepply structure

**English** · [Tiếng Việt](STRUCTURE.md) · [Documentation](../README.md) · [Module boundaries](MODULES.en.md)

This repository contains the product source, test fixtures, build/gate scripts and current documentation. Runtime logs and QA output live outside tracked source.

See the [documentation index](../README.md) for user, contributor and release pages. Telemetry currently covers CPU, RAM, GPU/iGPU and network throughput; motherboard temperatures and fan RPM are not included.

Nepply is a WPF modular monolith: one main application assembly organized by feature and lifecycle, plus a small non-WPF recovery helper (`Nepply.DesktopGuardian`). Open `Nepply.slnx` at the repository root to work with the app, test runner and diagnostic tool.

| Path | Responsibility |
|---|---|
| `src/Nepply/Composition` | Service wiring (`WindowServices`) and ordered cleanup (`CleanupSequence`) |
| `src/Nepply/Features/Desktop` | Discovery, catalog, watcher, icon visibility/recovery and icon grid UI |
| `src/Nepply/Features/Panel` | Main window, screen-edge geometry, layout editing and edge activation |
| `src/Nepply/Features/Search` | Search session, query normalization and reveal animation |
| `src/Nepply/Features/Settings` | Draft/preview session, persistence, welcome onboarding and Settings window |
| `src/Nepply/Features/Telemetry` | Sensor/counter providers, sampling loop, snapshots and monitor UI |
| `src/Nepply/Features/Updates` | Release feed, bounded download, Authenticode/SPKI verification, daily background check (on by default, tray notification only) and update UI |
| `src/Nepply/Presentation` | Theme/appearance resources, shared controls, converters and system tray |
| `src/Nepply/Platform/Windows` | Win32 interop, Shell context menu, display enumeration, global hotkeys, startup registry and helper process pipes |
| `src/Nepply/Diagnostics` | Bounded file and in-memory logging (`AppLogger`) |
| `src/Nepply.DesktopGuardian` | Non-WPF helper tied to a hidden-Desktop session; waits for owner exit and restores Desktop icons, with no persistent registration |
| `tests/Nepply.Tests` | Executable test runner and WPF/runtime fixtures |
| `tools/SensorDumper` | Standalone sensor inspection tool; excluded from the release payload |
| `packaging/inno` | Inno Setup installer script, branding assets, translations and per-language pre-release notices |
| `packaging/msix` | MSIX manifest, assets and Store listing preparation (currently parked) |
| `packaging/release-notes` | Per-version 8-language plain-text summaries (`<version>.json`) for the in-app What's new view |
| `scripts` | Build, packaging, dependency audit, notice inventory, manifest and release gate scripts |
| `docs` | User guide, troubleshooting, testing guide, image provenance, architecture, release and legal docs |
| `artifacts` | Reproducible build/test outputs (git-ignored) |

## Identity and compatibility

The current source version is `2.0.0-rc.2` (`FileVersion` `2.0.0.2`). Grouping files by feature does not mean every class is fully decoupled; see [MODULES.en.md](MODULES.en.md) for current boundaries and known coupling.

The root namespace, assembly name, solution, project files, session locks and Desktop recovery journal all use the `Nepply` name. Compatibility namespaces such as `Nepply.Services`, `Nepply.Views` and `Nepply.Modules` are not separate assemblies. Do not rename session mutexes or the recovery journal just to match a version bump.

## Development rules

- Place new code in the feature that owns it. Only put code in `Presentation` or `Platform` when it is genuinely shared.
- Pure logic must not hold a `Window`, `Dispatcher` or native service reference. UI adapters own data binding and dispatcher marshaling.
- Every long-lived service has a single owner responsible for `Start`/`Stop`/`Dispose`; prefer explicit startup over blocking I/O in constructors. `DesktopWatcherService` starts its reconcile loop from the constructor to preserve its existing contract, but only schedules background work; its owner disposes it and can await `Completion` off the UI thread during test cleanup.
- Keep interfaces small and consumer-focused; do not introduce a service locator or global event bus to hide dependencies.
- Keep one data pipeline per source; Settings live preview must not spawn a second Desktop scanner or hardware collector.
- Keep tests in `tests/Nepply.Tests`. `scripts/Test-BuildBoundary.ps1` rejects any non-production source or resource under `src/Nepply`.
- Do not add projects, timers, helper processes or NuGet dependencies solely to split folders.

## Resources and legal files

WPF resource dictionaries live under `src/Nepply/Presentation/Appearance` and are merged in `App.xaml`. Move `.xaml` and `.xaml.cs` files together. The application icon keeps the assembly resource URI `Resources/icon.ico`. Legal documents (`PRIVACY.md`, `SECURITY.md`, `THIRD_PARTY_NOTICES.md`) are linked from `docs/legal` into the build output root.

## Output hygiene

Do not run destructive `git clean` without a tracked baseline. Clean `bin`/`obj` only when no build or Nepply process is running. Before removing binaries from a QA pass, preserve the required reports and screenshots in the verified archive and keep one portable baseline for comparison. Reference and research repositories live outside this repository and are never build dependencies.
