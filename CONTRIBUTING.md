# 🧩 Phát triển Nepply

[English](CONTRIBUTING.en.md) · **Tiếng Việt** · [Tài liệu](docs/README.md)

Tài liệu này dành cho người muốn tự build hoặc đóng góp mã nguồn. Nếu bạn chỉ muốn sử dụng ứng dụng, xem [hướng dẫn người dùng](docs/USER_GUIDE.md). Mã do Nepply sở hữu dùng [MIT License](LICENSE); dependency giữ giấy phép riêng. Tác giả và người bảo trì: Minh Quang. Đội ngũ AI: Codex và Claude.

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

Phiên bản nguồn hiện tại là `2.0.0-rc.2` trong `src/Nepply/Nepply.csproj`; metadata Windows dùng `2.0.0.2`. Lặp lại cả hai giá trị trong `src/Nepply.DesktopGuardian/Nepply.DesktopGuardian.csproj`, vì app từ chối Guardian khác file version. Mỗi bản RC tăng số thứ tư (`rc.3` sẽ là `2.0.0.3`); bản ổn định dùng `2.0.0.100` để Windows luôn thấy nó mới hơn. Không thay phiên bản thư viện hoặc các dữ liệu test giả lập khi đổi phiên bản sản phẩm. Quy tắc tag/prerelease và chuyển từ bản thử cũ nằm trong [chính sách cập nhật](docs/release/EXE_UPDATES.md#phiên-bản-hiện-hành).

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

## Kiểm chứng trên Explorer thật

CI chỉ thử việc ẩn icon trên file fixture, nên một bản cập nhật Windows có thể làm hỏng tính năng chính trong khi CI vẫn xanh. `scripts/Test-DesktopRuntime.ps1` chạy các test dùng Explorer thật. Chạy trước mọi tag, kể cả bản RC, từ bản build Release của đúng commit sẽ gắn tag:

```powershell
pwsh scripts/Test-DesktopRuntime.ps1
pwsh scripts/Test-DesktopRuntime.ps1 -Live -ReleaseTag v2.0.0-rc.2
```

Lệnh đầu là chạy khô: chỉ đọc trạng thái, in kế hoạch và không đổi gì. Lệnh sau là chạy thật.

- **Script đổi Desktop của bạn.** Khi chạy thật, script ẩn icon nhiều lần và khởi động lại Explorer hai lần, nên các cửa sổ File Explorer sẽ đóng. Mỗi lần chạy, script yêu cầu gõ một cụm từ trong cửa sổ console và không có tùy chọn nào để bỏ qua. Không chạy từ tự động hóa. CI chỉ chạy `scripts/Test-DesktopRuntimeContract.ps1`, bản kiểm tra quy tắc dùng tiến trình giả.
- **Script từ chối chạy** khi Nepply, guardian hoặc binary test đang mở, khi icon đã ẩn hoặc không tìm thấy cửa sổ icon, khi còn journal khôi phục của Nepply, hoặc khi binary không build từ commit hiện tại.
- **Có sáu chặng:** guardian dùng bình thường và khi tiến trình chủ crash; tùy chọn lưu của Explorer khi tiến trình chủ crash và khi khởi động lại Explorer; hai trường hợp đó với `ShowWindow`, cách bản Store dùng. Script dừng ở lỗi đầu tiên và khôi phục icon.
- **Kết quả** nằm ở `artifacts/desktop-runtime/<tag>/<thời-gian>-<commit>/`: `summary.json`, cùng file kết quả và log console của từng chặng. Chỉ `ACCEPTED` khi mọi chặng đạt trên cây sạch và binary không đổi. Giữ thư mục đó và chép dòng script in ra vào bảng trong [kế hoạch phát hành](docs/release/PUBLIC_RELEASE_CHECKLIST.md).
- **Mã thoát:** 0 là đạt, hoặc chạy khô không có gì để từ chối. 1 là không đạt. 2 là từ chối, chưa đổi gì. 3 là icon vẫn ẩn: nhấp phải Desktop, chọn View > Show desktop icons; nếu mục đó đã được chọn thì khởi động lại Windows Explorer từ Task Manager.
- **Giới hạn:** kết quả chỉ có giá trị với commit và bản Windows đó, nên chạy thêm trên bản Insider khi có thể. Test chạy từ tiến trình chưa đóng gói, nên không thay thế nghiệm thu bộ cài, gỡ cài đặt, MSIX hay Store.

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
