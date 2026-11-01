# Kế hoạch phát hành Nepply 2.0.0

Cập nhật 02/11/2026. Đây là kế hoạch duy nhất từ hiện tại đến sau bản chính thức. Làm từng giai đoạn theo thứ tự; giai đoạn sau chỉ bắt đầu khi giai đoạn trước đạt điều kiện **Xong khi**.

Đánh dấu một mục khi đã hoàn tất và kiểm chứng thật, không đánh dấu theo dự đoán. **[Bạn]** là việc chỉ Minh Quang làm được (tài khoản, duyệt, máy thật); **[AI]** là việc code hoặc tài liệu trong repo do đội ngũ AI (Codex, Claude) làm; **[Cả hai]** cần phối hợp.

| Giai đoạn | Mục tiêu | Trạng thái |
| --- | --- | --- |
| 0 | Nền tảng đã có | Xong |
| 1 | Sửa các lỗi nhỏ từ đợt rà soát | Xong |
| 2 | Đưa code lên GitHub | Đã merge vào `main`, CI xanh; còn các mục chưa đánh dấu bên dưới |
| 3 | Chuẩn bị repo công khai | Đang làm; lịch sử đã sạch, repo mới đã tạo, còn các mục cài đặt của bạn |
| 4 | Phát hành `2.0.0-rc.1` chưa ký | Ghi chú và chạy thử đã xong; chờ repo Public |
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
- [x] Cửa sổ cập nhật có nút mở trang tải; ngôn ngữ đầu tiên theo Windows; bộ cài 8 ngôn ngữ; nút Báo lỗi; ghi chú phát hành dạng văn bản.
- [x] `.gitattributes`, `.editorconfig`, kiểm format trong CI, CodeQL.

## Giai đoạn 1. Sửa các lỗi nhỏ từ đợt rà soát

Kết quả rà 30/10/2026: build từ clone sạch đạt; cấy 20 lỗi thử thì 17 bị bắt, 3 lọt, 1 trong số bị bắt làm test treo. Sau khi sửa: cả 3 lỗi đã lọt đều bị bắt, thêm 5 lỗi cấy mới ở phần tải cập nhật và màn chào cũng bị bắt, test treo hoặc crash đều báo đúng tên.

- [x] **[AI]** Màn chào: giữ ngôn ngữ đang xem trước khi Windows đổi cài đặt giao diện. Trước đây `PreferenceChanged` áp lại ngôn ngữ đã lưu.
- [x] **[AI]** Thêm timeout cho từng nhóm test, báo đúng vị trí bị treo. Trước đây CI chờ hết 20 phút mà không biết test nào hỏng.
- [x] **[AI]** Sửa test giới hạn kích thước file cài đặt: dùng JSON hợp lệ quá 2 MiB, vì test cũ đạt nhờ lỗi parse.
- [x] **[AI]** Thêm test `IsAllowedPath` từ chối thư mục khác `HoverBox`.
- [x] **[AI]** Tách phần tải bản cập nhật để test tự động được checksum, kích thước và redirect mà không cần chữ ký thật.
- [x] **[AI]** Bật `ContinuousIntegrationBuild` trên CI để build tái lập được ở mọi thư mục.
- [x] **[AI]** Chuyển các test logic thuần về bộ core. Một số test trước đây CI không chạy ở bộ nào.

**Xong khi:** các lỗi cấy thử đã lọt đều bị test bắt; toàn bộ test và gate đạt.

## Giai đoạn 2. Đưa code lên GitHub

