; Vietnamese messages for Inno Setup, translated for Nepply from the English
; Default.isl shipped with Inno Setup 7.1.0 (messages for version 6.5.0+).
; Inno Setup ships no Vietnamese file. When the Inno version in release.yml
; changes, compare this file with the new Default.isl.

[LangOptions]
LanguageName=Tiếng Việt
LanguageID=$042A
LanguageCodePage=1258

[Messages]

; *** Application titles
SetupAppTitle=Cài đặt
SetupWindowTitle=Cài đặt - %1
UninstallAppTitle=Gỡ cài đặt
UninstallAppFullTitle=Gỡ cài đặt %1

; *** Misc. common
InformationTitle=Thông tin
ConfirmTitle=Xác nhận
ErrorTitle=Lỗi

; *** SetupLdr messages
SetupLdrStartupMessage=Chương trình này sẽ cài đặt %1. Bạn có muốn tiếp tục không?
LdrCannotCreateTemp=Không thể tạo tệp tạm. Đã hủy cài đặt
LdrCannotExecTemp=Không thể chạy tệp trong thư mục tạm. Đã hủy cài đặt
HelpTextNote=

; *** Startup error messages
LastErrorMessage=%1.%n%nLỗi %2: %3
SetupFileMissing=Thiếu tệp %1 trong thư mục cài đặt. Vui lòng khắc phục sự cố hoặc lấy bản sao mới của chương trình.
SetupFileCorrupt=Các tệp cài đặt đã bị hỏng. Vui lòng lấy bản sao mới của chương trình.
SetupFileCorruptOrWrongVer=Các tệp cài đặt đã bị hỏng hoặc không tương thích với phiên bản Cài đặt này. Vui lòng khắc phục sự cố hoặc lấy bản sao mới của chương trình.
InvalidParameter=Dòng lệnh chứa tham số không hợp lệ:%n%n%1
SetupAlreadyRunning=Cài đặt đang chạy.
WindowsVersionNotSupported=Chương trình này không hỗ trợ phiên bản Windows đang chạy trên máy tính của bạn.
WindowsServicePackRequired=Chương trình này yêu cầu %1 Service Pack %2 trở lên.
NotOnThisPlatform=Chương trình này sẽ không chạy trên %1.
OnlyOnThisPlatform=Chương trình này phải chạy trên %1.
OnlyOnTheseArchitectures=Chương trình này chỉ cài đặt được trên các phiên bản Windows được thiết kế cho những kiến trúc bộ xử lý sau:%n%n%1
WinVersionTooLowError=Chương trình này yêu cầu %1 phiên bản %2 trở lên.
WinVersionTooHighError=Không thể cài đặt chương trình này trên %1 phiên bản %2 trở lên.
AdminPrivilegesRequired=Bạn phải đăng nhập với tư cách quản trị viên khi cài đặt chương trình này.
PowerUserPrivilegesRequired=Bạn phải đăng nhập với tư cách quản trị viên hoặc thành viên của nhóm Power Users khi cài đặt chương trình này.
SetupAppRunningError=Cài đặt phát hiện %1 đang chạy.%n%nVui lòng đóng tất cả các phiên bản đang chạy của ứng dụng ngay bây giờ, rồi bấm OK để tiếp tục hoặc Hủy để thoát.
UninstallAppRunningError=Gỡ cài đặt phát hiện %1 đang chạy.%n%nVui lòng đóng tất cả các phiên bản đang chạy của ứng dụng ngay bây giờ, rồi bấm OK để tiếp tục hoặc Hủy để thoát.

; *** Startup questions
PrivilegesRequiredOverrideTitle=Chọn chế độ cài đặt
PrivilegesRequiredOverrideInstruction=Chọn chế độ cài đặt
PrivilegesRequiredOverrideText1=%1 có thể được cài đặt cho tất cả người dùng (cần quyền quản trị) hoặc chỉ cho bạn.
PrivilegesRequiredOverrideText2=%1 có thể được cài đặt chỉ cho bạn hoặc cho tất cả người dùng (cần quyền quản trị).
PrivilegesRequiredOverrideAllUsers=Cài đặt cho &tất cả người dùng
PrivilegesRequiredOverrideAllUsersRecommended=Cài đặt cho &tất cả người dùng (khuyên dùng)
PrivilegesRequiredOverrideCurrentUser=&Chỉ cài đặt cho tôi
PrivilegesRequiredOverrideCurrentUserRecommended=&Chỉ cài đặt cho tôi (khuyên dùng)

