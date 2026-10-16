# Ranh giới module Nepply

Sau lần sắp xếp 30/09/2026, vị trí file đã chuyển sang `src/Nepply/Features`, `Presentation`, `Composition` và `Platform/Windows`. Xem [STRUCTURE.md](STRUCTURE.md). Tên module và namespace trong mô tả dưới đây được giữ để truy vết; các chuỗi đường dẫn cũ mô tả vị trí trước chuyển đổi.

## Nguyên tắc

Tách theo trách nhiệm và vòng đời, không tạo plugin loader, service locator hoặc một project cho mỗi lớp. Các module hiện có thể cùng assembly WPF; ranh giới phải rõ trước khi cân nhắc tách assembly. Không di chuyển hàng loạt source chỉ để đổi cây thư mục.

## Cập nhật EXE — 15–16/10/2026

`Features/Updates` giữ so sánh phiên bản, chính sách nguồn/signer, tải giới hạn và xác minh Authenticode. `Presentation/UpdateWindow.xaml` cùng code-behind chỉ trình bày trạng thái, tiến độ, xác nhận và cancellation. Settings bàn giao gói đã xác minh cho MainWindow để thoát bình thường; Inno chịu trách nhiệm chờ app, phục hồi Desktop trước copy và khởi động lại. Không cho release notes hoặc feed thay pin signer/command line; không tự áp dụng cơ chế EXE cho portable/MSIX. Chi tiết và giới hạn nghiệm thu ở [EXE_UPDATES.md](../release/EXE_UPDATES.md).

## Bổ sung sau audit 03/10/2026

- `Features/Desktop/Presentation/DesktopIconCollection`: adapter collection chỉ dùng trên UI thread. Reconcile giữ phần đầu/cuối không đổi; batch lớn gửi một Reset. `DesktopIconViewModel` sở hữu selection identity, `IconGridPanel` sở hữu scroll anchor và container virtualization. Không đưa WPF collection vào scanner hoặc watcher.
- `Features/Desktop/Watching/DesktopWatcherService`: một worker sở hữu dictionary subscription; timer chỉ yêu cầu đối chiếu hoặc debounce. Filesystem probe và dispose watcher không chạy dưới khóa mà caller UI cần. `Dispose` ngăn công việc mới, cleanup tiếp tục sau worker đang chạy; `Completion` là điểm đợi ngoài UI.
- `Features/Telemetry/Models/MetricValue`: quy tắc thuần dùng chung kiểm tra số đo finite/trong giới hạn; provider không gọi lớp điều phối chỉ để validation.
- `Platform/Windows/Input/HotkeyGesture`: quy tắc VK và nhãn dùng chung cho settings/panel; không coi VK là mã ký tự với phím điều hướng.
- `Presentation/Localization/LocalizationService` và `Resources/Strings.json`: văn bản hiển thị thuộc presentation. Dialog dùng DynamicResource; ViewModel thông báo thay đổi chữ khi đổi ngôn ngữ, không đánh thức sampler. Event ngôn ngữ phải tháo khi đóng/cleanup.

Các thay đổi này không có nghĩa incremental Desktop đã hoàn tất. Theo dõi F02/F07 và nghiệm thu còn thiếu trong biên bản sửa (báo cáo nội bộ đã chuyển ngoài repository).

## Đã triển khai

### Search

`SearchSession` sở hữu trạng thái expanded và timer thu sau tương tác của một ô tìm kiếm. MainWindow và SettingsWindow cung cấp predicate giữ mở và callback trình bày. Query, filtering, focus và animation thuộc adapter của cửa sổ, không nằm trong module này.

`SearchReveal` là adapter WPF dùng chung cho clip/translation, không animate Width hay scale chữ. `Settle` hủy clocks và completion cũ khi host kết thúc vòng đời. Quy tắc vùng trống nằm ở `Views/SearchHitTest` vì phải biết các input riêng như ColorField; module Search không tham chiếu ngược tới cửa sổ hoặc control nghiệp vụ.

- Chỉ được gọi trên UI dispatcher sở hữu nó.
- Timer không chạy khi idle, sau một lần kiểm tra hoặc khi dispose.
- Hover mở không giành focus; query/focus giữ mở theo predicate của host.
- Click vùng trống yêu cầu host xóa query rồi thu, hủy cả yêu cầu timer phát sinh đồng bộ từ TextChanged.
- Dispose không gọi lại UI; host phải dispose trước khi bị hủy.
- Kiểm thử: `Tests/SearchSessionTests.cs` và các test tương tác/render hiện có.

