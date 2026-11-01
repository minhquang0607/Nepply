# Cập nhật EXE có xác nhận

## Phạm vi hiện tại

Settings → Ứng dụng → Kiểm tra cập nhật. Tùy chọn **Tự kiểm tra cập nhật mỗi
ngày** mặc định tắt, tối đa một yêu cầu mỗi ngày và chỉ báo ở khay hệ thống,
không tải hay cài. Bản cài EXE có
luồng tải/xác minh/bàn giao Setup; portable chỉ xem thông tin, chưa thay file;
MSIX dùng Store và không gọi feed EXE.

Feed cố định: `https://api.github.com/repos/minhquang0607/Nepply/releases?per_page=30`.
Chỉ chọn bản mới hơn; stable không nhận prerelease, prerelease nhận cả hai. Chọn
phiên bản lớn nhất trong 30 release mới nhất, không dựa thứ tự đăng. Draft bị bỏ.
Ghi chú hiển thị dạng text, không thực thi HTML hoặc tải ảnh nhúng.

Asset bắt buộc có tên `Nepply-{version}-Setup.exe`, tag `v{version}` hoặc
`{version}`, size 1–512 MiB, digest GitHub `sha256:` và URL download cùng repo/tag.
Response metadata tối đa 2 MiB, timeout 30 giây; tải tối đa 10 phút và 3 redirect,
chỉ HTTPS tới GitHub repo hoặc `release-assets.githubusercontent.com`.

## Chốt chữ ký — CHƯA BẬT CÀI TỰ ĐỘNG CÔNG KHAI

`UpdatePolicy.Signers` hiện rỗng. Không nhận policy qua feed, registry,
environment hoặc file cấu hình người dùng. Sau khi nhà cung cấp ký duyệt, chạy
`scripts/Get-SignerPolicy.ps1 -File <Setup-đã-ký>`, review rồi đưa `SignerRule`
vào nguồn, build và ký bản đầu.

Không pin chứng thư lá: SignPath gia hạn chứng thư, Azure Artifact Signing đổi
chứng thư liên tục, nên pin lá sẽ làm mọi bản cài từ chối cập nhật. Mỗi
`SignerRule` gồm CN và O chính xác của chứng thư lá (một giá trị duy nhất, phân
biệt hoa thường) cộng với pin SHA-256 của khóa công khai (SPKI) của **CA phát hành
trực tiếp**. Pin khóa thay vì chứng thư CA nên vẫn đúng khi CA cấp lại chứng thư
hoặc chuỗi đi qua cross-sign. Khi CA đổi khóa hoặc đổi nhà cung cấp ký, phát hành
trước một bản chứa cả pin/rule cũ và mới, chỉ bỏ rule cũ ở bản sau đó.

SHA256 asset (trường `digest` của GitHub) chỉ kiểm tra toàn vẹn; quyền chạy dựa
thêm WinVerifyTrust với Authenticode, kiểm tra revocation và khớp `SignerRule`
trên chuỗi signer đã được provider xác minh. ProductName/ProductVersion phải khớp Nepply và phiên bản
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
- Native verifier: chữ ký tin cậy, sai CA phát hành, sai publisher, không ký, file bị sửa;
  không đổi trust store (`Nepply.Tests.exe <out> --update-signature <file> <CN> <O> <issuer-spki>`
  với giá trị từ `Get-SignerPolicy.ps1`). Logic `SignerRule` có test chứng thư trong bộ nhớ ở core.
- Gói Nepply ký thật: tải đúng/sai digest, sai signer/version, revoked/unreachable trust,
  đóng cửa sổ khi tải, thao tác lặp, thiếu disk, file khóa và chuyển hướng lạ.
- Cài bản ký A → app tự nâng B → tự mở B, giữ Settings/startup, phục hồi Desktop;
  thử app chưa thoát, journal recovery thất bại, restart Explorer, cài lỗi.
- Kiểm tra UI trên máy và phiên bản Windows khác; bản này chưa chứng minh mọi cấu hình.

Mã nguồn đã được đưa lên GitHub; việc đó không đồng nghĩa đã publish binary,
cấu hình ký hoặc nghiệm thu cập nhật có chữ ký trên máy thật.

## Phiên bản hiện hành

Phiên bản hiện tại là `2.0.0-rc.1` (chốt 30/10/2026), metadata Windows
`2.0.0.1`: bản phát hành thử công khai **chưa ký**, đáp ứng điều kiện "đã phát
hành" của SignPath. Tag `v2.0.0-rc.1`, asset `Nepply-2.0.0-rc.1-Setup.exe`, đánh
dấu prerelease trên GitHub. Bản ký đầu tiên là `2.0.0` (metadata `2.0.0.100`),
cao hơn `2.0.0-rc.1` theo SemVer nên updater nhận đúng thứ tự. Nhãn cũ
`1.0.0-beta.1` và `1.0.0-17.10.26` chưa từng phát hành. Chỉ bật `-RequireStable`
khi mọi điều kiện ký và nghiệm thu đều đạt.

Đây là lần đặt lại mốc trước phát hành, thấp hơn các bản thử `2.5.0-*` cũ.
Updater giữ nguyên nguyên tắc không downgrade: bản thử cũ cần chuyển thủ công,
không nhận mốc này như bản mới hơn. Các bản sau phải tăng phiên bản SemVer;
không dùng ngày/tháng làm thứ tự cập nhật (ví dụ bản vá tiếp theo là `2.0.1`).
