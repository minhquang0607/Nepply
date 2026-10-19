# Ba phần bàn giao Nepply

Một mã nguồn duy nhất ở root repository, không nhân bản app thành nhánh EXE và MSIX.


| Phần | Nội dung | Đầu ra trong artifacts/<checkpoint>/ |
|---|---|---|
| 1 — Toàn bộ mã nguồn | App, tests, tools, scripts, cấu hình, tài liệu; không có build cache/reference/.git | `01-source/` |
| 2 — Bộ cài | EXE Setup, portable ZIP, licenses, manifest/hash | `02-installer/` |
| 3 — MSIX/Store | Manifest, assets, MSIX nội bộ và checklist Store; độc lập với Inno | `03-msix-store/` |

`scripts/Build-SourceBundle.ps1` tạo bản sao/ZIP nguồn nội bộ để bàn giao. Nó không thay Git commit/tag, không tự chọn license, không tự rà hết thông tin cá nhân trong tài liệu. Repository hiện tại vẫn là nơi sửa code duy nhất.

`scripts/Build-Distribution.ps1 -InternalOnly` tạo bộ EXE/portable. Các cổng license/notices/chữ ký public vẫn giữ nguyên. Version installer lấy từ `Nepply.csproj`, không phải chuỗi hardcode riêng trong Inno.

Mốc hiện tại: **1.0.0-beta.1** (beta công khai chưa ký; bản ký đầu tiên dự kiến `1.0.0`). Metadata Windows dùng `FileVersion=1.0.0.0`; app, Guardian và Setup lấy phiên bản từ cùng nguồn. MSIX có identity QA riêng. Không đổi tên binary cũ thành phiên bản mới: phải dựng lại payload, manifest và bộ cài từ cùng commit. Đây là phiên bản ổn định dự kiến, chưa phải binary đã phát hành; xem [chính sách phiên bản updater](../docs/release/EXE_UPDATES.md#phiên-bản-hiện-hành) trước tạo tag.

MSIX không dùng uninstaller của Inno. Khôi phục Desktop, startup và AppData phải kiểm chứng riêng dưới package identity trước khi public. Không mang nguyên kết quả QA EXE sang MSIX.

## Tạo cả ba phần

```powershell
./scripts/Prepare-Release.ps1 -OutputDirectory artifacts/<checkpoint-moi>
```

Lệnh chạy tuần tự, chỉ chuẩn bị nội bộ, không cài/gỡ hay public. Mỗi lần phải dùng thư mục mới; không ghi đè kết quả cũ. Có thể chạy riêng ba script nếu chỉ cần một phần. Chi tiết và các mục chưa hoàn tất ở [MSIX/Store](msix/README.md).
