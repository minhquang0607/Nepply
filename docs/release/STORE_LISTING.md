# Nội dung gửi Microsoft Store

Bản soạn sẵn để dán vào Partner Center, đối chiếu với tài liệu Microsoft và Store Policies 7.20 ngày 02/11/2026. Bản dịch do AI làm, chưa có người bản ngữ đọc lại.

## Thông tin chung

| Trường | Nội dung |
| --- | --- |
| Tên sản phẩm | Nepply |
| Danh mục | Utilities & tools |
| Giá | Free |
| Thị trường | Tất cả thị trường Partner Center cho phép |
| Lần gửi đầu | Audience chọn **Private audience** để tự thử trước khi mở công khai |
| Privacy policy URL | https://github.com/minhquang0607/Nepply/blob/main/docs/legal/PRIVACY.md |
| Website | https://github.com/minhquang0607/Nepply |
| Support contact | https://github.com/minhquang0607/Nepply/issues |
| Từ khóa (tối đa 7) | desktop, panel, launcher, system monitor, CPU, GPU, sidebar |
| Xếp hạng tuổi | Trả lời đúng bảng hỏi IARC: app không có nội dung người dùng, không có mua bán, không có giao tiếp giữa người dùng, không thu thập vị trí |
| Ảnh | `packaging/msix/store/screenshots` (3 ảnh 1920 × 1080). Microsoft khuyên ít nhất 4 ảnh, nên bổ sung ảnh chụp thật khi cài được bản Store |

Các ảnh dựng từ view WPF thật trong test, số liệu và tên mục là dữ liệu minh họa. Bạn nên thay bằng ảnh chụp thật khi có thể.

## Giải trình runFullTrust (Submission options)

Dán nguyên văn tiếng Anh vào ô giải trình:

> Nepply is a full-trust WPF desktop application packaged with MSIX. It needs runFullTrust because it reads the Windows Shell Desktop folder to list and open the user's Desktop items, can hide the Desktop icon window when the user turns that on, shows a screen-edge panel across all apps, registers user-chosen global hotkeys, shows a notification area icon, and reads CPU, memory, GPU and network statistics through a helper process from the same package. It does not need administrator rights, does not install drivers or services, and does not download executable code.

## Ghi chú cho người duyệt (Notes for certification)

> Nepply opens from the screen edge: move the pointer to the left edge of the screen, or use the notification area icon. No account or test data is needed. No hotkey is preset; one can be set in Settings > Shortcuts. Some temperature readings depend on hardware and can show a dash. The app starts with Windows only if the user turns that on. Hiding the Windows Desktop icons is an option the user turns on in Settings; it hides the icon window for the current session, changes no Windows setting, and is undone when the app exits. If Explorer restarts while the app is running, the app hides the icons again; if the app is not running, a restarted Explorer shows them. The Store edition updates only through the Store and does not contact GitHub for updates.

## Nội dung theo ngôn ngữ

Gói khai báo 8 ngôn ngữ, nên cần đủ 8 bản dưới đây. Mô tả là văn bản thuần, không có HTML hay URL.

### English (en-US)

**Short description:** A tidier desktop. Open your Desktop items and check system stats from a small panel at the edge of your screen.

**Description:**

Nepply is a small panel that sits at the edge of your Windows screen. Move the pointer to the edge to open it, then open a Desktop item, search by name, or check how your PC is doing without returning to the desktop.

Turn on one option to hide the icons on your Desktop and enjoy a clean wallpaper. Your files stay where they are, and the icons come back when Nepply exits.

Choose the screen edge, the grid of items and the size of the panel. The look follows the light or dark mode of Windows, or you can pick your own colors. Nepply runs locally: no ads, no analytics and no automatic crash uploads.

Available readings depend on your hardware and permissions. CPU temperature and some other sensors are unavailable on many PCs. Nepply does not install drivers and does not need administrator rights.

**Features:**

- Hide the Windows Desktop icons and use them from the panel instead
- Open from any screen edge you choose, and pin the panel when needed
- Search and open Desktop items, with the Windows context menu
- CPU, RAM, GPU, integrated GPU and network activity
- Columns and rows that fit your screen
- Light, dark and system themes with custom colors
- Eight languages, with shortcuts you set yourself