SettingsWindow đặt hint/kết quả trong SearchOverlayLayer cùng visual tree. Canvas không đóng góp DesiredSize vào hàng form; lớp kết quả theo hàng/cột của SettingsScroll khi chuyển rail/tab, được giới hạn theo ActualWidth/ActualHeight. Một ListBox sở hữu phần cuộn; không tạo Popup/HWND hoặc danh sách thứ hai. Query trắng/rỗng thu cả layer. SearchSession/Reveal, chỉ mục SettingsSearch, focus cancellation và draft vẫn giữ trách nhiệm cũ. Đây là adapter presentation của Settings, chưa cần tách thêm service/control khi chưa có consumer thứ hai.

## Lộ trình, chưa triển khai hết

### Appearance: chính sách màu đã tách

`AccentPalette` nhận màu gốc và các nền theme, trả Source/Functional/OnFunctional. Không đọc registry, settings hay Application.Resources; `ThemeService` là adapter WPF. Source giữ màu người dùng, Functional phục vụ chữ/viền/fill hiện có, OnFunctional phục vụ chữ trên fill. Chưa tách mỗi vai trò text/stroke/fill thành token riêng khi không có nhu cầu khác nhau được kiểm chứng.

Màu chức năng được giữ nguyên nếu đủ tương phản; nếu không, tìm hữu hạn trên các ứng viên RGB làm sáng/tối và kiểm tra màu sau làm tròn. Chỉ chạy lúc đổi theme, không polling. Các kiểm tra tương phản dùng nền opaque đã khai báo, không bảo đảm mọi wallpaper dưới nền translucent hoặc mọi gradient trạng thái.

### Desktop: ranh giới presentation đã tách

- `IDesktopCatalog`: snapshot danh sách và trạng thái đọc, events có thể đến từ worker. ViewModel tự coalesce/marshal lên UI và bỏ callback của subscription cũ. Không có Shell command, quyền visibility hay Dispose trên contract đọc.
- `IDesktopActions`: Refresh, LaunchIcon, Shell context menu. Đây là boundary Windows-specific có HWND/điểm màn hình; không giả vờ là contract đa nền tảng.
- `DesktopScanState`: record trạng thái nằm cạnh contract, không bắt ViewModel phụ thuộc loại state của implementation.
- `DesktopIconService` hiện implement hai contract. WindowServices sở hữu dịch vụ; MainWindow sở hữu scope và cấp cùng instance vào hai vai trò. IconPanel không dispose dịch vụ và xóa tham chiếu command khi cleanup.
- `DesktopContractTests` dùng catalog/action fake không dựng scanner, visibility lease hoặc Shell COM. Chứng minh UI có thể thay nguồn dữ liệu mà không sửa logic trình bày.

Đây chưa phải tách xong scanner, visibility và menu khỏi DesktopIconService. Tách tiếp theo ownership/call chain, không chia project vì tên interface. DesktopIcon vẫn là model presentation có WPF ImageSource; contract không được gọi là domain thuần đa nền tảng.

### Desktop: hình học lưới

`DesktopScanOptions` là revision cấu hình bất biến được service giữ và capture một lần ở đầu mỗi lượt quét. Thư mục user/public, Shell enumeration và shortcut grouping cùng dùng revision đó; helper process nhận cùng bộ giá trị. Setter tương thích publish revision mới dưới lock, không thay revision đang chạy. Đây không phải transaction cho ba setter: nếu caller thay từng property thì lượt quét có thể capture một revision trung gian; caller vẫn phải yêu cầu refresh sau cập nhật như luồng hiện hữu. Chưa loại bỏ publication của một lượt cũ chỉ vì settings vừa đổi; follow-up scan coalesced vẫn xử lý cấu hình mới. Không thêm timer hay thread riêng cho options.

`DesktopCatalogStore` sở hữu membership theo copy-on-write: Read trả snapshot read-only hiện hành, không cấp phát mảng mỗi lần đọc; Reconcile chỉ publish collection mới khi references/thứ tự thực sự đổi. Adapter truyền thuật toán DesktopSnapshot hiện có để giữ so sánh icon/pixel. Add/Remove khóa writers, các readers giữ được snapshot cũ ổn định. Không tuyên bố model DesktopIcon là immutable: callers vẫn phải không sửa các đối tượng đã publish. Store không có Shell, dispatcher, timer hoặc sự kiện riêng; DesktopIconService phát event sau khi store xác nhận thay đổi.