- [ ] **[Bạn]** Xem diff hai nhánh; bỏ qua commit format `f80d742` vì chỉ đổi khoảng trắng.
- [x] **[AI]** Push `fix/release-readiness` và `chore/pro-polish`, mở PR #8 và #9. Lần chạy đầu cho thấy CI đỏ vì hết dung lượng artifact, không phải vì lỗi code; đã sửa workflow để upload không chặn các gate. CI của cả hai PR xanh.
- [x] **[Cả hai]** Merge #8 vào `main`, đổi base của #9 sang `main`, rồi merge #9. `main` ở `e1d92e8` chứa đủ commit của cả hai PR; Verify chạy tay trên commit này xanh. GitHub không xử lý sự kiện push của lần merge #9 nên #9 kẹt ở Open và đã được đóng tay ngày 31/10/2026. Dòng "Closed with unmerged commits" trên trang #9 là dữ liệu cũ.
- [x] **[AI]** Xác nhận push lên `main` tự chạy Verify trở lại: merge #10 (`a0b0525`) tự chạy Verify #54, xanh.
- [x] **[Cả hai]** Release nháp "Nepply 1.0.0" và các nhánh cũ không còn ở repo `Nepply`; chúng nằm lại trong kho lưu trữ Private `Nepply-archive` từ ngày 02/11/2026.
- [ ] **[Bạn]** Chạy thử bộ cài trên máy của bạn: trang thông báo và chữ tiếng Việt hiển thị đúng, cài và gỡ được.

**Xong khi:** `main` có đủ thay đổi và CI xanh.

## Giai đoạn 3. Chuẩn bị repo công khai

Làm trước khi chuyển Public, vì sau đó nội dung có thể đã bị lưu lại ở nơi khác.

### Quyết định về lịch sử Git

- [x] **[Cả hai]** Xóa link liên hệ cá nhân cũ khỏi lịch sử: ngày 02/11/2026 viết lại 91 commit, thay link bằng trang Issues rồi force push `main` và `fix/release-readiness`. Mọi mã commit đã đổi; quét toàn bộ object của hai nhánh này không còn link, đối chứng trên lịch sử cũ tìm thấy link. 10 commit từng mang tên `MQuang07` nay có tác giả Minh Quang.
- [x] **[Cả hai]** Xử lý bản cũ còn sót: `refs/pull/1` đến `refs/pull/12` vẫn giữ commit cũ có link và không tự xóa được, nên ngày 02/11/2026 repo cũ được đổi tên thành `Nepply-archive` (vẫn Private, còn đủ PR, lịch sử Actions và Release nháp), rồi tạo repo `Nepply` mới và chỉ đẩy `main`. Repo mới có đúng một ref; commit cũ `46cd02f` trả về 404.
- [ ] **[Bạn]** Luôn giữ `Nepply-archive` ở Private vì nó vẫn chứa link. Đừng push các nhánh local cũ lên repo mới. Chỉ xóa kho này khi chắc chắn không cần xem lại.
- [x] **[AI]** Rà toàn bộ lịch sử: quét 86 commit trên mọi nhánh ngày 01/11/2026, không thấy mẫu khóa hay token, đường dẫn máy cá nhân, email cá nhân hay file nhạy cảm từng được thêm. Đối chứng: cùng cách quét tìm đúng 19 commit chứa link liên hệ. Đây là quét theo mẫu, không chứng minh tuyệt đối. Quét lại nếu có thêm nhiều commit trước khi Public.

### Cài đặt GitHub

Repo `Nepply` tạo ngày 02/11/2026 không mang theo cài đặt của repo cũ. Các số PR, số lần chạy CI và *Release build* ghi trong kế hoạch trước ngày này thuộc `Nepply-archive`. Giá trị bên dưới được đối chiếu với repo cũ, nên làm đúng từng mục là đủ.

- [ ] **[Bạn] Tên hiển thị**: GitHub → Settings → Public profile → Name: `Minh Quang`. Commit merge trên web đang lấy tên `MQuang07` từ hồ sơ, nên chưa đổi thì commit merge mới vẫn mang tên đó.
- [ ] **[Bạn] MFA tài khoản**: Settings (tài khoản) → Password and authentication → bật Two-factor authentication. Lưu recovery codes ngoài máy.
- [ ] **[Bạn] Bảo mật repo**: repo → Settings → *Advanced Security*: bật Dependency graph, Dependabot alerts, Dependabot security updates và Grouped security updates.
- [ ] **[Bạn] Actions**: Settings → Actions → General:
  - Bật *Require actions to be pinned to a full-length commit SHA*. Repo mới đang tắt mục này.
  - Workflow permissions: *Read repository contents and packages permissions*. Repo mới đã đúng.
  - Bỏ chọn *Allow GitHub Actions to create and approve pull requests*. Repo mới đã đúng.
