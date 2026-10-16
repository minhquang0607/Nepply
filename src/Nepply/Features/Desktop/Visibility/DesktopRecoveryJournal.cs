using System.IO;

namespace Nepply.Services;

internal static class DesktopRecoveryJournal
{
    internal static bool IsAllowedPath(string path,string localRoot)
    {
        if(!Path.IsPathFullyQualified(path)) return false;
        string full=Path.GetFullPath(path),root=Path.GetFullPath(localRoot).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        return full.StartsWith(root,StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetFileName(full),"desktop-visibility.lease",StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetFileName(Path.GetDirectoryName(full)),"HoverBox",StringComparison.OrdinalIgnoreCase);
    }

    internal static bool HasRestoreMarker(string path)
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(stream.Length>128) return false;
        using var reader=new StreamReader(stream,System.Text.Encoding.UTF8,true,128);
        const string marker="restore-visible-v1";
        Span<char> buffer=stackalloc char[marker.Length+1];
        int count=reader.ReadBlock(buffer);
        return count==marker.Length && buffer[..count].SequenceEqual(marker);
    }

    // Caller authenticates the owner, holds its generation fence and verifies recovery.
    internal static bool ClearCompleted(string path)
    {
        for(string? current=Path.GetFullPath(path);current is not null;current=Path.GetDirectoryName(current))
            if((File.Exists(current)||Directory.Exists(current)) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)
                return false;
        if(Directory.Exists(path)) return false;
        if(!File.Exists(path)) return true; // Package removal may already have removed its cache.
        if(!HasRestoreMarker(path)) return false;
        File.Delete(path);
        return true;
    }
}
