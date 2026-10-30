# Kế hoạch phát hành Nepply 2.0.0

Cập nhật 30/10/2026. Đây là kế hoạch duy nhất từ hiện tại đến sau bản chính thức. Làm từng giai đoạn theo thứ tự; giai đoạn sau chỉ bắt đầu khi giai đoạn trước đạt điều kiện **Xong khi**.

Đánh dấu một mục khi đã hoàn tất và kiểm chứng thật, không đánh dấu theo dự đoán. **[Bạn]** là việc chỉ chủ dự án làm được (tài khoản, duyệt, máy thật); **[Claude]** là việc code hoặc tài liệu trong repo; **[Cả hai]** cần phối hợp.

| Giai đoạn | Mục tiêu | Trạng thái |
| --- | --- | --- |
| 0 | Nền tảng đã có | Xong |
| 1 | Sửa các lỗi nhỏ từ đợt rà soát | Chưa làm |
| 2 | Đưa code lên GitHub | Chờ bạn duyệt diff |
| 3 | Chuẩn bị repo công khai | Chưa làm |
| 4 | Phát hành `2.0.0-rc.1` chưa ký | Chưa làm |
| 5 | Xin chữ ký số | Chưa làm |
| 6 | Tích hợp ký và phát hành RC đã ký | Chưa làm |
| 7 | Hoàn thiện chất lượng | Chưa làm |
| 8 | Nghiệm thu trên máy thật | Chưa làm |
| 9 | Phát hành `2.0.0` | Chưa làm |
| 10 | Trả nợ kỹ thuật sau 2.0.0 | Chưa làm |

## Quyết định đã chốt

- Bản chính là `2.0.0`. Trước đó phát hành `2.0.0-rc.1` chưa ký để đủ điều kiện SignPath, vì dự án phải **đã phát hành** ở dạng sẽ được ký. Bản ổn định luôn phải có chữ ký.
- Phương án không chọn: phát hành `2.0.0` chưa ký rồi ký từ `2.0.1`, vì bản ổn định chưa ký sẽ bị Smart App Control chặn.
- Tag cũ `v1.0.0-beta.1` đã xóa ngày 30/10/2026. Chỉ tạo tag mới lúc phát hành.
- `FileVersion`: RC thứ N dùng `2.0.0.N`, bản ổn định dùng `2.0.0.100`. Luôn đổi đồng thời trong `Nepply.csproj` và `Nepply.DesktopGuardian.csproj`.
- Bản dịch tiếng Việt của bộ cài do repo tự giữ, dịch từ `Default.isl` của Inno Setup 7.1.0.

## Giai đoạn 0. Nền tảng đã có

- [x] `.github/SECURITY.md`, `CODE_OF_CONDUCT.md`, issue templates, chính sách ký mã tiếng Anh, Dependabot cho NuGet.
- [x] Workflow `release.yml`: tag `vX.Y.Z` → build trên GitHub runner → Release **nháp** chưa ký. Chưa xác nhận từng chạy thành công.
- [x] Luồng ký uninstaller (`-SignedUninstallerDirectory`), kiểm chữ ký cập nhật theo nhà phát hành + CA (`Get-SignerPolicy.ps1`).
- [x] Cửa sổ cập nhật có nút mở trang tải; ngôn ngữ đầu tiên theo Windows; bộ cài 8 ngôn ngữ; nút Báo lỗi; ghi chú phát hành dạng văn bản (nhánh `fix/release-readiness`, `chore/pro-polish`, chưa push).
- [x] `.gitattributes`, `.editorconfig`, kiểm format trong CI, CodeQL.

## Giai đoạn 1. Sửa các lỗi nhỏ từ đợt rà soát

Kết quả rà 30/10/2026: build từ clone sạch đạt; cấy 20 lỗi thử thì 17 bị bắt, 3 lọt, 1 trong số bị bắt làm test treo.

