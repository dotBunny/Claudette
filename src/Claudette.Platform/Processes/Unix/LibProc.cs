using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Claudette.Platform.Processes.Unix;

/// <summary>macOS's <c>libproc</c>, for what <c>ps</c> doesn't say.</summary>
[SupportedOSPlatform("macos")]
internal static unsafe partial class LibProc
{
    /// <summary><c>PROC_PIDVNODEPATHINFO</c>: the process's working folder and root.</summary>
    private const int PidVnodePathInfo = 9;

    /// <summary><c>struct vnode_info</c>, before each path in a <c>vnode_info_path</c>.</summary>
    private const int VnodeInfoSize = 152;

    /// <summary><c>MAXPATHLEN</c>: a path's room, ending in a NUL.</summary>
    private const int MaxPath = 1024;

    /// <summary><c>struct proc_vnodepathinfo</c>: two <c>vnode_info_path</c>s, the working folder's, then the root's.</summary>
    private const int VnodePathInfoSize = 2 * (VnodeInfoSize + MaxPath);

    [LibraryImport("libproc", EntryPoint = "proc_pidinfo", SetLastError = true)]
    private static partial int ProcPidInfo(int pid, int flavor, ulong arg, void* buffer, int size);

    /// <summary>The process's working folder; null when it can't be read, as for another user's process.</summary>
    public static string? WorkingFolder(int pid)
    {
        var buffer = stackalloc byte[VnodePathInfoSize];
        if (ProcPidInfo(pid, PidVnodePathInfo, 0, buffer, VnodePathInfoSize) != VnodePathInfoSize)
        {
            return null;
        }
        var path = new ReadOnlySpan<byte>(buffer + VnodeInfoSize, MaxPath);
        var end = path.IndexOf((byte)0);
        return end > 0 ? Encoding.UTF8.GetString(path[..end]) : null;
    }
}
