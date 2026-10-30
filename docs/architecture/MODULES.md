# Ranh giới module Nepply

Kiến trúc hiện hành của **2.0.0**, rà soát 17/10/2026. Xem [cây thư mục](STRUCTURE.md). Đây là bản đồ trách nhiệm và giới hạn, không phải nhật ký refactor hay chứng nhận runtime.

## Nguyên tắc

Nepply là modular monolith WPF, không phải hệ thống plugin. Tách theo trách nhiệm và vòng đời; không tạo project, timer hoặc dependency chỉ để chia lớp. Namespace tương thích như Services/Views/Modules không có nghĩa là assembly độc lập.

## Desktop: đọc → đối chiếu → trình bày

- `Discovery` đọc Desktop/Shell và icon; `DesktopScanOptions` chụp cấu hình cho mỗi lượt đọc.
- `State` giữ catalog/snapshot; `Watching` gom thay đổi filesystem và Shell. `DesktopUpdateCoordinator` sở hữu subscriptions và yêu cầu refresh khi resume.
- `IDesktopCatalog` cung cấp snapshot/trạng thái; `IDesktopActions` cung cấp refresh, mở mục và menu Shell. `DesktopIconService` thực hiện hai vai trò này.
- `DesktopIconViewModel` lọc/sắp xếp và chuyển thông báo lên UI. `DesktopIconCollection` reconcile/batch; `IconGridPanel` sở hữu virtualization và scroll anchor; `IconGridGeometry` dùng chung hình học arrange/drop.
- `ShellContextMenu` trong Platform/Windows/Shell sở hữu COM/native resource cho từng lần mở menu. UI không tự giải phóng resource của dịch vụ.

Snapshot membership ổn định không có nghĩa model chứa ImageSource là immutable sâu. Không coi việc có watcher là bằng chứng mỗi thay đổi chỉ tốn O(1); phải đo scan/reconcile khi có event storm.

## Desktop visibility và shutdown

`Visibility` quản lý lease/journal và kết nối Guardian. `Nepply.DesktopGuardian` là helper riêng cho phục hồi, không phải dịch vụ tự đăng ký chạy thường trực. Định danh HoverBox còn giữ ở mutex/journal để các bản không cùng chiếm Desktop.

`WindowServices` sở hữu dịch vụ Desktop/monitor, được chuyển quyền sở hữu cho MainWindow. Host tháo subscriptions/view trước khi dispose scope. `CleanupSequence` tiếp tục cleanup khi một bước lỗi. Không khẳng định phục hồi được nếu cả app/helper bị dừng hoặc Explorer không hoạt động.

## Panel và tương tác

`PanelGeometry` tính bounds từ work area vật lý và DPI; MainWindow chọn màn hình và áp dụng WPF/Win32 placement. `PanelDimensions` giữ quy tắc kích thước; các gate trong `Interaction` quyết định mở cạnh, trạng thái con trỏ và tự thu.

`LayoutEditSession` giữ snapshot/dirty/commit/cancel cho phiên bố cục. UI chịu trách nhiệm thông báo lỗi và áp dụng layout. Không nhầm phiên này với lựa chọn lưới icon trong Settings.

Con trỏ bị ẩn/chặn trong game được xét trước tự mở cạnh. Không hook hoặc sửa tiến trình game. Fixture tọa độ/DPI không thay thế thử mixed-DPI, hotplug hay mọi game.

## Settings

`SettingsEditSession` giữ draft và lưu candidate trước khi công bố vào target; `SettingsChangeSet` phân loại thay đổi cho preview và commit. Apply lưu và giữ mở; OK lưu rồi đóng; Cancel chỉ bỏ phần chưa lưu.

`SettingsStore` phụ trách persistence/backup/migration. SettingsWindow trình bày controls và lỗi; preview không được tạo scanner hoặc collector thứ hai. Startup/hotkey/Desktop visibility chỉ áp dụng khi lưu theo nhóm thay đổi. Công cụ chỉ dành cho phát triển không phải tính năng Release cho người dùng.

## Telemetry

Provider → `HardwareMonitorService` → snapshot/ViewModel → `MonitorPanel` và `MetricReadingView`. Collector không phụ thuộc cửa sổ; view không tự query phần cứng. `MetricValue` kiểm tra số đo; provider cảm biến có ranh giới process/IPC và lifetime riêng.

Phạm vi hiện tại là CPU, RAM, GPU/iGPU và tốc độ mạng. Không có thẻ chi tiết, nhiệt độ bo mạch chủ hoặc RPM. Dữ liệu thiếu không được giả thành số đo hợp lệ. Không tự cài driver hoặc yêu cầu tắt anti-cheat.

## Search và appearance

`SearchSession` sở hữu trạng thái/timer; `SearchReveal` trình bày clip/translation trên dispatcher của host. Host cung cấp predicate giữ mở và dispose session khi đóng. Settings search dùng overlay cùng visual tree, không tạo cửa sổ hay nguồn dữ liệu thứ hai.

`Presentation/Appearance` chứa theme, token và template. `AccentPalette` tính chính sách màu; `ThemeService` cập nhật WPF resources. `MetallicSurface` và các control vật liệu quản lý effect/clock/tương tác; lifecycle phải tháo clocks/subscriptions khi unload. Tắt animation/high contrast phải được tôn trọng, kể cả trong fixture.

Localization thuộc presentation và Resources/Strings.json, hỗ trợ tám ngôn ngữ. Thay ngôn ngữ không khởi động lại sampler. Native controls giữ semantics bàn phím/automation; render test không thay thế nghiệm thu Narrator.

## Updates

`Features/Updates` giữ so sánh phiên bản, nguồn/signer policy, tải giới hạn và Authenticode. UI ở `Features/Updates/Presentation/UpdateWindow.xaml`, không thực thi release notes.

Settings bàn giao gói đã xác minh cho MainWindow để thoát bình thường; Inno chờ app, phục hồi Desktop trước copy và khởi động lại. Portable chưa tự thay file; MSIX dùng Store. Chưa cấu hình signer production, chưa nghiệm thu signed A → B. Xem [cập nhật EXE](../release/EXE_UPDATES.md).

## Giới hạn và refactor có ích

MainWindow và DesktopIconService vẫn điều phối nhiều trách nhiệm. Chỉ tách thêm khi giảm coupling hoặc tạo test boundary thực sự; không rewrite toàn bộ hay thêm DI container theo hình thức. Giữ ownership, exception cleanup, migration và hành vi người dùng khi refactor.

Test nằm ở `tests/Nepply.Tests`; đây là executable fixture, không phải suite dotnet test. CI/build xanh không chứng minh độ ổn định dài hạn, tài nguyên native trên mọi máy, hay nghiệm thu bộ cài đã ký. Các báo cáo QA lịch sử nằm ngoài repository sản phẩm.
