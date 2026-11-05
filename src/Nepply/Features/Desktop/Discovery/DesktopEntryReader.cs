using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Nepply.Modules.Desktop;
using DesktopIconModel = Nepply.Models.DesktopIcon;
using static Nepply.Helpers.NativeInterop;

namespace Nepply.Services;

/// <summary>What a Desktop read asks the system for; tests replace these to fail one entry on purpose.</summary>
internal sealed record DesktopReadSource(string[] Roots,
    Func<bool, DesktopScanIssues, IReadOnlyList<NativeDesktopNamespace.Entry>> Namespace,
    Func<string, FileAttributes> Attributes);

/// <summary>One STA operation owns native discovery and all temporary icon/COM resources.</summary>
internal static class DesktopEntryReader
{
    internal static string[] GetDesktopRoots() => [Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)];
    internal static Task<List<DesktopIconModel>> ReadNativeAsync(DesktopScanOptions options, string[]? paths, DesktopScanIssues? issues = null) => ShellDesktopReader.RunSta(() =>
        ReadNative(options, paths, new(GetDesktopRoots(), NativeDesktopNamespace.Read, File.GetAttributes), issues ?? new()));
    internal static List<DesktopIconModel> ReadNative(DesktopScanOptions options, string[]? paths, DesktopReadSource source, DesktopScanIssues issues)
    {
        var roots = source.Roots;
        var list = new List<DesktopIconModel>();
        var cache = new Dictionary<int, BitmapSource>();
        long remainingPixels = DesktopImageBudget.MaximumDecodedBytes;
        IImageList? imageList = null;
        try
        {
            Guid iid = IID_IImageList;
            SHGetImageList(SHIL_EXTRALARGE, ref iid, out imageList);
            if (paths == null)
            {
                foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase)) ScanFolder(root, list, cache, imageList, options, ref remainingPixels, source.Attributes, issues);
                var known = list.Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var item in ShellDesktopReader.ReadFrom(source, options.ShowHiddenFiles, options.ShowSystemIcons, issues))
                {
                    if (!known.Add(item.Path)) continue;
                    RequireItemCapacity(list.Count);
                    if (remainingPixels > 0) item.Icon = RetainImage(GetShellIcon(item.Path) ?? GetFileIcon(item.Path, cache, imageList), cache, ref remainingPixels);
                    list.Add(item);
                }
                // Nothing readable while something failed is not an empty Desktop.
                if (list.Count == 0 && issues.Count > 0) throw new IOException("No Desktop entry could be read.");
            }
            else
            {
                if (paths.Length > DesktopChangeBatch.MaximumPaths || paths.Any(p => !DesktopChangeBatch.IsDirectChild(p, roots)))
                    throw new InvalidDataException("Partial Desktop request escapes its roots.");
                foreach (var root in roots) _ = File.GetAttributes(root);
                int alreadySkipped = issues.Count;
                foreach (var path in paths) ReadEntryIsolated(path, Path.GetDirectoryName(path)!, list, cache, imageList, options, ref remainingPixels, source.Attributes, issues);
                DesktopScanIssues.RequireSomeRead(paths.Length, issues.Count - alreadySkipped, "the changed Desktop entries");
                foreach (var root in roots) _ = File.GetAttributes(root);
            }
            foreach (var item in list)
                item.GroupIdentity = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                    options.MergeEquivalentShortcuts ? ShellDesktopReader.DestinationIdentity(item) : ShellDesktopReader.Identity(item))));
            return list;
        }
        finally { if (imageList != null) Marshal.ReleaseComObject(imageList); }
    }

    internal static void ScanFolder(string folderPath, List<DesktopIconModel> list, Dictionary<int, BitmapSource> cache, IImageList? imageList, DesktopScanOptions options, ref long remainingPixels,
        Func<string, FileAttributes> attributes, DesktopScanIssues issues)
    {
        // An unavailable source is not an empty Desktop. Abort publication so
        // the last complete snapshot survives access errors and redirection gaps.
        if (string.IsNullOrWhiteSpace(folderPath)) throw new IOException("Desktop location is unavailable.");

        try
        {
            var entries = ReadFolderEntries(folderPath);
            int alreadySkipped = issues.Count;

            foreach (var entry in entries)
                ReadEntryIsolated(entry, folderPath, list, cache, imageList, options, ref remainingPixels, attributes, issues);
            DesktopScanIssues.RequireSomeRead(entries.Length, issues.Count - alreadySkipped, folderPath);
            // Losing the source is not equivalent to an empty, successful scan.
            _ = File.GetAttributes(folderPath);
        }
        catch (Exception ex) { throw new IOException("Desktop source could not be read completely: " + folderPath, ex); }
    }

    private static void ReadEntryIsolated(string entry, string root, List<DesktopIconModel> list, Dictionary<int, BitmapSource> cache, IImageList? imageList, DesktopScanOptions options, ref long remainingPixels,
        Func<string, FileAttributes> attributes, DesktopScanIssues issues)
    {
        try { ReadFileEntry(entry, root, list, cache, imageList, options, ref remainingPixels, attributes); }
        catch (InvalidDataException) { throw; } // The item budget limits the whole read, not this entry.
        catch (Exception ex)
        {
            // This throws when the root went too: then the source is lost, not one entry.
            _ = File.GetAttributes(root);
            issues.Skip(entry, ex);
        }
    }

    private static void ReadFileEntry(string entry, string root, List<DesktopIconModel> list, Dictionary<int, BitmapSource> cache, IImageList? imageList, DesktopScanOptions options, ref long remainingPixels,
        Func<string, FileAttributes> readAttributes)
    {
        var attributes = ReadEntryAttributes(entry, root, readAttributes);
        if (attributes == null || (!options.ShowHiddenFiles && (attributes.Value & FileAttributes.Hidden) != 0)) return;
        string name = Path.GetFileName(entry);
        if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) return;
        RequireItemCapacity(list.Count);
        if (Path.GetExtension(entry).ToLowerInvariant() is ".lnk" or ".url") name = Path.GetFileNameWithoutExtension(entry);
        var icon = remainingPixels > 0 ? RetainImage(GetFileIcon(entry, cache, imageList), cache, ref remainingPixels) : null;
        list.Add(new DesktopIconModel { Name = name, Path = entry, Icon = icon, IsSystemIcon = false });
    }
    internal static ImageSource? RetainImage(ImageSource? image, Dictionary<int, BitmapSource> cache, ref long remaining)
    {
        if (image is not BitmapSource bitmap) return null;
        long bytes = (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;
        if (bitmap.PixelWidth is <= 0 or > DesktopImageBudget.MaximumDimension || bitmap.PixelHeight is <= 0 or > DesktopImageBudget.MaximumDimension || bytes > remaining)
        {
            cache.Clear(); // Release any newly cached image rejected by the producer budget.
            remaining = 0;
            return null;
        }
        remaining -= bytes; // Conservative: count each presented item, even when a bitmap is shared.
        return bitmap;
    }

    internal static string[] ReadFolderEntries(string path) => ReadBoundedEntries(Directory.EnumerateFileSystemEntries(path));
    internal static string[] ReadBoundedEntries(IEnumerable<string> entries)
    {
        var result = new List<string>();
        foreach (var entry in entries)
        {
            RequireItemCapacity(result.Count);
            result.Add(entry);
        }
        return result.ToArray();
    }
    private static void RequireItemCapacity(int count)
    {
        if (count >= DesktopImageBudget.MaximumEntries) throw new InvalidDataException("Desktop enumeration exceeds the item budget.");
    }
    internal static FileAttributes? ReadEntryAttributes(string entry, string root, Func<string, FileAttributes>? read = null)
    {
        try { return read != null ? read(entry) : File.GetAttributes(entry); }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            _ = File.GetAttributes(root); // Preserve root access/unavailability failures.
            return null;
        }
    }
    private static ImageSource? GetShellIcon(string path)
    {
        IntPtr pidl = IntPtr.Zero; var info = new SHFILEINFO();
        try
        {
            if (SHParseDisplayName(path, IntPtr.Zero, out pidl, 0, out _) != 0) return null;
            if (SHGetFileInfoPidl(pidl, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x100 | 0x8) == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;
            var image = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze(); return image;
        }
        catch { return null; }
        finally { if (info.hIcon != IntPtr.Zero) DestroyIcon(info.hIcon); if (pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl); }
    }


    private static ImageSource? GetFileIcon(string path, Dictionary<int, BitmapSource>? cache = null, IImageList? imageList = null)
    {
        var shinfo = new SHFILEINFO();
        IntPtr hIcon = IntPtr.Zero;
        bool localImageList = false;

        try
        {
            // 1. Lấy Icon Index từ SHGetFileInfo
            if (SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_SYSICONINDEX) == IntPtr.Zero) return null;

            if (cache != null && cache.TryGetValue(shinfo.iIcon, out var cachedIcon))
            {
                return cachedIcon;
            }

            if (imageList == null)
            {
                Guid iid = IID_IImageList;
                int hResult = SHGetImageList(SHIL_EXTRALARGE, ref iid, out imageList);
                localImageList = (hResult == 0 && imageList != null);
            }

            if (imageList != null)
            {
                // 3. Extract hIcon từ IImageList
                imageList.GetIcon(shinfo.iIcon, ILD_TRANSPARENT, out hIcon);
            }

            // --- FALLBACK ---
            if (hIcon == IntPtr.Zero)
            {
                // Nếu SHGetImageList thất bại, gọi lại SHGetFileInfo chuẩn mặc định (32x32)
                SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);
                hIcon = shinfo.hIcon;

                // Đánh dấu hIcon trong shinfo về Zero để avoid double-free vì ta đã gán nó vào hIcon
                shinfo.hIcon = IntPtr.Zero;
            }

            if (hIcon == IntPtr.Zero) return null;

            // 4. Tạo BitmapSource
            var iconSource = Imaging.CreateBitmapSourceFromHIcon(
                hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            iconSource.Freeze();

            if (cache != null)
            {
                cache[shinfo.iIcon] = (BitmapSource)iconSource;
            }
            return iconSource;
        }
        catch { return null; }
        finally
        {
            // Giải phóng GDI Object (hIcon)
            if (hIcon != IntPtr.Zero)
            {
                DestroyIcon(hIcon);
            }

            // Đảm bảo không quên giải phóng shinfo.hIcon trong trường hợp ngoại lệ giữa chừng
            if (shinfo.hIcon != IntPtr.Zero)
            {
                DestroyIcon(shinfo.hIcon);
            }

            // Giải phóng COM Object (IImageList)
            if (localImageList && imageList != null)
            {
                Marshal.ReleaseComObject(imageList);
            }
        }
    }

}
