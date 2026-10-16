# Nhánh MSIX / Microsoft Store

## Trạng thái

Đang chuẩn bị, **chưa sẵn sàng public**. Manifest và identity hiện tại chỉ dành cho QA (`Nepply.Internal.Preview`), không phải danh tính đã được cấp trên Partner Center. Không tự tạo/chấp nhận chứng thư, cài package, gửi Store hay bật chế độ Developer Mode.

```powershell
./scripts/Build-Msix.ps1 -OutputDirectory artifacts/<checkpoint>/03-msix-store -InternalOnly
```

Script publish self-contained x64 dạng nhiều file (MSIX đã nén), tạo logo từ asset hiện có, chạy MakeAppx với validation mặc định, unpack rồi đối chiếu hash từng file payload. Nó không dùng `/nv`, không gọi Inno, không cài driver và không coi pack thành công là runtime QA thành công.

`msix-build.json` ghi rõ `Signed`, `Installed`, `StoreReady`; trạng thái hiện tại đều false. Windows SDK đang dùng mặc định là 10.0.26100.0; có thể truyền `-MakeAppx` tới SDK chính thức đã cài. Không tải tool tùy ý để chạy.

## Những việc phải hoàn tất

1. Nhận Package/Identity/Name và Publisher thật từ Partner Center; chốt package version bốn phần có revision cuối bằng 0 cho Store. Không dùng version preview riêng làm version chính thức vô tình.
2. Adapter startup đã nối trong source: MSIX dùng StartupTask, tôn trọng DisabledByUser/Policy và không ghi HKCU Run. Core/Settings fixture đã chạy; còn nghiệm thu thao tác Settings trong package thực và logon. Manifest vẫn khai báo task tắt, không tự bật lúc khởi tạo.
3. Thiết kế và test khôi phục icon Desktop khi process bị kill, package update/remove. MSIX không chạy Inno `InitializeUninstall`; không giả định custom uninstall action của desktop game MSIXVC dùng được cho app này.
4. Test AppData/config migration, log, lease, IPC/helper và cùng tồn tại với portable/EXE. Chưa tự đổi SID mutex hay vị trí journal vì có thể phá quyền sở hữu Desktop giữa hai kênh.
5. Test chạy không admin, mất sensor/driver, không tự cài driver; package không được yêu cầu tắt cơ chế bảo vệ để dùng app.
6. Chốt notices, publisher metadata, privacy URL/asset listing, kiểm tra package thực và N → N+1. Trước khi cài QA cần thỏa thuận riêng về identity/cert thử, không tự thêm cert vào trust store.

## Kênh cập nhật

- Bản MSIX qua Store: để Store quản lý bản cập nhật; không cho updater EXE ghi đè thư mục package. Trạng thái cập nhật còn phụ thuộc chính sách Windows/Store của người dùng.
- Bản EXE/portable: tạm dùng thay bản thủ công từ kênh phát hành chính thức khi kênh đó được tạo; **chưa có auto-updater**. Nếu thêm sau, phải có HTTPS, xác minh publisher/chữ ký, version policy và rollback/lifecycle rõ ràng.
- `.appinstaller` là một nhánh phân phối MSIX ngoài Store khác; chưa cần đưa vào để chuẩn bị Store trước.

## Nguồn chính thức đã đối chiếu

- [Đóng gói desktop app: giới hạn driver, AppData và install directory](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-prepare).
- [StartupTask qua packaging extension](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/desktop-to-uwp-extensions).
- [MakeAppx và phạm vi semantic validation](https://learn.microsoft.com/en-us/windows/msix/package/create-app-package-with-makeappx-tool).
- [Yêu cầu package và version Store](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements).
- [Phạm vi hạn chế của UninstallActions](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-desktop6-uninstallactions).
- [Cập nhật tự động của Store](https://support.microsoft.com/en-us/windows/apps/turn-on-automatic-app-updates).

Đây là hướng dẫn chuẩn bị kỹ thuật, không phải xác nhận ứng dụng đã được Microsoft duyệt.