- [ ] **[Claude]** Màn chào: giữ ngôn ngữ đang xem trước khi Windows đổi cài đặt giao diện. Hiện `PreferenceChanged` áp lại ngôn ngữ đã lưu.
- [ ] **[Claude]** Thêm timeout cho từng nhóm test, báo đúng tên nhóm bị treo. Hiện CI chờ hết 20 phút mà không biết test nào hỏng.
- [ ] **[Claude]** Sửa test giới hạn kích thước file cài đặt: dùng JSON hợp lệ quá 2 MiB, vì test hiện tại đạt nhờ lỗi parse.
- [ ] **[Claude]** Thêm test `IsAllowedPath` từ chối thư mục khác `HoverBox`.
- [ ] **[Claude]** Tách phần tải bản cập nhật để test tự động được checksum, kích thước và redirect mà không cần chữ ký thật.
- [ ] **[Claude]** Bật `ContinuousIntegrationBuild` trên CI để build tái lập được ở mọi thư mục.
- [ ] **[Claude]** Chuyển các test logic thuần đang nằm sau điểm thoát `--core-only` về đúng bộ core.

**Xong khi:** các lỗi cấy thử đã lọt đều bị test bắt; toàn bộ test và gate đạt.

## Giai đoạn 2. Đưa code lên GitHub

- [ ] **[Bạn]** Xem diff hai nhánh; bỏ qua commit format `79c57b8` vì chỉ đổi khoảng trắng.
- [ ] **[Claude]** Push `fix/release-readiness` và `chore/pro-polish`, mở PR.
- [ ] **[Cả hai]** Merge theo thứ tự: `fix/release-readiness`, rồi `chore/pro-polish`. Theo dõi CI lần đầu chạy kiểm format.
- [ ] **[Bạn]** Xóa Release nháp của beta.1 trên trang Releases nếu còn. Nếu bản đó đã publish thì dừng lại, đừng xóa.
- [ ] **[Bạn]** Chạy thử bộ cài trên máy của bạn: trang thông báo và chữ tiếng Việt hiển thị đúng, cài và gỡ được.

**Xong khi:** `main` có đủ thay đổi và CI xanh.

## Giai đoạn 3. Chuẩn bị repo công khai

Làm trước khi chuyển Public, vì sau đó nội dung có thể đã bị lưu lại ở nơi khác.

### Quyết định về lịch sử Git

- [ ] **[Bạn]** Quyết định về link liên hệ cá nhân cũ: link không còn trong code hiện tại nhưng vẫn nằm trong lịch sử, ở 19 commit (các file `SECURITY.md`, `PRIVACY.md`, `Nepply.iss`, `SettingsWindow.xaml.cs`, `BETA_NOTICE.txt`, `README.md`). Giữ nguyên thì link công khai cùng repo. Muốn xóa phải viết lại lịch sử `main`, nên làm khi chưa có fork hay PR nào.
- [ ] **[Claude]** Rà lại toàn bộ lịch sử ngay trước khi Public: lần rà cũ chỉ có 25 commit, nay repo có hơn 70 commit trên mọi nhánh. Không tìm thấy khóa hay token ở lần quét 26/10/2026, nhưng phải quét lại bản cuối.

### Cài đặt GitHub

- [ ] **[Bạn] MFA tài khoản**: Settings (tài khoản) → Password and authentication → bật Two-factor authentication. Lưu recovery codes ngoài máy.
- [ ] **[Bạn] Bảo mật repo**: repo → Settings → *Code security* (có thể tên *Advanced Security*): bật Dependency graph, Dependabot alerts, Dependabot security updates, **Private vulnerability reporting**, Secret scanning và Push protection.
- [ ] **[Bạn] Actions**: Settings → Actions → General:
  - Workflow permissions: *Read repository contents and packages permissions*.
  - Bỏ chọn *Allow GitHub Actions to create and approve pull requests*.
  - Fork pull request workflows: *Require approval for all external contributors*.
- [ ] **[Bạn] Bảo vệ nhánh `main`**: Settings → Rules → Rulesets → New branch ruleset, target `main`: chặn force push và xóa nhánh, yêu cầu status check `build` (workflow *Verify Windows build*). Có thể bật *Require a pull request* với 0 approval nếu muốn mọi thay đổi đi qua PR.
- [ ] **[Bạn] Bảo vệ tag**: thêm tag ruleset cho `v*`, chỉ bạn được tạo/xóa.
- [ ] **[Bạn] Môi trường release**: Settings → Environments → New environment `release` → *Required reviewers*: chính bạn; *Deployment branches and tags*: chỉ tag `v*`.
- [ ] **[Bạn]** Duyệt hoặc đóng các PR Dependabot đang mở (CI phải xanh trước khi merge).
- [ ] **[Bạn] Chuyển Public**: Settings → General → Danger Zone → Change repository visibility → Public.
- [ ] **[Bạn]** Sau khi Public, mở thử `…/security/advisories/new` và trang Issues. Xem workflow CodeQL đã chạy (trước đó nó tự bỏ qua).
- [ ] **[Bạn]** Thêm status check của CodeQL vào ruleset nếu muốn bắt buộc.

