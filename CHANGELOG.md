# 📝 Những thay đổi của Nepply

[English](CHANGELOG.en.md) · **Tiếng Việt** · [Trang chính](README.vi.md)

## 2.0.0-rc.2 — 10/11/2026

Bản phát hành thử thứ hai, phát hành ngày **10/11/2026**, chưa ký số.

### Nâng cấp từ 2.0.0-rc.1

- **Hãy gỡ `2.0.0-rc.1` trước khi cài bản này.** Khóa phiên chạy và dấu khôi phục icon Desktop đã đổi sang tên Nepply, nên bản mới không nhận ra rc.1 đang chạy hoặc icon do rc.1 ẩn. Cấu hình ở `%APPDATA%\Nepply` vẫn dùng được nếu bạn không chọn xóa dữ liệu khi gỡ.
- Bỏ phần chuyển đổi từ tên sản phẩm cũ: cấu hình, mục khởi động, shortcut và file bố cục của đời trước không còn được nhập.

### Bản Microsoft Store

- Chuẩn bị bản Microsoft Store: Microsoft ký gói sau khi duyệt, cập nhật qua Store. Bản này vẫn ẩn được icon Desktop, nhưng ẩn trực tiếp cửa sổ icon thay vì đổi cài đặt Windows, nên gỡ app không để lại gì. Bản Store không kiểm tra cập nhật qua GitHub. Setup và portable giữ nguyên.

### Bảo mật

- Cập nhật bộ .NET đóng kèm lên 10.0.14, gồm các bản vá bảo mật của Microsoft từ tháng 10/2026. Bản rc.1 dùng 10.0.12.

### Độ tin cậy

- Kiểm tra cập nhật hằng ngày thất bại khi có mạng sẽ thử lại sau 12 giờ thay vì đợi một ngày. Hai lần thất bại liên tiếp thì Nepply báo một lần ở khay hệ thống; bấm vào để mở Cài đặt cập nhật. Khi Windows báo không có mạng, Nepply không gửi gì và không tính là thất bại.
- Hai cảm biến trùng mã phần cứng không còn làm hỏng lượt đọc số đo: Nepply giữ cảm biến đầu tiên.
- Nếu tiến trình bảo vệ icon Desktop đang ẩn dừng đột ngột, panel giờ có thông báo và log ghi lại kèm mã thoát. Các lần ẩn hoặc khôi phục icon thất bại cũng được ghi log.

### Desktop và thao tác

- Một mục Desktop không đọc được không còn chặn cả danh sách: Nepply bỏ qua mục đó, vẫn hiện các mục khác và ghi lỗi vào log. Nếu mất cả một vị trí Desktop, danh sách cũ vẫn giữ trên màn hình như trước.

## 2.0.0-rc.1 — 02/11/2026

Bản phát hành thử công khai đầu tiên, phát hành ngày **02/11/2026**, chưa ký số. Danh sách dưới đây gom các thay đổi trong giai đoạn phát triển.

### Desktop và thao tác

- Mở panel từ cạnh màn hình hoặc phím tắt, ghim khi cần giữ mở. Không còn phím mở panel đặt sẵn vì các tổ hợp dễ bấm đều trùng ứng dụng khác; bạn tự đặt trong Cài đặt → Phím tắt và xóa được như mọi phím khác. Phím đã lưu từ bản trước được giữ nguyên.
- Tìm kiếm và mở các mục trên Desktop, sử dụng menu chuột phải của Windows.
- Mở shortcut trỏ tới ổ mạng mất kết nối không còn làm panel đơ.
- Chỉnh lưới bằng số cột × hàng, với kích thước panel thích ứng theo màn hình.
- Hạn chế tự mở panel khi trò chơi ẩn con trỏ để điều khiển nhân vật.
- Panel đã ghim vẫn mở khi bạn gõ phím ở ứng dụng khác (lúc Windows tạm ẩn con trỏ).
- Menu chuột phải của mục Desktop đóng lại khi bấm ra ngoài, giống menu ở khay hệ thống.
- Phím tắt trong tệp cài đặt phải có Ctrl hoặc Alt; tổ hợp chỉ có Shift sẽ trở về chưa đặt để không chặn việc gõ chữ bình thường.

### Giao diện và cá nhân hóa

