# Phát triển Nepply

Đây là hướng dẫn kỹ thuật chuẩn bị công khai repository, chưa phải giấy phép cấp quyền sử dụng mã nguồn. Chủ dự án chưa chọn license; chưa mở nhận đóng góp công khai.

## Bắt đầu

1. Dùng Windows x64 và .NET SDK theo `global.json`.
2. Mở `Nepply.slnx`; chạy lệnh build/test trong [README](README.md).
3. Đọc [cấu trúc](docs/architecture/STRUCTURE.md) và [ranh giới module](docs/architecture/MODULES.md).

## Quy tắc thay đổi

- Mã nghiệp vụ đặt trong feature sở hữu nó; interop ở `Platform/Windows`, giao diện dùng chung ở `Presentation`.
- Không thêm dependency, timer, process hay project chỉ để chia thư mục.
- Test hồi quy nằm trong `tests/Nepply.Tests`. Công cụ chẩn đoán không nằm trong payload phát hành.
- Không commit `bin`, `obj`, `artifacts`, log cá nhân, cấu hình người dùng, chứng thư riêng hoặc khóa ký.
- Không chép source từ repository tham khảo. Kiểm tra license và notice khi thay dependency.
- Không tắt bảo mật Windows/anti-cheat để làm test vượt qua. Ghi rõ test bị chặn thay vì báo pass.

## Trước khi gửi thay đổi

Restore locked-mode, build Release, chạy bộ test phù hợp và kiểm tra build boundary. Các test là executable, không phải suite `dotnet test`. Thay đổi native/lifecycle cần thêm kiểm chứng runtime tương ứng.
