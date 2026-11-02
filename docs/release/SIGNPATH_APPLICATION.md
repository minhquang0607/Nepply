# Hồ sơ nộp SignPath Foundation

Bản nháp câu trả lời cho form <https://signpath.org/apply>, đối chiếu với form và điều kiện tại <https://signpath.org/terms> ngày 02/11/2026.

Minh Quang phải tự nộp, vì form tạo tài khoản SignPath đứng tên người nộp và yêu cầu cam kết tuân thủ Code of Conduct. Trạng thái: Minh Quang báo đã nộp ngày 02/11/2026, đang chờ SignPath trả lời.

## Điều kiện trước khi nộp

- Repo `Nepply` đã Public.
- Bản `2.0.0-rc.1` đã phát hành trên trang Releases, vì SignPath yêu cầu dự án đã phát hành đúng dạng sẽ được ký.
- Tài khoản GitHub đã bật MFA, và tài khoản SignPath cũng phải bật MFA sau khi tạo.
- Trang Release có mục *Code signing policy*.

## Câu trả lời

| Trường | Nội dung |
| --- | --- |
| Project Name | Nepply |
| Repository URL | https://github.com/minhquang0607/Nepply |
| Homepage URL | https://github.com/minhquang0607/Nepply |
| Download URL | https://github.com/minhquang0607/Nepply/releases |
| Privacy Policy URL | https://github.com/minhquang0607/Nepply/blob/main/docs/legal/PRIVACY.md |
| Wikipedia URL | Bỏ trống |
| Tagline | A screen-edge panel for Windows that keeps Desktop items and system stats within reach. |
| Category | Chọn mục gần nhất với tiện ích desktop hoặc năng suất |
| Description | Nepply is an open source panel for Windows that slides in from the edge of the screen. It lets people open and search their Desktop items, customize the layout and check system activity such as CPU, GPU and memory, while keeping the Desktop itself tidy. It runs locally and restores anything it changes on the Desktop when it exits or is uninstalled. |
| Reputation | Xem bên dưới |
| Maintainer Type | Cá nhân (Individual) |
| Build System | GitHub Actions |
| First Name / Last Name | Minh / Quang |
| Email | Email bạn dùng để nhận thư của SignPath |
| Company Name | Bỏ trống |
| Primary Discovery Channel | Chọn đúng với cách bạn biết đến SignPath |
| I have read and agree to the SignPath Foundation Code of Conduct | Bạn đọc rồi tự tick |
| I agree to receive other communications | Không bắt buộc, nên bỏ trống |
| I agree to allow SignPath to store and process my personal data | Bạn đọc rồi tự tick |

## Trường Reputation

Viết đúng thực tế, không phóng đại. Ngày 02/11/2026 repo có 0 sao và 0 lượt tải, nên câu trong ngoặc vuông chỉ giữ lại khi đã có số thật; nếu chưa có thì bỏ cả câu:

> Nepply is a new open source project released under the MIT license. The first public release is 2.0.0-rc.1: https://github.com/minhquang0607/Nepply/releases/tag/v2.0.0-rc.1. [It has N downloads and N GitHub stars so far.] The repository shows its full development history, automated tests on GitHub Actions, security policy and code signing policy. Code signing would let users install it without SmartScreen and Smart App Control warnings.

Dự án mới nên phần này có thể yếu. Nếu bị từ chối, xem phương án dự phòng ở giai đoạn 5 của [kế hoạch phát hành](PUBLIC_RELEASE_CHECKLIST.md).

## Đã đối chiếu với điều kiện của SignPath

- **Giấy phép:** Nepply dùng MIT. Các thư viện dùng MIT, MPL-2.0 hoặc Apache-2.0, đều là giấy phép OSI. Đã đọc từ package NuGet đã khóa ngày 02/11/2026.
- **Mã độc quyền:** repo không có thành phần đóng nguồn của tác giả.
- **Gỡ cài đặt:** có uninstaller.
- **Thay đổi hệ thống:** app chỉ ẩn icon Desktop khi người dùng chọn và có hướng dẫn khôi phục.
- **Vai trò và quyền riêng tư:** ghi trong [chính sách ký](CODE_SIGNING_POLICY.en.md).
- **Trang chủ và trang tải:** README và ghi chú phát hành có mục *Code signing policy*.
- **Metadata file ký:** `Nepply.exe` và `Nepply.DesktopGuardian.exe` đều có Product là `Nepply` và cùng phiên bản.
