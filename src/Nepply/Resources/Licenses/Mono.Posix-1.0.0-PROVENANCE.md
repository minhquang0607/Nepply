# Mono.Posix.NETStandard 1.0.0 — provenance review

## Cập nhật ngày 12/10/2026 — trạng thái hiện tại

Đã bổ sung `Mono.Posix-1.0.0-NOTICES.md`, gồm MIT và attribution managed/eglib,
zlib 1.2.5, minizip 1.01e, Info-ZIP, libxml2 2.6.26, MinGW-w64 5.0.3 và
libgcc 6.4.0 kèm GCC Runtime Library Exception 3.1. Các điều khoản giữ
nguyên tiếng Anh từ upstream. Phần lịch sử bên dưới được giữ lại; các câu
“missing license text” trong lịch sử không còn mô tả trạng thái hiện tại.

Đã đọc danh sách `Mono.Posix.dll.sources`, header các file managed được liệt kê,
`Locale.cs`, `Consts.cs.in`, patch PR-7024 (chỉ thêm `[In,Out]`), danh sách source
trong `msvc/monoposixhelper.vcxproj`, các targets eglib common/win32/posix và
header của các file native được liệt kê. Native project link eglib và compile
zlib/minizip; `unzip.c` dẫn tới Info-ZIP, `gunicode.c` ghi nguồn libxml2 2.6.26.
Điều khoản Info-ZIP lấy từ phần Infozip BSD của LICENSE Mono tại commit đã ghi;
libxml2 lấy từ `GNOME/libxml2`, tag `LIBXML2_2_6_26`, file `Copyright`.

Đã đọc chuỗi ASCII/debug trực tiếp trong hai native DLL, không chạy DLL:
`libMonoPosixHelper.dll` có đường dẫn source runtime MinGW-w64 5.0.3-1 và
GCC 6.4.0/libgcc2.c/cygwin.S; đối chiếu header các file CRT được nhận diện
và hai source libgcc xác nhận GPL với Runtime Library Exception 3.1.
`math/sqrt.c` có ZPL, `stdio/strtok_r.c` có BSD (Softweyr/Regents), các CRT
khác chủ yếu có public-domain disclaimer. Đã kèm các notice này riêng,
không dùng chỉ giấy phép MIT của Mono để bao phủ toolchain runtime.
Chuỗi trong binary hỗ trợ nhận diện thành phần, không chứng minh build tái lập.

Collector chỉ áp dụng bundle này khi hash package, đúng ba asset Windows x64
và SHA256 từng asset khớp bằng chứng bên dưới; thay đổi sẽ buộc rà notice lại.
`MissingLicenseText=false` có nghĩa đã kèm văn bản và attribution được rà,
không phải chứng nhận pháp lý hoặc chứng minh build tái lập.

**Giới hạn còn lại:** chưa lấy được artifact Jenkins build 46 để đối chiếu byte;
chưa chứng minh binary-to-source. Không khai báo `RepositoryCommit` hoặc
`HasPinnedSource` dựa trên suy luận lịch sử. Không sửa dependency hay binary.

## Hồ sơ lịch sử (giữ nguyên để truy vết)

Reviewed 2026-09-26. This is evidence for further notice review, **not a license grant, complete attribution bundle, or a binary-to-source attestation**. No package, monitor code, or native binary was changed.

## Restored package

The local `obj/project.assets.json` records `Mono.Posix.NETStandard/1.0.0` with NuGet SHA512:

`vSN/L1uaVwKsiLa95bYu2SGkF0iY3xMblTfxc8alSziPuVfJpj3geVqHGAA75J7cZkMuKpFVikz82Lo6y6LLdA==`

This is the restore graph's package hash, not a separately recomputed download hash. Its cached nuspec declares Microsoft copyright and these URLs. Direct HTTPS retrieval succeeded after the web retrieval tool failed:

