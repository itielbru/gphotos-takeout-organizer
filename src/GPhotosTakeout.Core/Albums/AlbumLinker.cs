using GPhotosTakeout.Core.IO;

namespace GPhotosTakeout.Core.Albums;

public enum LinkMethod { Symlink, Hardlink, Copy }

public readonly record struct LinkOutcome(LinkMethod Method, bool Succeeded, string? Error);

/// <summary>
/// Materializes an album entry that points at the single canonical copy in
/// ALL_PHOTOS, degrading gracefully: symlink → hardlink (same volume) → copy.
/// Windows requires Developer Mode or admin for symlinks, so the fallback chain
/// keeps the tool working for ordinary users.
/// </summary>
public sealed class AlbumLinker
{
    private bool _symlinkBlocked;

    /// <summary>
    /// Creates a link at <paramref name="linkPath"/> pointing to <paramref name="targetPath"/>.
    /// Returns which method actually succeeded so the run report can summarize.
    /// </summary>
    public LinkOutcome Link(string targetPath, string linkPath)
    {
        var dir = Path.GetDirectoryName(linkPath);
        if (!string.IsNullOrEmpty(dir))
            LongPath.EnsureDirectory(dir);

        if (!_symlinkBlocked)
        {
            try
            {
                File.CreateSymbolicLink(LongPath.Extended(linkPath), LongPath.Extended(targetPath));
                // Verify the link is actually accessible: on some filesystems the API
                // succeeds but the resulting entry is not reachable (e.g. cross-volume
                // junctions on certain SMB mounts). Fall through to hardlink if so.
                if (File.Exists(LongPath.Extended(linkPath)))
                    return new LinkOutcome(LinkMethod.Symlink, true, null);
                _symlinkBlocked = true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // Most commonly: Developer Mode off / insufficient privilege. Stop
                // trying symlinks for the rest of the run to avoid repeated failures.
                _symlinkBlocked = true;
            }
        }

        if (SameVolume(targetPath, linkPath))
        {
            try
            {
                CreateHardLink(LongPath.Extended(linkPath), LongPath.Extended(targetPath));
                return new LinkOutcome(LinkMethod.Hardlink, true, null);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // fall through to copy
                _ = ex;
            }
        }

        try
        {
            File.Copy(LongPath.Extended(targetPath), LongPath.Extended(linkPath), overwrite: true);
            return new LinkOutcome(LinkMethod.Copy, true, null);
        }
        catch (Exception ex)
        {
            return new LinkOutcome(LinkMethod.Copy, false, ex.Message);
        }
    }

    private static bool SameVolume(string a, string b)
    {
        var ra = Path.GetPathRoot(Path.GetFullPath(a));
        var rb = Path.GetPathRoot(Path.GetFullPath(b));
        return string.Equals(ra, rb, StringComparison.OrdinalIgnoreCase);
    }

    private static void CreateHardLink(string link, string target)
    {
        if (!NativeMethods.CreateHardLink(link, target, IntPtr.Zero))
            throw new IOException($"CreateHardLink failed (error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}).");
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);
    }
}
