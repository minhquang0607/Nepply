using Nepply.Services;

internal static class PointerActivationTests
{
    internal static void Run(Action<string, bool> check)
    {
        var visible = new PointerContext(0, 0, 10, 20, 1);
        var hidden = visible with { CursorFlags = 0 };
        var suppressed = visible with { CursorFlags = 3 };
        var other = visible with { Foreground = 11 };
        var gate = new PointerActivationGate();
        bool Tick(PointerContext? context, bool edge, long time, bool blocked = false, bool inside = true) =>
            gate.Update(context, inside, edge, edge, blocked, time, 120);
        check("Pointer hiện được nhận diện", visible.CursorVisible);
        check("Pointer ẩn và suppressed không được xem là hiện", !hidden.CursorVisible && !suppressed.CursorVisible);
        check("Khởi động ngay tại cạnh không mở", !Tick(visible, true, 0) && !Tick(visible, true, 500));
        check("Rê trong màn hình để chuẩn bị", !Tick(visible, false, 600));
        check("Cạnh cần đủ dwell", !Tick(visible, true, 700) && !Tick(visible, true, 780));
        check("Chuột hiện và dwell đủ mở panel", Tick(visible, true, 860));
        check("Không mở lặp khi đứng nguyên cạnh", !Tick(visible, true, 1200));
        Tick(visible, false, 1300);
        Tick(visible, true, 1400);
        check("Ẩn chuột hủy dwell", !Tick(hidden, true, 1500));
        check("Hiện lại tại cạnh không mở", !Tick(visible, true, 1600) && !Tick(visible, true, 2000));
        Tick(visible, false, 2100);
        Tick(visible, true, 2200);
        check("Rời cạnh sau hiện lại cho phép mở", Tick(visible, true, 2400));
        Tick(visible, false, 2500);
        Tick(visible, true, 2600);
        check("Alt Tab tại cạnh hủy dwell", !Tick(other, true, 2800) && !Tick(other, true, 3000));
        Tick(other, false, 3100);
        Tick(other, true, 3200);
        check("Ngữ cảnh mới cần lượt rê mới", Tick(other, true, 3400));
        Tick(other, false, 3500);
        Tick(other, true, 3600);
        check("API lỗi hủy dwell", !Tick(null, true, 3800) && !Tick(other, true, 4000));
        Tick(other, false, 4100);
        check("Rời màn hình hủy quyền kích hoạt", !Tick(other, false, 4200, inside: false) && !Tick(other, true, 4400));
        Tick(other, false, 4500);
        Tick(other, true, 4600);
        check("Modal chặn và yêu cầu rời cạnh", !Tick(other, true, 4800, blocked: true) && !Tick(other, true, 5000));
        Tick(other, false, 5100);
        Tick(other, true, 5200);
        check("PID đổi dù HWND giống nhau vẫn hủy", !Tick(other with { ProcessId = 21 }, true, 5400));
        check("Kiểm tra cuối cho phép snapshot hợp lệ", PointerActivationGate.CanCommit(visible, visible, true));
        check("Kiểm tra cuối từ chối chuột ẩn", !PointerActivationGate.CanCommit(visible, hidden, true));
        check("Kiểm tra cuối từ chối đổi cửa sổ", !PointerActivationGate.CanCommit(visible, other, true));
        check("Kiểm tra cuối từ chối API lỗi", !PointerActivationGate.CanCommit(visible, null, true));
        check("Kiểm tra cuối từ chối rời cạnh", !PointerActivationGate.CanCommit(visible, visible, false));
        check("Nhường panel khi ứng dụng khác ẩn chuột", PointerActivationGate.ShouldYield(hidden, 99, false));
        check("Không nhường khi chuột hiện", !PointerActivationGate.ShouldYield(visible, 99, false));
        check("Không thu panel giữa chỉnh sửa", !PointerActivationGate.ShouldYield(hidden, 99, true));
        check("Không nhường do con trỏ của chính app", !PointerActivationGate.ShouldYield(hidden, 20, false));
        check("API lỗi không đủ bằng chứng thu panel", !PointerActivationGate.ShouldYield(null, 99, false));
        gate = new();
        gate.Update(visible, true, false, false, false, 0, 0);
        check("Dwell bằng không vẫn yêu cầu lượt rê hợp lệ", gate.Update(visible, true, true, true, false, 1, 0));
        gate.Disarm();
        check("Disarm không cho mở ngay dù dwell bằng không", !gate.Update(visible, true, true, true, false, 2, 0));
        gate = new();
        gate.Update(visible, true, false, false, false, 0, 120);
        gate.Update(visible, true, true, true, false, 100, 120);
        gate.Update(visible, false, false, false, false, 200, 120, besideEdge: true);
        check("Quay lại từ góc thanh tác vụ vẫn cần dwell", !gate.Update(visible, true, true, true, false, 300, 120));
        check("Đi dọc cạnh từ góc thanh tác vụ mở panel", gate.Update(visible, true, true, true, false, 420, 120));
        gate = new();
        gate.Update(visible, false, false, false, false, 0, 120, besideEdge: true);
        gate.Update(visible, true, true, true, false, 100, 120);
        check("Khởi động tại góc thanh tác vụ rồi đi lên mở được", gate.Update(visible, true, true, true, false, 220, 120));
        gate = new();
        gate.Update(visible, false, false, false, false, 0, 120);
        gate.Update(visible, true, true, true, false, 100, 120);
        check("Chuột sang từ màn hình khác vẫn không mở panel", !gate.Update(visible, true, true, true, false, 300, 120));
        gate = new();
        gate.Update(visible, true, false, false, false, 0, 120);
        gate.Update(visible, false, false, false, true, 100, 120, besideEdge: true);
        gate.Update(visible, true, true, true, false, 200, 120);
        check("Góc thanh tác vụ không bỏ qua trạng thái bị chặn", !gate.Update(visible, true, true, true, false, 400, 120));
        var taskbar = new Nepply.Helpers.DisplayScreen("fixture", 0, 0, 1920, 1040, 1, true) { Monitor = (0, 0, 1920, 1080) };
        check("Thanh tác vụ nằm trên màn hình nhưng ngoài vùng làm việc", taskbar.OnMonitor(2, 1060) && !taskbar.Contains(2, 1060));
        check("Màn hình khác không nằm trên màn hình này", !taskbar.OnMonitor(2, 1100) && !taskbar.OnMonitor(-2, 500));
        check("Bản ghi không có thông tin màn hình dùng vùng làm việc", !new Nepply.Helpers.DisplayScreen("fixture", 0, 0, 1920, 1040, 1, true).OnMonitor(2, 1060));
        check("Góc thanh tác vụ nằm cạnh mép trái", Nepply.Modules.Panel.EdgeHitTest.BesideEdge(Nepply.Models.EdgePosition.Left, 2, 1060, 0, 0, 1920, 1040, 16));
        check("Giữa thanh tác vụ không nằm cạnh mép trái", !Nepply.Modules.Panel.EdgeHitTest.BesideEdge(Nepply.Models.EdgePosition.Left, 400, 1060, 0, 0, 1920, 1040, 16));
        check("Góc thanh tác vụ nằm cạnh mép phải", Nepply.Modules.Panel.EdgeHitTest.BesideEdge(Nepply.Models.EdgePosition.Right, 1919, 1060, 0, 0, 1920, 1040, 16));
    }
}