; *** Misc. errors
ErrorCreatingDir=Cài đặt không thể tạo thư mục "%1"
ErrorTooManyFilesInDir=Không thể tạo tệp trong thư mục "%1" vì thư mục chứa quá nhiều tệp

; *** Setup common messages
ExitSetupTitle=Thoát Cài đặt
ExitSetupMessage=Quá trình cài đặt chưa hoàn tất. Nếu thoát ngay bây giờ, chương trình sẽ không được cài đặt.%n%nBạn có thể chạy lại Cài đặt vào lúc khác để hoàn tất.%n%nThoát Cài đặt?
AboutSetupMenuItem=&Giới thiệu về Cài đặt...
AboutSetupTitle=Giới thiệu về Cài đặt
AboutSetupMessage=%1 %2%n%3%n%nTrang chủ %1:%n%4
AboutSetupNote=
TranslatorNote=

; *** Buttons
ButtonBack=< &Quay lại
ButtonNext=&Tiếp theo >
ButtonInstall=&Cài đặt
ButtonOK=OK
ButtonCancel=Hủy
ButtonYes=&Có
ButtonYesToAll=Có với &tất cả
ButtonNo=&Không
ButtonNoToAll=Không với t&ất cả
ButtonFinish=&Hoàn tất
ButtonBrowse=&Duyệt...
ButtonWizardBrowse=&Duyệt...
ButtonNewFolder=Tạo thư &mục mới

; *** "Select Language" dialog messages
SelectLanguageTitle=Chọn ngôn ngữ cài đặt
SelectLanguageLabel=Chọn ngôn ngữ sử dụng trong quá trình cài đặt.

; *** Common wizard text
ClickNext=Bấm Tiếp theo để tiếp tục hoặc Hủy để thoát Cài đặt.
BeveledLabel=
BrowseDialogTitle=Duyệt tìm thư mục
BrowseDialogLabel=Chọn một thư mục trong danh sách dưới đây, rồi bấm OK.
NewFolderName=Thư mục mới

; *** "Welcome" wizard page
WelcomeLabel1=Chào mừng bạn đến với Trình cài đặt [name]
WelcomeLabel2=Chương trình này sẽ cài đặt [name/ver] trên máy tính của bạn.%n%nBạn nên đóng tất cả các ứng dụng khác trước khi tiếp tục.

; *** "Password" wizard page
WizardPassword=Mật khẩu
PasswordLabel1=Bản cài đặt này được bảo vệ bằng mật khẩu.
PasswordLabel3=Vui lòng nhập mật khẩu, rồi bấm Tiếp theo để tiếp tục. Mật khẩu phân biệt chữ hoa và chữ thường.
PasswordEditLabel=&Mật khẩu:
IncorrectPassword=Mật khẩu bạn nhập không đúng. Vui lòng thử lại.

; *** "License Agreement" wizard page
WizardLicense=Thỏa thuận cấp phép
LicenseLabel=Vui lòng đọc thông tin quan trọng sau đây trước khi tiếp tục.
LicenseLabel3=Vui lòng đọc Thỏa thuận cấp phép sau đây. Bạn phải chấp nhận các điều khoản của thỏa thuận này trước khi tiếp tục cài đặt.
LicenseAccepted=Tôi &chấp nhận thỏa thuận
LicenseNotAccepted=Tôi &không chấp nhận thỏa thuận

; *** "Information" wizard pages
WizardInfoBefore=Thông tin
InfoBeforeLabel=Vui lòng đọc thông tin quan trọng sau đây trước khi tiếp tục.
InfoBeforeClickLabel=Khi đã sẵn sàng tiếp tục cài đặt, hãy bấm Tiếp theo.
WizardInfoAfter=Thông tin
InfoAfterLabel=Vui lòng đọc thông tin quan trọng sau đây trước khi tiếp tục.
InfoAfterClickLabel=Khi đã sẵn sàng tiếp tục cài đặt, hãy bấm Tiếp theo.

