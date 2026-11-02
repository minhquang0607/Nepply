# Kế hoạch phát hành Nepply 2.0.0

Cập nhật 02/11/2026. Đây là kế hoạch duy nhất từ hiện tại đến sau bản chính thức. Làm từng giai đoạn theo thứ tự; giai đoạn sau chỉ bắt đầu khi giai đoạn trước đạt điều kiện **Xong khi**.

Đánh dấu một mục khi đã hoàn tất và kiểm chứng thật, không đánh dấu theo dự đoán. **[Bạn]** là việc chỉ Minh Quang làm được (tài khoản, duyệt, máy thật); **[AI]** là việc code hoặc tài liệu trong repo do đội ngũ AI (Codex, Claude) làm; **[Cả hai]** cần phối hợp.

| Giai đoạn | Mục tiêu | Trạng thái |
| --- | --- | --- |
| 0 | Nền tảng đã có | Xong |
| 1 | Sửa các lỗi nhỏ từ đợt rà soát | Xong |
| 2 | Đưa code lên GitHub | Xong |
| 3 | Chuẩn bị repo công khai | Xong; còn tùy chọn thêm CodeQL vào ruleset |
| 3A | Sửa lỗi từ các lượt cài thử | Xong; bạn báo đạt ngày 02/11/2026 |
| 4 | Phát hành `2.0.0-rc.1` chưa ký | Đã công khai và đối chiếu ngày 02/11/2026; đang nhận phản hồi |
| 5 | Xin chữ ký số | SignPath từ chối ngày 02/11/2026; chờ bạn chọn hướng khác |
| 6 | Tích hợp ký và phát hành RC đã ký | Chưa làm; các bước bên dưới viết cho SignPath, phải sửa theo hướng được chọn |
| 7 | Hoàn thiện chất lượng | Bài đo 24 giờ đang chạy; còn các mục của bạn |
| 8 | Nghiệm thu trên máy thật | Chưa làm |
| 9 | Phát hành `2.0.0` | Đã tạo lại ảnh README; phần còn lại chờ RC đã ký |
| 10 | Trả nợ kỹ thuật sau 2.0.0 | Chưa làm |

## Quyết định đã chốt

- Bản chính là `2.0.0`. Trước đó phát hành `2.0.0-rc.1` chưa ký để đủ điều kiện SignPath, vì dự án phải **đã phát hành** ở dạng sẽ được ký. Bản ổn định luôn phải có chữ ký.
- Phương án không chọn: phát hành `2.0.0` chưa ký rồi ký từ `2.0.1`, vì bản ổn định chưa ký sẽ bị Smart App Control chặn.
- Tag cũ `v1.0.0-beta.1` đã xóa ngày 30/10/2026. Chỉ tạo tag mới lúc phát hành.
- `FileVersion`: RC thứ N dùng `2.0.0.N`, bản ổn định dùng `2.0.0.100`. Luôn đổi đồng thời trong `Nepply.csproj` và `Nepply.DesktopGuardian.csproj`.
- Bản dịch tiếng Việt của bộ cài do repo tự giữ, dịch từ `Default.isl` của Inno Setup 7.1.0.

## Giai đoạn 0. Nền tảng đã có

- [x] `.github/SECURITY.md`, `CODE_OF_CONDUCT.md`, issue templates, chính sách ký mã tiếng Anh, Dependabot cho NuGet.
- [x] Workflow `release.yml`: tag `vX.Y.Z` → build trên GitHub runner → Release **nháp** chưa ký. Đã chạy thật với tag `v2.0.0-rc.1` ngày 02/11/2026.
- [x] Luồng ký uninstaller (`-SignedUninstallerDirectory`), kiểm chữ ký cập nhật theo nhà phát hành + CA (`Get-SignerPolicy.ps1`).
- [x] Cửa sổ cập nhật có nút mở trang tải; ngôn ngữ đầu tiên theo Windows; bộ cài 8 ngôn ngữ; nút Báo lỗi; ghi chú phát hành dạng văn bản.
- [x] `.gitattributes`, `.editorconfig`, kiểm format trong CI, CodeQL.

## Giai đoạn 1. Sửa các lỗi nhỏ từ đợt rà soát

Kết quả rà 30/10/2026: build từ clone sạch đạt; cấy 20 lỗi thử thì 17 bị bắt, 3 lọt, 1 trong số bị bắt làm test treo. Sau khi sửa: cả 3 lỗi đã lọt đều bị bắt, thêm 5 lỗi cấy mới ở phần tải cập nhật và màn chào cũng bị bắt, test treo hoặc crash đều báo đúng tên.

- [x] **[AI]** Màn chào: giữ ngôn ngữ đang xem trước khi Windows đổi cài đặt giao diện. Trước đây `PreferenceChanged` áp lại ngôn ngữ đã lưu.
- [x] **[AI]** Thêm timeout cho từng nhóm test, báo đúng vị trí bị treo. Trước đây CI chờ hết 20 phút mà không biết test nào hỏng.
- [x] **[AI]** Sửa test giới hạn kích thước file cài đặt: dùng JSON hợp lệ quá 2 MiB, vì test cũ đạt nhờ lỗi parse.
- [x] **[AI]** Thêm test `IsAllowedPath` từ chối thư mục khác `HoverBox`.
- [x] **[AI]** Tách phần tải bản cập nhật để test tự động được checksum, kích thước và redirect mà không cần chữ ký thật.
- [x] **[AI]** Bật `ContinuousIntegrationBuild` trên CI để build tái lập được ở mọi thư mục.
- [x] **[AI]** Chuyển các test logic thuần về bộ core. Một số test trước đây CI không chạy ở bộ nào.