DesktopIconViewModel giữ IReadOnlyList từ catalog thay vì ToArray mỗi tín hiệu. Cache rank dùng identity của snapshot và so sánh nội dung IconOrder như trước; cleanup nhả cả snapshot/rank. Contract yêu cầu membership ổn định, không chỉ kiểu IReadOnlyList bọc list đang bị sửa. Việc bỏ bản sao không loại các allocation lọc/sort/ObservableCollection, không chứng minh model bất biến sâu hoặc mức giảm RAM đã đo.

`IconGridGeometry` là snapshot record struct thuần số học, không phụ thuộc WPF/Win32, dùng cùng origin/gap/tile dimensions cho arrange, drop hint và insertion. Khoảng cách giữa các ô tối đa 12 DIP; phần dư được căn đều hai mép. Số cột vẫn phụ thuộc chiều ngang khả dụng và chiều rộng ô người dùng chọn. `IconGridPanel` giữ trách nhiệm virtualization/scroll và gọi geometry, không tự tính lại khoảng cách ở ba đường khác nhau. FLIP chỉ là chuyển động trình bày; insertion dùng slot đích ổn định để tránh phản hồi tự dao động. Không tự triển khai lại hit-test WPF.

### Panel: phần đã tách

`LayoutEditSession` sở hữu snapshot/target và trạng thái dirty của phiên chỉnh bố cục; host cung cấp callback persist. Commit thất bại không giải phóng snapshot, nên vẫn retry/cancel được. Complete không thay đổi không ghi đĩa; Cancel giữ semantics CopyFrom snapshot hiện có. MainWindow không còn giữ cờ edit và snapshot độc lập; trạng thái lấy từ session. Giao diện thông báo lỗi, suppression tự ẩn và apply layout vẫn do host sở hữu. Cleanup hủy draft qua CleanupSequence. Module chưa phải domain thuần: phụ thuộc AppSettings và LayoutProfile hiện hữu, không giả vờ đã tách persistence/schema hoàn toàn.

Phần Chi tiết, `DetailContentSession` và lịch sử biểu đồ đã được loại bỏ. Monitor hiện chỉ trình bày CPU/RAM/GPU và mạng; không có content tree chi tiết để reparent.

`PanelGeometry` tính placement thuần từ work area vật lý, DPI, cạnh và kích thước cấu hình. `PanelPlacement` là kết quả bất biến: WPF lấy kích thước DIP từ cùng pixel bounds mà Win32 sử dụng. Module không gọi API Windows, không giữ Window, không đọc/ghi settings và không có timer.

`MainWindow` vẫn sở hữu việc chọn màn hình, áp dụng geometry và `SetWindowPos`. Giới hạn chiều rộng monitor dùng chung `PanelGeometry.MonitorWidth` trong cả cấu hình ban đầu, resize và reposition. Edge interaction/layout transaction chưa tách xong.

Các test `PanelGeometryTests` phủ cả ba cạnh, tọa độ âm, DPI lẻ, work area cực nhỏ và giới hạn monitor. Không coi các test này là chứng nhận mixed-DPI/hotplug thực tế.

### Những bước còn lại

`SettingsEditSession` sở hữu target, draft và callback lưu. Nó clone/validate candidate, persist trước rồi mới CopyFrom vào target; array của draft, candidate và target không dùng chung sau commit. ReplaceDraft clone đầu vào profile/preset. SettingsWindow chỉ đọc controls, báo lỗi và đóng dialog; store/schema vẫn dùng SettingsStore/AppSettings hiện có. Đây chưa phải tách toàn bộ persistence khỏi model. Không áp dụng kiểu lưu mỗi setter của Paddock cho phiên chỉnh sửa có Cancel.

`WindowServices` là composition/ownership scope cho monitor và DesktopIconService. App tạo scope rồi chuyển quyền sở hữu cho MainWindow; constructor tương thích của MainWindow dùng cùng factory. Tạo scope không Start dịch vụ. Nếu tạo Desktop thất bại, monitor đã tạo được Dispose; lỗi cleanup không che exception gốc. MainWindow gỡ subscription/view trước khi Dispose scope; scope idempotent và tiếp tục giải phóng Desktop nếu monitor Dispose lỗi. Dịch vụ inject vào constructor được chuyển quyền sở hữu, không mượn dùng chung giữa nhiều cửa sổ. Chưa đưa settings, hotkey, watcher, tray vào scope; không gọi đây là đã hoàn thành composition toàn ứng dụng.