; *** "User Information" wizard page
WizardUserInfo=Thông tin người dùng
UserInfoDesc=Vui lòng nhập thông tin của bạn.
UserInfoName=&Tên người dùng:
UserInfoOrg=Tổ &chức:
UserInfoSerial=&Số sê-ri:
UserInfoNameRequired=Bạn phải nhập tên.

; *** "Select Destination Location" wizard page
WizardSelectDir=Chọn vị trí cài đặt
SelectDirDesc=[name] nên được cài đặt ở đâu?
SelectDirLabel3=Cài đặt sẽ cài [name] vào thư mục sau.
SelectDirBrowseLabel=Để tiếp tục, hãy bấm Tiếp theo. Nếu muốn chọn thư mục khác, hãy bấm Duyệt.
DiskSpaceGBLabel=Cần ít nhất [gb] GB dung lượng đĩa trống.
DiskSpaceMBLabel=Cần ít nhất [mb] MB dung lượng đĩa trống.
CannotInstallToNetworkDrive=Cài đặt không thể cài vào ổ đĩa mạng.
CannotInstallToUNCPath=Cài đặt không thể cài vào đường dẫn UNC.
InvalidPath=Bạn phải nhập đường dẫn đầy đủ kèm ký tự ổ đĩa; ví dụ:%n%nC:\App%n%nhoặc đường dẫn UNC dạng:%n%n\\server\share
InvalidDrive=Ổ đĩa hoặc thư mục chia sẻ UNC bạn chọn không tồn tại hoặc không truy cập được. Vui lòng chọn nơi khác.
DiskSpaceWarningTitle=Không đủ dung lượng đĩa
DiskSpaceWarning=Cài đặt cần ít nhất %1 KB dung lượng trống, nhưng ổ đĩa đã chọn chỉ còn %2 KB.%n%nBạn vẫn muốn tiếp tục chứ?
DirNameTooLong=Tên thư mục hoặc đường dẫn quá dài.
InvalidDirName=Tên thư mục không hợp lệ.
BadDirName32=Tên thư mục không được chứa bất kỳ ký tự nào sau đây:%n%n%1
DirExistsTitle=Thư mục đã tồn tại
DirExists=Thư mục:%n%n%1%n%nđã tồn tại. Bạn vẫn muốn cài đặt vào thư mục đó chứ?
DirDoesntExistTitle=Thư mục không tồn tại
DirDoesntExist=Thư mục:%n%n%1%n%nkhông tồn tại. Bạn có muốn tạo thư mục này không?

; *** "Select Components" wizard page
WizardSelectComponents=Chọn thành phần
SelectComponentsDesc=Những thành phần nào nên được cài đặt?
SelectComponentsLabel2=Chọn các thành phần bạn muốn cài đặt; bỏ chọn các thành phần bạn không muốn cài đặt. Bấm Tiếp theo khi đã sẵn sàng tiếp tục.
FullInstallation=Cài đặt đầy đủ
CompactInstallation=Cài đặt gọn nhẹ
CustomInstallation=Cài đặt tùy chỉnh
NoUninstallWarningTitle=Thành phần đã tồn tại
NoUninstallWarning=Cài đặt phát hiện các thành phần sau đã được cài đặt trên máy tính của bạn:%n%n%1%n%nBỏ chọn các thành phần này sẽ không gỡ cài đặt chúng.%n%nBạn vẫn muốn tiếp tục chứ?
ComponentSize1=%1 KB
ComponentSize2=%1 MB
ComponentsDiskSpaceGBLabel=Lựa chọn hiện tại cần ít nhất [gb] GB dung lượng đĩa.
ComponentsDiskSpaceMBLabel=Lựa chọn hiện tại cần ít nhất [mb] MB dung lượng đĩa.

; *** "Select Additional Tasks" wizard page
WizardSelectTasks=Chọn tác vụ bổ sung
SelectTasksDesc=Những tác vụ bổ sung nào nên được thực hiện?
SelectTasksLabel2=Chọn các tác vụ bổ sung bạn muốn Cài đặt thực hiện khi cài [name], rồi bấm Tiếp theo.