**Xong khi:** các lỗi cấy thử đã lọt đều bị test bắt; toàn bộ test và gate đạt.

## Giai đoạn 2. Đưa code lên GitHub

- [x] **[Bạn]** Xem diff hai nhánh; bỏ qua commit format `f80d742` vì chỉ đổi khoảng trắng. Không còn cần: cả hai nhánh đã merge từ lâu.
- [x] **[AI]** Push `fix/release-readiness` và `chore/pro-polish`, mở PR #8 và #9. Lần chạy đầu cho thấy CI đỏ vì hết dung lượng artifact, không phải vì lỗi code; đã sửa workflow để upload không chặn các gate. CI của cả hai PR xanh.
- [x] **[Cả hai]** Merge #8 vào `main`, đổi base của #9 sang `main`, rồi merge #9. `main` ở `e1d92e8` chứa đủ commit của cả hai PR; Verify chạy tay trên commit này xanh. GitHub không xử lý sự kiện push của lần merge #9 nên #9 kẹt ở Open và đã được đóng tay ngày 31/10/2026. Dòng "Closed with unmerged commits" trên trang #9 là dữ liệu cũ.
- [x] **[AI]** Xác nhận push lên `main` tự chạy Verify trở lại: merge #10 (`a0b0525`) tự chạy Verify #54, xanh.
- [x] **[Cả hai]** Release nháp "Nepply 1.0.0" và các nhánh cũ không còn ở repo `Nepply`; chúng nằm lại trong kho lưu trữ Private `Nepply-archive` từ ngày 02/11/2026.
- [x] **[Bạn]** Chạy thử bộ cài trên máy của bạn: trang thông báo và chữ tiếng Việt hiển thị đúng, cài và gỡ được. Đã làm trong giai đoạn 3A.

**Xong khi:** `main` có đủ thay đổi và CI xanh.

## Giai đoạn 3. Chuẩn bị repo công khai

Rà nội dung trước khi chuyển Public, vì sau đó nội dung có thể đã bị lưu lại ở nơi khác. Repo đã Public ngày 02/11/2026 sau lần quét 100 commit sạch. Các mục cài đặt bên dưới đã xong trước khi tạo tag.

### Quyết định về lịch sử Git

- [x] **[Cả hai]** Xóa link liên hệ cá nhân cũ khỏi lịch sử: ngày 02/11/2026 viết lại 91 commit, thay link bằng trang Issues rồi force push `main` và `fix/release-readiness`. Mọi mã commit đã đổi; quét toàn bộ object của hai nhánh này không còn link, đối chứng trên lịch sử cũ tìm thấy link. 10 commit từng mang tên `MQuang07` nay có tác giả Minh Quang.
- [x] **[Cả hai]** Xử lý bản cũ còn sót: `refs/pull/1` đến `refs/pull/12` vẫn giữ commit cũ có link và không tự xóa được, nên ngày 02/11/2026 repo cũ được đổi tên thành `Nepply-archive` (vẫn Private, còn đủ PR, lịch sử Actions và Release nháp), rồi tạo repo `Nepply` mới và chỉ đẩy `main`. Repo mới có đúng một ref; commit cũ `46cd02f` trả về 404.
- [ ] **[Bạn]** Luôn giữ `Nepply-archive` ở Private vì nó vẫn chứa link. Đừng push các nhánh local cũ lên repo mới. Chỉ xóa kho này khi chắc chắn không cần xem lại.
- [x] **[AI]** Rà toàn bộ lịch sử: quét 86 commit trên mọi nhánh ngày 01/11/2026, không thấy mẫu khóa hay token, đường dẫn máy cá nhân, email cá nhân hay file nhạy cảm từng được thêm. Đối chứng: cùng cách quét tìm đúng 19 commit chứa link liên hệ. Đây là quét theo mẫu, không chứng minh tuyệt đối. Quét lại nếu có thêm nhiều commit trước khi Public.

### Cài đặt GitHub

Repo `Nepply` tạo ngày 02/11/2026 không mang theo cài đặt của repo cũ. Các số PR, số lần chạy CI và *Release build* ghi trong kế hoạch trước ngày này thuộc `Nepply-archive`. Giá trị bên dưới được đối chiếu với repo cũ, nên làm đúng từng mục là đủ.

- [x] **[Bạn] Tên hiển thị**: GitHub → Settings → Public profile → Name: `Minh Quang`. Đã đổi; API công khai trả về đúng tên này.
- [x] **[Bạn] MFA tài khoản**: Settings (tài khoản) → Password and authentication → bật Two-factor authentication. Lưu recovery codes ngoài máy. Ngày 02/11/2026 trang này cho thấy Authenticator app và GitHub Mobile đã cấu hình, recovery codes đã được xem. Nơi lưu recovery codes do bạn tự kiểm tra.
- [x] **[Bạn] Bảo mật repo**: repo → Settings → *Advanced Security*: bật Dependency graph, Dependabot alerts, Dependabot security updates và Grouped security updates. Đã bật.
- [x] **[Bạn] Actions**: Settings → Actions → General:
  - Bật *Require actions to be pinned to a full-length commit SHA*. Đã bật.
  - Workflow permissions: *Read repository contents and packages permissions*. Đúng.
  - Bỏ chọn *Allow GitHub Actions to create and approve pull requests*. Đúng.
