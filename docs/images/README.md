# Nguồn ảnh giao diện

## settings-1.0.0-wide.png

- Tạo mới ngày 17/10/2026; không dùng ảnh người dùng hoặc sao chép kết quả QA cũ.
- Source commit: `055c5cccc4651017d913f109aeb9fd0c11e821b5`, đã đối chiếu origin/main trước build.
- Git tree của thư mục src: `bdf88f4567228b981dbc3a1cb6fa2ba6b683786c`.
- Release rebuild, Windows x64; assembly ProductVersion: `1.0.0+055c5cccc4651017d913f109aeb9fd0c11e821b5`.
- SHA-256 Nepply.dll được render: `BD12584FBEE90DFF9D0B1276C682721DE1B3C2FBABE5E9E6F29316F32B57AFBC`.
- SHA-256 PNG: `9FCEB4FC22B96F002BE1D5A2D52E2124AC358E39495EDA80CBBBE41973A3F60C`.
- Render trực tiếp SettingsWindow.Content: 820 × 740 DIP, 96 DPI, giao diện tối/Champagne, menu dọc. Không phải screenshot của tiến trình app đang sử dụng cấu hình cá nhân.

## Tái tạo

Restore locked-mode, rebuild Release rồi chạy executable Nepply.Tests với thư mục kết quả mới và `--ui-only`. Ảnh đầu ra là `settings-wide-rail.png`; kiểm tra trực tiếp bố cục trước khi đưa vào README. Fixture dùng dữ liệu cô lập, không nghiệm thu cảm biến thật hoặc installer. Kết quả lượt này: 1789 kiểm tra đạt, 0 lỗi.

Mỗi lần thay source/UI phải kiểm tra lại commit, metadata assembly và render mới. Không đổi tên ảnh cũ để coi là ảnh của phiên bản mới; số liệu QA ở đây chỉ áp dụng cho lượt build được ghi phía trên. Chỉnh sửa tài liệu/ảnh không thay đổi src tree; thay đổi app thì phải tạo bằng chứng mới.
