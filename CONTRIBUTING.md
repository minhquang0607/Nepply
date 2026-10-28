# 🧩 Phát triển Nepply

[English](CONTRIBUTING.en.md) · **Tiếng Việt** · [Tài liệu](docs/README.md)

Tài liệu này dành cho người muốn tự build hoặc đóng góp mã nguồn. Nếu bạn chỉ muốn sử dụng ứng dụng, xem [hướng dẫn người dùng](docs/USER_GUIDE.md). Mã do Nepply sở hữu dùng [MIT License](LICENSE); dependency giữ giấy phép riêng.

## Bắt đầu nhanh

```powershell
dotnet restore Nepply.slnx --locked-mode
dotnet build Nepply.slnx -c Release --no-restore
dotnet run --project tests/Nepply.Tests/Nepply.Tests.csproj -c Release --no-build -- artifacts/current/core --core-only
```

Trước khi commit thay đổi C#, chạy `dotnet format whitespace Nepply.slnx`; CI từ chối code không khớp `.editorconfig`. Để `git blame` bỏ qua commit format hàng loạt, chạy một lần `git config blame.ignoreRevsFile .git-blame-ignore-revs`.

## Bắt đầu

1. Dùng Windows x64 và .NET SDK theo `global.json`.
2. Mở `Nepply.slnx`; chạy các lệnh build/test dưới đây.
3. Đọc [cấu trúc](docs/architecture/STRUCTURE.md) và [ranh giới module](docs/architecture/MODULES.md).

## Quy tắc thay đổi

Phiên bản nguồn hiện tại là `1.0.0-beta.1` trong `src/Nepply/Nepply.csproj`; metadata Windows dùng `1.0.0.0`. Không thay phiên bản thư viện hoặc các dữ liệu test giả lập khi đổi phiên bản sản phẩm. Quy tắc tag/prerelease và chuyển từ bản thử cũ nằm trong [chính sách cập nhật](docs/release/EXE_UPDATES.md#phiên-bản-hiện-hành).

- Mã nghiệp vụ đặt trong feature sở hữu nó; interop ở `Platform/Windows`, giao diện dùng chung ở `Presentation`.
- Không thêm dependency, timer, process hay project chỉ để chia thư mục.
- Test hồi quy nằm trong `tests/Nepply.Tests`. Công cụ chẩn đoán không nằm trong payload phát hành.
- Không commit `bin`, `obj`, `artifacts`, log cá nhân, cấu hình người dùng, chứng thư riêng hoặc khóa ký.
- Không chép source từ repository tham khảo. Kiểm tra license và notice khi thay dependency.
- Không tắt bảo mật Windows/anti-cheat để làm test vượt qua. Ghi rõ test bị chặn thay vì báo pass.

## Trước khi gửi thay đổi

Restore locked-mode, build Release, chạy bộ test phù hợp và kiểm tra build boundary. Các test là executable, không phải suite `dotnet test`. Thay đổi native/lifecycle cần thêm kiểm chứng runtime tương ứng.

```powershell
dotnet restore Nepply.slnx --locked-mode
dotnet build Nepply.slnx -c Release --no-restore
./scripts/Test-BuildBoundary.ps1
./scripts/Test-LanguageResources.ps1
dotnet run --project tests/Nepply.Tests/Nepply.Tests.csproj -c Release --no-build --no-restore -- artifacts/current/core --core-only
dotnet run --project tests/Nepply.Tests/Nepply.Tests.csproj -c Release --no-build --no-restore -- artifacts/current/ui --ui-only
```

Test giao diện cần phiên Windows desktop phù hợp. Fixture không thay thế thử phần cứng thật, DPI nhiều màn hình hoặc nghiệm thu bộ cài. Không báo pass cho kiểm tra chưa chạy được.

CI chạy thêm appearance, tray, DPI, crash, kiểm tra dependency/notice và hợp đồng đóng gói. Appearance fixture đặt rõ tùy chọn animation thay vì phụ thuộc mặc định của runner. Xem cấu hình hiện hành trong `.github/workflows`; kết quả CI của commit trước không tự chứng minh commit mới đã đạt.

`Directory.Build.props` và `Directory.Build.targets` khóa runtime và ILLink theo dependency lock. Giữ hai file này khi build; không bỏ `--locked-mode` để che lỗi lệch dependency. Khi nâng phiên bản, cập nhật pin và lock có chủ đích rồi kiểm tra trên máy sạch/CI.

## Ảnh minh họa và bằng chứng kiểm thử

- Cập nhật đồng thời bản Anh–Việt khi hành vi, phiên bản, tải xuống hoặc giới hạn thay đổi; giữ liên kết chuyển ngôn ngữ. Không sửa văn bản giấy phép gốc hoặc ngày bằng chứng lịch sử chỉ để đồng bộ hình thức.
- Trước mỗi lượt kiểm chứng, đối chiếu repository, commit hiện tại, thay đổi chưa commit và đường dẫn binary thực sự chạy. Không suy ra phiên bản từ tên thư mục hay tên file.
- Build từ nguồn hiện tại trước khi test; dùng thư mục kết quả mới và ghi commit/cấu hình/lệnh chạy cùng báo cáo. Kết quả của commit cũ chỉ là lịch sử, không xác nhận bản mới.
- Ảnh giới thiệu ưu tiên cửa sổ app thật ở bố cục mặc định. Ảnh fixture kích thước hẹp hoặc dữ liệu giả phải được ghi rõ và không dùng làm ảnh đại diện mặc định.
- Ảnh người dùng cung cấp phải ghi nguồn/ngày; nếu chưa đối chiếu binary thì không gán commit hoặc phiên bản đã xác minh. Không lấy ảnh QA cũ chỉ vì tên file giống nhau.
- Thay ảnh tài liệu không phải thay đổi ứng dụng và không thay thế nghiệm thu bộ cài cuối.

## Đóng gói thử

```powershell
./scripts/Build-Distribution.ps1 -OutputDirectory artifacts/internal-build -InternalOnly
```

Cần Inno Setup; dùng thư mục đầu ra mới cho mỗi lần đóng gói. Script gom app, helper phục hồi Desktop, notice và manifest; build app đơn lẻ không tạo đủ payload phát hành. Xem [hướng dẫn đóng gói](packaging/README.md).

Đầu ra `artifacts`, `bin`, `obj` không được commit. Bản build nội bộ chưa ký không phải bản phát hành chính thức; xem [quy trình ký](docs/release/RELEASE_SIGNING.md) trước khi phân phối.
