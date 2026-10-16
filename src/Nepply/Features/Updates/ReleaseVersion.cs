using System.Text.RegularExpressions;

namespace Nepply.Modules.Updates;

internal sealed record ReleaseVersion(int Major, int Minor, int Patch, string Pre) : IComparable<ReleaseVersion>
{
    internal static ReleaseVersion Parse(string text)
    {
        if (text.Length > 128) throw new FormatException("Invalid release version.");
        var match = Regex.Match(text, @"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!match.Success || text.Length > 128) throw new FormatException("Invalid release version.");
        var pre = match.Groups[4].Value;
        if (pre.Split('.').Any(p => p.Length > 1 && p.All(char.IsAsciiDigit) && p[0] == '0')) throw new FormatException("Invalid prerelease.");
        return new(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value), pre);
    }
    public int CompareTo(ReleaseVersion? other)
    {
        if (other == null) return 1;
        foreach (var value in new[] { Major.CompareTo(other.Major), Minor.CompareTo(other.Minor), Patch.CompareTo(other.Patch) })
            if (value != 0) return value;
        if (Pre == other.Pre) return 0;
        if (Pre.Length == 0) return 1;
        if (other.Pre.Length == 0) return -1;
        var left = Pre.Split('.'); var right = other.Pre.Split('.');
        for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            bool a = left[i].All(char.IsAsciiDigit), b = right[i].All(char.IsAsciiDigit);
            int result = a && b ? left[i].Length.CompareTo(right[i].Length) : a != b ? (a ? -1 : 1) : 0;
            if (result == 0) result = string.CompareOrdinal(left[i], right[i]);
            if (result != 0) return result;
        }
        return left.Length.CompareTo(right.Length);
    }
}
