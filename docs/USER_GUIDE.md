# 📖 Sử dụng Nepply

[English](USER_GUIDE.en.md) · **Tiếng Việt** · [Trang chính](../README.vi.md) · [Xử lý sự cố](TROUBLESHOOTING.md)

Hướng dẫn cho **Nepply 1.0.0-beta.1**, bản beta công khai đầu tiên (chưa ký) đang chuẩn bị; chưa có bộ tải chính thức đã ký số.

## Mở và đóng panel

Đưa chuột tới cạnh màn hình đã chọn để mở panel, hoặc dùng **Ctrl + Space** nếu chưa đổi phím tắt trong Cài đặt.

- Dùng nút ghim để giữ panel mở.
- **Ctrl + F** đưa con trỏ vào phần tìm kiếm.
- **Esc** thoát thao tác hiện tại hoặc xóa/đóng tìm kiếm trước; khi không còn thao tác đó, Esc thu panel.
- Thu panel không thoát ứng dụng. Để đóng hẳn, chọn **Thoát** từ biểu tượng Nepply trong khay hệ thống; nếu cần, mở vùng biểu tượng ẩn của Windows.

Khi game ẩn chuột để điều khiển nhân vật, Nepply không tự mở từ cạnh. Khi chuột hiện lại, chẳng hạn ở menu game, bạn có thể dùng panel. Hành vi này phụ thuộc cách từng game quản lý con trỏ.

## Mở mục Desktop và chỉnh lưới

File gốc vẫn ở vị trí cũ trên Desktop. Nhập tên để lọc, chọn mục để mở theo chế độ nhấp đã cấu hình, hoặc nhấp chuột phải để dùng menu Windows. Các thao tác xóa/đổi tên trong menu đó tác động lên mục thật, không phải bản sao.

Trong **Cài đặt**, dùng bảng chấm tròn để chọn số **cột × hàng**:

- Panel cạnh trái/phải: tối đa 5 cột.
- Panel cạnh trên/dưới: tối đa 3 hàng.
- Nếu có nhiều mục hơn vùng hiển thị, cuộn để xem phần còn lại.

Kích thước panel có thể điều chỉnh theo tỷ lệ màn hình. Chọn bố cục vừa đủ đọc tên và thao tác thuận tiện; không cần mở panel hết màn hình để chứa tất cả icon.

## Thử giao diện trước khi lưu

Bạn có thể đổi cạnh, kích thước, lưới, màu, chế độ sáng/tối và hiệu ứng trong Cài đặt. Panel phản ánh các tùy chọn có hỗ trợ xem trước để bạn quan sát.

| Nút | Tác dụng |
| --- | --- |
| Apply | Lưu lựa chọn hiện tại, giữ Cài đặt mở. |
| OK | Lưu lựa chọn hiện tại rồi đóng Cài đặt. |
| Hủy hoặc nút đóng | Bỏ thay đổi chưa lưu, trở lại trạng thái đã lưu gần nhất. |

Nếu đã bấm Apply, Hủy sau đó chỉ bỏ những thay đổi mới kể từ lần Apply đó. Các thao tác hệ thống như tự khởi động được áp dụng khi lưu, không phải hiệu ứng xem trước.

Có thể bật/tắt neon và ánh kim. Nếu muốn giao diện tĩnh, giảm hoặc tắt chuyển động trong tùy chọn giao diện. Dùng xuất/nhập bố cục để lưu cách trình bày; file bố cục không phải bản sao lưu toàn bộ cấu hình và Desktop.

## Theo dõi hệ thống

Các thẻ hiển thị mức sử dụng CPU, RAM, GPU/iGPU và tốc độ mạng. Nhiệt độ, xung nhịp chỉ có khi hệ thống cung cấp dữ liệu phù hợp; trên nhiều máy Intel/AMD nhiệt độ CPU không khả dụng vì Nepply không cài driver ([giải thích](TROUBLESHOOTING.md#vì-sao-nhiệt-độ-cpu-hiện-chưa-khả-dụng)). Nepply không hiển thị nhiệt độ bo mạch chủ hay tốc độ quạt trong bản hiện tại.

Trong Cài đặt, có thể tắt monitor hoặc tốc độ mạng khi không cần. Phần nâng cao cho phép chỉnh chu kỳ cập nhật và tạm ngừng đo khi panel ẩn. Chọn đúng adapter mạng nếu máy có nhiều kết nối hoặc VPN.

Tốc độ tải xuống/tải lên là lưu lượng của adapter, không phải bài kiểm tra tốc độ Internet. Không cần tắt antivirus/anti-cheat hoặc cài driver lạ để có thêm số đo.

## Khởi động cùng Windows

Bật tùy chọn tự khởi động trong Cài đặt rồi bấm **Apply** hoặc **OK**. Windows có thể quản lý hoặc vô hiệu hóa mục này trong phần ứng dụng khởi động. Với bản portable, giữ nguyên vị trí thư mục ứng dụng; nếu di chuyển, mở lại app ở vị trí mới và lưu lại tùy chọn tự khởi động.

## Cập nhật

Mở **Cài đặt → Ứng dụng → Kiểm tra cập nhật** để chủ động kiểm tra. Khi có bản phù hợp, cửa sổ cập nhật hiển thị phiên bản và ghi chú để bạn quyết định cập nhật hay để sau. Nepply hiện không tự kiểm tra định kỳ trong nền.

**Giới hạn hiện tại:** tự cài cập nhật EXE chưa được bật cho phát hành vì chưa hoàn tất cấu hình ký số và nghiệm thu. Portable chưa tự thay thế file ứng dụng. Không coi bản build thử là kênh cập nhật chính thức; xem [Releases](https://github.com/minhquang0607/Nepply/releases) khi bản tải được công bố.

## Gỡ ứng dụng và dữ liệu

- **Bản Setup:** thoát Nepply, kiểm tra icon Desktop đã trở lại nếu từng cho app ẩn chúng, rồi gỡ trong danh sách ứng dụng đã cài của Windows.
- **Bản portable:** tắt tự khởi động và lưu, thoát Nepply, kiểm tra Desktop rồi xóa riêng thư mục đã giải nén.

Cấu hình có thể được giữ lại để dùng khi cài lại. Nếu muốn đặt lại dữ liệu, xem [xử lý sự cố](TROUBLESHOOTING.md). Đừng xóa dấu khôi phục khi icon Desktop vẫn bị ẩn.