- [x] **[Bạn] Bảo vệ nhánh `main`**: [`rulesets/protect-main.json`](rulesets/protect-main.json) đã import, đang Active: chặn force push và xóa nhánh, yêu cầu status check `build`, Repository admin được bỏ qua.
- [x] **[Bạn] Bảo vệ tag**: [`rulesets/protect-release-tags.json`](rulesets/protect-release-tags.json) đã import, đang Active cho tag `v*`: chặn sửa, xóa và force push, Repository admin được bỏ qua.
- [x] **[Bạn] Môi trường release**: `release` có Required reviewers là chính bạn và tag rule `v*`, đã kiểm chứng ngày 02/11/2026.
- [x] **[Bạn]** Duyệt hoặc đóng các PR Dependabot đang mở (CI phải xanh trước khi merge). Ngày 01/11/2026 không có PR nào đang mở.
- [x] **[Cả hai] Chuyển Public**: Settings → General → Danger Zone → Change repository visibility → Public. Xong ngày 02/11/2026; truy cập ẩn danh thấy `Nepply` Public, `Nepply-archive` vẫn trả về 404, 11 link tới repo trong tài liệu đều mở được.
- [x] **[Bạn] Ngay sau khi Public**, làm các mục GitHub chỉ cho bật ở repo công khai (đã kiểm chứng cả ba):
  - Settings → *Advanced Security*: bật **Private vulnerability reporting**, Secret scanning và Push protection.
  - Settings → Actions → General → Fork pull request workflows: *Require approval for all external contributors*.
  - Settings → Environments → `release` → *Required reviewers*: chính bạn.
- [x] **[AI]** Sau khi bật Private vulnerability reporting, mở thử `…/security/advisories/new` và trang Issues. Ngày 02/11/2026 API trả về `enabled: true`, tài khoản chủ repo mở được form nháp (không gửi gì), trang Issues mở ẩn danh được. Chưa thử bằng một tài khoản bên ngoài.
- [x] **[AI]** Chạy CodeQL lần đầu sau khi Public: lần chạy #10 trên `a408a16` xanh ở cả hai job `csharp` và `actions`, 836 cảnh báo mở. Không có cảnh báo nào mang mức bảo mật critical, high, medium hay low, và không có cảnh báo nào về workflow. Tất cả là cảnh báo chất lượng code: 38 Error, 203 Warning, 595 Note, trong đó nhiều mục nằm ở test hoặc file `obj` sinh tự động.
- [x] **[AI]** Rà nhóm có thể là lỗi thật, không đổi code. Cả 38 Error là cảnh báo nhầm: 26 *Invalid string formatting* do `LocalizationService.Get` trả về chính khóa khi thiếu chuỗi, còn số tham số của 8 ngôn ngữ đã được `Test-LanguageResources.ps1` kiểm; 12 *Possible loss of precision* là phép chia nguyên có chủ đích khi tính hàng và tâm lề, hoặc phép nhân số nhỏ. 20 *Path.Combine* trong `src` chỉ ghép hằng, GUID hoặc tên trong danh sách cho phép. 4 *Constant condition* ở `UpdateWindow` kiểm tra `_closed` sau `await`, nơi giá trị có thể đổi.
- [ ] **[Bạn]** Thêm status check của CodeQL vào ruleset nếu muốn bắt buộc. Trang Code scanning chỉ người có quyền ghi xem được.

**Xong khi:** repo Public, báo lỗ hổng riêng hoạt động, CI và CodeQL xanh.

## Giai đoạn 3A. Sửa lỗi từ các lượt cài thử

Bạn cài Release build #1 ngày 02/11/2026 và thấy các vấn đề dưới đây, nên chưa tag. Mỗi nhóm một commit riêng. Con số thời gian là mục tiêu, phải đo thật rồi mới đánh dấu. Góp ý từ lượt cài thứ hai nằm ở mục riêng phía dưới.

### Kích hoạt panel

- [x] **[AI]** Sửa lỗi di vào góc mà panel không trượt ra. Vùng làm việc không tính thanh tác vụ, nên chuột vào góc dưới trái bị coi là ra khỏi màn hình. `PointerActivationGate` khi đó đòi chuột rời cạnh hơn 16 DIP rồi mới cho mở lại, nên đi tiếp lên dọc cạnh không mở được. Giờ góc thanh tác vụ của chính màn hình đó được tính là đã rời cạnh; chuột sang từ màn hình khác vẫn không mở panel.
- [x] **[AI]** Giảm độ trễ tới cạnh: trước đây chuột ở xa được đọc 250 ms một lần, cộng 80 ms và 120 ms chờ, nên chậm nhất khoảng 450 ms. Giờ đọc 125 ms ở xa và 40 ms gần cạnh, nên tính theo chu kỳ là 120 đến 160 ms khi đi bình thường, tối đa khoảng 245 ms khi lao rất nhanh. Timer cạnh chạy ở mức ưu tiên Input. Cảm nhận thật do bạn xác nhận ở bước nghiệm thu.
- [x] **[AI]** Đo khung hình lúc trượt bằng 3 lượt smoke test, mỗi lượt 3 lần mở: luồng giao diện chỉ bị chặn 3 ms, khung hình chậm nhất khoảng 20 ms ở cả hai bản, nên animation không phải nguyên nhân. ReadyToRun không cải thiện đáng kể mà làm `Nepply.exe` tăng từ 194 lên 256 MB, nên **không bật**. Smoke test giờ ghi các số này để phát hiện thoái lui. Lượt đo tắt cảm biến và panel mới ẩn 300 ms, nên chưa phủ trường hợp ẩn rất lâu.

