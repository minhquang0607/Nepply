# Nepply

Ứng dụng Windows x64 dùng WPF / .NET 10: panel nổi ở cạnh màn hình để truy cập Desktop, tìm kiếm và theo dõi hệ thống. Nhà phát triển: Minh Quang.

Hỗ trợ Windows 10 build 19041 trở lên và Windows 11 x64. Mã Nepply dùng [MIT](LICENSE); thư viện bên thứ ba giữ [giấy phép và notice riêng](docs/legal/THIRD_PARTY_NOTICES.md). Binary hiện chưa ký phát hành.

## Sử dụng

- Chạm cạnh đã chọn hoặc nhấn Ctrl + Space để mở; ghim để giữ mở, Esc để thu.
- Truy cập mục Desktop, tìm kiếm, menu Shell; không di chuyển file Desktop.
- Settings xem trước trực tiếp; Apply lưu và giữ mở, OK lưu và đóng, Hủy hoàn tác phần chưa lưu.
- Monitor CPU/RAM/GPU/mạng có thể tắt; số đo phụ thuộc phần cứng/provider/quyền truy cập.
- Kiểm tra cập nhật tại Settings → Ứng dụng. Tự cài EXE còn khóa đến khi cấu hình signer tin cậy và nghiệm thu; portable chưa tự thay file, Store cập nhật riêng.
- Thoát qua tray để khôi phục icon do app ẩn. Guardian hỗ trợ khi app chết đột ngột, không bảo đảm khi cả guardian cũng bị kết thúc hoặc Explorer không khả dụng.

Cấu hình: `%APPDATA%/Nepply/settings.json`. Log: `%LOCALAPPDATA%/Nepply/Logs`. App hỗ trợ nhập cấu hình HoverBox cũ, giữ nguyên bản gốc. Journal EXE/portable giữ `%LOCALAPPDATA%/HoverBox/desktop-visibility.lease` để tương thích; không xóa journal khi Desktop chưa phục hồi.

## Phát triển

Mở `Nepply.slnx`. App ở `src/Nepply`, helper phục hồi ở `src/Nepply.DesktopGuardian`, test ở `tests/Nepply.Tests`, công cụ dev ở `tools/SensorDumper` (không thuộc payload phát hành).

```powershell
dotnet restore Nepply.slnx --locked-mode
dotnet build Nepply.slnx -c Release --no-restore
./scripts/Test-BuildBoundary.ps1
dotnet run --project tests/Nepply.Tests/Nepply.Tests.csproj -c Release --no-build --no-restore -- artifacts/current/core --core-only
dotnet run --project tests/Nepply.Tests/Nepply.Tests.csproj -c Release --no-build --no-restore -- artifacts/current/ui --ui-only
```

Test suite là executable có exit code, không dùng `dotnet test`. Fixture không thay thế sensor thật, DPI nhiều màn hình, chạy dài hoặc nghiệm thu bộ cài. Không tắt bảo vệ Windows để vượt chặn.

## Đóng gói

```powershell
./scripts/Build-Distribution.ps1 -OutputDirectory artifacts/internal-build -InternalOnly
```

Cần Inno Setup; mỗi lần dùng folder mới. Pipeline đóng gói guardian, notices và manifest. Build app đơn lẻ không tạo đầy đủ guardian. Giữ cả `Directory.Build.props` và `Directory.Build.targets` để pin runtime.

`artifacts`, `bin`, `obj` là đầu ra sinh tự động, không commit. Nhật ký làm việc và bằng chứng QA local không thuộc source sản phẩm.

## Tài liệu

- [Bản đồ tài liệu](docs/README.md), [cấu trúc](docs/architecture/STRUCTURE.md), [module](docs/architecture/MODULES.md).
- [Đóng gói](packaging/README.md), [ký mã](docs/release/RELEASE_SIGNING.md), [chính sách ký](docs/release/CODE_SIGNING_POLICY.md), [cập nhật EXE](docs/release/EXE_UPDATES.md).
- [Đóng góp](CONTRIBUTING.md), [thay đổi](CHANGELOG.md), [quyền riêng tư](docs/legal/PRIVACY.md), [bảo mật](docs/legal/SECURITY.md).
- [Hỗ trợ](https://github.com/minhquang0607/Nepply/issues).