- [ ] **[Bạn] Bảo vệ nhánh `main`**: Settings → Rules → Rulesets → New ruleset → Import a ruleset, chọn [`rulesets/protect-main.json`](rulesets/protect-main.json): chặn force push và xóa nhánh, yêu cầu status check `build`, Repository admin được bỏ qua. Nếu import lỗi thì tạo tay theo đúng các giá trị đó.
- [ ] **[Bạn] Bảo vệ tag**: import [`rulesets/protect-release-tags.json`](rulesets/protect-release-tags.json) cho tag `v*`: chặn sửa, xóa và force push, Repository admin được bỏ qua.
- [ ] **[Bạn] Môi trường release**: Settings → Environments → New environment `release` → *Deployment branches and tags*: *Selected branches and tags*, thêm tag rule `v*`.
- [x] **[Bạn]** Duyệt hoặc đóng các PR Dependabot đang mở (CI phải xanh trước khi merge). Ngày 01/11/2026 không có PR nào đang mở.
- [ ] **[Bạn] Chuyển Public**: Settings → General → Danger Zone → Change repository visibility → Public.
- [ ] **[Bạn] Ngay sau khi Public**, làm các mục GitHub chỉ cho bật ở repo công khai:
  - Settings → *Advanced Security*: bật **Private vulnerability reporting**, Secret scanning và Push protection.
  - Settings → Actions → General → Fork pull request workflows: *Require approval for all external contributors*.
  - Settings → Environments → `release` → *Required reviewers*: chính bạn.
- [ ] **[Bạn]** Sau khi Public, mở thử `…/security/advisories/new` và trang Issues. Xem workflow CodeQL đã chạy (trước đó nó tự bỏ qua).
- [ ] **[Bạn]** Thêm status check của CodeQL vào ruleset nếu muốn bắt buộc.

**Xong khi:** repo Public, báo lỗ hổng riêng hoạt động, CI và CodeQL xanh.

## Giai đoạn 4. Phát hành `2.0.0-rc.1` chưa ký

- [x] **[AI]** Viết ghi chú phát hành RC song ngữ, ghi rõ bản chưa ký và cách xử lý cảnh báo Windows: [RELEASE_2.0.0-rc.1.md](RELEASE_2.0.0-rc.1.md).
- [x] **[Cả hai]** Kiểm tra dung lượng artifact: lần chạy thử ngày 01/11/2026 upload được 186 MB. `release.yml` cần upload bản build để tạo Release; nếu lần tag báo lỗi upload, xem Settings → Billing and licensing và xóa artifact cũ. Dung lượng được tính lại sau 6 đến 12 giờ.
- [x] **[Cả hai]** Chạy thử: *Release build* #3 trên `f030380` (trước khi viết lại lịch sử là `2241652`) xanh, gồm biên dịch bộ cài và artifact `nepply-release-unsigned`. Bước `draft-release` chỉ chạy khi có tag.
- [ ] **[Cả hai]** Tạo tag `v2.0.0-rc.1` từ `main` và push. Bạn duyệt bước `release` khi GitHub hỏi.
- [ ] **[Bạn]** Mở Release nháp, đánh dấu *pre-release*, dán ghi chú, bấm Publish.
- [ ] **[Bạn]** Tải file từ trang Release về một máy và cài thử.
- [ ] **[Cả hai]** Nhận phản hồi trong lúc chờ SignPath, nên từ 2 đến 4 tuần. Lỗi nặng được sửa ở `2.0.0-rc.2`, tăng `FileVersion` thành `2.0.0.2`.

**Xong khi:** Release công khai tải được, cài được, không có lỗi chặn.

## Giai đoạn 5. Xin chữ ký số