; *** "Select Start Menu Folder" wizard page
WizardSelectProgramGroup=Chọn thư mục Start Menu
SelectStartMenuFolderDesc=Cài đặt nên đặt các lối tắt của chương trình ở đâu?
SelectStartMenuFolderLabel3=Cài đặt sẽ tạo các lối tắt của chương trình trong thư mục Start Menu sau.
SelectStartMenuFolderBrowseLabel=Để tiếp tục, hãy bấm Tiếp theo. Nếu muốn chọn thư mục khác, hãy bấm Duyệt.
MustEnterGroupName=Bạn phải nhập tên thư mục.
GroupNameTooLong=Tên thư mục hoặc đường dẫn quá dài.
InvalidGroupName=Tên thư mục không hợp lệ.
BadGroupName=Tên thư mục không được chứa bất kỳ ký tự nào sau đây:%n%n%1
NoProgramGroupCheck2=&Không tạo thư mục Start Menu

; *** "Ready to Install" wizard page
WizardReady=Sẵn sàng cài đặt
ReadyLabel1=Cài đặt đã sẵn sàng bắt đầu cài [name] trên máy tính của bạn.
ReadyLabel2a=Bấm Cài đặt để tiếp tục hoặc bấm Quay lại nếu muốn xem lại hoặc thay đổi cài đặt.
ReadyLabel2b=Bấm Cài đặt để tiếp tục.
ReadyMemoUserInfo=Thông tin người dùng:
ReadyMemoDir=Vị trí cài đặt:
ReadyMemoType=Kiểu cài đặt:
ReadyMemoComponents=Thành phần đã chọn:
ReadyMemoGroup=Thư mục Start Menu:
ReadyMemoTasks=Tác vụ bổ sung:

; *** TDownloadWizardPage wizard page and DownloadTemporaryFile
DownloadingLabel2=Đang tải tệp xuống...
ButtonStopDownload=&Dừng tải xuống
StopDownload=Bạn có chắc muốn dừng tải xuống không?
ErrorDownloadAborted=Đã hủy tải xuống
ErrorDownloadFailed=Tải xuống thất bại: %1 %2
ErrorDownloadSizeFailed=Không lấy được kích thước: %1 %2
ErrorProgress=Tiến độ không hợp lệ: %1 trên %2
ErrorFileSize=Kích thước tệp không hợp lệ: mong đợi %1, thực tế %2

; *** TExtractionWizardPage wizard page and ExtractArchive
ExtractingLabel=Đang giải nén tệp...
ButtonStopExtraction=&Dừng giải nén
StopExtraction=Bạn có chắc muốn dừng giải nén không?
ErrorExtractionAborted=Đã hủy giải nén
ErrorExtractionFailed=Giải nén thất bại: %1

; *** Archive extraction failure details
ArchiveIncorrectPassword=Mật khẩu không đúng
ArchiveIsCorrupted=Tệp nén đã bị hỏng
ArchiveUnsupportedFormat=Định dạng tệp nén không được hỗ trợ

; *** "Preparing to Install" wizard page
WizardPreparing=Đang chuẩn bị cài đặt
PreparingDesc=Cài đặt đang chuẩn bị cài [name] trên máy tính của bạn.
PreviousInstallNotCompleted=Quá trình cài đặt/gỡ bỏ một chương trình trước đó chưa hoàn tất. Bạn cần khởi động lại máy tính để hoàn tất quá trình đó.%n%nSau khi khởi động lại máy tính, hãy chạy lại Cài đặt để hoàn tất việc cài đặt [name].
CannotContinue=Cài đặt không thể tiếp tục. Vui lòng bấm Hủy để thoát.
ApplicationsFound=Các ứng dụng sau đang dùng những tệp mà Cài đặt cần cập nhật. Bạn nên cho phép Cài đặt tự động đóng các ứng dụng này.
ApplicationsFound2=Các ứng dụng sau đang dùng những tệp mà Cài đặt cần cập nhật. Bạn nên cho phép Cài đặt tự động đóng các ứng dụng này. Sau khi cài đặt hoàn tất, Cài đặt sẽ cố gắng khởi động lại các ứng dụng.
CloseApplications=&Tự động đóng các ứng dụng
DontCloseApplications=&Không đóng các ứng dụng
ErrorCloseApplications=Cài đặt không thể tự động đóng tất cả các ứng dụng. Bạn nên đóng mọi ứng dụng đang dùng những tệp mà Cài đặt cần cập nhật trước khi tiếp tục.
PrepareToInstallNeedsRestart=Cài đặt phải khởi động lại máy tính của bạn. Sau khi khởi động lại máy tính, hãy chạy lại Cài đặt để hoàn tất việc cài đặt [name].%n%nBạn có muốn khởi động lại ngay bây giờ không?

