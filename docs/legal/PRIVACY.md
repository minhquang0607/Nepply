# Quyền riêng tư của Nepply

Cập nhật: 17/10/2026 · Nepply 1.0.0

Nhà phát triển: Minh Quang  
Hỗ trợ: https://github.com/minhquang0607/Nepply/issues

## Tóm tắt

Nepply xử lý dữ liệu trên máy để hiển thị icon Desktop và thông số hệ thống. Mã ứng dụng hiện tại không có analytics, quảng cáo, crash uploader, tài khoản đám mây hoặc máy chủ đồng bộ. Dữ liệu không tự động được gửi cho Minh Quang.

## Dữ liệu được xử lý cục bộ

- Tên, đường dẫn, thuộc tính và hình đại diện của mục nằm trực tiếp trong Desktop người dùng/Public; các system icon Desktop đang bật.
- Metadata shortcut như target, arguments và working directory được đọc cục bộ khi cần phân biệt hoặc mở đúng shortcut. Nepply không đọc nội dung tài liệu chỉ để dựng lưới.
- Tên/định danh thiết bị và số đo CPU, RAM, GPU/iGPU cùng bộ đếm mạng khi chức năng tương ứng được bật.
- Bộ đếm mạng chỉ là số byte gửi/nhận của adapter được chọn. Nepply không đọc nội dung gói tin, địa chỉ trang web hoặc lịch sử duyệt web.
- Từ khóa tìm trong panel/cài đặt được dùng trong bộ nhớ để lọc giao diện, không được lưu như lịch sử tìm kiếm.

Helper Desktop và cảm biến trao đổi với giao diện qua named pipe cục bộ giới hạn cho tài khoản Windows hiện tại. Đây không phải kết nối Internet.

## Dữ liệu được lưu

Cấu hình ở `%APPDATA%/Nepply` có thể chứa lựa chọn giao diện, hotkey, ID màn hình/network/GPU, thứ tự widget và đường dẫn các icon đã sắp xếp. File xuất `.nepply-layout` chỉ chứa phần trình bày; không xuất đường dẫn icon, ID thiết bị, tự khởi động hoặc trạng thái ẩn Desktop. Lần chạy đầu có thể nhập cấu hình từ `%APPDATA%/HoverBox`; bản gốc không bị xóa hoặc ghi đè. File bố cục `.hoverbox-layout` vẫn nhập được.

Nhật ký ở `%LOCALAPPDATA%/Nepply/Logs`. Dấu khôi phục Desktop giữ đường dẫn `%LOCALAPPDATA%/HoverBox/desktop-visibility.lease` để tương thích bản cũ. Nhật ký lỗi có thể chứa tên thiết bị, đường dẫn, tên tài khoản xuất hiện trong đường dẫn và nội dung exception. Mỗi vai trò tiến trình giữ một file tối đa khoảng 5 MiB cùng tối đa ba bản xoay vòng, khoảng 20 MiB mỗi vai trò. Queue log trong bộ nhớ có giới hạn và bỏ bản ghi cũ khi đầy.

Monitor dùng số đo mới nhất, không ghi lịch sử cảm biến thành cơ sở dữ liệu. Báo cáo chẩn đoán cảm biến chỉ được tạo khi người dùng mở cửa sổ; thao tác Sao chép mới đặt nội dung cho phép vào clipboard. Báo cáo loại trừ đường dẫn Desktop, tên mạng, nhãn ổ, ID thiết bị và log thô nhưng vẫn có tên phần cứng để hỗ trợ chẩn đoán.

## Quyền truy cập và thay đổi hệ thống

Nepply chạy dưới tài khoản người dùng thông thường. Ứng dụng có thể đọc Desktop, đăng ký hotkey, thêm mục tự khởi động khi người dùng bật, và yêu cầu Explorer ẩn/hiện icon Desktop. Dấu khôi phục được ghi trước thao tác ẩn để lần chạy sau có thể khôi phục. Nepply không tự cài driver, không tự nâng quyền và không yêu cầu tắt Vanguard, antivirus, Secure Boot hoặc Memory Integrity.

Mở file, shortcut, URL hoặc menu Shell là hành động do người dùng yêu cầu; chương trình/website được mở có chính sách riêng ngoài phạm vi Nepply.

## Xóa và chia sẻ

Khi Nepply đã đóng và icon Desktop đã được khôi phục, người dùng có thể xóa thư mục cấu hình và Logs để đặt lại dữ liệu cục bộ. Việc gỡ cài đặt hiện giữ cấu hình để hỗ trợ cài lại. Không xóa `desktop-visibility.lease` nếu icon Desktop đang bị ẩn; hãy mở lại Nepply hoặc bật lại icon trong Windows trước.

Nếu người dùng tự gửi log, ảnh chụp hoặc báo cáo qua GitHub Issues, việc truyền dữ liệu do người dùng chủ động và còn chịu chính sách của GitHub. Hãy che đường dẫn, tên tài khoản và dữ liệu cá nhân; không gửi mật khẩu, token, khóa ký số hoặc tài liệu riêng tư.

## Kiểm tra và tải cập nhật

Khi bạn bấm **Kiểm tra cập nhật**, bản EXE/portable gửi yêu cầu HTTPS đến GitHub API của repo `minhquang0607/Nepply` để đọc phiên bản, ghi chú phát hành và thông tin bộ cài. Không gửi cấu hình, danh sách Desktop, số đo phần cứng hoặc log. GitHub vẫn nhận địa chỉ IP và User-Agent `Nepply-Updater/1.0` theo cơ chế kết nối thông thường. Phiên bản hiện tại được so sánh cục bộ.

Chỉ khi bạn chọn **Cập nhật ngay**, bản cài EXE mới tải Setup từ GitHub/CDN GitHub vào `%LOCALAPPDATA%/Nepply/Updates`. Windows có thể kết nối tới dịch vụ chứng thư để kiểm tra độ tin cậy/thu hồi chữ ký. Việc tự cài bị khóa nếu chưa cấu hình nhà phát hành tin cậy. Bản Store dùng cơ chế của Microsoft Store; portable chưa tự thay file.

Hiện không có kiểm tra nền theo lịch, analytics hoặc crash upload. Đóng cửa sổ cập nhật hủy yêu cầu đang chờ/tải; xác minh chữ ký Windows đang chạy có thể cần thêm thời gian để kết thúc. Gói tải bị hủy/lỗi được dọn theo khả năng truy cập; Setup đã bắt đầu chạy được giữ lại để tránh xóa file nó đang cần. Lần tải tiếp theo dọn các gói cache do updater tạo đã cũ hơn 7 ngày, không xóa nội dung lạ hoặc file đang khóa.
