# Cấu trúc Nepply

Repository chứa mã sản phẩm, kiểm thử, công cụ phát triển và tài liệu hiện hành. Nhật ký và đầu ra QA được lưu ngoài repository.

Xem [bản đồ tài liệu](../README.md) để chọn hướng dẫn người dùng, phát triển hoặc phát hành. Telemetry hiện hỗ trợ CPU/RAM/GPU/mạng; không có nhiệt độ bo mạch chủ hoặc RPM.

Nepply là modular monolith WPF: một assembly ứng dụng, nhóm mã theo tính năng và vòng đời. Mở `Nepply.slnx` ở root để làm việc với ứng dụng, bộ kiểm thử và công cụ chẩn đoán.

| Thư mục | Trách nhiệm |
|---|---|
| `src/Nepply/Composition` | Lắp ghép dịch vụ và cleanup |
| `src/Nepply/Features/Desktop` | Discovery, catalog, watcher, visibility và giao diện icon |
| `src/Nepply/Features/Panel` | Cửa sổ chính, vị trí, bố cục và tương tác cạnh |
| `src/Nepply/Features/Search` | Phiên tìm kiếm, chuẩn hóa từ khóa và trình bày |
| `src/Nepply/Features/Settings` | Draft, persistence, onboarding và cửa sổ cài đặt |
| `src/Nepply/Features/Telemetry` | Provider, sampling, snapshot và giao diện monitor |
| `src/Nepply/Features/Updates` | Feed phiên bản, tải có giới hạn, xác minh chữ ký và UI cập nhật; không polling nền |
| `src/Nepply/Presentation` | Appearance, control dùng chung, converter và tray |
| `src/Nepply/Platform/Windows` | Win32, COM menu, màn hình, hotkey, startup và process |
| `src/Nepply/Diagnostics` | Logging |
| `src/Nepply.DesktopGuardian` | Helper không WPF, tồn tại theo phiên ẩn Desktop; chờ owner thoát và phục hồi, không đăng ký persistence |
| `tests/Nepply.Tests` | Chương trình kiểm thử và fixture WPF |
| `tools/SensorDumper` | Công cụ chẩn đoán cảm biến độc lập; không thuộc payload ứng dụng |
| `packaging/inno` | Cấu hình bộ cài chính, ảnh, thông điệp và trang thông báo theo từng ngôn ngữ |
| `scripts` | Kiểm tra build, dependency, notice, manifest và ký mã |
| `docs` | Hướng dẫn người dùng, xử lý sự cố, ảnh minh họa, architecture, release và legal |
| `artifacts` | Kết quả build/test có thể tái tạo, không commit |

## Định danh và tương thích

Phiên bản hiện hành là `2.0.0-rc.1`. Việc nhóm file theo feature không đồng nghĩa mọi lớp đã tách trách nhiệm hoàn chỉnh; xem các boundary và giới hạn trong [MODULES.md](MODULES.md).

Namespace gốc/assembly, solution, project và source dùng tên Nepply. Các namespace `Services`, `Views`, `Modules` chưa phải boundary assembly riêng. Khóa phiên chạy và journal Desktop giữ định danh HoverBox để tương thích; không đổi chúng chỉ để đồng bộ thương hiệu hoặc số phiên bản.

## Quy tắc phát triển

- Mã mới đặt tại feature sở hữu nó. Chỉ đưa vào Presentation/Platform khi có trách nhiệm dùng chung rõ ràng.
- Logic thuần không giữ Window, Dispatcher hay service native. Adapter UI chịu trách nhiệm binding và marshal.
- Dịch vụ có một owner chịu trách nhiệm Start/Stop/Dispose; ưu tiên Start tường minh và không làm I/O chặn trong constructor. `DesktopWatcherService` hiện khởi động scope từ constructor để giữ contract cũ, nhưng chỉ lên lịch worker; owner dispose scope và có thể đợi `Completion` ngoài UI khi cần nghiệm thu cleanup.
- Contract nhỏ theo consumer; không dùng service locator hoặc event bus toàn ứng dụng để che dependency.
- Giữ một pipeline dữ liệu cho từng nguồn; UI preview không khởi tạo scanner/collector thứ hai.
- Test ở project riêng. Build boundary chỉ chấp nhận mã/resource production dưới `src/Nepply`.
- Không thêm project, timer, helper process hoặc dependency chỉ để chia thư mục.

## Di chuyển tài nguyên

WPF resource dictionary hiện ở `Presentation/Appearance`; App.xaml dùng đường dẫn mới. XAML và code-behind luôn được chuyển cùng nhau. Icon ứng dụng giữ URI `Resources/icon.ico` trong assembly. Tài liệu legal được project link từ `docs/legal`, vẫn xuất ra gốc payload với tên cũ.

## Dọn đầu ra


Không xóa source bằng `git clean` khi chưa có baseline được theo dõi. `bin/obj` chỉ dọn khi không có build hoặc tiến trình đang sử dụng. Trước khi bỏ binary của một đợt QA, giữ báo cáo và ảnh cần thiết trong archive đã kiểm tra; giữ một portable baseline để đối chiếu.

Thư viện tham khảo và nghiên cứu nằm ngoài repository sản phẩm; không phải dependency build.