; *** "Installing" wizard page
WizardInstalling=Đang cài đặt
InstallingLabel=Vui lòng chờ trong khi Cài đặt cài [name] trên máy tính của bạn.

; *** "Setup Completed" wizard page
FinishedHeadingLabel=Hoàn tất Trình cài đặt [name]
FinishedLabelNoIcons=Cài đặt đã hoàn tất việc cài [name] trên máy tính của bạn.
FinishedLabel=Cài đặt đã hoàn tất việc cài [name] trên máy tính của bạn. Bạn có thể mở ứng dụng bằng các lối tắt đã cài đặt.
ClickFinish=Bấm Hoàn tất để thoát Cài đặt.
FinishedRestartLabel=Để hoàn tất việc cài đặt [name], Cài đặt phải khởi động lại máy tính của bạn. Bạn có muốn khởi động lại ngay bây giờ không?
FinishedRestartMessage=Để hoàn tất việc cài đặt [name], Cài đặt phải khởi động lại máy tính của bạn.%n%nBạn có muốn khởi động lại ngay bây giờ không?
ShowReadmeCheck=Có, tôi muốn xem tệp README
YesRadio=&Có, khởi động lại máy tính ngay bây giờ
NoRadio=&Không, tôi sẽ khởi động lại máy tính sau
RunEntryExec=Chạy %1
RunEntryShellExec=Xem %1

; *** "Setup Needs the Next Disk" stuff
ChangeDiskTitle=Cài đặt cần đĩa tiếp theo
SelectDiskLabel2=Vui lòng đưa Đĩa %1 vào rồi bấm OK.%n%nNếu các tệp trên đĩa này nằm trong thư mục khác với thư mục hiển thị dưới đây, hãy nhập đường dẫn đúng hoặc bấm Duyệt.
PathLabel=Đường &dẫn:
FileNotInDir2=Không tìm thấy tệp "%1" trong "%2". Vui lòng đưa đúng đĩa vào hoặc chọn thư mục khác.
SelectDirectoryLabel=Vui lòng chỉ định vị trí của đĩa tiếp theo.

; *** Installation phase messages
SetupAborted=Quá trình cài đặt chưa hoàn tất.%n%nVui lòng khắc phục sự cố rồi chạy lại Cài đặt.
AbortRetryIgnoreSelectAction=Chọn thao tác
AbortRetryIgnoreRetry=&Thử lại
AbortRetryIgnoreIgnore=&Bỏ qua lỗi và tiếp tục
AbortRetryIgnoreCancel=Hủy cài đặt
RetryCancelSelectAction=Chọn thao tác
RetryCancelRetry=&Thử lại
RetryCancelCancel=Hủy

; *** Installation status messages
StatusClosingApplications=Đang đóng các ứng dụng...
StatusCreateDirs=Đang tạo thư mục...
StatusExtractFiles=Đang giải nén tệp...
StatusDownloadFiles=Đang tải tệp xuống...
StatusCreateIcons=Đang tạo lối tắt...
StatusCreateIniEntries=Đang tạo mục INI...
StatusCreateRegistryEntries=Đang tạo mục registry...
StatusRegisterFiles=Đang đăng ký tệp...
StatusSavingUninstall=Đang lưu thông tin gỡ cài đặt...
StatusRunProgram=Đang hoàn tất cài đặt...
StatusRestartingApplications=Đang khởi động lại các ứng dụng...
StatusRollback=Đang hoàn tác các thay đổi...

