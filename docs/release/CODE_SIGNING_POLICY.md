# Code signing policy

## Trạng thái

Nepply dùng MIT cho mã do dự án sở hữu; dependency giữ giấy phép riêng.
Đang chuẩn bị đăng ký SignPath Foundation. **Chưa được phê duyệt, chưa nhận
tài trợ/chứng thư và chưa có binary ký sản xuất.** Không dùng chứng thư QA
hoặc yêu cầu người dùng tắt bảo vệ Windows để thay thế chữ ký phát hành.

## Vai trò dự kiến

- Chủ repository/người bảo trì: [minhquang0607](https://github.com/minhquang0607).
- Người duyệt release và yêu cầu ký: chủ dự án; cần cấu hình rõ trong dịch vụ
  ký sau khi được chấp thuận. Mỗi yêu cầu sản xuất phải được duyệt thủ công.
- Mọi tài khoản GitHub/SignPath tham gia phải bật MFA; không chia sẻ token.

## Ranh giới tin cậy

- Chỉ ký artifact của Nepply từ commit đã duyệt, build trên GitHub-hosted
  runner theo yêu cầu chương trình OSS; không gửi binary local tùy ý.
- Không cho pull request không tin cậy truy cập signing secret. Workflow ký
  không dùng `pull_request_target` để chạy source PR với secret.
- Chỉ ký Nepply.exe, Nepply.DesktopGuardian.exe và thành phần installer được
  nhà cung cấp duyệt. Không ký lại thư viện bên thứ ba dưới danh nghĩa dự án.
- Giữ pinned action SHA, quyền tối thiểu, bảo vệ nhánh và môi trường release.
- Giữ trace commit → workflow run → artifact → signing request → checksum.

## Kiểm tra trước phát hành

Phải kiểm tra Authenticode hợp lệ, signer đúng policy và timestamp cho app,
guardian, Setup và uninstaller thực tế. Tạo manifest/checksum sau ký. Ký riêng
Setup không chứng minh uninstaller đã ký. Thực hiện cài/nâng cấp/gỡ trên đúng
Setup đã ký; kết quả bản unsigned không thay thế bước này.

## Quyền riêng tư

Xem [chính sách quyền riêng tư](../legal/PRIVACY.md). Không đưa token, private
key hoặc log chứa dữ liệu người dùng vào repository/artifact ký. Khi đăng ký
được chấp thuận, bổ sung attribution SignPath đúng yêu cầu, không ghi nhận
tài trợ trước khi có thật.

Nguồn: [điều kiện SignPath](https://signpath.org/terms.html),
[tích hợp GitHub](https://docs.signpath.io/trusted-build-systems/github).
