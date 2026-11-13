<p align="center"><img src="src/Nepply/Resources/nepply.png" width="80" alt="Biểu tượng Nepply"></p>
<h1 align="center">Nepply</h1>
<p align="center"><strong>Desktop gọn hơn. Ứng dụng và thông số hệ thống luôn trong tầm tay.</strong></p>
<p align="center"><a href="README.md">English</a> · <strong>Tiếng Việt</strong></p>
<p align="center"><a href="#-tải-ứng-dụng">Tải ứng dụng</a> · <a href="docs/USER_GUIDE.md">Hướng dẫn</a> · <a href="CHANGELOG.md">Có gì mới</a> · <a href="docs/README.md">Tài liệu</a></p>

> **Bản phát hành thử của 2.0.0 · cần người dùng thử**
>
> Bản này chưa ký số nên Windows có thể cảnh báo. Dev và người dùng am hiểu máy tính: hãy thử trên máy bạn và cho biết kết quả. [Hướng dẫn dùng thử →](docs/TESTING.md)

## ✨ Panel nhỏ, tiện ngay bên cạnh

Nepply nằm ở cạnh màn hình Windows. Mở mục Desktop, tìm theo tên và xem CPU, RAM, GPU/iGPU cùng tốc độ mạng mà không cần quay về màn hình nền.

| | Bạn có thể làm gì? |
| --- | --- |
| 🖱️ Mở nhanh | Mở từ cạnh màn hình hoặc phím tắt; ghim panel khi cần giữ mở. Đổi được mọi phím tắt. |
| 🔎 Tìm trên Desktop | Tìm và mở mục, dùng menu chuột phải Windows. File vẫn ở vị trí cũ. |
| 🎨 Cá nhân hóa | Sáng/tối/theo hệ thống, bảng màu, neon và ánh kim; xem trước trước khi lưu. |
| 📐 Bố cục linh hoạt | Chọn cột × hàng bằng lưới chấm tròn; cuộn để xem các mục còn lại. |
| 📊 Theo dõi máy | CPU, RAM, GPU/iGPU và tốc độ tải lên/xuống. Cảm biến tùy phần cứng. |
| 🎮 Hạn chế làm phiền game | Không tự mở cạnh khi game ẩn chuột để điều khiển nhân vật. Hành vi tùy từng game. |

Ứng dụng hỗ trợ tiếng Việt, Anh, Nhật, Đức, Trung giản thể, Nga, Pháp và Thái.

## 🖼️ Panel & Cài đặt

<table>
<tr><th>Panel Desktop</th><th>Cài đặt · bố cục rộng</th></tr>
<tr>
<td valign="top" width="32%"><img src="docs/images/panel.png" width="300" alt="Panel Nepply với thẻ hệ thống và các mục Desktop mẫu"></td>
<td valign="top" width="68%"><img src="docs/images/settings-wide.png" width="640" alt="Cài đặt Nepply với menu dọc và tùy chọn giao diện"></td>
</tr>
</table>

Ảnh render mới từ **build Release**, source `2fa1e78`. Đây là các view WPF thật trong fixture UI cô lập, không phải ảnh Desktop cá nhân. Tên mục, icon và số đo panel là dữ liệu minh họa; ảnh dùng giao diện tiếng Việt. Settings chuyển sang tab ngang khi cửa sổ hẹp. [Nguồn ảnh →](docs/images/README.md)

## 📦 Tải ứng dụng

**Windows 10 phiên bản 2004 (build 19041) trở lên / Windows 11 · x64**

| Lựa chọn | Tệp | Cách dùng |
| --- | --- | --- |
| **Setup — khuyên dùng** | `Nepply-<phiên bản>-Setup.exe` | Chạy bộ cài; gỡ qua danh sách ứng dụng của Windows. |
| **Portable** | `Nepply-<phiên bản>-win-x64-portable.zip` | Giải nén toàn bộ rồi chạy `Nepply.exe`. Giữ `guardian` và các file đi kèm. |