**What's new:** First release on Microsoft Store.

### Tiếng Việt (vi-VN)

**Mô tả ngắn:** Desktop gọn hơn. Mở các mục Desktop và xem thông số máy ngay trên một panel nhỏ ở cạnh màn hình.

**Mô tả:**

Nepply là một panel nhỏ nằm ở cạnh màn hình Windows. Đưa chuột tới cạnh để mở, rồi mở mục Desktop, tìm theo tên hoặc xem tình trạng máy mà không cần quay về màn hình nền.

Bật một tùy chọn để ẩn biểu tượng trên Desktop, giữ màn hình nền thật gọn. File vẫn ở nguyên chỗ cũ, và biểu tượng trở lại khi thoát Nepply.

Bạn chọn cạnh màn hình, lưới hiển thị và kích thước panel. Giao diện theo chế độ sáng hoặc tối của Windows, hoặc bạn tự chọn màu. Nepply chạy ngay trên máy: không quảng cáo, không analytics và không tự gửi báo cáo lỗi.

Số đo hiển thị được tùy phần cứng và quyền truy cập. Nhiều máy không đọc được nhiệt độ CPU và một số cảm biến khác. Nepply không cài driver và không cần quyền quản trị.

**Tính năng:**

- Ẩn biểu tượng trên Desktop Windows và dùng chúng ngay trong panel
- Mở từ cạnh màn hình bạn chọn, ghim panel khi cần
- Tìm và mở các mục Desktop, có menu chuột phải của Windows
- Theo dõi CPU, RAM, GPU, GPU tích hợp và mạng
- Chỉnh số cột và hàng cho vừa màn hình
- Giao diện sáng, tối, theo hệ thống và màu tùy chọn
- Tám ngôn ngữ, phím tắt do bạn tự đặt

**Có gì mới:** Bản phát hành đầu tiên trên Microsoft Store.

### 日本語 (ja-JP)

**短い説明:** デスクトップをすっきりと。画面の端にある小さなパネルから、デスクトップの項目を開いてシステムの状態を確認できます。

**説明:**

Nepply は Windows の画面の端に配置する小さなパネルです。ポインターを端に移動すると開き、デスクトップに戻らなくても項目を開いたり、名前で検索したり、PC の状態を確認したりできます。

オプションをオンにすると、デスクトップのアイコンを非表示にして壁紙をすっきり見せられます。ファイルは移動されず、Nepply を終了するとアイコンは元に戻ります。

画面の端、項目のグリッド、パネルのサイズを選べます。外観は Windows のライト モードまたはダーク モードに従い、好みの色も選べます。Nepply はローカルで動作し、広告、分析、クラッシュ情報の自動送信はありません。

読み取れる項目はハードウェアと権限によって異なります。多くの PC では CPU 温度などの一部のセンサーを読み取れません。Nepply はドライバーをインストールせず、管理者権限も必要ありません。

**機能:**

- Windows のデスクトップ アイコンを非表示にして、パネルから利用
- 選んだ画面の端から開き、必要に応じてパネルをピン留め
- デスクトップの項目を検索して開き、Windows のコンテキスト メニューも利用可能
- CPU、RAM、GPU、内蔵 GPU、ネットワークの状態を表示
- 画面に合わせて列と行を調整
- ライト、ダーク、システムのテーマとカスタム カラー
- 8 言語に対応し、ショートカットは自分で設定

**新機能:** Microsoft Store での最初のリリースです。

### Deutsch (de-DE)

**Kurzbeschreibung:** Ein aufgeräumterer Desktop. Öffnen Sie Desktopelemente und prüfen Sie Systemwerte über ein kleines Panel am Bildschirmrand.

**Beschreibung:**

Nepply ist ein kleines Panel am Rand Ihres Windows-Bildschirms. Bewegen Sie den Mauszeiger an den Rand, um es zu öffnen. Dann können Sie Desktopelemente öffnen, nach Namen suchen oder den Zustand Ihres PCs prüfen, ohne zum Desktop zurückzukehren.

Mit einer Option blenden Sie die Symbole auf dem Desktop aus und genießen ein aufgeräumtes Hintergrundbild. Ihre Dateien bleiben an ihrem Ort, und die Symbole erscheinen wieder, wenn Nepply beendet wird.

