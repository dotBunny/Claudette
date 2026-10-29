using System.Runtime.Versioning;
using Claudette.Platform.Notifications.Mac;

namespace Claudette.Platform.Shell.Mac;

/// <summary>One of a macOS window's traffic lights.</summary>
public enum MacWindowButtonKind
{
    Close,
    Minimize,
    Zoom,
}

/// <summary>
/// A traffic light where AppKit has it, in points from the top left of the window's view: where the window draws into
/// its title bar, the view is the whole window.
/// </summary>
public sealed record MacWindowButton(MacWindowButtonKind Kind, double X, double Y, double Width, double Height, bool IsEnabled);

/// <summary>
/// A macOS window's traffic lights, which AppKit draws over the window's view, so a picture of what the window draws
/// leaves them out (DESIGN.md §9, "Taking screenshots without the focus"). Read from AppKit's own buttons
/// (<c>-[NSWindow standardWindowButton:]</c>), on the UI thread.
/// </summary>
[SupportedOSPlatform("macos")]
public static class MacWindowButtons
{
    /// <summary>
    /// The traffic lights showing over <paramref name="nsView"/>, a window's <c>NSView</c>, that are inside it: none
    /// while the window has the system's title bar, which they're in.
    /// </summary>
    public static IReadOnlyList<MacWindowButton> Over(nint nsView)
    {
        var pool = ObjC.AutoreleasePoolPush();
        try
        {
            var window = nsView == 0 ? 0 : ObjC.Send(nsView, "window");
            if (window == 0)
            {
                return [];
            }
            var view = ObjC.SendRect(nsView, "bounds");
            var flipped = ObjC.SendBool(nsView, "isFlipped");
            var buttons = new List<MacWindowButton>();
            foreach (var kind in Enum.GetValues<MacWindowButtonKind>())
            {
                // NSWindowCloseButton, NSWindowMiniaturizeButton and NSWindowZoomButton are 0, 1 and 2.
                var button = ObjC.Send(window, "standardWindowButton:", (nint)kind);
                if (button == 0 || ObjC.SendBool(button, "isHidden"))
                {
                    continue;
                }
                var frame = ObjC.SendRect(button, "convertRect:toView:", ObjC.SendRect(button, "bounds"), nsView);
                // AppKit's y runs up from the bottom unless the view is flipped.
                var top = flipped ? frame.Y - view.Y : view.Y + view.Height - frame.Y - frame.Height;
                var left = frame.X - view.X;
                if (top >= 0 && left >= 0 && frame.Width > 0 && frame.Height > 0)
                {
                    buttons.Add(new MacWindowButton(kind, left, top, frame.Width, frame.Height, ObjC.SendBool(button, "isEnabled")));
                }
            }
            return buttons;
        }
        finally
        {
            ObjC.AutoreleasePoolPop(pool);
        }
    }
}