; *** Misc. errors
ErrorInternal2=Lỗi nội bộ: %1
ErrorFunctionFailedNoCode=%1 thất bại
ErrorFunctionFailed=%1 thất bại; mã %2
ErrorFunctionFailedWithMessage=%1 thất bại; mã %2.%n%3
ErrorExecutingProgram=Không thể chạy tệp:%n%1

; *** Registry errors
ErrorRegOpenKey=Lỗi khi mở khóa registry:%n%1\%2
ErrorRegCreateKey=Lỗi khi tạo khóa registry:%n%1\%2
ErrorRegWriteKey=Lỗi khi ghi vào khóa registry:%n%1\%2

; *** INI errors
ErrorIniEntry=Lỗi khi tạo mục INI trong tệp "%1".

; *** File copying errors
FileAbortRetryIgnoreSkipNotRecommended=&Bỏ qua tệp này (không khuyên dùng)
FileAbortRetryIgnoreIgnoreNotRecommended=&Bỏ qua lỗi và tiếp tục (không khuyên dùng)
SourceIsCorrupted=Tệp nguồn đã bị hỏng
SourceDoesntExist=Tệp nguồn "%1" không tồn tại
SourceVerificationFailed=Xác minh tệp nguồn thất bại: %1
VerificationSignatureDoesntExist=Tệp chữ ký "%1" không tồn tại
VerificationSignatureInvalid=Tệp chữ ký "%1" không hợp lệ
VerificationKeyNotFound=Tệp chữ ký "%1" dùng khóa không xác định
VerificationFileNameIncorrect=Tên tệp không đúng
VerificationFileTagIncorrect=Thẻ của tệp không đúng
VerificationFileSizeIncorrect=Kích thước tệp không đúng
VerificationFileHashIncorrect=Mã băm của tệp không đúng
ExistingFileReadOnly2=Không thể thay thế tệp hiện có vì tệp được đánh dấu chỉ đọc.
ExistingFileReadOnlyRetry=&Xóa thuộc tính chỉ đọc và thử lại
ExistingFileReadOnlyKeepExisting=&Giữ tệp hiện có
ErrorReadingExistingDest=Đã xảy ra lỗi khi đọc tệp hiện có:
FileExistsSelectAction=Chọn thao tác
FileExists2=Tệp đã tồn tại.
FileExistsOverwriteExisting=&Ghi đè tệp hiện có
FileExistsKeepExisting=&Giữ tệp hiện có
FileExistsOverwriteOrKeepAll=&Làm như vậy cho các xung đột tiếp theo
ExistingFileNewerSelectAction=Chọn thao tác
ExistingFileNewer2=Tệp hiện có mới hơn tệp mà Cài đặt đang cố cài.
ExistingFileNewerOverwriteExisting=&Ghi đè tệp hiện có
ExistingFileNewerKeepExisting=&Giữ tệp hiện có (khuyên dùng)
ExistingFileNewerOverwriteOrKeepAll=&Làm như vậy cho các xung đột tiếp theo
ErrorChangingAttr=Đã xảy ra lỗi khi đổi thuộc tính của tệp hiện có:
ErrorCreatingTemp=Đã xảy ra lỗi khi tạo tệp trong thư mục đích:
ErrorReadingSource=Đã xảy ra lỗi khi đọc tệp nguồn:
ErrorCopying=Đã xảy ra lỗi khi sao chép tệp:
ErrorDownloading=Đã xảy ra lỗi khi tải tệp xuống:
ErrorExtracting=Đã xảy ra lỗi khi giải nén tệp nén:
ErrorReplacingExistingFile=Đã xảy ra lỗi khi thay thế tệp hiện có:
ErrorRestartReplace=RestartReplace thất bại:
ErrorRenamingTemp=Đã xảy ra lỗi khi đổi tên tệp trong thư mục đích:
ErrorRegisterServer=Không thể đăng ký DLL/OCX: %1
ErrorRegSvr32Failed=RegSvr32 thất bại với mã thoát %1
ErrorRegisterTypeLib=Không thể đăng ký thư viện kiểu: %1