Sie wählen den Bildschirmrand, das Raster und die Größe des Panels. Das Aussehen folgt dem hellen oder dunklen Modus von Windows, oder Sie wählen eigene Farben. Nepply läuft lokal: keine Werbung, keine Analysen und kein automatisches Hochladen von Absturzberichten.

Welche Werte verfügbar sind, hängt von Hardware und Berechtigungen ab. Die CPU-Temperatur und einige andere Sensoren sind auf vielen PCs nicht verfügbar. Nepply installiert keine Treiber und benötigt keine Administratorrechte.

**Funktionen:**

- Windows-Desktopsymbole ausblenden und stattdessen im Panel verwenden
- Öffnen vom gewählten Bildschirmrand, Panel bei Bedarf anheften
- Desktopelemente suchen und öffnen, mit dem Windows-Kontextmenü
- CPU, RAM, GPU, integrierte GPU und Netzwerkaktivität
- Spalten und Zeilen passend zum Bildschirm
- Helles, dunkles und System-Design mit eigenen Farben
- Acht Sprachen, Tastenkürzel legen Sie selbst fest

**Neuigkeiten:** Erste Version im Microsoft Store.

### 简体中文 (zh-CN)

**简短说明:** 让桌面更整洁。通过屏幕边缘的小面板打开桌面项目并查看系统状态。

**说明:**

Nepply 是位于 Windows 屏幕边缘的小面板。将指针移到边缘即可打开，无需返回桌面就能打开桌面项目、按名称搜索或查看电脑状态。

打开一个选项即可隐藏桌面上的图标，让壁纸保持整洁。文件仍在原处，退出 Nepply 后图标会恢复。

你可以选择屏幕边缘、项目网格和面板大小。外观跟随 Windows 的浅色或深色模式，也可以选择自己的颜色。Nepply 在本地运行：没有广告、没有分析，也不会自动上传崩溃报告。

可显示的读数取决于硬件和权限。许多电脑无法读取 CPU 温度和部分其他传感器。Nepply 不安装驱动程序，也不需要管理员权限。

**功能:**

- 隐藏 Windows 桌面图标，改在面板中使用
- 从你选择的屏幕边缘打开，需要时固定面板
- 搜索并打开桌面项目，支持 Windows 上下文菜单
- 显示 CPU、内存、GPU、集成 GPU 和网络活动
- 按屏幕调整列数和行数
- 浅色、深色和跟随系统的主题，支持自定义颜色
- 支持八种语言，快捷键由你自行设置

**新增内容:** Microsoft Store 上的首个版本。

### Русский (ru-RU)

**Краткое описание:** Более аккуратный рабочий стол. Открывайте его элементы и смотрите состояние системы на небольшой панели у края экрана.

**Описание:**

Nepply — это небольшая панель у края экрана Windows. Переместите указатель к краю, чтобы открыть её, а затем открывайте элементы рабочего стола, ищите по имени или проверяйте состояние компьютера, не возвращаясь на рабочий стол.

Включите один параметр, чтобы скрыть значки на рабочем столе и оставить чистые обои. Файлы остаются на месте, а значки возвращаются при выходе из Nepply.

Вы выбираете край экрана, сетку элементов и размер панели. Оформление следует светлому или тёмному режиму Windows, также можно выбрать свои цвета. Nepply работает локально: без рекламы, аналитики и автоматической отправки отчётов о сбоях.

Доступные показания зависят от оборудования и разрешений. На многих компьютерах температура процессора и некоторые другие датчики недоступны. Nepply не устанавливает драйверы и не требует прав администратора.

**Возможности:**

- Скрытие значков рабочего стола Windows и доступ к ним из панели
- Открытие от выбранного края экрана, закрепление панели при необходимости
- Поиск и открытие элементов рабочего стола, контекстное меню Windows
- Процессор, память, GPU, встроенный GPU и сеть
- Столбцы и строки под размер экрана
- Светлая, тёмная и системная темы, собственные цвета
- Восемь языков, сочетания клавиш настраиваете вы сами

**Что нового:** Первый выпуск в Microsoft Store.

### Français (fr-FR)

