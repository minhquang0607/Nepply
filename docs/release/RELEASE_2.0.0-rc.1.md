# Nepply 2.0.0-rc.1

[English](#english) · [Tiếng Việt](#tiếng-việt)

## English

> Release candidate, **not code-signed**. Use it to try Nepply before 2.0.0 and to report problems. Stable 2.0.0 will be signed.

A tidier desktop. Your apps and system stats, within reach. Nepply is a Windows screen-edge panel for opening Desktop items, searching, customizing your layout and checking system activity.

### 📦 Choose your download

**Windows 10 version 2004 or later / Windows 11 · x64**

| Choose | Asset |
| --- | --- |
| Standard installation — recommended | `Nepply-2.0.0-rc.1-Setup.exe` |
| Extract and run, no installer | `Nepply-2.0.0-rc.1-win-x64-portable.zip` |
| Compare download SHA-256 hashes | `SHA256.json` |

Extract the complete portable archive and run `Nepply.exe`; keep `guardian` and all companion files. Settings still live in your Windows user profile. GitHub's Source code archives are not ready-to-run apps. Both downloads bundle .NET.

### ⚠️ Because this build is unsigned

- Windows SmartScreen may warn about an unknown publisher, and Smart App Control may block the file.
- Continue only if you downloaded it from this page and its SHA-256 matches `SHA256.json`.
- To confirm a file was built from this repository, run `gh attestation verify <file> --repo minhquang0607/Nepply` with the GitHub CLI.
- Never turn off Windows security features to install or run Nepply.
- This build cannot update itself. The update window opens the Releases page, so install later versions by hand.

### 🧪 Please try

- Installing, opening the panel from a screen edge, and uninstalling.
- Choosing a language in Setup, or on the welcome screen. Windows' language is preselected.
- Setting your own hotkey for the panel in Settings → Shortcuts. None is preset.
- Hiding Desktop icons, then exiting Nepply: the icons must come back.
- Monitoring on your hardware, and Report a problem in Settings.

### 🛡️ Before you start

- This build has not finished acceptance testing on a clean machine. Stable 2.0.0 ships after that testing.
- Sensor readings depend on hardware; motherboard temperature/RPM are not supported. Do not disable antivirus or anti-cheat to obtain data.
- Internal `2.5.0-*` builds do not see this version as an upgrade. Exit the old build, back up settings and switch manually.
- Nepply restores Desktop icons it hid when exiting. If abnormal termination leaves icons hidden, right-click the Desktop and choose View > Show desktop icons.

[User guide](https://github.com/minhquang0607/Nepply/blob/main/docs/USER_GUIDE.en.md) · [Troubleshooting](https://github.com/minhquang0607/Nepply/blob/main/docs/TROUBLESHOOTING.en.md) · [Code signing policy](https://github.com/minhquang0607/Nepply/blob/main/docs/release/CODE_SIGNING_POLICY.en.md) · [Report a bug](https://github.com/minhquang0607/Nepply/issues)

Include Windows/Nepply versions and reproduction steps; **Settings → Application → Copy diagnostics** copies the version, Windows and sensor details for you. Redact paths, account names and personal data from screenshots or logs.

Built by Minh Quang. AI team: Codex and Claude.

## Tiếng Việt

> Bản phát hành thử, **chưa ký số**. Dùng để trải nghiệm Nepply trước 2.0.0 và báo lỗi. Bản chính thức 2.0.0 sẽ có chữ ký.

Desktop gọn hơn, ứng dụng và thông số hệ thống luôn trong tầm tay.

Nepply là panel ở cạnh màn hình Windows: mở nhanh các mục Desktop, tìm kiếm, chỉnh bố cục và theo dõi máy ngay trên một giao diện.

### 📦 Chọn bản tải

Yêu cầu: **Windows 10 phiên bản 2004 trở lên hoặc Windows 11, x64**.

| Bạn muốn | Tệp trong Assets |
| --- | --- |
| Cài như ứng dụng thông thường — khuyên dùng | `Nepply-2.0.0-rc.1-Setup.exe` |
| Giải nén rồi chạy, không dùng bộ cài | `Nepply-2.0.0-rc.1-win-x64-portable.zip` |
| Đối chiếu SHA-256 của tệp tải | `SHA256.json` |

Với portable, giải nén **toàn bộ** và chạy `Nepply.exe`; không tách EXE khỏi thư mục `guardian` hoặc các file đi kèm. Cấu hình vẫn lưu trong tài khoản Windows. Không chọn `Source code` nếu bạn chỉ muốn dùng app. Cả hai bản có sẵn .NET.

### ⚠️ Vì bản này chưa ký số

- Windows SmartScreen có thể cảnh báo nhà phát hành không xác định, và Smart App Control có thể chặn file.
- Chỉ tiếp tục nếu bạn tải từ trang này và SHA-256 khớp với `SHA256.json`.
- Muốn xác nhận file được build từ repo này, chạy `gh attestation verify <file> --repo minhquang0607/Nepply` bằng GitHub CLI.
- Không tắt các tính năng bảo mật của Windows để cài hoặc chạy Nepply.
- Bản này không tự cập nhật được. Cửa sổ cập nhật sẽ mở trang Releases, nên các bản sau cần cài tay.

### 🧪 Nên thử

- Cài đặt, mở panel từ cạnh màn hình, rồi gỡ cài đặt.
- Chọn ngôn ngữ trong bộ cài hoặc ở màn chào. Ngôn ngữ của Windows được chọn sẵn.
- Tự đặt phím mở panel trong Cài đặt → Phím tắt. App không đặt sẵn phím nào.
- Ẩn icon Desktop rồi thoát Nepply: icon phải trở lại.
- Theo dõi hệ thống trên phần cứng của bạn, và mục Báo lỗi trong Cài đặt.

### 🛡️ Trước khi sử dụng

- Bản này chưa nghiệm thu xong trên máy cài mới. Bản chính thức 2.0.0 chỉ ra sau khi nghiệm thu.
- Số đo cảm biến phụ thuộc phần cứng; không có nhiệt độ bo mạch chủ/RPM. Không tắt antivirus hay anti-cheat để lấy thêm số đo.
- Nếu đang dùng bản thử `2.5.0-*`, updater không coi bản này là bản nâng cấp. Thoát bản cũ và chuyển thủ công; sao lưu cấu hình trước khi chuyển.
- Khi thoát, Nepply khôi phục icon Desktop do app đã ẩn. Nếu app đóng bất thường và icon chưa trở lại, nhấp chuột phải vào Desktop và chọn Xem > Hiển thị biểu tượng màn hình nền.

[Hướng dẫn sử dụng](https://github.com/minhquang0607/Nepply/blob/main/docs/USER_GUIDE.md) · [Xử lý sự cố](https://github.com/minhquang0607/Nepply/blob/main/docs/TROUBLESHOOTING.md) · [Chính sách ký mã](https://github.com/minhquang0607/Nepply/blob/main/docs/release/CODE_SIGNING_POLICY.md) · [Báo lỗi](https://github.com/minhquang0607/Nepply/issues)

Khi báo lỗi, gửi phiên bản Windows/Nepply và các bước tái hiện; nút **Cài đặt → Ứng dụng → Sao chép chẩn đoán** chép sẵn phiên bản, Windows và thông tin cảm biến. Che đường dẫn, tên tài khoản và dữ liệu cá nhân trong ảnh hoặc log.

Phát triển bởi Minh Quang. Đội ngũ AI: Codex và Claude.