Header panel hiện dùng SearchSession/SearchReveal trong vùng cố định 40 DIP cho cả ba cạnh. `MainWindow.UpdateSearchHeader` chỉ quyết định phần trình bày: thay brand bằng editor, thu ba nút phụ khi header hẹp và search mở, giữ Settings/Search. Session không biết toolbar hoặc cạnh màn hình. Cơ chế này không tăng số timer, không ghi settings; SettingsWindow vẫn có adapter bố cục riêng.

1. **Presentation:** hợp nhất search reveal, token và lifecycle effect; không đưa logic file/cảm biến vào control.
2. **Desktop:** phân tách discovery, snapshot/reconciliation và Shell actions bằng các contract nhỏ, giữ hành vi nguồn Desktop hiện tại.
3. **Panel:** placement, edge activation, interaction lock và layout transaction tách khỏi thao tác UI cụ thể.
4. **Telemetry:** giữ `IHardwareMonitor`/provider hiện có, tách presentation khỏi collector; không rewrite thuật toán cảm biến chỉ để đổi cấu trúc.
5. **Settings/persistence:** draft, validation, migration và lưu cấu hình có ownership rõ; không để module UI tự ghi file.
6. **Composition root:** App lắp ghép các dịch vụ và quản lý shutdown; chỉ thêm DI container nếu composition thực tế cần, không vì hình thức.

Mỗi bước phải có mapping caller, test hồi quy, ownership/disposal và đối chiếu thư viện. Những mục lộ trình không phải tuyên bố kiến trúc đã chuyển đổi hoàn tất.

## Bài học tham khảo

### Appearance: control nhập liệu

`Inputs.xaml` sở hữu template CheckBox, ComboBox không editable và Slider ngang hiện đang dùng trong ứng dụng. Giữ control/command/automation WPF; không có event handler, timer, persistence hoặc dependency mới. DynamicResource theo theme; foreground của nội dung sinh tự động được ràng buộc về control để tránh implicit TextBlock style ghi đè.

Slider có glyph 12 DIP trong vùng kéo 28 DIP, hai vùng repeat cùng cao 28; halo chỉ phản hồi hover/drag, không thay kích thước layout hoặc làm trễ giá trị. ComboBox có trạng thái mở/focus/disabled; item giữ dấu chọn khi highlight và có viền focus. CheckBox biểu thị checked/mixed/unchecked và disabled. Không coi template này hỗ trợ Slider dọc hoặc editable ComboBox; cần mở rộng và test khi có consumer như vậy.

Đối chiếu Paddock: tài nguyên màu ngữ nghĩa và bề mặt gọn; không nhập palette cố định. Đối chiếu Pickets: trạng thái phải đi cùng semantics, không tự viết lại input engine. `InputAppearanceTests` kiểm tra template thực và lệnh WPF trên fixture; không thay kiểm thử chuột/phím/Narrator thật.

### Appearance: nút thao tác chính

`PresentationDensity.Compact` là attached property kế thừa cho chế độ trình bày gọn. MonitorPanel áp dụng cho cả cạnh trên và dưới khi không chỉnh bố cục. Cùng dữ liệu được dùng lại; không có collector hay dashboard phụ. Các cấu hình ShowDetails và expander Chi tiết không còn thuộc kiến trúc hiện tại.

`Appearance/Buttons.xaml` cung cấp `PrimaryButton` dùng chung cho các hành động xác nhận. Resource dictionary được App nạp; màu dùng DynamicResource để theo theme/high contrast mà không giữ bản sao palette. Hover/pressed chỉ đổi viền ngoài; focus bàn phím dùng PrimaryActionFocus adorner bo góc nằm ngoài nút, không vẽ vòng bên trong khi bấm chuột. Không thay cặp nền–chữ, opacity hay kích thước layout. Không sở hữu command, settings, timer hoặc event handler. Nút thường/IconButton vẫn dùng template hiện có; chưa coi toàn bộ controls đã được module hóa.

`ButtonAppearanceTests` kiểm tra render màu dark/light, Custom cực trị, trạng thái disabled và cập nhật high contrast trên instance hiện có. Kiểm tra trigger là kiểm tra cấu trúc template, không thay cho chuột/bàn phím thực tế.

