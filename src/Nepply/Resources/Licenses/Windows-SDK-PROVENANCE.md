# Windows SDK projection — nguồn điều khoản

Đối chiếu ngày 09/10/2026, package `Microsoft.Windows.SDK.NET.Ref/10.0.19041.57`.

- Metadata NuGet khai báo Microsoft, yêu cầu license acceptance và dẫn tới `https://aka.ms/WinSDKLicenseURL`; không khai báo MIT.
- URL đó chuyển tới [tài liệu RTF Microsoft](https://download.microsoft.com/download/0/F/F/0FF2B061-47DD-4F55-89B6-FD1D8C44F14D/sdk_license.rtf). `Windows-SDK-License.rtf` là bản nguyên byte, 246945 byte, SHA256 `DD07EB178E00C6BBA4148457FC00FF77CD4887EB521D504186FE59C9EC8BBE62`; EULAID `WIN10SDK.RTM.AUG_2018_en-US`.
- [Danh sách REDIST chính thức](https://learn.microsoft.com/en-us/legal/windows-sdk/redist#microsoftwindowssdknetref), ghi cập nhật 21/10/2024, có mục riêng cho package này. Danh sách gồm `lib/net8.0/Microsoft.Windows.SDK.NET.dll` và `lib/net8.0/WinRT.Runtime.dll`, cho phép phân phối không sửa đổi cùng chương trình để gọi WinRT, theo điều khoản SDK.
- Không dùng câu trả lời Q&A tháng 6/2024 thay cho danh sách REDIST cập nhật sau đó. Không gán MIT của công cụ C#/WinRT cho toàn bộ package Windows SDK.

Collector kèm nguyên bản RTF và tài liệu nguồn này trong notices, ghi hash hai DLL projection từ package đã restore. Việc đánh dấu đủ văn bản chỉ nói rằng đã có license text, không phải xác nhận mọi nghĩa vụ phát hành đã được đáp ứng.

Trước public cần rà điều khoản phân phối, thông báo bản quyền và điều khoản áp dụng cho thành phần Microsoft đối với người nhận; first-party license của Nepply không thay thế điều khoản SDK. Chưa tự chọn license Nepply, chưa coi đây là tư vấn pháp lý hoặc phê duyệt phát hành.