- [x] **[AI]** Đối chiếu repo với điều kiện SignPath ngày 02/11/2026 và soạn sẵn câu trả lời: [SIGNPATH_APPLICATION.md](SIGNPATH_APPLICATION.md). Giấy phép dependency đều là giấy phép OSI; ghi chú rc.1 đã có link *Code signing policy*.
- [ ] **[Bạn]** Kiểm tra trước khi nộp: repo Public, MFA đã bật, đã có Release công khai.
- [ ] **[Bạn]** Tự nộp đơn tại <https://signpath.org/apply> theo [SIGNPATH_APPLICATION.md](SIGNPATH_APPLICATION.md). Form tạo tài khoản đứng tên bạn và yêu cầu bạn cam kết với Code of Conduct, nên AI không nộp thay được. Bật MFA cho tài khoản SignPath sau khi được cấp.
- [ ] **[Bạn]** Khi được duyệt, SignPath cung cấp organization ID, project/policy slug và API token. Lưu token vào Settings → Environments → `release` → *Environment secrets* (không để ở repository secret chung).
- [ ] **[Bạn]** Nếu bị từ chối: dùng chứng thư OV cho người làm OSS (ví dụ Certum Open Source Code Signing). Azure Artifact Signing chỉ cấp cho cá nhân ở Mỹ/Canada.

**Xong khi:** có chứng thư hoặc dịch vụ ký dùng được từ workflow.

## Giai đoạn 6. Tích hợp ký và phát hành RC đã ký

Bản chưa ký không thể tự cập nhật, nên phải có một RC đã ký để kiểm thật đường cập nhật lên `2.0.0`.

- [ ] **[AI]** Thêm bước gửi ký vào `release.yml` (action SignPath pin theo SHA): ký `Nepply.exe` + `Nepply.DesktopGuardian.exe`, dựng lại Setup với `-SignedUninstallerDirectory`, ký Setup, tạo checksum sau ký.
- [ ] **[AI]** Ký một bản thử rồi chạy `pwsh scripts/Get-SignerPolicy.ps1 -File <Setup-đã-ký>`, đưa kết quả vào `UpdatePolicy.Signers` để bật tự cập nhật.
- [ ] **[AI]** Mở public mode trong `scripts/Build-Distribution.ps1` (hiện từ chối khi thiếu `-InternalOnly`).
- [ ] **[AI]** Bổ sung attribution SignPath đúng yêu cầu vào README và chính sách ký.
- [ ] **[Cả hai]** Phát hành `2.0.0-rc.2` (hoặc RC kế tiếp) đã ký. Chạy `Verify-Release.ps1 -RequireSigned -AllowedSignerThumbprints <thumbprint> -Uninstaller <unins000.exe từ lượt cài thật>`.

**Xong khi:** một bản RC đã ký được phát hành và qua `Verify-Release`.

## Giai đoạn 7. Hoàn thiện chất lượng

Không chặn việc xin chữ ký, có thể làm song song từ giai đoạn 4.

- [ ] **[AI]** Mở mục Desktop không chặn luồng giao diện, để shortcut trỏ tới ổ mạng mất kết nối không làm panel đơ.
- [ ] **[Cả hai]** Chạy bài đo dài 24 giờ (`scripts/Run-UiSoak.ps1`), phân tích CPU, RAM, handle.
- [ ] **[AI]** Rà trợ năng: điều hướng bằng bàn phím, trình đọc màn hình, High Contrast.
- [ ] **[Bạn]** Nhờ người bản ngữ duyệt bản dịch Nhật, Đức, Trung, Nga, Pháp, Thái, cả app lẫn bộ cài.
- [ ] **[Bạn]** Nhờ người đọc lại bản dịch tiếng Việt của bộ cài.
- [ ] **[AI]** Tùy chọn tự kiểm tra cập nhật định kỳ, mặc định tắt, cập nhật chính sách quyền riêng tư.
- [x] **[AI]** Chứng thực nguồn gốc build (`actions/attest-build-provenance`) trong bước `draft-release`, chỉ chạy khi repo Public. Chưa chạy thật lần nào; kiểm bằng `gh attestation verify` trên tệp của bản đầu tiên có tag.
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

