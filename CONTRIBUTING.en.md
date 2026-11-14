# 🧩 Contributing to Nepply

**English** · [Tiếng Việt](CONTRIBUTING.md) · [Documentation](docs/README.md)

For building and contributing. To use the app, start with the [user guide](docs/USER_GUIDE.en.md). Nepply-owned code uses [MIT](LICENSE); dependencies retain their own licenses. Author and maintainer: Minh Quang. AI team: Codex and Claude.

## Quick start

```powershell
dotnet restore Nepply.slnx --locked-mode
dotnet build Nepply.slnx -c Release --no-restore
dotnet run --project tests/Nepply.Tests/Nepply.Tests.csproj -c Release --no-build -- artifacts/current/core --core-only
```

Before committing C# changes, run `dotnet format whitespace Nepply.slnx`; CI rejects code that does not match `.editorconfig`. To keep `git blame` useful, run `git config blame.ignoreRevsFile .git-blame-ignore-revs` once.

## Start here

Use Windows x64 and the .NET SDK pinned in `global.json`. Open `Nepply.slnx`. Consult the [source structure](docs/architecture/STRUCTURE.en.md) and [module boundaries](docs/architecture/MODULES.en.md).

## Change guidelines

- Product version `2.0.0-rc.2` and Windows file version `2.0.0.2` come from `src/Nepply/Nepply.csproj`; repeat both in `src/Nepply.DesktopGuardian/Nepply.DesktopGuardian.csproj`, because the app refuses a guardian with another file version. Each release candidate raises the fourth number (`rc.3` would be `2.0.0.3`); the stable release uses `2.0.0.100`, so Windows always sees it as newer. Do not change dependency versions or simulated test versions just to match the product.
- Put logic in its owning feature, interop in Platform/Windows and shared UI in Presentation. Do not add projects, timers, processes or dependencies solely to split folders.
- Keep regression tests in tests/Nepply.Tests. Diagnostic tools do not belong in the release payload.
- Every release needs `packaging/release-notes/<version>.json`: a few plain-text summary lines in all 8 languages, the same number of lines in each, at most 400 characters per line. The update window of later versions shows it in the user's language. `scripts/Test-ReleaseNotes.ps1` checks the format; `release.yml` stops when the tagged version has no file and attaches it to the release as `release-notes.json`.
- Never commit bin/obj/artifacts, personal logs/settings, signing keys or private certificates.
- Do not copy reference-repository source. Review licenses/notices when dependencies change.
- Do not disable Windows protection or anti-cheat to make tests pass. Record blocked tests honestly.

## Validate your change

```powershell
dotnet restore Nepply.slnx --locked-mode
dotnet build Nepply.slnx -c Release --no-restore
./scripts/Test-BuildBoundary.ps1
./scripts/Test-LanguageResources.ps1
dotnet run --project tests/Nepply.Tests/Nepply.Tests.csproj -c Release --no-build --no-restore -- artifacts/current/core --core-only
dotnet run --project tests/Nepply.Tests/Nepply.Tests.csproj -c Release --no-build --no-restore -- artifacts/current/ui --ui-only
```

The tests are executable fixtures, not a dotnet test suite. UI tests require a suitable Windows desktop session. Native/lifecycle changes need relevant runtime tests; fixtures do not replace real hardware, mixed-DPI or installer acceptance.

CI additionally checks appearance, tray, DPI, crash behavior, dependencies, notices and packaging contracts. Appearance tests set animation preferences explicitly rather than relying on runner defaults. Check `.github/workflows`; a previous commit's green result does not certify a new commit.

Keep Directory.Build.props/targets and locked restore: they pin runtime and ILLink consistently. Update pins and lock files deliberately, then verify on a clean machine/CI.

## Real Explorer gate

CI tests Desktop hiding against file fixtures, so a Windows update can break the main feature while CI stays green. `scripts/Test-DesktopRuntime.ps1` runs the tests that use the real Explorer. Run it before every tag, including release candidates, from a Release build of the commit you will tag:

```powershell
pwsh scripts/Test-DesktopRuntime.ps1
pwsh scripts/Test-DesktopRuntime.ps1 -Live -ReleaseTag v2.0.0-rc.2
```

The first command is a dry run: it reads the current state, prints the plan and changes nothing. The second is the real run.

- **It changes your Desktop.** A real run hides your icons several times and restarts Explorer twice, which closes File Explorer windows. It asks you to type a phrase at a console each time and has no switch to skip that. Never run it from automation. CI runs only `scripts/Test-DesktopRuntimeContract.ps1`, which checks the rules and uses a fake process.
- **It refuses to start** when Nepply, its guardian or the test binary is running, when icons are already hidden or the icon window cannot be found, when a Nepply recovery journal exists, or when the binaries were not built from the current commit.
- **It covers six stages:** guardian normal use and owner crash; Explorer's saved option with owner crash and Explorer restart; and the same two for `ShowWindow`, which the Store edition uses. It stops at the first failure and restores the icons.
- **Results** are in `artifacts/desktop-runtime/<tag>/<time>-<commit>/`: `summary.json`, plus each stage's result file and console log. The run is `ACCEPTED` only if every stage passes on a clean tree with unchanged binaries. Keep that folder and copy the printed row into the record in the [release plan](docs/release/PUBLIC_RELEASE_CHECKLIST.md).
- **Exit codes:** 0 accepted, or dry run with nothing to refuse. 1 not accepted. 2 refused, with nothing changed. 3 icons are still hidden: right-click the Desktop and choose View > Show desktop icons; if that is already ticked, restart Windows Explorer from Task Manager.
- **Limits:** a result applies only to that commit and Windows build, so also run it on an Insider build when you can. Tests run from an unpackaged process, so this does not replace installer, uninstall, MSIX or Store acceptance.

## Current documentation and images

- Update English and Vietnamese user-facing counterparts in the same change when behavior, version, downloads or known limits change. Keep relative language links intact.
- Before verification, record the repository, commit, uncommitted changes and executable path. Never infer a binary version from a folder name.
- Build current source, use a fresh result directory and record build configuration/commands. Old QA evidence is historical only.
- Use current default-layout views for product images. Label fixture data and render dimensions; do not present narrow QA layouts as the default UI.
- Record source, version and hashes in docs/images/README.md. Review the actual images before using them. Do not reuse user screenshots as substitutes for build verification.
- Documentation/image edits do not validate an installer. Do not rewrite upstream license texts or historical provenance dates for visual consistency.

## Internal packaging

```powershell
./scripts/Build-Distribution.ps1 -OutputDirectory artifacts/internal-build -InternalOnly
```

Requires Inno Setup and a fresh output directory. The script includes the app, Desktop recovery helper, notices and manifest; an app-only build is not a release payload. See [packaging](packaging/README.md) and [signing](docs/release/RELEASE_SIGNING.md) (Vietnamese). Unsigned internal output is not an official release. Production signer configuration and signed installer acceptance remain separate gates.
