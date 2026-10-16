# Cập nhật EXE có xác nhận

## Phạm vi hiện tại

Settings → Ứng dụng → Kiểm tra cập nhật. Không polling nền. Bản cài EXE có
luồng tải/xác minh/bàn giao Setup; portable chỉ xem thông tin, chưa thay file;
MSIX dùng Store và không gọi feed EXE.

Feed cố định: `https://api.github.com/repos/minhquang0607/Nepply/releases?per_page=30`.
Chỉ chọn bản mới hơn; stable không nhận prerelease, beta nhận cả hai. Chọn
phiên bản lớn nhất trong 30 release mới nhất, không dựa thứ tự đăng. Draft bị bỏ.
Ghi chú hiển thị dạng text, không thực thi HTML hoặc tải ảnh nhúng.

Asset bắt buộc có tên `Nepply-{version}-Setup.exe`, tag `v{version}` hoặc
`{version}`, size 1–512 MiB, digest GitHub `sha256:` và URL download cùng repo/tag.
Response metadata tối đa 2 MiB, timeout 30 giây; tải tối đa 10 phút và 3 redirect,
chỉ HTTPS tới GitHub repo hoặc `release-assets.githubusercontent.com`.

## Chốt chữ ký — CHƯA BẬT CÀI TỰ ĐỘNG CÔNG KHAI

`UpdatePolicy.SignerSha256` hiện rỗng. Không nhận fingerprint qua feed, registry,
environment hoặc file cấu hình người dùng. Sau khi nhà cung cấp ký duyệt,
review/bổ sung SHA-256 certificate fingerprint vào nguồn, build và ký bản đầu.
Phải có kế hoạch overlap chứng thư cũ/mới trước khi đổi chứng thư.

SHA256 asset chỉ kiểm tra toàn vẹn; quyền chạy dựa thêm WinVerifyTrust với
Authenticode, kiểm tra revocation và pin đúng chứng thư của signer đã được
provider xác minh. ProductName/ProductVersion phải khớp Nepply và phiên bản
đã chọn. Mở file không cho ghi/xóa từ lúc hash/signature tới CreateProcess.
Không dùng shell, PowerShell hay arguments từ release notes để chạy Setup.
Không có đường bỏ qua chữ ký cho bản production hoặc test.

Tham khảo API: [WinVerifyTrust](https://learn.microsoft.com/en-us/windows/win32/api/wintrust/nf-wintrust-winverifytrust),
[provider signer](https://learn.microsoft.com/en-us/windows/win32/api/wintrust/ns-wintrust-crypt_provider_sgnr),
[Inno command line](https://jrsoftware.org/ishelp/topic_setupcmdline.htm).

## Bàn giao nâng cấp

Chỉ bản chạy đúng đường dẫn cài đặt trong khóa uninstall HKCU production được
phép tự cài. Không dùng phát hiện bằng cách thấy một uninstaller cạnh EXE.
Sau tải/xác minh, người dùng xác nhận lưu Settings và restart. Lưu thất bại thì
không cập nhật; từ chối thì xóa gói mới tải. Setup khởi chạy với arguments cố
định `/SP- /SILENT /NORESTART /NEPPLYUPDATE=1 /DIR=<installation>` rồi app thoát
bình thường. Setup chờ mutex app được nhả, tối đa 60 giây; không kill process.
PrepareToInstall khôi phục lease Desktop còn tồn đọng trước khi copy file.
Nếu recovery thất bại thì chặn cài. Silent update thành công chạy lại Nepply.

Download bị hủy/lỗi không chạm bản cài. Sau khi Setup đã bắt đầu thay file,
không tuyên bố có transaction rollback/khôi phục binary khi mất điện. Cần
nghiệm thu lỗi cài/thiếu dung lượng, restart và repair bằng Setup trước public.
Setup cũ không chứa giao thức update này không được dùng làm release mới.

## Các kiểm chứng bắt buộc trước bật

- Fixture: phiên bản, prerelease, URL/digest/size, cancel, HTTP lỗi, ghi chú text.
- Native verifier: chữ ký tin cậy, sai pin, không ký, file bị sửa; không đổi trust store.
- Gói Nepply ký thật: tải đúng/sai digest, sai signer/version, revoked/unreachable trust,
  đóng cửa sổ khi tải, thao tác lặp, thiếu disk, file khóa và chuyển hướng lạ.
- Cài bản ký A → app tự nâng B → tự mở B, giữ Settings/startup, phục hồi Desktop;
  thử app chưa thoát, journal recovery thất bại, restart Explorer, cài lỗi.
- Kiểm tra UI trên máy và phiên bản Windows khác; bản này chưa chứng minh mọi cấu hình.

Chưa push nguồn, publish release, cấu hình ký hoặc cài nâng cấp trên máy thật chỉ
bằng việc thêm module. Thực thi các bước đó cần nghiệm thu riêng theo artifact.