### Monitor khi mở panel

Nguyên nhân: khi ẩn, app bỏ mốc đo CPU và mạng nên lần đọc đầu trống, xóa số cảm biến đã có, và tắt tiến trình cảm biến sau 60 giây nên lần mở sau phải khởi động lại.

- [x] **[AI]** Giữ tính năng ngủ khi ẩn, nhưng mở lên là có số ngay: giữ giá trị gần nhất cho tới khi có số mới, đo lại CPU và mạng sau 150 ms thay vì đợi hết chu kỳ. Số cảm biến cũ chỉ đứng tạm tối đa 15 giây, đúng hạn đọc của tiến trình cảm biến.
- [x] **[AI]** Giữ tiến trình cảm biến 10 phút khi ẩn thay vì 60 giây, không đọc số liệu trong lúc đó, và khởi động sớm khi chuột tiến gần cạnh. Tiến trình lỗi chỉ được thử lại khi panel mở thật. Ẩn hơn 10 phút vẫn tắt nó.
- [x] **[AI]** Đo trên cảm biến thật ngày 02/11/2026: khởi động lạnh 1,8 đến 3 giây, đọc khi đã chạy 7 đến 39 ms, lúc rảnh 0 CPU và 22 MB RAM riêng (71 MB working set, phần lớn dùng chung). Đây là lý do giữ tiến trình lâu hơn. Từ lúc mở panel tới khi có đủ CPU, RAM, mạng: 189 đến 215 ms qua 5 lần, đạt mục tiêu 300 ms. Lúc ẩn hơn 10 phút, số mới vẫn cần 2 đến 3 giây, số cũ hiện trong lúc chờ.

### Bộ cài

- [x] **[AI]** Luôn hỏi ngôn ngữ khi mở Setup (`ShowLanguageDialog=yes`), chọn sẵn theo Windows. Setup ghi lựa chọn vào `HKCU\Software\Nepply\Setup`, app đọc một lần ở lần chạy sau; đã chọn ngôn ngữ trong app thì cài lại không đè.
- [x] **[AI]** Làm bộ cài mang nhận diện Nepply: giao diện tối, ảnh từ biểu tượng app, trang chào, lời chào và trang kết thúc riêng ở 8 ngôn ngữ (AI tự dịch), tùy chọn khởi động cùng Windows. Đã xem thật các trang tới bước tác vụ bằng tiếng Việt; Setup vẫn 78,2 MB.
- [x] **[AI]** Khi gỡ, hỏi có xóa luôn cài đặt và nhật ký không; mặc định giữ lại, bản gỡ im lặng không xóa. Luôn dọn `%TEMP%\.net\Nepply`. Dấu khôi phục Desktop trong `HoverBox` luôn được giữ. Chưa chạy thật toàn bộ luồng cài và gỡ, việc này nằm ở bước nghiệm thu.

### Thiết lập mặc định

- [x] **[AI]** Bật mặc định kiểm tra cập nhật mỗi ngày, thêm lựa chọn này vào màn hình chào. Người đã chủ động tắt vẫn giữ nguyên. Đã cập nhật chính sách riêng tư, README, hướng dẫn và CHANGELOG, vì app sẽ tự kết nối GitHub.
- [x] **[AI]** Rà lại toàn bộ mặc định trong `AppSettings` và màn hình chào. Độ trễ 120 ms, vùng cạnh 4 px, ẩn sau 600 ms, cập nhật số 1,5 giây vẫn hợp lý. Ghi nhận: *Tiết kiệm khi ẩn* không có tác dụng khi ngủ khi ẩn đang bật, là nợ nhỏ về giao diện cài đặt.
- [x] **[AI]** Báo rõ khi Windows đang chặn tự khởi động. Máy này có `StartupApproved` ở trạng thái tắt từ 15/10/2026, nên bật tự khởi động trong app thì Windows vẫn bỏ qua. App chỉ báo và chỉ chỗ bật lại, không tự đổi lựa chọn đó.

### Góp ý từ lượt cài thứ hai

Bạn góp ý ngày 02/11/2026. Lần rà mặc định trước được làm lại dưới góc nhìn người dùng.