[Mở GitHub Releases →](https://github.com/minhquang0607/Nepply/releases)

`<phiên bản>` là số phiên bản ghi trên trang Releases. Chọn tệp trong **Assets**. Các gói “Source code” của GitHub là mã nguồn cho người phát triển, không phải app chạy ngay. Cả hai bản đóng gói sẵn .NET, không cần cài SDK. Portable vẫn lưu cấu hình trong tài khoản Windows.

Chỉ tải từ kênh phát hành chính thức. Nếu Windows chặn file, kiểm tra nguồn và thông tin release. **Không tắt bảo vệ Windows để chạy Nepply.**

<details>
<summary><strong>Kiểm tra file tải về (tùy chọn)</strong></summary>

Khi Nepply chưa có chữ ký số, hai cách sau cho biết file đúng là bản repo này phát hành:

1. **File không hỏng khi tải.** Mở PowerShell ở thư mục chứa file:

   ```powershell
   Get-FileHash .\Nepply-<phiên bản>-Setup.exe -Algorithm SHA256
   ```

   So kết quả với dòng của file trong `SHA256.json`, hoặc với giá trị `sha256:` GitHub hiện cạnh file trong **Assets**. Cả hai nằm cùng trang Release, nên cách này phát hiện file tải hỏng, không phát hiện được một bản phát hành giả.

2. **File do repo này build.** Cần [GitHub CLI](https://cli.github.com/) đã đăng nhập:

   ```powershell
   gh attestation verify .\Nepply-<phiên bản>-Setup.exe --repo minhquang0607/Nepply
   ```

   Lệnh chỉ đạt với file do quy trình phát hành của repo này build và chứng thực. Mọi file trên trang Releases đều có chứng thực.

</details>

## 🚀 Bắt đầu

1. Mở Nepply và chọn cạnh màn hình.
2. Đưa chuột tới cạnh đó. Nếu thích dùng phím, hãy đặt trong Cài đặt → Phím tắt.
3. Mở một mục Desktop hoặc tìm theo tên.
4. Thử thay đổi trong Cài đặt: **Áp dụng** lưu và giữ mở; **OK** lưu rồi đóng; **Hủy** bỏ phần thay đổi kể từ lần lưu gần nhất.
5. Muốn thoát hẳn, chọn **Thoát** ở biểu tượng khay hệ thống.

[Hướng dẫn sử dụng](docs/USER_GUIDE.md) · [Xử lý sự cố](docs/TROUBLESHOOTING.md)

## 🛡️ Quyền riêng tư & hỗ trợ

Không quảng cáo, analytics hay tự gửi báo cáo lỗi. Kiểm tra cập nhật kết nối GitHub mỗi ngày một lần (thử lại sau 12 giờ nếu thất bại) và khi bạn yêu cầu; bạn có thể tắt kiểm tra hằng ngày. [Quyền riêng tư — EN / VI](docs/legal/PRIVACY.md) · [Điều khoản sử dụng — EN / VI](docs/legal/TERMS.md)

- [Báo lỗi hoặc đề xuất](https://github.com/minhquang0607/Nepply/issues): ghi phiên bản và bước tái hiện; che dữ liệu cá nhân trong ảnh/log.
- Đã thử bản phát hành thử? Chia sẻ phần chạy đúng và chưa đúng qua [form Báo cáo dùng thử](https://github.com/minhquang0607/Nepply/issues/new?template=test_report.yml). Xem [hướng dẫn dùng thử](docs/TESTING.md).
- Lỗ hổng bảo mật: dùng [kênh liên hệ riêng — EN / VI](docs/legal/SECURITY.md), không đăng cách khai thác công khai.
- Tự cài cập nhật EXE chưa mở cho phát hành; portable chưa tự thay file. Chưa công bố bản Microsoft Store.

## 🧩 Mã nguồn & giấy phép

Phát triển bởi **Minh Quang** bằng WPF / .NET. Đội ngũ AI: Codex và Claude. Mã Nepply sở hữu dùng [MIT License](LICENSE); thư viện giữ [giấy phép và thông báo riêng](docs/legal/THIRD_PARTY_NOTICES.md).

[Đóng góp / tự build](CONTRIBUTING.md) · [Tài liệu](docs/README.md) · [Chính sách ký mã](docs/release/CODE_SIGNING_POLICY.md) · [Quy tắc ứng xử](CODE_OF_CONDUCT.md)
