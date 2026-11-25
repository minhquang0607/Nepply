# Nepply 2.0.0-rc.3

[English](#english) · [Tiếng Việt](#tiếng-việt)

## English

> Third release candidate, **not code-signed**. It carries the post-rc.2 fixes, .NET 10.0.15 security updates, and community testing updates. How stable 2.0.0 will be signed and distributed will be decided after community feedback.

### ⚠️ On rc.1? Uninstall it first

Upgrading from `2.0.0-rc.2` works directly (exit rc.2 first or let Setup wait for it to close). If you are still on **`2.0.0-rc.1`**, uninstall it first:

1. Exit `2.0.0-rc.1` from its notification area icon, so any Desktop icons it hid come back.
2. Uninstall it from Windows Installed apps. When asked about removing data, choose **No** to keep your settings.
3. Install rc.3.

Builds from `2.0.0-rc.2` onward use the Nepply session lock and Desktop icon recovery marker names, so they cannot see a running rc.1 or icons that rc.1 hid.

### 📦 Choose your download

**Windows 10 version 2004 or later / Windows 11 · x64**

| Choose | Asset |
| --- | --- |
| Standard installation — recommended | `Nepply-2.0.0-rc.3-Setup.exe` |
| Extract and run, no installer | `Nepply-2.0.0-rc.3-win-x64-portable.zip` |
| Compare download SHA-256 hashes | `SHA256.json` |
| Short localized update summary | `release-notes.json` |

Extract the complete portable archive and run `Nepply.exe`; keep `guardian` and all companion files. Both downloads bundle .NET 10.0.15.

### ✨ What changed since rc.2

- **Security:** the bundled .NET runtime moves from 10.0.14 to 10.0.15, which includes Microsoft's November 2026 security fixes.
- **Updates:** **What's new** in the update window now shows short summaries in your selected Nepply language across newer releases that include `release-notes.json`.
- **Settings:** the display list shows friendly Windows monitor names instead of technical codes like `DISPLAY1` (adding the code only when two monitors share a name), and the network adapter list hides disabled, loopback and internal virtual adapters while keeping active VPN connections.
- **Desktop & reliability:** editing file contents on the Desktop no longer triggers background rescans, layout save error dialogs attach properly to the panel with the Nepply title/warning icon, and resolving `.lnk` shortcuts allocates less memory.
- **Testing & terms:** packaged [Terms of use](https://github.com/minhquang0607/Nepply/blob/main/docs/legal/TERMS.md#english) with Setup/portable and linked Terms/Privacy directly from the welcome screen and Settings; made daily background update checks opt-in (off by default and blocked until onboarding completes); added the [Testing guide](https://github.com/minhquang0607/Nepply/blob/main/docs/TESTING.en.md), clearer Desktop icon hiding/recovery docs, and English architecture docs.

Full list: [CHANGELOG](https://github.com/minhquang0607/Nepply/blob/main/CHANGELOG.en.md).

### 🔐 Because this build is unsigned

- Windows SmartScreen may warn about an unknown publisher, and Smart App Control may block the file. Never turn off Windows security features to install or run Nepply.
- Compare the file's SHA-256 with `SHA256.json` or with the value GitHub shows next to it. To confirm this repository built it, run `gh attestation verify <file> --repo minhquang0607/Nepply`. Step by step: [Check a download](https://github.com/minhquang0607/Nepply#-download).
- This build cannot update itself. The update window opens the Releases page.

[Testing guide](https://github.com/minhquang0607/Nepply/blob/main/docs/TESTING.en.md) · [User guide](https://github.com/minhquang0607/Nepply/blob/main/docs/USER_GUIDE.en.md) · [Troubleshooting](https://github.com/minhquang0607/Nepply/blob/main/docs/TROUBLESHOOTING.en.md) · [Report a bug](https://github.com/minhquang0607/Nepply/issues)

Built by Minh Quang. AI team: Codex and Claude.

## Tiếng Việt

> Bản phát hành thử thứ ba, **chưa ký số**. Gồm các bản sửa và hoàn thiện sau rc.2, runtime .NET 10.0.15 kèm bản vá bảo mật, cùng tài liệu mời cộng đồng dev dùng thử. Hướng ký số và phân phối cho 2.0.0 sẽ chốt sau đợt phản hồi.

### ⚠️ Đang dùng rc.1? Gỡ bản đó trước

Nâng cấp từ `2.0.0-rc.2` lên `rc.3` có thể cài thẳng (nên thoát rc.2 trước hoặc để Setup chờ đóng). Nếu bạn vẫn đang dùng **`2.0.0-rc.1`**, hãy gỡ bản đó trước:

1. Thoát `2.0.0-rc.1` từ biểu tượng ở khay hệ thống, để icon Desktop do app ẩn trở lại.
2. Gỡ app trong danh sách ứng dụng của Windows. Khi được hỏi có xóa dữ liệu không, chọn **Không** để giữ cài đặt.
3. Cài rc.3.

Từ `2.0.0-rc.2` trở đi, khóa phiên chạy và dấu khôi phục icon Desktop dùng tên Nepply, nên không nhận ra rc.1 đang chạy hoặc icon do rc.1 ẩn.

### 📦 Chọn bản tải

Yêu cầu: **Windows 10 phiên bản 2004 trở lên hoặc Windows 11, x64**.

| Bạn muốn | Tệp trong Assets |
| --- | --- |
| Cài như ứng dụng thông thường — khuyên dùng | `Nepply-2.0.0-rc.3-Setup.exe` |
| Giải nén rồi chạy, không dùng bộ cài | `Nepply-2.0.0-rc.3-win-x64-portable.zip` |
| Đối chiếu SHA-256 của tệp tải | `SHA256.json` |
| Tóm tắt cập nhật theo ngôn ngữ | `release-notes.json` |

Với portable, giải nén **toàn bộ** và chạy `Nepply.exe`; không tách EXE khỏi thư mục `guardian` hoặc các file đi kèm. Cả hai bản có sẵn .NET 10.0.15.

### ✨ Thay đổi so với rc.2

- **Bảo mật:** bộ .NET đóng kèm lên 10.0.15 (từ 10.0.14), gồm các bản vá bảo mật của Microsoft tháng 11/2026.
- **Cập nhật:** mục **Có gì mới** trong cửa sổ cập nhật giờ hiện tóm tắt ngắn theo ngôn ngữ đang dùng trong Nepply cho các bản mới hơn có `release-notes.json`.
- **Cài đặt:** danh sách màn hình hiện tên màn hình Windows dễ đọc thay cho tên kỹ thuật như `DISPLAY1` (chỉ gắn mã màn hình khi hai màn hình trùng tên), và danh sách mạng ẩn bớt card đang tắt, loopback và switch ảo nội bộ trong khi vẫn giữ các kết nối VPN đang hoạt động.
- **Desktop & độ tin cậy:** chỉnh sửa nội dung tệp trên Desktop không còn kích hoạt quét lại nền; hộp thoại lỗi lưu bố cục gắn đúng cửa sổ panel kèm tiêu đề và biểu tượng Nepply; giảm cấp phát bộ nhớ khi đọc shortcut `.lnk`.
- **Dùng thử, quyền riêng tư & điều khoản:** đóng gói kèm [Điều khoản sử dụng](https://github.com/minhquang0607/Nepply/blob/main/docs/legal/TERMS.md) trong Setup/portable và gắn nút mở Điều khoản/Quyền riêng tư trên màn hình chào & Cài đặt; chuyển kiểm tra cập nhật hằng ngày sang mặc định tắt (chỉ chạy sau khi hoàn tất màn hình chào và khi bạn chủ động bật); bổ sung [Hướng dẫn dùng thử](https://github.com/minhquang0607/Nepply/blob/main/docs/TESTING.md), làm rõ hướng dẫn ẩn/khôi phục icon Desktop và thêm tài liệu kiến trúc tiếng Anh.

Danh sách đầy đủ: [CHANGELOG](https://github.com/minhquang0607/Nepply/blob/main/CHANGELOG.md).

### 🔐 Vì bản này chưa ký số

- Windows SmartScreen có thể cảnh báo nhà phát hành không xác định, và Smart App Control có thể chặn file. Không tắt các tính năng bảo mật của Windows để cài hoặc chạy Nepply.
- So SHA-256 của file với `SHA256.json` hoặc với giá trị GitHub hiện cạnh file. Muốn xác nhận file do repo này build, chạy `gh attestation verify <file> --repo minhquang0607/Nepply`. Hướng dẫn từng bước: [Kiểm tra file tải về](https://github.com/minhquang0607/Nepply/blob/main/README.vi.md#-tải-ứng-dụng).
- Bản này không tự cập nhật được. Cửa sổ cập nhật sẽ mở trang Releases.

[Hướng dẫn dùng thử](https://github.com/minhquang0607/Nepply/blob/main/docs/TESTING.md) · [Hướng dẫn sử dụng](https://github.com/minhquang0607/Nepply/blob/main/docs/USER_GUIDE.md) · [Xử lý sự cố](https://github.com/minhquang0607/Nepply/blob/main/docs/TROUBLESHOOTING.md) · [Báo lỗi](https://github.com/minhquang0607/Nepply/issues)

Phát triển bởi Minh Quang. Đội ngũ AI: Codex và Claude.
