# 🛠️ Xử lý sự cố

[English](TROUBLESHOOTING.en.md) · **Tiếng Việt** · [Trang chính](../README.vi.md) · [Hướng dẫn sử dụng](USER_GUIDE.md)

## Không mở được panel

1. Kiểm tra Nepply có đang chạy trong khay hệ thống không, kể cả vùng biểu tượng ẩn.
2. Thử **Ctrl + Space** nếu chưa đổi phím tắt; nếu ứng dụng khác dùng phím này, chọn tổ hợp khác trong **Cài đặt → Phím tắt**.
3. Kiểm tra cạnh và màn hình đã chọn. Sau khi tháo màn hình ngoài, mở lại Cài đặt để chọn màn hình còn dùng.
4. Nếu đang điều khiển nhân vật trong game và con trỏ bị ẩn, việc panel không tự mở là chủ đích. Thử lại khi con trỏ hiện trong menu.

## Panel tự thu khi gõ phím ở ứng dụng khác

Windows tạm ẩn con trỏ khi bạn gõ phím. Panel chưa ghim coi đó là ứng dụng khác đang dùng con trỏ nên tự thu. Ghim panel để giữ nó mở. Nếu panel đã ghim mà vẫn thu khi gõ phím, hãy báo lỗi kèm phiên bản Nepply.

## Không thấy một mục Desktop

Xóa từ khóa tìm kiếm và kiểm tra bộ lọc file ẩn/system icon. Nepply hiển thị các mục trực tiếp trên Desktop, không liệt kê toàn bộ nội dung mọi thư mục con. Nếu vừa thay đổi nhiều file, thử nút làm mới.

Nếu mục đã bị xóa hoặc đổi tên bên ngoài app, không thể mở bằng đường dẫn cũ. Kiểm tra lại trong File Explorer trước khi báo lỗi.

## Thiếu nhiệt độ hoặc số đo GPU

Không phải máy nào cũng cung cấp đủ cảm biến. Kiểm tra tùy chọn monitor/cảm biến và GPU đang chọn. Phần trăm sử dụng và nhiệt độ có thể đến từ các nguồn khác nhau, nên có phần trăm không đồng nghĩa chắc chắn có nhiệt độ.

### Vì sao nhiệt độ CPU hiện "chưa khả dụng"?

Đây là giới hạn đã biết, không phải lỗi cài đặt. Trên nhiều CPU Intel và AMD, nhiệt độ CPU chỉ đọc được qua driver cấp nhân (LibreHardwareMonitor dùng PawnIO), thường cần quyền quản trị. Nepply chạy bằng quyền người dùng thông thường và **không cài driver**, nên trên các máy này thẻ CPU vẫn hiện mức sử dụng và xung nhịp nhưng không có nhiệt độ.

- Một số máy AMD có đồ họa Radeon tích hợp (APU) đọc được nhiệt độ qua AMD ADL; nguồn này được ghi là *dự phòng* trong chi tiết thẻ.
- Nhiệt độ GPU thường đến từ API của driver đồ họa, nên có thể có nhiệt độ GPU dù thiếu nhiệt độ CPU.
- Không chạy Nepply bằng quyền quản trị và không cài driver lạ chỉ để có nhiệt độ. Dùng công cụ của hãng mainboard/CPU nếu cần theo dõi nhiệt độ CPU.

Không tắt Vanguard, antivirus hoặc bảo vệ Windows, và không tự cài driver để ép cảm biến hoạt động. Khi báo lỗi, gửi mẫu CPU/GPU, phiên bản Windows và trường dữ liệu bị thiếu. Nhiệt độ bo mạch chủ/RPM không thuộc tính năng hiện tại.

## Tốc độ mạng bằng 0 hoặc khác ứng dụng tải xuống

Kiểm tra adapter được chọn có phải kết nối đang dùng không, đặc biệt khi có VPN hoặc nhiều card mạng. Số đo là lưu lượng toàn adapter trong một khoảng lấy mẫu; khác đơn vị hoặc chu kỳ cập nhật có thể cho giá trị khác phần mềm tải file.

## Hiệu ứng hoặc chuyển động chưa mượt

Thử tắt bớt neon/ánh kim hoặc giảm chuyển động, tăng thời gian giữa các lần đo và bật tạm ngừng monitor khi panel ẩn. Nếu vẫn bị giật, báo lại thao tác gây lỗi, độ phân giải, scaling Windows, GPU và video ngắn đã che thông tin cá nhân.

## Icon Desktop chưa trở lại sau khi app đóng bất thường

Nepply có tiến trình hỗ trợ phục hồi, nhưng không bảo đảm nếu tiến trình đó cũng bị kết thúc hoặc Explorer không hoạt động.

1. Thử mở lại Nepply rồi chọn **Thoát** từ khay hệ thống.
2. Nếu icon vẫn ẩn, nhấp chuột phải lên Desktop → **View/Xem → Show desktop icons/Hiển thị biểu tượng màn hình nền**.
3. Không xóa dấu khôi phục hoặc dữ liệu app khi Desktop vẫn ở trạng thái sai. Gửi báo lỗi nếu tình trạng lặp lại.

## Windows chặn tệp hoặc cập nhật không thành công

Hiện chưa có bộ tải chính thức đã ký số. Không tắt SmartScreen, App Control hay antivirus để vượt chặn. Kiểm tra đúng nguồn tải và thông tin tại [Releases](https://github.com/minhquang0607/Nepply/releases).

Nếu kiểm tra cập nhật báo lỗi, kiểm tra kết nối Internet rồi thử lại. Repo chưa công khai hoặc chưa có release phù hợp cũng có thể khiến chức năng chưa dùng được. Chưa hỗ trợ tự cài production hoặc tự thay tệp portable ở giai đoạn hiện tại.

## Đặt lại cấu hình

Trong Cài đặt, chọn **Mặc định**, kiểm tra các tùy chọn rồi bấm **Áp dụng** hoặc **OK** để lưu. Nếu chưa muốn lưu, chọn **Hủy**.

Nếu cần sao lưu trước, thoát app rồi sao chép `%APPDATA%\Nepply\settings.json` sang nơi riêng. Không chỉ xóa file này để đặt lại: app có thể phục hồi từ bản `.bak` hoặc nhập cấu hình HoverBox cũ. Nếu Cài đặt không mở được, hãy báo lỗi trước khi tự xóa các file khôi phục.

Nhật ký ở `%LOCALAPPDATA%\Nepply\Logs`. Không cần xóa log để đặt lại giao diện. Dấu khôi phục Desktop cũ ở `%LOCALAPPDATA%\HoverBox\desktop-visibility.lease`; không xóa file này khi icon chưa được phục hồi. Xem [quyền riêng tư](legal/PRIVACY.md) để biết dữ liệu nào được lưu và việc nhập cấu hình HoverBox cũ.

## Báo lỗi hữu ích và an toàn

[Tạo issue](https://github.com/minhquang0607/Nepply/issues) với:

- Phiên bản Nepply và Windows; dùng Setup hay portable.
- Các bước tái hiện, điều bạn mong đợi và kết quả thực tế.
- Mức độ lặp lại; ảnh/video nếu giúp mô tả lỗi.
- Độ phân giải/scaling cho lỗi hiển thị, mẫu phần cứng cho lỗi monitor.

Trước khi gửi ảnh hoặc log, che tên tài khoản, đường dẫn cá nhân, tên file riêng tư và dữ liệu nhạy cảm. Không gửi toàn bộ thư mục cấu hình/log một cách mặc định. Với lỗ hổng bảo mật, dùng [kênh liên hệ riêng](legal/SECURITY.md).