- Giao diện sáng, tối hoặc theo hệ thống (mặc định theo Windows); bảng màu, hiệu ứng neon và ánh kim tương tác với chuột.
- Xem trước trên panel khi chỉnh cài đặt; Áp dụng để lưu, OK để lưu và đóng, Hủy để bỏ thay đổi chưa lưu.
- Hỗ trợ tám ngôn ngữ: Việt, Anh, Nhật, Đức, Trung giản thể, Nga, Pháp và Thái.
- Lần chạy đầu dùng ngôn ngữ hiển thị của Windows (tiếng Anh nếu Nepply chưa có ngôn ngữ đó) và màn chào cho chọn lại ngôn ngữ. Cài đặt cũ giữ nguyên ngôn ngữ đã dùng; Đặt lại cũng không đổi ngôn ngữ.
- Mục **Phím tắt** trong Cài đặt: đổi được mọi phím (mở/đóng, tìm, làm mới…) và thêm phím toàn cục mới để mở panel kèm tìm, ẩn/hiện icon Desktop, ghim panel và mở Cài đặt (mặc định chưa đặt).
- Cài đặt, menu khay và chế độ sửa bố cục chỉ còn tên chức năng; hướng dẫn sử dụng gom vào màn **Hướng dẫn**, mở lại được từ khay hoặc Cài đặt.
- Cỡ icon (nhỏ, vừa, lớn) lưu trong cài đặt hoặc layout nhập vào giờ được áp dụng; mặc định vẫn là cỡ vừa.

### Theo dõi hệ thống

- Các thẻ CPU, RAM, GPU/iGPU và tốc độ mạng; có thể bật/tắt theo nhu cầu.
- Phân biệt GPU/iGPU theo thông tin hệ thống; cảm biến không hỗ trợ có thể không hiển thị số đo.
- Mở panel là có số ngay: CPU, RAM và mạng hiện sau khoảng 0,2 giây, nhiệt độ và GPU giữ số gần nhất rồi cập nhật khi có số mới. Khi panel ẩn, tiến trình cảm biến được giữ 10 phút mà không đọc số liệu và khởi động sớm khi chuột tới gần cạnh; tắt theo dõi hoặc tắt cảm biến vẫn dừng nó ngay.
- Panel mở được khi di chuột từ góc thanh tác vụ lên dọc cạnh, và phản hồi nhanh hơn khi chuột tới cạnh.
- Mã nhận dạng GPU, màn hình hoặc mạng quá dài trong tệp cài đặt sẽ trở về chế độ tự chọn, thay vì làm cảm biến kẹt ở trạng thái đang kết nối lại.
- Khi không đọc được nhiệt độ, dấu — hiện màu nhạt và rê chuột sẽ thấy lý do.
- Nút **Sao chép chẩn đoán** (Cài đặt → Ứng dụng) chép phiên bản, Windows và thông tin cảm biến để dán khi báo lỗi; không chứa đường dẫn hay tên tài khoản.
- Tiến trình bảo vệ icon Desktop hiện tên rõ ràng trong Task Manager.

### Cập nhật và khôi phục

- Cửa sổ kiểm tra cập nhật hiển thị phiên bản, nội dung mới, tiến độ và lựa chọn thử lại khi gặp lỗi.
- Tùy chọn **Tự kiểm tra cập nhật mỗi ngày**, mặc định bật, chọn được ngay ở màn hình chào, chỉ báo ở khay hệ thống khi có bản mới.
- Công tắc tự khởi động trong Windows đang tắt thì Cài đặt báo ngay dưới ô chọn và chỉ chỗ bật lại; bật xong cảnh báo tự mất mà không cần mở lại app. Công tắc cũ còn sót từ lần cài trước không còn làm tắt mục mới.
- Khi bản đang dùng chưa tự cài được cập nhật (portable hoặc chưa ký số), cửa sổ cập nhật có nút **Mở trang tải** tới trang phát hành chính thức trên GitHub.
- Ghi chú phát hành trong cửa sổ cập nhật hiển thị dạng văn bản gọn thay vì ký hiệu Markdown thô.
- Bộ cài hỏi ngôn ngữ khi mở, chọn sẵn theo Windows: Việt, Anh, Nhật, Đức, Trung giản thể, Nga, Pháp và Thái. App dùng luôn ngôn ngữ đã chọn trong lần chạy đầu.
- Bộ cài có giao diện tối và hình ảnh Nepply. Trang thông tin chỉ hiện đúng ngôn ngữ đã chọn. Ô khởi động cùng Windows được chọn sẵn ở lần cài đầu, và bản cập nhật không đổi lựa chọn đó.
- Khi gỡ, bộ cài hỏi có xóa luôn cài đặt và nhật ký không (mặc định giữ lại), dọn thư mục giải nén trong Temp và bỏ công tắc tự khởi động của mục đã gỡ.
- Nút **Báo lỗi** (Cài đặt → Ứng dụng) chép chẩn đoán vào clipboard và mở form báo lỗi GitHub đã điền sẵn phiên bản Nepply và Windows.
- Hỗ trợ khôi phục icon Desktop khi ứng dụng kết thúc bất thường; vẫn có giới hạn nếu tiến trình hỗ trợ cũng bị dừng hoặc Explorer không hoạt động.

### Giới hạn hiện tại

- Chưa có bản phân phối ký số chính thức.
- Tự cài cập nhật EXE chưa được bật cho phát hành; portable chưa tự thay thế tệp ứng dụng.
- Chưa công bố bản Microsoft Store.