- [x] **[AI]** Trang Information chỉ hiện ngôn ngữ đã chọn: mỗi ngôn ngữ có file riêng trong `packaging/inno/Notices` (AI tự dịch). Đã mở Setup thật cả 8 ngôn ngữ và xem ảnh chụp. `RELEASE_NOTICE.txt` song ngữ vẫn đi kèm bản portable và thư mục cài.
- [x] **[AI]** Tìm lý do tự khởi động bị tắt. Cờ tắt trong `StartupApproved\Run` được ghi lúc 13:04:34 ngày 15/10/2026, 18 ngày trước lượt cài này, và Windows lưu cờ theo tên mục `Nepply` chứ không theo đường dẫn, nên gỡ cài đặt không xóa cờ và mục mới bị tắt ngay. Windows không tự ghi cờ này: Task Manager, Cài đặt Windows hoặc một công cụ quản lý khởi động đã ghi. Máy có Advanced SystemCare đang chạy, nhưng nhật ký của nó không có dòng nào về Nepply lúc đó, nên chưa xác định được ai ghi. Trong repo chỉ có bộ gỡ cài đặt chạm vào khóa này, và chỉ để xóa.
- [x] **[AI]** Sửa cảnh báo không tự mất: cảnh báo chỉ được tính một lần lúc app khởi động rồi nằm trên panel, không có gì kiểm tra lại sau khi bật ở Task Manager. Giờ mở panel là đọc lại và tự bỏ cảnh báo. Khi app khởi động thường, panel không còn nhắc, vì tắt trong Task Manager cũng là lựa chọn của người dùng; Cài đặt báo dưới ô chọn và đọc lại mỗi khi quay về cửa sổ.
- [x] **[AI]** Khi tạo mục khởi động mới mà chưa có mục nào, app xóa cờ cũ còn sót, còn mục đang tồn tại thì vẫn tôn trọng công tắc trong Windows. Bộ gỡ luôn bỏ cờ khi không còn mục `Nepply`, không phụ thuộc lựa chọn xóa dữ liệu.
- [x] **[AI]** Phát hiện thêm một lỗi: `dontinheritcheck` không có tác dụng với tác vụ cấp cao nhất, nên bản cập nhật im lặng vẫn có thể bật lại tự khởi động mà người dùng đã tắt. Giờ tác vụ này chỉ xuất hiện ở lần cài đầu. Bỏ chọn ở Setup cũng thắng cài đặt còn giữ từ lần cài trước.
- [x] **[AI]** Đổi mặc định cho bản cài mới. Khởi động cùng Windows được chọn sẵn trong Setup, vì app này chỉ có ích khi luôn chạy và người dùng vẫn thấy ô để bỏ chọn; bản portable vẫn tắt. Giao diện theo Windows thay vì luôn tối. Phím mở panel đổi từ Ctrl + Space, vì phím toàn cục cũ chiếm gợi ý code của VS, VS Code và phím đổi bộ gõ tiếng Trung, Nhật. Người đã có cài đặt giữ nguyên.
- [x] **[AI]** Lượt thử thứ ba: lựa chọn đầu là Ctrl + Alt + Space không đăng ký được vì Claude Desktop đang giữ nó (Windows trả lỗi 1409 khi Nepply không chạy, và bấm phím này mở thanh Claude). Tạm đổi sang Alt + Shift + Space, rồi bạn quyết định bỏ hẳn phím đặt sẵn vì trùng quá nhiều: Alt + Space là phím của PowerToys Run và nhiều trợ lý AI, mọi tổ hợp Ctrl + Alt trùng AltGr trên nhiều bàn phím. Giờ phím mở panel mặc định **chưa đặt** như các phím toàn cục khác, xóa được, và panel mở bằng cạnh màn hình hoặc khay hệ thống. Người dùng chỉ có bàn phím mở qua khay (Win + B). Phím đã lưu từ bản trước được giữ nguyên.
- [x] **[AI]** Giữ nguyên, có lý do: ẩn icon Desktop vẫn do người dùng tự chọn vì nó thay đổi Desktop; ngủ khi ẩn vẫn bật vì lần sửa trước đã làm monitor mở nhanh; không tạo lối tắt Desktop vì app dùng để dọn Desktop; một lần nhấp để mở giữ như menu Start.

### Nghiệm thu

- [x] **[AI]** Dựng Setup mới sau khi bỏ phím đặt sẵn, core 1039 và UI 2151 test đạt, `Test-InstallerContract` 31 mục đạt, đã xem các trang tới bước tác vụ. Chưa chạy thật toàn bộ luồng cài và gỡ, chưa bấm thử công tắc thật trong Task Manager, và giao diện sáng mới được kiểm bằng test ở chế độ Light chọn tay. Bản thử nằm ở `artifacts/`, chưa qua `Release build` trên GitHub.
- [x] **[Bạn]** Cài lại và thử. Bạn báo đạt ngày 02/11/2026 trên bản `2fa1e78`, cài đè lên bản trước. Máy cho thấy lượt cài mới đã bật tự khởi động đúng và không còn cờ tắt cũ. Sau đó bạn gỡ: máy không còn thư mục cài, cài đặt, nhật ký, mục khởi động, cờ cũ hay thư mục giải nén, kể cả log do test của AI tạo trước đó, nên bộ gỡ đã chạy phần xóa dữ liệu; lịch sử IObit Uninstaller không có lượt gỡ mới.

**Xong khi:** các mục trên đạt và bạn đồng ý với bản cài mới.

## Giai đoạn 4. Phát hành `2.0.0-rc.1` chưa ký

