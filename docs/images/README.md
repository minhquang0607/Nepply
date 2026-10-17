# 🖼️ Image provenance · Nguồn ảnh

Both images were generated together on **17 October 2026** from a fresh Release rebuild. No user screenshot or prior QA image was used.

Hai ảnh được tạo cùng lượt ngày **17/10/2026** từ Release rebuild mới; không dùng ảnh người dùng hoặc kết quả QA cũ.

## Source · Nguồn

| Field / Trường | Value / Giá trị |
| --- | --- |
| Commit | `15cdbb22f2533762ddccfb0b8cdb1fd7cbf2dd7e` — matched origin/main before build / khớp origin/main trước build |
| src Git tree | `bdf88f4567228b981dbc3a1cb6fa2ba6b683786c` |
| ProductVersion | `1.0.0+15cdbb22f2533762ddccfb0b8cdb1fd7cbf2dd7e` |
| Build | Release · Windows x64 |
| Nepply.dll SHA-256 | `6A62E7FADD6045976793D2F8FB94E30F441683DEDC2F6FD861894FE9C69552E0` |
| UI verification / Kiểm thử | 1789 passed / đạt · 0 failed / lỗi; native collector disabled / tắt collector thật |

## Images · Ảnh

| Image | Render | SHA-256 |
| --- | --- | --- |
| `panel-1.0.0.png` | MainWindow.Content · 380 × 880 DIP · 96 DPI | `CB08B033D0CFB0ACAC5D633BBA64A99413EBC590742E05339E51AB2DB644E663` |
| `settings-1.0.0-wide.png` | SettingsWindow.Content · 820 × 740 DIP · 96 DPI | `9FCEB4FC22B96F002BE1D5A2D52E2124AC358E39495EDA80CBBBE41973A3F60C` |

Dark/Champagne, Vietnamese UI. Real WPF views rendered in an isolated fixture; not screenshots of the user's running desktop. Panel names, icons and telemetry are illustrative. Settings uses vertical navigation at this width. Its PNG matches the previous render byte-for-byte because this view has not changed; it was still regenerated in this run.

Giao diện tối/Champagne, tiếng Việt. View WPF thật được render trong fixture cô lập, không phải screenshot Desktop người dùng. Tên mục, icon và số đo panel là minh họa. Settings dùng menu dọc ở kích thước này. PNG Settings giống từng byte với lượt trước vì view chưa thay đổi; ảnh vẫn được tạo lại trong lượt này.

## Reproduce · Tái tạo

1. Verify the current commit and a clean application source / Đối chiếu commit và source app sạch.
2. Run locked restore and Release rebuild / Restore locked-mode và rebuild Release.
3. Run Nepply.Tests with a new output directory and `--ui-only` / Chạy test với thư mục kết quả mới.
4. Inspect `nepply-graphite.png` and `settings-wide-rail.png`; copy without image editing / Xem trực tiếp rồi sao chép nguyên ảnh.
5. Record versions, hashes, dimensions and results / Ghi phiên bản, hash, kích thước và kết quả.

This proves image origin and fixture behavior, not real-sensor accuracy or installer acceptance. Changes to UI/source require new evidence; a docs-only commit does not change the src tree. Packaged legal documents must still be refreshed when rebuilding a distribution.

Bằng chứng này xác nhận nguồn ảnh và fixture, không chứng nhận cảm biến thật hoặc bộ cài. Thay đổi UI/source cần bằng chứng mới; commit chỉ sửa tài liệu không đổi src tree. Tài liệu legal đóng gói vẫn phải được lấy mới khi dựng bản phân phối.
