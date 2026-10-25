# 🖼️ Image provenance · Nguồn ảnh

Both images were generated together on **24 October 2026** from a fresh Release rebuild. No user screenshot or prior QA image was used.

Hai ảnh được tạo cùng lượt ngày **24/10/2026** từ Release rebuild mới; không dùng ảnh người dùng hoặc kết quả QA cũ.

## Source · Nguồn

| Field / Trường | Value / Giá trị |
| --- | --- |
| Commit | `ff63ba91eb9a421153933326734f6b4585779757` — branch `chore/release-prep`, clean tree before build / nhánh `chore/release-prep`, cây sạch trước build |
| src Git tree | `2f7dd41022a8150a2f558c9afe2e977e419b0b7c` |
| ProductVersion | `1.0.0-beta.1+ff63ba91eb9a421153933326734f6b4585779757` |
| Build | Release · Windows x64 |
| Nepply.dll SHA-256 | `BEA8B425FB8CB71A51CF5901F6E759A5DB798B7D7D03A5B8ECEFE057BB4F6FCC` |
| UI verification / Kiểm thử | 1970 passed / đạt · 0 failed / lỗi; native collector disabled / tắt collector thật |

## Images · Ảnh

| Image | Render | SHA-256 |
| --- | --- | --- |
| `panel-1.0.0.png` | MainWindow.Content · 380 × 880 DIP · 96 DPI | `7F612A9D956760D0A8EA4AACBF72D2A4A0C5061CF107A5E567FB6896F55E30BD` |
| `settings-1.0.0-wide.png` | SettingsWindow.Content · 820 × 740 DIP · 96 DPI | `6C9C99F0702DFA222E00EEE2E7EA19D2ABA19078C403AC1CA8A0C8A6B867CEDC` |

Dark/Champagne, Vietnamese UI. Real WPF views rendered in an isolated fixture; not screenshots of the user's running desktop. Panel names, icons and telemetry are illustrative. Settings uses vertical navigation at this width and shows the Shortcuts section, translated buttons and no explanatory captions.

Giao diện tối/Champagne, tiếng Việt. View WPF thật được render trong fixture cô lập, không phải screenshot Desktop người dùng. Tên mục, icon và số đo panel là minh họa. Settings dùng menu dọc ở kích thước này, có mục Phím tắt, nút đã Việt hóa và không còn dòng giải thích.

## Reproduce · Tái tạo

1. Verify the current commit and a clean application source / Đối chiếu commit và source app sạch.
2. Run locked restore and Release rebuild / Restore locked-mode và rebuild Release.
3. Run Nepply.Tests with a new output directory and `--ui-only` / Chạy test với thư mục kết quả mới.
4. Inspect `nepply-graphite.png` and `settings-wide-rail.png`; copy without image editing / Xem trực tiếp rồi sao chép nguyên ảnh.
5. Record versions, hashes, dimensions and results / Ghi phiên bản, hash, kích thước và kết quả.

This proves image origin and fixture behavior, not real-sensor accuracy or installer acceptance. Changes to UI/source require new evidence; a docs-only commit does not change the src tree. Packaged legal documents must still be refreshed when rebuilding a distribution.

Bằng chứng này xác nhận nguồn ảnh và fixture, không chứng nhận cảm biến thật hoặc bộ cài. Thay đổi UI/source cần bằng chứng mới; commit chỉ sửa tài liệu không đổi src tree. Tài liệu legal đóng gói vẫn phải được lấy mới khi dựng bản phân phối.