- [x] **[AI]** Viết ghi chú phát hành RC song ngữ, ghi rõ bản chưa ký và cách xử lý cảnh báo Windows: [RELEASE_2.0.0-rc.1.md](RELEASE_2.0.0-rc.1.md).
- [x] **[Cả hai]** Kiểm tra dung lượng artifact: lần chạy thử ngày 01/11/2026 upload được 186 MB. `release.yml` cần upload bản build để tạo Release; nếu lần tag báo lỗi upload, xem Settings → Billing and licensing và xóa artifact cũ. Dung lượng được tính lại sau 6 đến 12 giờ.
- [x] **[Cả hai]** Chạy thử: *Release build* #3 trên `f030380` (trước khi viết lại lịch sử là `2241652`) xanh, gồm biên dịch bộ cài và artifact `nepply-release-unsigned`. Bước `draft-release` chỉ chạy khi có tag. Repo mới: *Release build* #1 trên `245af2f` cũng xanh ngày 02/11/2026.
- [x] **[AI]** Tạo tag `v2.0.0-rc.1` từ `2fa1e78` và push ngày 02/11/2026, sau khi bạn báo đạt, Verify #23 và CodeQL #24 trên `main` xanh, và API xác nhận `release` có Required reviewers. *Release build* #2 tự chạy.
- [x] **[Bạn]** Duyệt bước `release`. Đã duyệt ngày 02/11/2026; *Release build* #2 xanh, tạo Release nháp với Setup, portable và `SHA256.json`, cả ba đều có attestation công khai.
- [x] **[AI]** Điền ghi chú song ngữ và chọn *Pre-release* trong Release nháp rồi lưu nháp; chưa công khai.
- [x] **[Bạn]** Xem lại Release nháp rồi bấm Publish. Đã công khai ngày 02/11/2026 ở dạng pre-release: <https://github.com/minhquang0607/Nepply/releases/tag/v2.0.0-rc.1>.
- [x] **[AI]** Đối chiếu ẩn danh sau khi công khai: `SHA256.json` khớp mã GitHub ghi cho Setup và portable, cả hai tệp tải được, cả ba tệp có attestation.
- [x] **[AI]** Tải file từ trang Release về máy và cài thử. Ngày 02/11/2026 cả ba tệp khớp mã GitHub và `SHA256.json`, `Verify-Release.ps1 -ExpectedVersion 2.0.0-rc.1` đạt trên bản portable và Setup. Chạy ẩn bằng Setup công khai: cài lần đầu ghi `AutoStart=1` và ngôn ngữ, cài đè không ghi lại `AutoStart`, gỡ xong sạch. Đây là chạy ẩn và không mở app; lượt cài qua trình duyệt có SmartScreen thuộc giai đoạn 8.
- [ ] **[Cả hai]** Nhận phản hồi trong lúc chờ SignPath, nên từ 2 đến 4 tuần. Lỗi nặng được sửa ở `2.0.0-rc.2`, tăng `FileVersion` thành `2.0.0.2`.

**Xong khi:** Release công khai tải được, cài được, không có lỗi chặn.

## Giai đoạn 5. Xin chữ ký số

- [x] **[AI]** Đối chiếu repo với điều kiện SignPath ngày 02/11/2026 và soạn sẵn câu trả lời: [SIGNPATH_APPLICATION.md](SIGNPATH_APPLICATION.md). Giấy phép dependency đều là giấy phép OSI; ghi chú rc.1 đã có link *Code signing policy*.
- [x] **[Bạn]** Kiểm tra trước khi nộp: repo Public, MFA đã bật, đã có Release công khai.
- [x] **[Bạn]** Tự nộp đơn tại <https://signpath.org/apply> theo [SIGNPATH_APPLICATION.md](SIGNPATH_APPLICATION.md). Bạn báo đã nộp ngày 02/11/2026; AI không có cách xác minh từ phía SignPath.
- [x] **[AI]** SignPath Foundation từ chối ngày 02/11/2026 vì dự án chưa đủ độ nhận biết công khai, mời nộp lại sau. Đã đối chiếu lại các hướng khác cùng ngày, xem bảng dưới.
- [ ] **[Bạn]** Chọn hướng ký. Azure Artifact Signing không dùng được vì cá nhân chỉ ở Mỹ/Canada.

| Hướng | Chi phí | Điều kiện đã biết | Chưa rõ |
| --- | --- | --- | --- |
| Microsoft Store (MSIX) | Tài khoản cá nhân miễn phí; Microsoft ký gói sau khi duyệt | Giấy tờ tùy thân và selfie; phải xong các mục trong `packaging/msix/README.md`, nhất là khôi phục Desktop khi gói bị gỡ | Store có chấp nhận app ẩn icon Desktop không. Setup trên GitHub vẫn chưa ký |
| Certum Open Source | Từ $58 bản cloud, từ €69 bộ thẻ | Chỉ cho cá nhân; xác minh giấy tờ, hóa đơn tiện ích, dự án công khai; bị thu hồi nếu ký phần mềm thương mại | Có bán cho Việt Nam không, thời hạn, còn hàng không, có ký tự động trong CI không. SmartScreen vẫn cảnh báo tới khi đủ uy tín |
| Chứng thư OV thường | Khoảng $150–300/năm | Khóa trên HSM hoặc token | SmartScreen vẫn cảnh báo tới khi đủ uy tín |
| SignPath trả phí | Không công bố giá | Liên hệ SignPath | Có kèm chứng thư không |
| Nộp lại SignPath Foundation | Miễn phí | Cần sao, fork, người đóng góp, bài viết hoặc thảo luận độc lập | Mức bao nhiêu là đủ |

**Xong khi:** có chứng thư, dịch vụ ký hoặc kênh phát hành đã ký dùng được.

## Giai đoạn 6. Tích hợp ký và phát hành RC đã ký

Bản chưa ký không thể tự cập nhật, nên phải có một RC đã ký để kiểm thật đường cập nhật lên `2.0.0`.