; *** Uninstall display name markings
UninstallDisplayNameMark=%1 (%2)
UninstallDisplayNameMarks=%1 (%2, %3)
UninstallDisplayNameMark32Bit=32-bit
UninstallDisplayNameMark64Bit=64-bit
UninstallDisplayNameMarkAllUsers=Tất cả người dùng
UninstallDisplayNameMarkCurrentUser=Người dùng hiện tại

; *** Post-installation errors
ErrorOpeningReadme=Đã xảy ra lỗi khi mở tệp README.
ErrorRestartingComputer=Cài đặt không thể khởi động lại máy tính. Vui lòng tự thực hiện việc này.

; *** Uninstaller messages
UninstallNotFound=Tệp "%1" không tồn tại. Không thể gỡ cài đặt.
UninstallOpenError=Không thể mở tệp "%1". Không thể gỡ cài đặt
UninstallUnsupportedVer=Tệp nhật ký gỡ cài đặt "%1" có định dạng mà phiên bản trình gỡ cài đặt này không nhận ra. Không thể gỡ cài đặt
UninstallUnknownEntry=Gặp mục không xác định (%1) trong nhật ký gỡ cài đặt
ConfirmUninstall=Bạn có chắc muốn gỡ hoàn toàn %1 và tất cả các thành phần của nó không?
UninstallOnlyOnWin64=Bản cài đặt này chỉ có thể được gỡ trên Windows 64-bit.
OnlyAdminCanUninstall=Bản cài đặt này chỉ có thể được gỡ bởi người dùng có quyền quản trị.
UninstallStatusLabel=Vui lòng chờ trong khi %1 được gỡ khỏi máy tính của bạn.
UninstalledAll=%1 đã được gỡ khỏi máy tính của bạn.
UninstalledMost=Đã gỡ cài đặt %1.%n%nMột số thành phần không gỡ được. Bạn có thể tự xóa chúng.
UninstalledAndNeedsRestart=Để hoàn tất việc gỡ cài đặt %1, máy tính của bạn phải được khởi động lại.%n%nBạn có muốn khởi động lại ngay bây giờ không?
UninstallDataCorrupted=Tệp "%1" đã bị hỏng. Không thể gỡ cài đặt

; *** Uninstallation phase messages
ConfirmDeleteSharedFileTitle=Xóa tệp dùng chung?
ConfirmDeleteSharedFile2=Hệ thống cho biết tệp dùng chung sau không còn được chương trình nào sử dụng. Bạn có muốn Gỡ cài đặt xóa tệp dùng chung này không?%n%nNếu vẫn còn chương trình đang dùng tệp này mà tệp bị xóa, những chương trình đó có thể không hoạt động đúng. Nếu không chắc chắn, hãy chọn Không. Giữ tệp trên hệ thống sẽ không gây hại gì.
SharedFileNameLabel=Tên tệp:
SharedFileLocationLabel=Vị trí:
WizardUninstalling=Trạng thái gỡ cài đặt
StatusUninstalling=Đang gỡ cài đặt %1...

; *** Shutdown block reasons
ShutdownBlockReasonInstallingApp=Đang cài đặt %1.
ShutdownBlockReasonUninstallingApp=Đang gỡ cài đặt %1.

[CustomMessages]

NameAndVersion=%1 phiên bản %2
AdditionalIcons=Lối tắt bổ sung:
CreateDesktopIcon=Tạo lối tắt trên &màn hình nền
CreateQuickLaunchIcon=Tạo lối tắt &Quick Launch
ProgramOnTheWeb=%1 trên web
UninstallProgram=Gỡ cài đặt %1
LaunchProgram=Mở %1
AssocFileExtension=&Liên kết %1 với phần mở rộng tệp %2
AssocingFileExtension=Đang liên kết %1 với phần mở rộng tệp %2...
AutoStartProgramGroupDescription=Khởi động:
AutoStartProgram=Tự động khởi động %1
AddonHostProgramNotFound=Không tìm thấy %1 trong thư mục bạn đã chọn.%n%nBạn vẫn muốn tiếp tục chứ?