- Pickets: tìm kiếm có luồng bàn phím và trạng thái rõ; Nepply giữ tìm tại chỗ thay vì cửa sổ QuickFind riêng.
- CusBox: đóng/mở gắn với ngữ cảnh tương tác; không nhập timer/phần cứng/desktop của repo đó vào module search.
- DeskBox: tách chính sách interaction/animation khỏi host cụ thể; Nepply dùng một lớp nhỏ thay vì engine nhiều cửa sổ.

Chỉ học nguyên tắc. Không sao chép source tham khảo.

### Appearance: trạng thái và token — UI-A

`Tokens.xaml` tập trung một phần radius, khoảng cách và typography theo vai trò; các consumer đã chuyển giữ đúng kích thước cũ. Chưa token hóa toàn bộ ứng dụng. `Metrics.xaml` và `NumericText` yêu cầu lining/tabular figures; font thực tế phải hỗ trợ và Viewbox hiện có vẫn có thể thu chữ. Không thay kích thước ô hoặc thuật toán lưới.

`Selectors.xaml` giữ ListBoxItem native, tách marker đang chọn khỏi viền focus. Navigation và kết quả tìm kiếm dùng SelectionBrush/SelectionTextBrush; trong high contrast đây là cặp Highlight/HighlightText hệ thống. ContentPresenter/TextBlock truyền foreground rõ để implicit style không che màu trạng thái. Button bên trong kết quả không vẽ lớp hover phủ nền đang chọn.

CheckBox disabled dùng DisabledControlBrush/DisabledMarkBrush riêng; high contrast dùng Window/GrayText, không để glyph và nền cùng WindowText. Disabled không bị áp ngưỡng tương phản của control enabled hoặc thay màu hệ thống người dùng. ThemeService cập nhật resources theo sự kiện, không tạo scheduler hoặc polling mới.

Không còn `DetailsNavigation` hoặc `DetailsWidget`. Tìm kiếm cài đặt vẫn có thông báo ItemStatus; kiểm thử fixture không thay thế nghiệm thu bàn phím/Narrator thực tế.

Kiểm thử trạng thái: `StateAppearanceTests`, `InputAppearanceTests`, `MonitorMaterialTests` và bộ UI trong `tests/Nepply.Tests`. Các mô tả UI-A trước đây là lịch sử; tính năng đã bỏ không còn là yêu cầu nghiệm thu hiện tại.

### Sửa hồi quy UI sau UI-A

NumericText phải khai báo Foreground riêng: style có tên không tự kế thừa implicit TextBlock style. InputAppearanceTests kiểm tra hình học và pixel vùng nối rail–thumb ở 0/10/50/90/100%, normal/reversed và LTR/RTL. ThumbFill phủ nửa vùng bắt kéo tới tâm; không đổi native Track, hit target hoặc command. Template chỉ hỗ trợ slider ngang hiện hành, không tự tuyên bố hỗ trợ slider dọc.

DropChevron là Path vector, nét tròn và rotation trạng thái không dùng animation clock. PrimaryActionFocus dùng cơ chế keyboard-only FocusVisualStyle của WPF; kiểm thử cấu trúc/render không thay keyboard thật. Tham khảo chính thức: [FocusVisualStyle](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/styling-for-focus-in-controls-and-focusvisualstyle).

### Cập nhật cấu trúc 02/10/2026

- `SettingsChangeSet` dùng chung phân loại thay đổi cho preview và commit. Apply chỉ tác động Desktop/hotkey/autostart/ẩn icon khi nhóm tương ứng thay đổi; persistence thành công mới công bố delta.
- `EdgePosition.IsHorizontal()` thống nhất bố cục Top/Bottom. Bottom được nối cuối enum để không đổi giá trị JSON cũ; trường Top trong settings/profile được giữ làm cấu hình hai cạnh ngang. `EdgeHitTest` kiểm tra theo work area, không bắt chuột trên taskbar.
- `MetricReadingView` thống nhất cấu trúc số liệu CPU/RAM/GPU, không sở hữu DataContext, timer hoặc collector. Border vật liệu và kích thước vẫn do MonitorPanel quản lý.
- `ShellContextMenu` trong Platform/Windows/Shell sở hữu tài nguyên native cho một lần mở menu. DesktopIconService chỉ ủy quyền qua contract hiện có. Không thay đổi lệnh Shell hay chiến lược COM.
- Đã bỏ TriggerWindow và các hàm Desktop cũ không còn nơi gọi. Không loại wrapper installer, khóa tương thích HoverBox hoặc thư viện runtime chỉ vì tên cũ/không dùng trực tiếp.