- [ ] **[AI]** Thêm bước gửi ký vào `release.yml` (action SignPath pin theo SHA): ký `Nepply.exe` + `Nepply.DesktopGuardian.exe`, dựng lại Setup với `-SignedUninstallerDirectory`, ký Setup, tạo checksum sau ký.
- [ ] **[AI]** Ký một bản thử rồi chạy `pwsh scripts/Get-SignerPolicy.ps1 -File <Setup-đã-ký>`, đưa kết quả vào `UpdatePolicy.Signers` để bật tự cập nhật.
- [ ] **[AI]** Mở public mode trong `scripts/Build-Distribution.ps1` (hiện từ chối khi thiếu `-InternalOnly`).
- [ ] **[AI]** Bổ sung attribution SignPath đúng yêu cầu vào README và chính sách ký.
- [ ] **[Cả hai]** Phát hành `2.0.0-rc.2` (hoặc RC kế tiếp) đã ký. Chạy `Verify-Release.ps1 -RequireSigned -AllowedSignerThumbprints <thumbprint> -Uninstaller <unins000.exe từ lượt cài thật>`.

**Xong khi:** một bản RC đã ký được phát hành và qua `Verify-Release`.

## Giai đoạn 7. Hoàn thiện chất lượng

Không chặn việc xin chữ ký, có thể làm song song từ giai đoạn 4.

- [x] **[AI]** Mở mục Desktop không chặn luồng giao diện, để shortcut trỏ tới ổ mạng mất kết nối không làm panel đơ.
- [ ] **[Cả hai]** Chạy bài đo dài 24 giờ (`pwsh scripts/Run-UiSoak.ps1`), máy phải bật suốt thời gian đo, rồi phân tích CPU, RAM, handle. AI đã khởi động ngày 02/11/2026 trên bản sao trong `artifacts/soak-bin-*`, chưa có kết quả. Máy đang đặt tự ngủ sau 3 giờ không dùng; nếu máy ngủ thì kết quả không dùng được.
- [x] **[AI]** Rà trợ năng bằng test tự động trên cây giao diện thật: mọi nút, ô nhập, thanh trượt và danh sách trong Cài đặt, màn chào, panel và các hộp thoại đều có tên cho trình đọc màn hình và đi tới được bằng bàn phím. Đã sửa ô Hiệu ứng chuyển động thiếu tên. Giao diện High Contrast đã có test sẵn.
- [ ] **[Bạn]** Thử thật với Narrator và chế độ tương phản cao của Windows, vì test tự động không thay được việc nghe thử.
- [ ] **[Bạn]** Nhờ người bản ngữ duyệt bản dịch Nhật, Đức, Trung, Nga, Pháp, Thái, cả app lẫn bộ cài.
- [ ] **[Bạn]** Nhờ người đọc lại bản dịch tiếng Việt của bộ cài.
- [x] **[AI]** Tùy chọn tự kiểm tra cập nhật mỗi ngày, chỉ báo ở khay hệ thống. Mặc định đổi thành bật ở giai đoạn 3A. Đã cập nhật chính sách quyền riêng tư và hướng dẫn. Bản dịch hai chuỗi mới thuộc mục duyệt bản dịch bên dưới.
- [x] **[AI]** Chứng thực nguồn gốc build (`actions/attest-build-provenance`) trong bước `draft-release`, chỉ chạy khi repo Public. Đã chạy thật ở *Release build* #2; API công khai trả về attestation cho cả ba tệp của `2.0.0-rc.1`. Chưa chạy `gh attestation verify` vì máy chưa có `gh`.
- [ ] **[Cả hai]** Xử lý các lỗi nhận được từ bản RC.

**Xong khi:** không còn lỗi nặng đang mở; bài đo 24 giờ không có dấu hiệu rò rỉ.

## Giai đoạn 8. Nghiệm thu trên máy thật

Làm bằng đúng file đã ký. Dùng VM Windows 11 cài mới (Hyper-V hoặc VirtualBox), **bật Smart App Control** ngay khi cài Windows (chỉ bật được trên máy mới cài). Ghi hash Setup, ảnh chụp và kết quả từng mục:

- [ ] **[Bạn]** Tải từ GitHub Release qua trình duyệt (để có Mark of the Web), chạy Setup: SmartScreen/SAC hiện gì?
- [ ] **[Bạn]** Lần đầu chạy: chọn ẩn icon Desktop → thoát qua khay → icon trở lại.
- [ ] **[Bạn]** Kill tiến trình `Nepply.exe` trong Task Manager khi icon đang ẩn → guardian khôi phục icon.
- [ ] **[Bạn]** Gỡ cài đặt khi icon đang ẩn → icon trở lại, không còn file trong `%LOCALAPPDATA%\Programs\Nepply`.
- [ ] **[Bạn]** Cài bản cũ → cài bản mới đè lên (nâng cấp) → giữ Settings, tự khởi động.
- [ ] **[Bạn]** Cập nhật trong app từ bản A lên bản B (cả hai đã ký): tải đúng, từ chối sai chữ ký, tự mở lại.
- [ ] **[Bạn]** AppLocker với rule mặc định (Windows Pro/Enterprise) và tài khoản người dùng thường.
- [ ] **[Bạn]** Windows tiếng Anh và tiếng Việt: ngôn ngữ đầu tiên và bộ cài đúng.
- [ ] **[Bạn]** Windows 10 22H2.
- [ ] **[Bạn]** Máy thật khác: Intel, nhiều màn hình với DPI khác nhau, laptop chạy pin.
- [ ] **[Bạn]** Nếu Defender báo nhầm: gửi file tại <https://www.microsoft.com/wdsi/filesubmission> (chọn *Software developer*).