- [ ] **[AI]** Đổi `Version`/`InformationalVersion` thành `2.0.0` và `FileVersion` thành `2.0.0.100` trong `Nepply.csproj` và `Nepply.DesktopGuardian.csproj`.
- [ ] **[AI]** Bỏ trang thông báo `RELEASE_NOTICE.txt` khỏi bộ cài (`InfoBeforeFile`, `[Files]`, hai script đóng gói) và banner RC trong README, hướng dẫn, ghi chú phát hành.
- [ ] **[AI]** Tạo lại ảnh README theo `docs/images/README.md` (ảnh hiện tại lấy từ `482575f`, trước các thay đổi giao diện gần đây).
- [ ] **[AI]** Hoàn thiện `RELEASE_2.0.0.md` và CHANGELOG; cập nhật mục giới hạn về tự cài cập nhật.
- [ ] **[Cả hai]** Tạo tag `v2.0.0`, phát hành Release chính thức (không đánh dấu pre-release). Chạy `Verify-Release.ps1 -RequireSigned -RequireStable`.
- [ ] **[Bạn]** Kiểm tra người dùng RC đã ký nhận được bản 2.0.0 qua cửa sổ cập nhật.

**Xong khi:** `2.0.0` đã ký có trên trang Releases và cập nhật từ RC hoạt động.

## Giai đoạn 10. Trả nợ kỹ thuật sau 2.0.0

Làm dần, mỗi mục một PR riêng. Không gộp với thay đổi tính năng, vì các mục này đụng nhiều file.

### Test

- [ ] **[AI]** Chuyển test sang xUnit hoặc MSTest theo từng nhóm, để chạy được bằng `dotnet test` và báo giá trị thực tế so với mong đợi.
- [ ] **[AI]** Tách `tests/Nepply.Tests/Program.cs` (hơn 1500 dòng) thành các file theo tính năng.
- [ ] **[AI]** Thay các test phụ thuộc thời gian bằng đồng hồ giả hoặc chờ theo điều kiện.

### Cấu trúc

- [ ] **[AI]** Sửa namespace cho khớp thư mục (`Nepply.Features.*`, `Nepply.Platform.*`), một PR cơ học riêng.
- [ ] **[AI]** Tách `MainWindow` (876 dòng): vị trí cửa sổ, kích hoạt từ cạnh, xem trước Settings, bố cục.
- [ ] **[AI]** Tách `SettingsWindow` (745 dòng) theo từng nhóm cài đặt.
- [ ] **[AI]** Tách `AppSettings` thành dữ liệu thuần; bỏ phụ thuộc vào giao diện và kho lưu file.
- [ ] **[AI]** Bỏ các lời gọi ngược lên `MainWindow` từ `IconPanel`, `MonitorPanel`, `TrayMenu`, `WelcomeWindow`.
- [ ] **[AI]** Bỏ các constructor chỉ dành cho test trong `DesktopIconService`, dùng một interface scanner.
- [ ] **[AI]** Tách các dòng dồn nhiều lệnh và bỏ biến `_settings` cục bộ che field trong `MainWindow`.

### Đơn giản hóa và dung lượng

- [ ] **[Cả hai]** Xem lại Guardian: exe 70 MB và khoảng 770 dòng cho việc khôi phục icon. Thử bản biên dịch native để giảm dung lượng.
- [ ] **[Cả hai]** Giảm dung lượng app: `Nepply.exe` 194 MB, Setup 78 MB. Thử nén single-file, đo ảnh hưởng tới thời gian khởi động.
- [ ] **[Bạn]** Quyết định về Microsoft Store: làm tiếp hoặc bỏ code và script MSIX.
- [ ] **[AI]** Gom bớt script kiểm tra trùng lặp, thêm số phiên bản lấy tự động từ tag.

### Phân phối và vận hành

- [ ] **[Cả hai]** Đưa Nepply lên winget sau khi có bản ký.
- [ ] **[AI]** Khi Inno Setup trong `release.yml` đổi phiên bản, đối chiếu `packaging/inno/Languages/Vietnamese.isl` với `Default.isl` mới.
- [ ] **[AI]** Bỏ dần tên legacy `HoverBox` (mutex `Local\HoverBox-SetupGuard`, thư mục `%LOCALAPPDATA%\HoverBox`) sau khi không còn người dùng bản cũ; giữ ít nhất một bản phát hành có migration.
- [ ] **[Bạn]** Ký mọi bản bằng cùng chứng thư: uy tín SmartScreen tăng dần theo lượt tải của cùng chứng thư.
- [ ] **[Bạn]** Nếu nhận donate: kiểm lại điều kiện giấy phép thương mại của Inno Setup.
