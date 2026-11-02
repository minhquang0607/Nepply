# 🖼️ Image provenance · Nguồn ảnh

Both images were generated together on **2 November 2026** from a fresh Release rebuild. No user screenshot or prior QA image was used.

Hai ảnh được tạo cùng lượt ngày **02/11/2026** từ Release rebuild mới; không dùng ảnh người dùng hoặc kết quả QA cũ.

## Source · Nguồn

| Field / Trường | Value / Giá trị |
| --- | --- |
| Commit | `2fa1e783f911450194aff400fc7de7a80c018498` — the source of tag `v2.0.0-rc.1` / mã nguồn của tag `v2.0.0-rc.1` |
| src Git tree | `737bd204bb428d5fd7c96ca153464e3163753fe4` |
| ProductVersion | `2.0.0-rc.1+2fa1e783f911450194aff400fc7de7a80c018498` (built just before that commit, from the same `src` tree / dựng ngay trước commit đó, cùng cây `src`) |
| Build | Release · Windows x64 |
| Nepply.dll SHA-256 | `73197FD030967E4E99C5C25BA5C80575BF11D5C6D10B6796E0A44DC1C0E20625` |
| UI verification / Kiểm thử | 2151 passed / đạt · 0 failed / lỗi; native collector disabled / tắt collector thật |

## Images · Ảnh

| Image | Render | SHA-256 |
| --- | --- | --- |
| `panel.png` | MainWindow.Content · 380 × 880 DIP · 96 DPI | `51AE7CAFB53081D9EBE4BF3DCC68016C5A9910EBA5EE57B0413BA19F7A8795E7` |
| `settings-wide.png` | SettingsWindow.Content · 820 × 740 DIP · 96 DPI | `36F72F07E8EDCE80F08B7FB48FCCBA64E4164AE07802A2CFFD1DCAAF860B09BD` |

Dark/Champagne, Vietnamese UI. Real WPF views rendered in an isolated fixture; not screenshots of the user's running desktop. Panel names, icons and telemetry are illustrative. Settings uses vertical navigation at this width and opens on Appearance, with no explanatory captions.

Giao diện tối/Champagne, tiếng Việt. View WPF thật được render trong fixture cô lập, không phải screenshot Desktop người dùng. Tên mục, icon và số đo panel là minh họa. Settings dùng menu dọc ở kích thước này, mở ở mục Giao diện và không có dòng giải thích.

## Reproduce · Tái tạo

1. Verify the current commit and a clean application source / Đối chiếu commit và source app sạch.
2. Run locked restore and Release rebuild / Restore locked-mode và rebuild Release.
3. Run Nepply.Tests with a new output directory and `--ui-only` / Chạy test với thư mục kết quả mới.
4. Inspect `nepply-graphite.png` and `settings-wide-rail.png`; copy without image editing / Xem trực tiếp rồi sao chép nguyên ảnh.
5. Record versions, hashes, dimensions and results / Ghi phiên bản, hash, kích thước và kết quả.

This proves image origin and fixture behavior, not real-sensor accuracy or installer acceptance. Changes to UI/source require new evidence; a docs-only commit does not change the src tree. Packaged legal documents must still be refreshed when rebuilding a distribution.

Bằng chứng này xác nhận nguồn ảnh và fixture, không chứng nhận cảm biến thật hoặc bộ cài. Thay đổi UI/source cần bằng chứng mới; commit chỉ sửa tài liệu không đổi src tree. Tài liệu legal đóng gói vẫn phải được lấy mới khi dựng bản phân phối.
