# Quy trình ký và nghiệm thu phát hành

Trạng thái: chưa có chứng thư của nhà phát hành; không có artifact nào được ký thay người dùng. Không cài root certificate tự ký, không tắt App Control/Defender/Vanguard.

## Thứ tự bắt buộc

1. Chốt nguồn, version và dependency lock; chạy test và audit. Chốt giấy phép/notice trước phát hành.
2. Publish payload cuối. Nếu ký từng DLL do Nepply sở hữu thì ký trước khi bundle; không thay chữ ký của thư viện bên thứ ba.
3. Ký EXE payload bằng chứng thư có quyền Code Signing và timestamp RFC3161/SHA256. Script `scripts/Sign-Artifact.ps1` dùng chứng thư trong CurrentUser/My, không nhận mật khẩu/PFX trong dòng lệnh; công cụ ký phải có chữ ký hợp lệ.
4. Tạo release manifest **sau khi ký**. Không dùng manifest lập trước đó vì chữ ký làm thay đổi hash.
5. Biên dịch installer production từ chính payload đó; ký và timestamp installer.
6. Chạy `Verify-Release.ps1 -RequireSigned -AllowedSignerThumbprints <thumbprint-đã-duyệt> -Uninstaller <unins000.exe-từ-bộ-cài-này>`; chỉ dùng `-RequireStable` khi đã đổi sang phiên bản stable thật và mọi gate đều đạt. Cổng yêu cầu chữ ký hợp lệ, certificate đúng allowlist và timestamp cho app/Setup/uninstaller. Có thể truyền nhiều thumbprint trong giai đoạn đổi chứng thư có phê duyệt; không lấy allowlist từ payload vừa tải về. Thumbprint là định danh chứng thư, không phải thuật toán băm nội dung để ký.
7. Kiểm thử cài mới/nâng cấp/gỡ, khôi phục Desktop, quyền thường và chính sách Windows mặc định. Chữ ký hợp lệ không bảo đảm mọi chính sách doanh nghiệp hoặc SmartScreen chấp nhận.

### Uninstaller và giới hạn cổng ký

Ký riêng Setup không ký uninstaller. Uninstaller được ký theo hai lượt biên dịch bằng `SignedUninstallerDir` của Inno, hợp với dịch vụ ký từ xa (SignPath) vì Inno không cần gọi công cụ ký:

1. Chạy `Build-Distribution.ps1 ... -SignedUninstallerDirectory <thư-mục-cố-định>`. Lượt đầu Inno tạo `uninst-<phiên-bản-Inno>-<hash>.e32` chưa ký trong thư mục đó rồi dừng; script báo `SIGNATURE PENDING` kèm đường dẫn và không tạo Setup.
2. Ký và timestamp đúng file `.e32` đó (gửi SignPath, hoặc `Sign-Artifact.ps1`), giữ nguyên tên và thư mục.
3. Chạy lại với `-OutputDirectory` mới. Inno nhúng chữ ký vào uninstaller của Setup; script kiểm chữ ký file đã dùng và ghi `signed-uninstaller.json`. Sau đó mới ký Setup.

Đổi phiên bản Inno, `VersionInfoVersion` (mỗi bản phát hành), icon hay `WizardStyle` sinh tên `.e32` mới nên phải ký lại. Bản build không truyền thư mục này vẫn đặt `SignedUninstaller=no` (CI, bản nội bộ). Bản nội bộ chấp nhận chữ ký chứng thư thử nghiệm có root không tin cậy; chữ ký hỏng/bị sửa luôn bị từ chối. File truyền vào `-Uninstaller` phải lấy từ một lượt cài cô lập của đúng Setup đang duyệt; validator kiểm chữ ký/hash nhưng không tự chứng minh quan hệ nguồn gốc giữa hai file độc lập. Lưu hash Setup, log cài và hash uninstaller cùng hồ sơ nghiệm thu; không chạy một EXE uninstaller chỉ để đọc metadata.

Hiện chưa có chứng thư/dịch vụ ký thật được cấu hình; bản nội bộ unsigned tiếp tục dựng được, public mode vẫn bị khóa. Validator có fixture sai publisher, thiếu timestamp, thiếu policy và rollover; không thay thế kiểm chữ ký thực tế. Policy này dành cho kênh EXE/Inno, không áp nguyên xi lên chữ ký MSIX do Store xử lý.

## Kết quả kiểm tra hiện tại

- Không tìm thấy chứng thư Code Signing trong CurrentUser/My ở lượt kiểm tra trước. Không suy ra rằng máy không có chứng thư tại các kho khác hoặc dịch vụ ký từ xa.
- Script hỗ trợ ký một artifact bằng thumbprint do nhà phát hành cung cấp, xác minh đúng signer, timestamp và Authenticode. Chưa kiểm thử ký thật vì chưa có chứng thư.
- Ngày 19/10/2026 đã loại Mono.Posix.NETStandard 1.0.0 (managed và native helper, gồm `libMonoPosixHelper.dll` đã ký nhưng chưa chứng minh nguồn gốc build) khỏi payload Windows; `Test-ExcludedPackages.ps1` chặn nếu nó quay lại. Không gán MIT cho toàn bộ payload. Việc đủ notice không thay thế ký mã hoặc phê duyệt phát hành.
- Mã Nepply dùng MIT; không có EULA riêng được nối vào installer. Bản dự thảo cũ không nằm trong repository sản phẩm và không phải điều khoản phát hành.
- Inno Setup 7 tại máy hiển thị “Non-commercial use only”. Cần đối chiếu mục đích sử dụng và điều kiện của nhà cung cấp trước phát hành; “phần mềm miễn phí” không tự chứng minh mọi trường hợp sử dụng công cụ đều phi thương mại.

## Phạm vi giấy phép thư viện

Các dependency MPL không tự động buộc mở toàn bộ mã Nepply; cần giữ quyền của người nhận đối với phần mã thuộc MPL, thông báo nơi lấy source tương ứng và xử lý các sửa đổi thuộc phạm vi giấy phép. Bản sao license đơn thuần không thay thế việc kiểm chứng source availability của đúng phiên bản. Xem [Mozilla MPL FAQ, Q7–Q11](https://www.mozilla.org/en-US/MPL/2.0/FAQ/). Chủ dự án vẫn cần rà soát nghĩa vụ cụ thể trước phân phối.

[Trang chính thức Inno Setup](https://jrsoftware.org/isinfo.php) dẫn tới điều kiện sử dụng và giấy phép thương mại. Chưa mua dịch vụ/chứng thư hoặc chấp nhận điều khoản thay nhà phát hành.

Nguồn kỹ thuật: [Microsoft SignTool](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool), [Authenticode timestamp](https://learn.microsoft.com/en-us/windows/win32/seccrypto/time-stamping-authenticode-signatures).

Đây là checklist kỹ thuật và hồ sơ rà soát, không phải ý kiến pháp lý. Chủ dự án cần chốt nhà cung cấp ký, kênh phân phối, điều khoản và nghĩa vụ dependency trước công khai.
