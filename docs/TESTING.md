# 🧪 Dùng thử Nepply

[English](TESTING.en.md) · **Tiếng Việt** · [Trang chính](../README.vi.md) · [Xử lý sự cố](TROUBLESHOOTING.md)

Nepply 2.0.0 đang ở giai đoạn phát hành thử. Người phát triển cần dev và người dùng am hiểu máy tính thử trên phần cứng mà anh không có. Trang này nói cần cài gì, thử gì và báo thế nào. Chỉ cần 10 phút cho một mục đã có ích.

## Trước khi bắt đầu

- Windows 10 bản 2004 (build 19041) trở lên hoặc Windows 11, x64.
- Chỉ tải từ [GitHub Releases](https://github.com/minhquang0607/Nepply/releases). Chọn bản mới nhất có nhãn *Pre-release*.
- Bản build **chưa ký số** nên SmartScreen có thể cảnh báo. Đừng tắt SmartScreen, Smart App Control hay antivirus để chạy; nếu Windows chặn hẳn, đó cũng là kết quả đáng báo.
- Nếu đã cài `2.0.0-rc.1`, hãy gỡ trước. Cài đặt được giữ lại trừ khi bạn chọn xóa dữ liệu.
- Đọc [Điều khoản sử dụng](legal/TERMS.md) và [Quyền riêng tư](legal/PRIVACY.md). Nepply không tự gửi gì, ngoài việc kiểm tra cập nhật qua GitHub, và bạn tắt được việc này.

## Nếu icon Desktop không hiện lại

Mở lại Nepply rồi chọn **Thoát** ở khay hệ thống. Nếu vẫn ẩn, chuột phải Desktop → **View/Xem → Show desktop icons/Hiển thị biểu tượng màn hình nền**. Đừng xóa `%LOCALAPPDATA%\Nepply\desktop-visibility.lease` khi icon đang ẩn. Sau đó hãy báo lỗi: đây là loại lỗi quan trọng nhất.

## Thử gì

Chọn các dòng hợp với máy bạn. Ghi lại chuyện gì xảy ra, kể cả khi mọi thứ chạy đúng.

| # | Tình huống | Cần để ý |
| --- | --- | --- |
| 1 | Tải Setup bằng trình duyệt rồi chạy | SmartScreen hoặc Smart App Control hiện gì; ngôn ngữ bộ cài |
| 2 | Lần chạy đầu | Ngôn ngữ khớp Windows; màn chào dễ đọc |
| 3 | Ẩn icon Desktop rồi Thoát từ khay | Icon trở lại |
| 4 | Ẩn icon rồi khởi động lại Explorer (Task Manager → Windows Explorer → Restart) | Icon còn ẩn hay hiện lại, panel còn chạy không |
| 5 | Ẩn icon rồi End task `Nepply.exe` trong Task Manager | Icon trở lại sau vài giây |
| 6 | Gỡ cài đặt khi icon đang ẩn | Icon trở lại; không còn gì trong `%LOCALAPPDATA%\Programs\Nepply` |
| 7 | Nhiều màn hình, tỉ lệ khác nhau (ví dụ 100 % + 150 %) | Panel mở đúng cạnh, đúng màn hình, đúng kích thước |
| 8 | Cảm biến CPU/GPU (Intel, NVIDIA, AMD, laptop chạy pin) | Số nào hiện, số nào trống; **Cài đặt → Ứng dụng → Sao chép chẩn đoán** cho chi tiết |
| 9 | Mở từ cạnh khi chơi game hoặc app toàn màn hình | Panel không bật khi game ẩn con trỏ |
| 10 | Phím tắt | Đặt hotkey; trùng với app khác |
| 11 | Để chạy một ngày với icon đang ẩn | RAM hoặc CPU trong Task Manager tăng dần |
| 12 | Windows 10 22H2, Windows không phải tiếng Anh, tài khoản thường (không admin) | Điều gì khác so với Windows 11 |
| 13 | Narrator hoặc High Contrast | Nút được đọc tên và nhìn rõ |

## Báo thế nào

- **Lỗi:** trong Nepply, **Cài đặt → Ứng dụng → Báo lỗi** chép chẩn đoán và mở form báo lỗi đã điền phiên bản. Xem lại phần chẩn đoán trước khi dán.
- **Kết quả thử (đạt hay không):** dùng [form Báo cáo dùng thử](https://github.com/minhquang0607/Nepply/issues/new?template=test_report.yml). Kết quả "chạy đúng" cũng quan trọng: nó cho biết phần cứng nào đã được thử.
- **Ý tưởng:** dùng [form đề xuất](https://github.com/minhquang0607/Nepply/issues/new?template=feature_request.yml).
- **Lỗ hổng bảo mật:** báo riêng theo [chính sách bảo mật](legal/SECURITY.md), không đăng Issue công khai.

Issue là công khai. Hãy che tên tài khoản, đường dẫn cá nhân và tên file riêng trong ảnh và log.

## Dành cho dev

Build từ mã nguồn và chạy test: xem [Đóng góp](../CONTRIBUTING.md). Pull request được hoan nghênh; với thay đổi lớn hãy mở Issue trước.