**Xong khi:** mọi mục đạt hoặc có ghi nhận giới hạn rõ ràng.

## Giai đoạn 9. Phát hành `2.0.0`

- [ ] **[AI]** Đổi `Version`/`InformationalVersion` thành `2.0.0` và `FileVersion` thành `2.0.0.100` trong `Nepply.csproj` và `Nepply.DesktopGuardian.csproj`.
- [ ] **[AI]** Bỏ trang thông báo khỏi bộ cài (`packaging/inno/Notices`, `InfoBeforeFile` trong `[Languages]`), bỏ `RELEASE_NOTICE.txt` khỏi `[Files]` và hai script đóng gói, bỏ banner RC trong README, hướng dẫn, ghi chú phát hành.
- [x] **[AI]** Tạo lại ảnh README theo `docs/images/README.md`. Đã làm ngày 02/11/2026 từ mã nguồn của `2fa1e78`; làm lại nếu giao diện đổi trước `2.0.0`.
- [ ] **[AI]** Hoàn thiện `RELEASE_2.0.0.md` và CHANGELOG; cập nhật mục giới hạn về tự cài cập nhật.
- [ ] **[Cả hai]** Tạo tag `v2.0.0`, phát hành Release chính thức (không đánh dấu pre-release). Chạy `Verify-Release.ps1 -RequireSigned -RequireStable`.
- [ ] **[Bạn]** Kiểm tra người dùng RC đã ký nhận được bản 2.0.0 qua cửa sổ cập nhật.

**Xong khi:** `2.0.0` đã ký có trên trang Releases và cập nhật từ RC hoạt động.

## Giai đoạn 10. Trả nợ kỹ thuật sau 2.0.0

Làm dần, mỗi mục một PR riêng. Không gộp với thay đổi tính năng, vì các mục này đụng nhiều file.

### Test

- [ ] **[AI]** Chuyển test sang xUnit hoặc MSTest theo từng nhóm, để chạy được bằng `dotnet test` và báo giá trị thực tế so với mong đợi.
- [ ] **[AI]** Tách `tests/Nepply.Tests/Program.cs` (hơn 1500 dòng) thành các file theo tính năng.
- [ ] **[AI]** Thay các test phụ thuộc thời gian bằng đồng hồ giả hoặc chờ theo điều kiện.

### Cấu trúc

- [ ] **[AI]** Sửa namespace cho khớp thư mục (`Nepply.Features.*`, `Nepply.Platform.*`), một PR cơ học riêng.
- [ ] **[AI]** Tách `MainWindow` (876 dòng): vị trí cửa sổ, kích hoạt từ cạnh, xem trước Settings, bố cục.
- [ ] **[AI]** Tách `SettingsWindow` (745 dòng) theo từng nhóm cài đặt.
- [ ] **[AI]** Tách `AppSettings` thành dữ liệu thuần; bỏ phụ thuộc vào giao diện và kho lưu file.
- [ ] **[AI]** Bỏ các lời gọi ngược lên `MainWindow` từ `IconPanel`, `MonitorPanel`, `TrayMenu`, `WelcomeWindow`.
- [ ] **[AI]** Bỏ các constructor chỉ dành cho test trong `DesktopIconService`, dùng một interface scanner.
- [ ] **[AI]** Tách các dòng dồn nhiều lệnh và bỏ biến `_settings` cục bộ che field trong `MainWindow`.
- [ ] **[AI]** Giảm nhiễu CodeQL: bỏ file sinh tự động trong `obj` khỏi kết quả, đóng cảnh báo nhầm kèm lý do, dọn vài mục nhỏ có thật (`insideMonitor` thừa ở `PointerActivationGate.cs:37`, ép kiểu thừa ở `DesktopEntryReader.cs:194` và `WidgetDrag.cs:99`).

### Đơn giản hóa và dung lượng

- [ ] **[Cả hai]** Xem lại Guardian: exe 70 MB và khoảng 770 dòng cho việc khôi phục icon. Thử bản biên dịch native để giảm dung lượng.
- [ ] **[Cả hai]** Giảm dung lượng app: `Nepply.exe` 194 MB, Setup 78 MB. Thử nén single-file, đo ảnh hưởng tới thời gian khởi động.
- [ ] **[Bạn]** Quyết định về Microsoft Store: làm tiếp hoặc bỏ code và script MSIX.
- [ ] **[AI]** Gom bớt script kiểm tra trùng lặp, thêm số phiên bản lấy tự động từ tag.

### Phân phối và vận hành

- [ ] **[Cả hai]** Đưa Nepply lên winget sau khi có bản ký.
- [ ] **[AI]** Khi Inno Setup trong `release.yml` đổi phiên bản, đối chiếu `packaging/inno/Languages/Vietnamese.isl` với `Default.isl` mới.
- [ ] **[AI]** Bỏ dần tên legacy `HoverBox` (mutex `Local\HoverBox-SetupGuard`, thư mục `%LOCALAPPDATA%\HoverBox`) sau khi không còn người dùng bản cũ; giữ ít nhất một bản phát hành có migration.
- [ ] **[Bạn]** Ký mọi bản bằng cùng chứng thư: uy tín SmartScreen tăng dần theo lượt tải của cùng chứng thư.
- [ ] **[Bạn]** Nếu nhận donate: kiểm lại điều kiện giấy phép thương mại của Inno Setup.
