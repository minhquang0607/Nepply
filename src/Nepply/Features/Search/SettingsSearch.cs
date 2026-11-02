using System.Globalization;
using System.Text;

namespace Nepply.Services;

internal static class SettingsSearch
{
    internal sealed record Entry(string Label, int Section, string Control, string Keywords);
    private static readonly Entry[] Entries =
    [
        new("Giao diện · Ngôn ngữ",0,"LanguageChoice","language tiếng việt english japanese german chinese russian french thai"),
        new("Giao diện · Hiệu ứng neon",0,"NeonEnabled","neon glow bật tắt"),
        new("Giao diện · Chế độ màu",0,"ThemeChoice","theme dark light sáng tối windows"),
        new("Giao diện · Kim loại & neon",0,"AccentChoice","màu nhấn champagne graphite metallic neon palette"),
        new("Giao diện · Màu riêng RGB",0,"ColorButton","custom hex màu riêng rgb"),
        new("Giao diện · Bề mặt ánh kim",0,"MetallicEnabled","bật tắt kim loại phản sáng nền phẳng"),
        new("Giao diện · Chuyển động giao diện",0,"MotionChoice","animation mượt nhanh tắt giảm chuyển động hiệu ứng"),
        new("Theo dõi · Ngủ khi ẩn",3,"SuspendMonitor","monitor tiết kiệm cpu ram nền ngủ cảm biến gaming"),
        new("Giao diện · Ánh kim chuyển động",0,"DynamicMaterial","animation hiệu ứng màu động metallic chuyển động"),
        new("Giao diện · Ánh neon",0,"GlowSlider","glow hiệu ứng cường độ"),
        new("Giao diện · Độ đậm nền",0,"OpacitySlider","trong suốt opacity"),
        new("Panel · Cạnh neo",1,"EdgeChoice","trái phải trên vị trí dock"),
        new("Panel · Màn hình",1,"ScreenChoice","display nhiều màn hình"),
        new("Panel · Chiều rộng",1,"WidthSlider","kích thước rộng"),
        new("Panel · Chiều dài cạnh trên",1,"TopWidthSlider","neo trên ngang"),
        new("Panel · Chiều cao cạnh trên",1,"HeightSlider","neo trên cao thấp kích thước"),
        new("Panel · Độ rộng vùng chạm mép",1,"ZoneSlider","vùng kích hoạt trigger chuột mép rộng"),
        new("Panel · Tự ẩn",1,"DelaySlider","thời gian chờ tự ẩn"),
        new("Panel · Chạm mép",1,"DwellSlider","thời gian mở chuột mép"),
        new("Phím tắt · Mở / đóng panel",5,"HotkeyButton","hotkey bàn phím mở đóng shortcut"),
        new("Phím tắt · Mở panel và tìm",5,"ShortcutSearchDesktop","hotkey shortcut tìm kiếm search"),
        new("Phím tắt · Ẩn / hiện icon Desktop",5,"ShortcutToggleDesktopIcons","hotkey shortcut icon desktop biểu tượng"),
        new("Phím tắt · Ghim panel",5,"ShortcutTogglePin","hotkey shortcut pin ghim giữ mở"),
        new("Phím tắt · Mở Cài đặt",5,"ShortcutOpenSettings","hotkey shortcut settings cài đặt"),
        new("Phím tắt · Tìm trên Desktop",5,"ShortcutFocusSearch","hotkey shortcut ctrl f tìm kiếm"),
        new("Phím tắt · Làm mới Desktop",5,"ShortcutRefreshDesktop","hotkey shortcut refresh f5 làm mới"),
        new("Desktop · Lưới icon",2,"GridPicker","icon grid hàng cột ngang dọc lưới chấm kích thước"),
        new("Desktop · Ẩn icon Windows",2,"HideIcons","desktop biểu tượng windows"),
        new("Desktop · Gộp lối tắt",2,"MergeShortcuts","trùng duplicate shortcut đường dẫn gộp"),
        new("Desktop · Nhấp để mở",2,"SingleClick","click nhấp đơn nhấp đúp double một lần hai lần mở"),
        new("Desktop · Hiển thị file ẩn",2,"HiddenFiles","hidden tập tin tệp thư mục ẩn"),
        new("Desktop · Icon hệ thống",2,"SystemIcons","system biểu tượng thùng rác recycle bin bảng điều khiển control panel pc này"),
        new("Theo dõi · GPU / iGPU",3,"GpuModeChoice","monitor card đồ họa gpu igpu hiển thị"),
        new("Theo dõi · Bật cảm biến",3,"Sensors","monitor cpu gpu nhiệt độ xung nhịp"),
        new("Theo dõi · Chu kỳ cập nhật",3,"IntervalSlider","monitor realtime tần suất tốc độ ms"),
        new("Theo dõi · Tiết kiệm khi ẩn",3,"EcoMode","monitor eco tài nguyên cpu ram"),
        new("Theo dõi · Kết nối mạng",3,"NetworkChoice","monitor wifi ethernet network"),
        new("Ứng dụng · Khởi động Windows",4,"AutoStart","startup tự chạy nền"),
        new("Ứng dụng · Tự kiểm tra cập nhật",4,"AutoCheckUpdates","update phiên bản mới định kỳ hằng ngày"),
        new("Ứng dụng · Bố cục mẫu",4,"PresetCards","preset gọn thoáng dải ngang mẫu"),
        new("Ứng dụng · Bố cục",4,"ProfileStatus","profile nhập xuất bố cục chia sẻ"),
        new("Ứng dụng · Quyền riêng tư",4,"AboutVersion","privacy dữ liệu analytics hỗ trợ tác giả phiên bản")
    ];
    internal static Entry[] Find(string query) => Find(query, ProcessPackageIdentity.IsPackaged);
    internal static Entry[] Find(string query, bool packaged)
    {
        var words = SearchText.Words(query);
        if (words.Length == 0) return [];
        return Entries.Where(e => !packaged || e.Control != "AutoCheckUpdates")
            .Select(e => (Original: e, Local: Localized(e)))
            .Where(e => SearchText.Matches(e.Original.Label + " " + e.Original.Keywords + " " + e.Local.Label, words))
            .Select(e => e.Local).ToArray();
    }
    private static Entry Localized(Entry entry)
    {
        if (LocalizationService.CurrentLanguage == "vi") return entry;
        string key = entry.Control switch
        {
            "LanguageChoice" => "Language",
            "NeonEnabled" => "Neon",
            "ThemeChoice" => "Theme",
            "AccentChoice" => "Materials",
            "ColorButton" => "CustomColor",
            "MetallicEnabled" => "Metallic",
            "MotionChoice" => "Motion",
            "SuspendMonitor" => "Suspend",
            "DynamicMaterial" => "Sheen",
            "GlowSlider" => "Glow",
            "OpacitySlider" => "Opacity",
            "EdgeChoice" => "Edge",
            "ScreenChoice" => "Screen",
            "WidthSlider" => "Width",
            "TopWidthSlider" => "TopWidth",
            "HeightSlider" => "TopHeight",
            "ZoneSlider" => "TriggerZone",
            "DelaySlider" => "HideDelay",
            "DwellSlider" => "TriggerDelay",
            "HotkeyButton" => "Hotkey",
            "GridPicker" => "IconGrid",
            "HideIcons" => "HideDesktop",
            "MergeShortcuts" => "MergeShortcuts",
            "SingleClick" => "SingleClick",
            "HiddenFiles" => "HiddenFiles",
            "SystemIcons" => "SystemIcons",
            "GpuModeChoice" => "Device",
            "Sensors" => "Sensors",
            "IntervalSlider" => "Interval",
            "EcoMode" => "Eco",
            "NetworkChoice" => "NetworkAdapter",
            "AutoStart" => "Startup",
            "AutoCheckUpdates" => "UpdateAutoCheck",
            "PresetCards" or "ProfileStatus" => "Layouts",
            "AboutVersion" => "About",
            { } control when control.StartsWith("Shortcut", StringComparison.Ordinal) => control,
            _ => entry.Label
        };
        return entry with { Label = LocalizationService.Get(key) };
    }
}
