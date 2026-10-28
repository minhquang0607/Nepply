using System.Windows;
using Nepply.Models;

namespace Nepply.Modules.Appearance;

internal static class MotionPolicy
{
    internal static MotionMode Mode { get; private set; } = MotionMode.Smooth;
    internal static bool Enabled => Mode != MotionMode.Off && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
    internal static void Configure(MotionMode mode) => Mode = Enum.IsDefined(mode) ? mode : MotionMode.Smooth;
    internal static TimeSpan Duration(double milliseconds) => TimeSpan.FromMilliseconds(Mode == MotionMode.Quick ? milliseconds * .65 : milliseconds);
}
