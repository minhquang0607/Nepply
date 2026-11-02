# Nhánh MSIX / Microsoft Store

## Trạng thái

Đang chuẩn bị gửi Store, xem giai đoạn 6 của [kế hoạch phát hành](../../docs/release/PUBLIC_RELEASE_CHECKLIST.md). Chưa có identity thật từ Partner Center, chưa cài thử gói Store và chưa gửi duyệt.

```powershell
# Gói QA nội bộ
./scripts/Build-Msix.ps1 -OutputDirectory artifacts/<checkpoint>/03-msix-store -InternalOnly
# Gói Store: cần packaging/msix/identity.store.json (mẫu ở identity.store.example.json) và mã nguồn đã là bản ổn định
./scripts/Build-Msix.ps1 -OutputDirectory artifacts/<checkpoint>/store -Store
```

Script publish self-contained x64 dạng nhiều file (MSIX đã nén), tạo logo từ asset hiện có, chạy MakeAppx với validation mặc định, unpack rồi đối chiếu hash từng file payload. Nó không dùng `/nv`, không gọi Inno, không cài driver và không coi pack thành công là đã nghiệm thu. `-AllowPrereleaseSource` chỉ để dựng thử.

`msix-build.json` ghi `Signed`, `Installed`, `StoreReady` là false khi script chỉ đóng gói. Gói gửi Store không cần ký trước: Microsoft ký lại sau khi duyệt. Identity QA `Nepply.Internal.Preview` chỉ để thử nội bộ, không dùng cho Store.

## Khác biệt của bản Store

- Ẩn icon Desktop bằng `ShowWindow` trên cửa sổ icon thay vì lệnh nội bộ của Explorer, nên không đổi cài đặt Windows nào. Explorer khởi động lại thì icon hiện lại và app ẩn lại khi nhận `TaskbarCreated`.
- Không kèm tiến trình bảo vệ: tiến trình trong gói không sống qua việc gỡ gói, và không có cài đặt nào cần khôi phục. App chết thì icon về khi đăng nhập lại, hoặc bấm *Xem > Hiển thị biểu tượng màn hình nền* hai lần.
- Không kiểm tra cập nhật qua GitHub; Store quản lý cập nhật.
- Tự khởi động dùng StartupTask, tôn trọng DisabledByUser/Policy và không ghi HKCU Run.
- Cấu hình nằm trong vùng dữ liệu riêng của gói, tách khỏi Setup và portable.

## Những việc còn phải hoàn tất

1. Nhận Package/Identity/Name và Publisher thật từ Partner Center, tạo `identity.store.json`.
2. Đổi mã nguồn sang `2.0.0`, dựng gói `2.0.0.0`. Phần cuối của phiên bản gói phải là 0.
3. Chạy Windows App Certification Kit nếu máy cho phép; công cụ cần quyền quản trị.
4. Nghiệm thu gói thật: chạy không admin, cảm biến, StartupTask, cập nhật N → N+1, gỡ cài đặt.

## Kênh cập nhật

- Bản MSIX qua Store: để Store quản lý bản cập nhật; không cho updater EXE ghi đè thư mục package. Trạng thái cập nhật còn phụ thuộc chính sách Windows/Store của người dùng.
- Bản EXE đã có module kiểm tra/tải/xác minh và bàn giao Setup; tự cài production còn bị khóa do chưa chốt signer và nghiệm thu ký thật. Portable chỉ kiểm tra/xem ghi chú, chưa tự thay file. Xem [cập nhật EXE](../../docs/release/EXE_UPDATES.md).
- `.appinstaller` là một nhánh phân phối MSIX ngoài Store khác; chưa cần đưa vào để chuẩn bị Store trước.

## Nguồn chính thức đã đối chiếu

- [Đóng gói desktop app: giới hạn driver, AppData và install directory](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-prepare).
- [StartupTask qua packaging extension](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/desktop-to-uwp-extensions).
- [MakeAppx và phạm vi semantic validation](https://learn.microsoft.com/en-us/windows/msix/package/create-app-package-with-makeappx-tool).
- [Yêu cầu package và version Store](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements).
- [Phạm vi hạn chế của UninstallActions](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-desktop6-uninstallactions).
- [Cập nhật tự động của Store](https://support.microsoft.com/en-us/windows/apps/turn-on-automatic-app-updates).

Đây là hướng dẫn chuẩn bị kỹ thuật, không phải xác nhận ứng dụng đã được Microsoft duyệt.
