# Third-party components

Nepply uses the following unmodified NuGet components. Upstream repositories contain corresponding source and license information. This inventory is taken from the resolved packages for this build.

| Component | Version | Package license / source |
|---|---|---|
| LibreHardwareMonitorLib | 0.9.6 | MPL-2.0 — https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6 |
| RAMSPDToolkit-NDD | 1.4.2 | MPL-2.0 — https://github.com/Blacktempel/RAMSPDToolkit |
| DiskInfoToolkit | 1.1.2 | MPL-2.0 — https://github.com/Blacktempel/DiskInfoToolkit |
| BlackSharp.Core | 1.0.7 | MPL-2.0 — https://github.com/Blacktempel/BlackSharp |
| Hardcodet.NotifyIcon.Wpf | 2.0.1 | MIT — https://github.com/hardcodet/wpf-notifyicon |
| HidSharp | 2.6.4 | See upstream LICENSE.txt — https://software.seekye.com/hidsharp |
| Mono.Posix.NETStandard | 1.0.0 | MIT và notice native/toolchain (zlib/minizip, Info-ZIP, libxml2, MinGW-w64, libgcc có runtime exception) — xem `licenses/mono.posix.netstandard-1.0.0/Mono.Posix-1.0.0-NOTICES.md` |
| System.Management | 10.0.2 | MIT — https://github.com/dotnet/runtime |
| System.IO.Ports | 10.0.3 | MIT — https://github.com/dotnet/runtime |
| System.IO.FileSystem.AccessControl | 5.0.0 | MIT — https://github.com/dotnet/runtime |
| .NET runtime / Windows Desktop runtime | bundled by SDK | https://github.com/dotnet/runtime and https://github.com/dotnet/wpf |

Platform-specific dependencies in the restore graph are not necessarily shipped in the Windows x64 executable. No proprietary sensor driver installer is included. Keep upstream notices/licenses with any redistributed build; this inventory is not a substitute for those license texts.

## Reproducible notice collection

Run `scripts/Collect-ThirdPartyNotices.ps1` after restore/publish. It selects runtime/native assets from the resolved Windows x64 target, adds the pinned Core/Desktop runtime packs, copies available package license/notice texts, and writes `licenses/inventory.json`. The inventory contains exact package versions and source commit links where supplied by the package. It does not expose local cache paths.

The inventory also retains package-declared license/project URLs and copyright, the restore graph's package SHA-512, and computed SHA-256/size for each resolved Windows runtime/native asset. The graph's package hash is recorded, not a fresh verification of the nupkg archive. Asset-level hashing does not cover the SDK runtime packs; their entries explicitly say so. Hashes identify files for review, not license permission or executable trust.

Package-declared `license type="file"` paths are also collected, including nested files, after checking that the path stays inside the package directory. Missing declared files fail collection. `NoticeEvidence` records SHA-256 and byte size of each collected notice so the packaged texts can be checked independently; it does not establish that the upstream notice is legally sufficient.

Mono.Posix.NETStandard 1.0.0 contributes three Windows x64 files in the current restore target: its managed DLL, `MonoPosixHelper.dll` and `libMonoPosixHelper.dll`. The package declares [license information](https://go.microsoft.com/fwlink/?linkid=869050) and [project source](https://go.microsoft.com/fwlink/?linkid=869051), but does not pin a source commit. Do not replace this evidence with the license of the newer standalone mono.posix project or infer that a Unix-oriented dependency is absent from the Windows distribution.

Unmodified MPL-covered components are available in source form at the commit links recorded in `inventory.json`; MPL 2.0 is included for each such component. Package-specific source notices still require review. See the [Mozilla executable-distribution guidance](https://www.mozilla.org/en-US/MPL/2.0/FAQ/).

`-RequireComplete` rejects missing license texts. This check is technical evidence, not legal approval.

Cập nhật 12/10/2026: đã kèm bundle Mono.Posix riêng và hồ sơ nguồn lịch sử trong
`licenses/mono.posix.netstandard-1.0.0/`. Bundle bao gồm attribution/điều khoản
managed và native từ cấu hình upstream 1.0.0 (Mono 5.8.0.127, PR-7024, helper
build 46). Collector kiểm tra hash package và ba asset Windows x64 trước khi
dùng bundle. Chưa chứng minh native binary tương đương source/build Jenkins;
không gán commit suy luận vào metadata NuGet. Đây là giới hạn provenance, không
phải văn bản giấy phép bị thiếu. Các cổng ký mã, license Nepply và phê duyệt phát
hành vẫn độc lập; việc đủ notice không tự cho phép phát hành công khai.