**Xong khi:** repo Public, báo lỗ hổng riêng hoạt động, CI và CodeQL xanh.

## Giai đoạn 4. Phát hành `2.0.0-rc.1` chưa ký

- [ ] **[Claude]** Viết ghi chú phát hành RC song ngữ, ghi rõ bản chưa ký và cách xử lý cảnh báo Windows.
- [ ] **[Bạn]** Chạy thử: Actions → *Release build* → Run workflow (chỉ build, không tạo Release). Đây là lần đầu workflow chạy trên GitHub; kiểm tra artifact `nepply-release-unsigned`.
- [ ] **[Cả hai]** Tạo tag `v2.0.0-rc.1` từ `main` và push. Bạn duyệt bước `release` khi GitHub hỏi.
- [ ] **[Bạn]** Mở Release nháp, đánh dấu *pre-release*, dán ghi chú, bấm Publish.
- [ ] **[Bạn]** Tải file từ trang Release về một máy và cài thử.
- [ ] **[Cả hai]** Nhận phản hồi trong lúc chờ SignPath, nên từ 2 đến 4 tuần. Lỗi nặng được sửa ở `2.0.0-rc.2`, tăng `FileVersion` thành `2.0.0.2`.

**Xong khi:** Release công khai tải được, cài được, không có lỗi chặn.

## Giai đoạn 5. Xin chữ ký số

- [ ] **[Bạn]** Kiểm tra trước khi nộp: repo Public; giấy phép MIT và dependency đều là giấy phép OSI (xem `docs/legal/THIRD_PARTY_NOTICES.md`); README có link *Code signing policy*; trang chính sách ghi vai trò (author/reviewer/approver = bạn) và link privacy policy; MFA đã bật; đã có Release công khai; có uninstaller; app chỉ ẩn icon Desktop khi người dùng tự bật và có hướng dẫn khôi phục.
- [ ] **[Bạn]** Nộp đơn tại <https://signpath.org> (mục apply cho dự án OSS), dùng link repo, Release và `docs/release/CODE_SIGNING_POLICY.en.md`.
- [ ] **[Bạn]** Khi được duyệt, SignPath cung cấp organization ID, project/policy slug và API token. Lưu token vào Settings → Environments → `release` → *Environment secrets* (không để ở repository secret chung).
- [ ] **[Bạn]** Nếu bị từ chối: dùng chứng thư OV cho người làm OSS (ví dụ Certum Open Source Code Signing). Azure Artifact Signing chỉ cấp cho cá nhân ở Mỹ/Canada.

**Xong khi:** có chứng thư hoặc dịch vụ ký dùng được từ workflow.

## Giai đoạn 6. Tích hợp ký và phát hành RC đã ký

Bản chưa ký không thể tự cập nhật, nên phải có một RC đã ký để kiểm thật đường cập nhật lên `2.0.0`.

- [ ] **[Claude]** Thêm bước gửi ký vào `release.yml` (action SignPath pin theo SHA): ký `Nepply.exe` + `Nepply.DesktopGuardian.exe`, dựng lại Setup với `-SignedUninstallerDirectory`, ký Setup, tạo checksum sau ký.
- [ ] **[Claude]** Ký một bản thử rồi chạy `pwsh scripts/Get-SignerPolicy.ps1 -File <Setup-đã-ký>`, đưa kết quả vào `UpdatePolicy.Signers` để bật tự cập nhật.
- [ ] **[Claude]** Mở public mode trong `scripts/Build-Distribution.ps1` (hiện từ chối khi thiếu `-InternalOnly`).
- [ ] **[Claude]** Bổ sung attribution SignPath đúng yêu cầu vào README và chính sách ký.
- [ ] **[Cả hai]** Phát hành `2.0.0-rc.2` (hoặc RC kế tiếp) đã ký. Chạy `Verify-Release.ps1 -RequireSigned -AllowedSignerThumbprints <thumbprint> -Uninstaller <unins000.exe từ lượt cài thật>`.