- [Declared license URL](https://go.microsoft.com/fwlink/?linkid=869050) redirects to [mono/mono LICENSE on main](https://github.com/mono/mono/blob/main/LICENSE).
- [Declared project URL](https://go.microsoft.com/fwlink/?linkid=869051) redirects to [mcs/class/Mono.Posix on main](https://github.com/mono/mono/tree/main/mcs/class/Mono.Posix).

Moving `main` branches cannot alone identify the source used for this historical package. The Mono root license contains multiple license texts and component-specific scope; copying its entire contents does not prove all of them apply to this payload.

## Historical packaging evidence

The upstream XamarinComponents commit [8589a9e88aa9a256be1660caa95038ab6c94cce4](https://github.com/xamarin/XamarinComponents/commit/8589a9e88aa9a256be1660caa95038ab6c94cce4), dated 2018-02-28, removes the beta suffix for 1.0.0. Its [Makefile](https://github.com/xamarin/XamarinComponents/blob/8589a9e88aa9a256be1660caa95038ab6c94cce4/XPlat/Mono.Posix/Makefile) specifies:

- NuGet version `1.0.0`.
- Mono version `5.8.0.127`.
- Native helper Jenkins build `46` (prebuilt artifacts, not built from managed source by this Makefile).
- A local `PR-7024.patch` applied to `Mono.Unix.Native/Syscall.cs`.

The official annotated Mono tag `mono-5.8.0.127` resolves through tag object `ed3880c66935087b9d0e9e5228c809bc381149d6` to commit [1eac626536b401a5eb5352db1af8c35b4b825996](https://github.com/mono/mono/tree/1eac626536b401a5eb5352db1af8c35b4b825996). These are upstream packaging/source references, **not a repository commit declared by the NuGet package itself**. Do not populate inventory `RepositoryCommit` / `HasPinnedSource` from this inference.

The newer [build.cake](https://github.com/xamarin/XamarinComponents/blob/5db2091df75abc988bb097d6298950efcab246cc/XPlat/Mono.Posix/build.cake) describes `5.20.1-preview`; it is not evidence for the installed 1.0.0 binary.

## Outstanding verification

1. Inspect the license/attribution scope of the historical managed source and patch, and the native helper sources/build 46; retain the required copyright notices.
2. Establish how the NuGet managed/native payload corresponds to the historical build. A matching package version in a Makefile is not reproducible-build proof.
3. Produce the redistribution notice bundle with explicit provenance, then review obligations before enabling `-RequireComplete` for public release.

Until those checks are resolved, the collector continues to report missing license text. Do not rename this review document to LICENSE or count it as a replacement license text.

## Additional source inspection

The historical commit's `mcs/class/Mono.Posix/Mono.Unix.Native/Syscall.cs` was retrieved directly and has a permissive permission header plus Novell/Jonathan Pryor attribution. `support/dirent.c` carries Jonathan Pryor attribution; `support/map.c` identifies itself as generated from Mono.Posix.dll. The root LICENSE describes different scopes for runtime, class libraries, build-time and documentation code. These samples narrow the investigation but do not establish the full managed/native package contents or Jenkins native build 46. The unresolved inventory flag remains unchanged; no monitor dependency was replaced.

## Xác minh tính toàn vẹn ngày 02/10/2026

Lệnh `dotnet nuget verify <mono.posix.netstandard.1.0.0.nupkg> --all` hoàn tất với exit 0. Chữ ký loại Repository, chủ thể `NuGet.org Repository by Microsoft`. Content hash công cụ báo trùng hash `vSN/L1...LLdA==` đầy đủ ở trên và trong restore graph.

SHA512 của toàn bộ archive là `RtGiutQZJAmajvQ0QvBvh73VJye85iW9f9tjZlzF88idLxNMo4lAktP/4Y9ilCpais0LDO0tpoICt9Hdv6wooA==`, trùng file `.nupkg.sha512`. Giá trị này khác content hash vì phạm vi hash có chữ ký khác nhau; không dùng việc khác nhau đó làm bằng chứng package bị sửa. Tham khảo [thiết kế metadata NuGet](https://github.com/NuGet/Home/wiki/Nupkg-Metadata-File).

Đã đọc các entry ZIP (không thực thi DLL), băm SHA256 và đối chiếu các file runtime đã giải nén:

| File Windows x64 | SHA256 | Khớp archive đã xác minh |
|---|---|---|
| Mono.Posix.NETStandard.dll | BE0215D90462ADBAE5F03D6E5C3A969C9842A866B013DF7A395769C5C352FA7E | Có |
| MonoPosixHelper.dll | A57141B26D6EA3326B165BDCB15CA2BCD34737370F52358E12FFC8DF41B92C62 | Có |
| libMonoPosixHelper.dll | B16F77C155A36559A44DA32E17E0BF63D899DB0BB48C6590BC8EBA7BB0FF87A8 | Có |

Đã thử đọc hai native artifact build 46 theo URL Makefile upstream. Host `xamjenkinsartifact.blob.core.windows.net` không phân giải được trong lượt này; không lấy được byte để đối chiếu. Chưa chứng minh native DLL tương ứng chính xác source/build lịch sử. Chữ ký repository chứng minh kết quả kiểm tra tính toàn vẹn package, **không** thay thế nghĩa vụ giấy phép hoặc xác nhận source-to-binary. Gate notice vẫn chưa đạt.
