using System.Runtime.Versioning;

namespace Claudette.Platform.Shell.Windows;

/// <summary>
/// A window whose content is its title bar (DESIGN.md §3, "Window frame"): the answer to Windows' <c>WM_NCHITTEST</c>
/// that makes a part of the content the window's icon. Windows does the rest, as for any window's icon: a click opens the
/// window menu (Restore, Move, Size, Minimize, Maximize, Close, the ones that apply enabled), and a double-click closes
/// the window.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowsTitleBar
{
    /// <summary><c>HTSYSMENU</c>: the window's icon.</summary>
    public const int WindowIcon = 3;
}
