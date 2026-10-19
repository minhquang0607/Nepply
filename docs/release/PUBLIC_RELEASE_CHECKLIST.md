# Checklist mở mã nguồn và phát hành công khai

Trạng thái ngày 19/10/2026. Đánh dấu từng mục khi hoàn tất; không đánh dấu mục chưa kiểm chứng thật.

## 0. Đã xong trong repo

- [x] Rà **toàn bộ lịch sử git** (25 commit, mọi nhánh, 528 đường dẫn kể cả file đã xóa): không có private key, token, mật khẩu, chuỗi kết nối, đường dẫn hồ sơ người dùng Windows cá nhân hay số điện thoại. Tác giả commit dùng email noreply của GitHub. Email duy nhất là của tác giả bên thứ ba trong notice Mono.Posix cũ. 5 ảnh trong lịch sử đều là dữ liệu fixture. Lịch sử vẫn chứa link liên hệ cá nhân cũ và các báo cáo nội bộ đã xóa (`BAO_CAO_KIEM_TOAN_2026-09-18.md`, `EULA_DRAFT.md`, ghi chú nghiên cứu); sẽ công khai cùng lịch sử. Không cần viết lại lịch sử trừ khi bạn không muốn công khai các file đó.
- [x] `.github/SECURITY.md`, `CODE_OF_CONDUCT.md`, issue templates, chính sách ký mã tiếng Anh, Dependabot cho NuGet.
- [x] Workflow `release.yml`: tag `vX.Y.Z` → build trên GitHub runner → Release **nháp** chưa ký. **Chưa từng chạy**; lần chạy đầu cần theo dõi.
- [x] Luồng ký uninstaller (`-SignedUninstallerDirectory`), kiểm chữ ký cập nhật theo nhà phát hành + CA (`Get-SignerPolicy.ps1`).

## 1. Cài đặt GitHub (bạn làm)

1. **MFA tài khoản**: Settings (tài khoản) → Password and authentication → bật Two-factor authentication. Lưu recovery codes ngoài máy.
2. **Bảo mật repo**: repo → Settings → *Code security* (có thể tên *Advanced Security*): bật Dependency graph, Dependabot alerts, Dependabot security updates, **Private vulnerability reporting**, Secret scanning và Push protection.
3. **Actions**: Settings → Actions → General:
   - Workflow permissions: *Read repository contents and packages permissions*.
   - Bỏ chọn *Allow GitHub Actions to create and approve pull requests*.
   - Fork pull request workflows: *Require approval for all external contributors*.
4. **Bảo vệ nhánh `main`**: Settings → Rules → Rulesets → New branch ruleset, target `main`: chặn force push và xóa nhánh, yêu cầu status check `build` (workflow *Verify Windows build*). Có thể bật *Require a pull request* với 0 approval nếu muốn mọi thay đổi đi qua PR.
5. **Bảo vệ tag**: thêm tag ruleset cho `v*`, chỉ bạn được tạo/xóa.
6. **Môi trường release**: Settings → Environments → New environment `release` → *Required reviewers*: chính bạn; *Deployment branches and tags*: chỉ tag `v*`.
7. Duyệt hoặc đóng 3 PR Dependabot cập nhật GitHub Actions (CI phải xanh trước khi merge).
8. **Chuyển Public**: Settings → General → Danger Zone → Change repository visibility → Public. Sau đó mở thử link báo lỗ hổng `…/security/advisories/new` và trang Issues để chắc chúng hoạt động.

## 2. Bản phát hành đầu tiên chưa ký (điều kiện của SignPath)

SignPath Foundation yêu cầu dự án **đã phát hành** ở dạng sẽ được ký. Quyết định số phiên bản trước:

- **Đã chốt (20/10/2026):** phát hành `1.0.0-beta.1` (prerelease) chưa ký, để bản ký đầu tiên vẫn là `1.0.0` và updater không nhận nhầm thứ tự. Cần đổi `<Version>` trong `src/Nepply/Nepply.csproj`, CHANGELOG và README.
- (Phương án không chọn: phát hành `1.0.0` chưa ký, rồi bản ký đầu tiên là `1.0.1`.)

Quy trình:

1. Chạy thử: Actions → *Release build* → Run workflow (chỉ build, không tạo Release). Kiểm tra artifact `nepply-release-unsigned`.
2. Tạo tag: `git tag v<phiên-bản>` rồi `git push origin v<phiên-bản>`. Duyệt bước `release` khi GitHub hỏi.
3. Mở Release nháp, đánh dấu *pre-release*, dán ghi chú từ CHANGELOG và ghi rõ **bản chưa ký: Windows có thể cảnh báo**. Bấm Publish.

## 3. Đăng ký SignPath Foundation (bạn làm)

1. Kiểm tra trước khi nộp: repo Public; giấy phép MIT và dependency đều là giấy phép OSI (xem `docs/legal/THIRD_PARTY_NOTICES.md`); README có link *Code signing policy*; trang chính sách ghi vai trò (author/reviewer/approver = bạn) và link privacy policy; MFA đã bật; đã có Release công khai; có uninstaller; app chỉ ẩn icon Desktop khi người dùng tự bật và có hướng dẫn khôi phục.
2. Nộp đơn tại <https://signpath.org> (mục apply cho dự án OSS), dùng link repo, Release và `docs/release/CODE_SIGNING_POLICY.en.md`.
3. Khi được duyệt, SignPath cung cấp organization ID, project/policy slug và API token. Lưu token vào Settings → Environments → `release` → *Environment secrets* (không để ở repository secret chung).
4. Báo lại để tích hợp: thêm bước gửi ký (action SignPath pin theo SHA), ký `Nepply.exe` + `Nepply.DesktopGuardian.exe`, dựng lại Setup với `-SignedUninstallerDirectory`, ký Setup, tạo checksum sau ký.
5. Phương án dự phòng nếu bị từ chối: chứng thư OV cho người làm OSS (ví dụ Certum Open Source Code Signing). Azure Artifact Signing chỉ cấp cho cá nhân ở Mỹ/Canada.

## 4. Sau khi có chữ ký

- [ ] `pwsh scripts/Get-SignerPolicy.ps1 -File <Setup-đã-ký>` → đưa kết quả vào `UpdatePolicy.Signers` (bật tự cập nhật).
- [ ] Mở public mode trong `scripts/Build-Distribution.ps1` (hiện từ chối khi thiếu `-InternalOnly`).
- [ ] `Verify-Release.ps1 -RequireSigned -AllowedSignerThumbprints <thumbprint> -Uninstaller <unins000.exe từ lượt cài thật>`.
- [ ] Bổ sung attribution SignPath đúng yêu cầu vào README và chính sách ký.

## 5. Nghiệm thu bằng đúng file đã ký (bạn làm, trên VM)

Dùng VM Windows 11 cài mới (Hyper-V hoặc VirtualBox), **bật Smart App Control** ngay khi cài Windows (chỉ bật được trên máy mới cài). Ghi hash Setup, ảnh chụp và kết quả từng mục:

- [ ] Tải từ GitHub Release qua trình duyệt (để có Mark of the Web), chạy Setup: SmartScreen/SAC hiện gì?
- [ ] Lần đầu chạy: chọn ẩn icon Desktop → thoát qua khay → icon trở lại.
- [ ] Kill tiến trình `Nepply.exe` trong Task Manager khi icon đang ẩn → guardian khôi phục icon.
- [ ] Gỡ cài đặt khi icon đang ẩn → icon trở lại, không còn file trong `%LOCALAPPDATA%\Programs\Nepply`.
- [ ] Cài bản cũ → cài bản mới đè lên (nâng cấp) → giữ Settings, tự khởi động.
- [ ] Cập nhật trong app từ bản A lên bản B (cần cả hai bản đều đã ký).
- [ ] AppLocker với rule mặc định (Windows Pro/Enterprise) và tài khoản người dùng thường.
- [ ] Máy thật khác: Intel, nhiều màn hình với DPI khác nhau, laptop chạy pin.
- [ ] Nếu Defender báo nhầm: gửi file tại <https://www.microsoft.com/wdsi/filesubmission> (chọn *Software developer*).

## 6. Sau phát hành

- Uy tín SmartScreen tăng dần theo lượt tải của cùng một chứng thư; ký mọi bản bằng cùng chứng thư.
- Nếu nhận donate: kiểm lại điều kiện giấy phép thương mại của Inno Setup.
- Bỏ dần tên legacy `HoverBox` (mutex `Local\HoverBox-SetupGuard`, thư mục `%LOCALAPPDATA%\HoverBox`) sau khi không còn người dùng bản cũ; giữ ít nhất một bản phát hành có migration.