**Xong khi:** một bản RC đã ký được phát hành và qua `Verify-Release`.

## Giai đoạn 7. Hoàn thiện chất lượng

Không chặn việc xin chữ ký, có thể làm song song từ giai đoạn 4.

- [ ] **[Claude]** Mở mục Desktop không chặn luồng giao diện, để shortcut trỏ tới ổ mạng mất kết nối không làm panel đơ.
- [ ] **[Cả hai]** Chạy bài đo dài 24 giờ (`scripts/Run-UiSoak.ps1`), phân tích CPU, RAM, handle.
- [ ] **[Claude]** Rà trợ năng: điều hướng bằng bàn phím, trình đọc màn hình, High Contrast.
- [ ] **[Bạn]** Nhờ người bản ngữ duyệt bản dịch Nhật, Đức, Trung, Nga, Pháp, Thái, cả app lẫn bộ cài.
- [ ] **[Bạn]** Nhờ người đọc lại bản dịch tiếng Việt của bộ cài.
- [ ] **[Claude]** Tùy chọn tự kiểm tra cập nhật định kỳ, mặc định tắt, cập nhật chính sách quyền riêng tư.
- [ ] **[Claude]** Chứng thực nguồn gốc build (`actions/attest-build-provenance`) để người dùng kiểm file đúng từ source.
- [ ] **[Cả hai]** Xử lý các lỗi nhận được từ bản RC.

**Xong khi:** không còn lỗi nặng đang mở; bài đo 24 giờ không có dấu hiệu rò rỉ.

## Giai đoạn 8. Nghiệm thu trên máy thật

Làm bằng đúng file đã ký. Dùng VM Windows 11 cài mới (Hyper-V hoặc VirtualBox), **bật Smart App Control** ngay khi cài Windows (chỉ bật được trên máy mới cài). Ghi hash Setup, ảnh chụp và kết quả từng mục:

- [ ] **[Bạn]** Tải từ GitHub Release qua trình duyệt (để có Mark of the Web), chạy Setup: SmartScreen/SAC hiện gì?
- [ ] **[Bạn]** Lần đầu chạy: chọn ẩn icon Desktop → thoát qua khay → icon trở lại.
- [ ] **[Bạn]** Kill tiến trình `Nepply.exe` trong Task Manager khi icon đang ẩn → guardian khôi phục icon.
- [ ] **[Bạn]** Gỡ cài đặt khi icon đang ẩn → icon trở lại, không còn file trong `%LOCALAPPDATA%\Programs\Nepply`.
- [ ] **[Bạn]** Cài bản cũ → cài bản mới đè lên (nâng cấp) → giữ Settings, tự khởi động.
- [ ] **[Bạn]** Cập nhật trong app từ bản A lên bản B (cả hai đã ký): tải đúng, từ chối sai chữ ký, tự mở lại.
- [ ] **[Bạn]** AppLocker với rule mặc định (Windows Pro/Enterprise) và tài khoản người dùng thường.
- [ ] **[Bạn]** Windows tiếng Anh và tiếng Việt: ngôn ngữ đầu tiên và bộ cài đúng.
- [ ] **[Bạn]** Windows 10 22H2.
- [ ] **[Bạn]** Máy thật khác: Intel, nhiều màn hình với DPI khác nhau, laptop chạy pin.
- [ ] **[Bạn]** Nếu Defender báo nhầm: gửi file tại <https://www.microsoft.com/wdsi/filesubmission> (chọn *Software developer*).

**Xong khi:** mọi mục đạt hoặc có ghi nhận giới hạn rõ ràng.

## Giai đoạn 9. Phát hành `2.0.0`