**Description courte :** Un bureau mieux rangé. Ouvrez vos éléments du Bureau et consultez l'état du système depuis un petit panneau au bord de l'écran.

**Description :**

Nepply est un petit panneau placé au bord de votre écran Windows. Amenez le pointeur au bord pour l'ouvrir, puis ouvrez un élément du Bureau, recherchez par nom ou consultez l'état de votre PC sans revenir au Bureau.

Activez une option pour masquer les icônes du Bureau et profiter d'un fond d'écran dégagé. Vos fichiers restent à leur place et les icônes reviennent lorsque Nepply se ferme.

Vous choisissez le bord de l'écran, la grille d'éléments et la taille du panneau. L'apparence suit le mode clair ou sombre de Windows, ou vos propres couleurs. Nepply fonctionne localement : ni publicité, ni statistiques, ni envoi automatique de rapports de plantage.

Les mesures disponibles dépendent du matériel et des autorisations. La température du processeur et certains autres capteurs ne sont pas disponibles sur de nombreux PC. Nepply n'installe aucun pilote et ne nécessite pas de droits d'administrateur.

**Fonctionnalités :**

- Masquez les icônes du Bureau Windows et retrouvez-les dans le panneau
- Ouverture depuis le bord d'écran de votre choix, panneau épinglable
- Recherche et ouverture des éléments du Bureau, avec le menu contextuel de Windows
- Processeur, mémoire, GPU, GPU intégré et réseau
- Colonnes et lignes adaptées à votre écran
- Thèmes clair, sombre et système, couleurs personnalisées
- Huit langues, raccourcis définis par vous

**Nouveautés :** Première version sur le Microsoft Store.

### ไทย (th-TH)

**คำอธิบายสั้น:** เดสก์ท็อปที่เป็นระเบียบกว่าเดิม เปิดรายการบนเดสก์ท็อปและดูสถานะระบบได้จากแผงเล็กๆ ที่ขอบหน้าจอ

**คำอธิบาย:**

Nepply คือแผงขนาดเล็กที่อยู่ที่ขอบหน้าจอ Windows เลื่อนตัวชี้ไปที่ขอบเพื่อเปิด จากนั้นเปิดรายการบนเดสก์ท็อป ค้นหาตามชื่อ หรือดูสถานะของพีซีได้โดยไม่ต้องกลับไปที่เดสก์ท็อป

เปิดตัวเลือกเดียวเพื่อซ่อนไอคอนบนเดสก์ท็อปและให้วอลเปเปอร์ดูสะอาดตา ไฟล์ยังอยู่ที่เดิม และไอคอนจะกลับมาเมื่อออกจาก Nepply

คุณเลือกขอบหน้าจอ ตารางรายการ และขนาดของแผงได้ รูปลักษณ์จะเป็นไปตามโหมดสว่างหรือมืดของ Windows หรือเลือกสีเองก็ได้ Nepply ทำงานภายในเครื่อง ไม่มีโฆษณา ไม่มีการวิเคราะห์ และไม่อัปโหลดรายงานข้อขัดข้องโดยอัตโนมัติ

ค่าที่อ่านได้ขึ้นอยู่กับฮาร์ดแวร์และสิทธิ์การเข้าถึง พีซีจำนวนมากอ่านอุณหภูมิ CPU และเซ็นเซอร์บางตัวไม่ได้ Nepply ไม่ติดตั้งไดรเวอร์และไม่ต้องใช้สิทธิ์ผู้ดูแลระบบ

**คุณลักษณะ:**

- ซ่อนไอคอนบนเดสก์ท็อป Windows แล้วใช้งานจากแผงแทน
- เปิดจากขอบหน้าจอที่คุณเลือก และตรึงแผงเมื่อต้องการ
- ค้นหาและเปิดรายการบนเดสก์ท็อป พร้อมเมนูบริบทของ Windows
- CPU, RAM, GPU, GPU ในตัว และกิจกรรมเครือข่าย
- ปรับจำนวนคอลัมน์และแถวให้พอดีกับหน้าจอ
- ธีมสว่าง มืด และตามระบบ พร้อมสีที่กำหนดเอง
- แปดภาษา และปุ่มลัดที่คุณตั้งเอง

**มีอะไรใหม่:** รุ่นแรกบน Microsoft Store
