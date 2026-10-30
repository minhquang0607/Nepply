using System;
using LibreHardwareMonitor.Hardware;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("PawnIO installed (library): " + LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled);
        Console.WriteLine("PawnIO version: " + LibreHardwareMonitor.PawnIo.PawnIo.Version);
        using (var handle = CreateFile(@"\\?\GLOBALROOT\Device\PawnIO", 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
            Console.WriteLine("PawnIO device open: " + (!handle.IsInvalid) + "; Win32 error: " + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = false,
            IsControllerEnabled = false,
            IsNetworkEnabled = !args.Contains("--temperature"),
            IsStorageEnabled = !args.Contains("--temperature")
        };

        Exception? failure = null;
        try
        {
            computer.Open();
            computer.Accept(new UpdateVisitor());

            Console.WriteLine("=== LIBRE HARDWARE MONITOR DUMP ===");
            foreach (var hardware in computer.Hardware)
            {
                PrintHardware(hardware, 0);
                if (args.Contains("--temperature") && hardware.HardwareType == HardwareType.GpuAmd)
                    Console.WriteLine(hardware.GetReport());
            }
        }
        catch (Exception ex) { failure = ex; throw; }
        finally
        {
            try { computer.Close(); }
            catch (Exception ex) when (failure != null)
            { Console.Error.WriteLine("Sensor cleanup also failed: " + ex.Message); }
        }
    }
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    static void PrintHardware(IHardware hardware, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 4);
        Console.WriteLine($"{indent}- Hardware: {hardware.Name} (Type: {hardware.HardwareType})");

        foreach (var sensor in hardware.Sensors)
        {
            Console.WriteLine($"{indent}    Sensor: {sensor.Name} (Type: {sensor.SensorType}) = {sensor.Value}");
        }

        foreach (var subHardware in hardware.SubHardware)
        {
            PrintHardware(subHardware, indentLevel + 1);
        }
    }
}

class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer)
    {
        computer.Traverse(this);
    }

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware subHardware in hardware.SubHardware)
            subHardware.Accept(this);
    }

    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}
