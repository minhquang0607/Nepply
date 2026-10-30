# 🧩 Contributing to Nepply

**English** · [Tiếng Việt](CONTRIBUTING.md) · [Documentation](docs/README.md)

For building and contributing. To use the app, start with the [user guide](docs/USER_GUIDE.en.md). Nepply-owned code uses [MIT](LICENSE); dependencies retain their own licenses.

## Quick start

```powershell
dotnet restore Nepply.slnx --locked-mode
dotnet build Nepply.slnx -c Release --no-restore
dotnet run --project tests/Nepply.Tests/Nepply.Tests.csproj -c Release --no-build -- artifacts/current/core --core-only
```

Before committing C# changes, run `dotnet format whitespace Nepply.slnx`; CI rejects code that does not match `.editorconfig`. To keep `git blame` useful, run `git config blame.ignoreRevsFile .git-blame-ignore-revs` once.

## Start here

Use Windows x64 and the .NET SDK pinned in `global.json`. Open `Nepply.slnx`. Consult the [source structure](docs/architecture/STRUCTURE.md) and [module boundaries](docs/architecture/MODULES.md); these detailed architecture references are currently in Vietnamese.

## Change guidelines

- Product version `2.0.0-rc.1` and Windows file version `2.0.0.1` come from `src/Nepply/Nepply.csproj`; repeat both in `src/Nepply.DesktopGuardian/Nepply.DesktopGuardian.csproj`, because the app refuses a guardian with another file version. Each release candidate raises the fourth number (`rc.2` is `2.0.0.2`); the stable release uses `2.0.0.100`, so Windows always sees it as newer. Do not change dependency versions or simulated test versions just to match the product.
- Put logic in its owning feature, interop in Platform/Windows and shared UI in Presentation. Do not add projects, timers, processes or dependencies solely to split folders.
- Keep regression tests in tests/Nepply.Tests. Diagnostic tools do not belong in the release payload.
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
