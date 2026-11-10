# Nepply 2.0.0-rc.2

[English](#english) · [Tiếng Việt](#tiếng-việt)

## English

> Second release candidate, **not code-signed**. It carries the fixes made since rc.1 and the .NET security updates. Stable 2.0.0 will be signed.

### ⚠️ On rc.1? Uninstall it first

1. Exit `2.0.0-rc.1` from its notification area icon, so any Desktop icons it hid come back.
2. Uninstall it from Windows Installed apps. When asked about removing data, choose **No** to keep your settings.
3. Install rc.2.

rc.2 uses new names for its session lock and its Desktop icon recovery marker, so it cannot see a running rc.1 or icons that rc.1 hid. Installing over rc.1 is not supported.

### 📦 Choose your download

**Windows 10 version 2004 or later / Windows 11 · x64**

| Choose | Asset |
| --- | --- |
| Standard installation — recommended | `Nepply-2.0.0-rc.2-Setup.exe` |
| Extract and run, no installer | `Nepply-2.0.0-rc.2-win-x64-portable.zip` |
| Compare download SHA-256 hashes | `SHA256.json` |

Extract the complete portable archive and run `Nepply.exe`; keep `guardian` and all companion files. Both downloads bundle .NET 10.0.14.

### ✨ What changed since rc.1

- **Security:** the bundled .NET runtime moves from 10.0.12 to 10.0.14, which includes Microsoft's October 2026 security fixes.
- **Update check:** a failed daily check retries after 12 hours instead of a day. After two failures in a row Nepply says so once in the notification area. Without a network it sends nothing.
- **Desktop icons:** if the process that protects hidden icons stops, the panel now says so and the log records its exit code.
- **Desktop list:** one unreadable item no longer empties the whole list; Nepply skips it and logs the error.
- **Sensors:** two sensors sharing a hardware ID no longer break the readings.

Full list: [CHANGELOG](https://github.com/minhquang0607/Nepply/blob/main/CHANGELOG.en.md).

### 🔐 Because this build is unsigned

- Windows SmartScreen may warn about an unknown publisher, and Smart App Control may block the file. Never turn off Windows security features to install or run Nepply.
- Compare the file's SHA-256 with `SHA256.json` or with the value GitHub shows next to it. To confirm this repository built it, run `gh attestation verify <file> --repo minhquang0607/Nepply`. Step by step: [Check a download](https://github.com/minhquang0607/Nepply#-download).
- This build cannot update itself. The update window opens the Releases page.

[User guide](https://github.com/minhquang0607/Nepply/blob/main/docs/USER_GUIDE.en.md) · [Troubleshooting](https://github.com/minhquang0607/Nepply/blob/main/docs/TROUBLESHOOTING.en.md) · [Report a bug](https://github.com/minhquang0607/Nepply/issues)

Built by Minh Quang. AI team: Codex and Claude.

## Tiếng Việt

> Bản phát hành thử thứ hai, **chưa ký số**. Gồm các sửa lỗi kể từ rc.1 và bản vá bảo mật .NET. Bản chính thức 2.0.0 sẽ có chữ ký.

### ⚠️ Đang dùng rc.1? Gỡ bản đó trước

1. Thoát `2.0.0-rc.1` từ biểu tượng ở khay hệ thống, để icon Desktop do app ẩn trở lại.
2. Gỡ app trong danh sách ứng dụng của Windows. Khi được hỏi có xóa dữ liệu không, chọn **Không** để giữ cài đặt.
3. Cài rc.2.

rc.2 đổi tên khóa phiên chạy và dấu khôi phục icon Desktop, nên không nhận ra rc.1 đang chạy hoặc icon do rc.1 ẩn. Không hỗ trợ cài đè lên rc.1.

### 📦 Chọn bản tải

Yêu cầu: **Windows 10 phiên bản 2004 trở lên hoặc Windows 11, x64**.

| Bạn muốn | Tệp trong Assets |
| --- | --- |
| Cài như ứng dụng thông thường — khuyên dùng | `Nepply-2.0.0-rc.2-Setup.exe` |
| Giải nén rồi chạy, không dùng bộ cài | `Nepply-2.0.0-rc.2-win-x64-portable.zip` |
| Đối chiếu SHA-256 của tệp tải | `SHA256.json` |

Với portable, giải nén **toàn bộ** và chạy `Nepply.exe`; không tách EXE khỏi thư mục `guardian` hoặc các file đi kèm. Cả hai bản có sẵn .NET 10.0.14.

### ✨ Thay đổi so với rc.1

- **Bảo mật:** bộ .NET đóng kèm lên 10.0.14 (từ 10.0.12), gồm các bản vá bảo mật của Microsoft tháng 10/2026.
- **Kiểm tra cập nhật:** thất bại thì thử lại sau 12 giờ thay vì một ngày. Hai lần thất bại liên tiếp, Nepply báo một lần ở khay hệ thống. Không có mạng thì không gửi gì.
- **Icon Desktop:** nếu tiến trình bảo vệ icon đang ẩn dừng, panel có thông báo và log ghi lại mã thoát.
- **Danh sách Desktop:** một mục không đọc được không còn làm trống cả danh sách; Nepply bỏ qua mục đó và ghi log.
- **Cảm biến:** hai cảm biến trùng mã phần cứng không còn làm hỏng số đo.

Danh sách đầy đủ: [CHANGELOG](https://github.com/minhquang0607/Nepply/blob/main/CHANGELOG.md).

### 🔐 Vì bản này chưa ký số

- Windows SmartScreen có thể cảnh báo nhà phát hành không xác định, và Smart App Control có thể chặn file. Không tắt các tính năng bảo mật của Windows để cài hoặc chạy Nepply.
- So SHA-256 của file với `SHA256.json` hoặc với giá trị GitHub hiện cạnh file. Muốn xác nhận file do repo này build, chạy `gh attestation verify <file> --repo minhquang0607/Nepply`. Hướng dẫn từng bước: [Kiểm tra file tải về](https://github.com/minhquang0607/Nepply/blob/main/README.vi.md#-tải-ứng-dụng).
- Bản này không tự cập nhật được. Cửa sổ cập nhật sẽ mở trang Releases.

[Hướng dẫn sử dụng](https://github.com/minhquang0607/Nepply/blob/main/docs/USER_GUIDE.md) · [Xử lý sự cố](https://github.com/minhquang0607/Nepply/blob/main/docs/TROUBLESHOOTING.md) · [Báo lỗi](https://github.com/minhquang0607/Nepply/issues)

Phát triển bởi Minh Quang. Đội ngũ AI: Codex và Claude.