- [ ] **[Claude]** Đổi `Version`/`InformationalVersion` thành `2.0.0` và `FileVersion` thành `2.0.0.100` trong `Nepply.csproj` và `Nepply.DesktopGuardian.csproj`.
- [ ] **[Claude]** Bỏ trang thông báo `RELEASE_NOTICE.txt` khỏi bộ cài (`InfoBeforeFile`, `[Files]`, hai script đóng gói) và banner RC trong README, hướng dẫn, ghi chú phát hành.
- [ ] **[Claude]** Tạo lại ảnh README theo `docs/images/README.md` (ảnh hiện tại lấy từ `ff63ba9`, trước các thay đổi giao diện gần đây).
- [ ] **[Claude]** Hoàn thiện `RELEASE_2.0.0.md` và CHANGELOG; cập nhật mục giới hạn về tự cài cập nhật.
- [ ] **[Cả hai]** Tạo tag `v2.0.0`, phát hành Release chính thức (không đánh dấu pre-release). Chạy `Verify-Release.ps1 -RequireSigned -RequireStable`.
- [ ] **[Bạn]** Kiểm tra người dùng RC đã ký nhận được bản 2.0.0 qua cửa sổ cập nhật.

**Xong khi:** `2.0.0` đã ký có trên trang Releases và cập nhật từ RC hoạt động.

## Giai đoạn 10. Trả nợ kỹ thuật sau 2.0.0

Làm dần, mỗi mục một PR riêng. Không gộp với thay đổi tính năng, vì các mục này đụng nhiều file.

### Test

- [ ] **[Claude]** Chuyển test sang xUnit hoặc MSTest theo từng nhóm, để chạy được bằng `dotnet test` và báo giá trị thực tế so với mong đợi.
- [ ] **[Claude]** Tách `tests/Nepply.Tests/Program.cs` (hơn 1500 dòng) thành các file theo tính năng.
- [ ] **[Claude]** Thay các test phụ thuộc thời gian bằng đồng hồ giả hoặc chờ theo điều kiện.

### Cấu trúc

- [ ] **[Claude]** Sửa namespace cho khớp thư mục (`Nepply.Features.*`, `Nepply.Platform.*`), một PR cơ học riêng.
- [ ] **[Claude]** Tách `MainWindow` (876 dòng): vị trí cửa sổ, kích hoạt từ cạnh, xem trước Settings, bố cục.
- [ ] **[Claude]** Tách `SettingsWindow` (745 dòng) theo từng nhóm cài đặt.
- [ ] **[Claude]** Tách `AppSettings` thành dữ liệu thuần; bỏ phụ thuộc vào giao diện và kho lưu file.
- [ ] **[Claude]** Bỏ các lời gọi ngược lên `MainWindow` từ `IconPanel`, `MonitorPanel`, `TrayMenu`, `WelcomeWindow`.
- [ ] **[Claude]** Bỏ các constructor chỉ dành cho test trong `DesktopIconService`, dùng một interface scanner.
- [ ] **[Claude]** Tách các dòng dồn nhiều lệnh và bỏ biến `_settings` cục bộ che field trong `MainWindow`.

### Đơn giản hóa và dung lượng

- [ ] **[Cả hai]** Xem lại Guardian: exe 70 MB và khoảng 770 dòng cho việc khôi phục icon. Thử bản biên dịch native để giảm dung lượng.
- [ ] **[Cả hai]** Giảm dung lượng app: `Nepply.exe` 194 MB, Setup 78 MB. Thử nén single-file, đo ảnh hưởng tới thời gian khởi động.
- [ ] **[Bạn]** Quyết định về Microsoft Store: làm tiếp hoặc bỏ code và script MSIX.
- [ ] **[Claude]** Gom bớt script kiểm tra trùng lặp, thêm số phiên bản lấy tự động từ tag.

### Phân phối và vận hành

- [ ] **[Cả hai]** Đưa Nepply lên winget sau khi có bản ký.
- [ ] **[Claude]** Khi Inno Setup trong `release.yml` đổi phiên bản, đối chiếu `packaging/inno/Languages/Vietnamese.isl` với `Default.isl` mới.
- [ ] **[Claude]** Bỏ dần tên legacy `HoverBox` (mutex `Local\HoverBox-SetupGuard`, thư mục `%LOCALAPPDATA%\HoverBox`) sau khi không còn người dùng bản cũ; giữ ít nhất một bản phát hành có migration.
- [ ] **[Bạn]** Ký mọi bản bằng cùng chứng thư: uy tín SmartScreen tăng dần theo lượt tải của cùng chứng thư.
- [ ] **[Bạn]** Nếu nhận donate: kiểm lại điều kiện giấy phép thương mại của Inno Setup.
