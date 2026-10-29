<#
.SYNOPSIS
What screenshots.ps1 and guide-screenshots.ps1 share (Windows): building Claudette and the demo, starting Claudette on the
demo and closing it, finding its controls through UI Automation and driving them, and capturing its windows with
PrintWindow.

.DESCRIPTION
A run leaves the focus, the keyboard and the pointer to the user (DESIGN.md §9, "Taking screenshots without the
focus"). Claudette runs with CLAUDETTE_UNFOCUSED=1, so its windows never take the focus and stay behind the user's, and
with CLAUDETTE_AUTOMATION_PIPE, so it takes commands on that pipe: click, right-click, toggle, set text, show, scroll,
show a tip, press keys and close a window, each carried out by Claudette itself. Controls are found and read through UI
Automation, which only reads, and each command names the control as UI Automation saw it. Nothing here sends the
mouse's or the keyboard's input, or uses UI Automation's own actions, which Windows lets bring a window to the front.
The focus watch warns if Claudette came to the front anyway.
#>
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
if (-not ('WindowShot' -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

public static class WindowShot
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);

    /// <summary>
    /// The window as it draws itself, without the resize borders and shadow around its frame; down to
    /// <paramref name="bottom"/> on the screen when it isn't zero.
    /// </summary>
    public static void Save(IntPtr hwnd, string path, int bottom)
    {
        DrawActive(hwnd);
        RECT outer, frame;
        GetWindowRect(hwnd, out outer);
        DwmGetWindowAttribute(hwnd, 9 /* DWMWA_EXTENDED_FRAME_BOUNDS */, out frame, Marshal.SizeOf(typeof(RECT)));
        using (var full = new Bitmap(outer.Right - outer.Left, outer.Bottom - outer.Top, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(full))
            {
                var hdc = g.GetHdc();
                PrintWindow(hwnd, hdc, 2 /* PW_RENDERFULLCONTENT */);
                g.ReleaseHdc(hdc);
            }
            var to = bottom == 0 ? frame.Bottom : Math.Min(bottom, frame.Bottom);
            var crop = new Rectangle(frame.Left - outer.Left, frame.Top - outer.Top, frame.Right - frame.Left, to - frame.Top);
            using (var shot = full.Clone(crop, PixelFormat.Format32bppArgb))
            {
                shot.Save(path, ImageFormat.Png);
            }
        }
    }

    /// <summary>The part of the window from (<paramref name="left"/>, <paramref name="top"/>) to (<paramref name="right"/>, <paramref name="bottom"/>) on the screen.</summary>
    public static void SaveRegion(IntPtr hwnd, string path, int left, int top, int right, int bottom)
    {
        DrawActive(hwnd);
        RECT outer;
        GetWindowRect(hwnd, out outer);
        using (var full = new Bitmap(outer.Right - outer.Left, outer.Bottom - outer.Top, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(full))
            {
                var hdc = g.GetHdc();
                PrintWindow(hwnd, hdc, 2 /* PW_RENDERFULLCONTENT */);
                g.ReleaseHdc(hdc);
            }
            var crop = Rectangle.Intersect(new Rectangle(left - outer.Left, top - outer.Top, right - left, bottom - top), new Rectangle(0, 0, full.Width, full.Height));
            using (var shot = full.Clone(crop, PixelFormat.Format32bppArgb))
            {
                shot.Save(path, ImageFormat.Png);
            }
        }
    }

    /// <summary>Screen pixels per device-independent pixel in the window.</summary>
    public static double Scale(IntPtr hwnd)
    {
        return GetDpiForWindow(hwnd) / 96.0;
    }

    /// <summary>Sets a window's size in device-independent pixels, keeping its place.</summary>
    public static void Resize(IntPtr hwnd, int width, int height)
    {
        var scale = Scale(hwnd);
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, (int)(width * scale), (int)(height * scale), 0x0002 /* NOMOVE */ | 0x0004 /* NOZORDER */);
    }

    /// <summary>A visible top-level window of the process other than <paramref name="main"/>, or zero.</summary>
    public static IntPtr OtherWindow(int processId, IntPtr main)
    {
        var found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr hwnd, IntPtr lParam)
        {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            RECT rect;
            GetWindowRect(hwnd, out rect);
            if (pid == processId && hwnd != main && IsWindowVisible(hwnd) && rect.Right - rect.Left > 400)
            {
                found = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    /// <summary>
    /// Has the window draw its frame as active, as it does with the focus: without it, Windows draws the title bar
    /// plainer. Claudette never has the focus to lose again, so it stays drawn that way.
    /// </summary>
    static void DrawActive(IntPtr hwnd)
    {
        SendMessage(hwnd, 0x0086 /* WM_NCACTIVATE */, new IntPtr(1), IntPtr.Zero);
        Thread.Sleep(200);
    }

    /// <summary>Puts the window behind every other, without activating it: PrintWindow captures it there all the same.</summary>
    public static void ToBack(IntPtr hwnd)
    {
        SetWindowPos(hwnd, new IntPtr(1) /* HWND_BOTTOM */, 0, 0, 0, 0, 0x0001 /* NOSIZE */ | 0x0002 /* NOMOVE */ | 0x0010 /* NOACTIVATE */);
    }

    /// <summary>Whether the pointer is over a window of the same process as <paramref name="hwnd"/>.</summary>
    public static bool PointerOver(IntPtr hwnd)
    {
        POINT point;
        uint under, mine;
        GetCursorPos(out point);
        GetWindowThreadProcessId(WindowFromPoint(point), out under);
        GetWindowThreadProcessId(hwnd, out mine);
        return under == mine;
    }

}

/// <summary>Notes each step during which a window of Claudette's came to the front: a run leaves the focus to the user.</summary>
public static class FocusWatch
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    static Timer timer;
    static uint watched;
    static uint last;
    static readonly List<string> taken = new List<string>();

    /// <summary>What the script is doing, to say when Claudette came to the front.</summary>
    public static volatile string Step = "";

    public static void Start(int processId)
    {
        Stop();
        lock (taken) { taken.Clear(); }
        watched = (uint)processId;
        last = 0;
        Step = "starting Claudette";
        timer = new Timer(delegate { Check(); }, null, 0, 100);
    }

    /// <summary>Stops watching; the steps during which Claudette came to the front.</summary>
    public static string[] Stop()
    {
        if (timer != null) { timer.Dispose(); timer = null; }
        lock (taken) { return taken.ToArray(); }
    }

    static void Check()
    {
        uint now;
        GetWindowThreadProcessId(GetForegroundWindow(), out now);
        // Coming to the front counts, each time.
        if (now == watched && last != watched)
        {
            lock (taken) { if (!taken.Contains(Step)) { taken.Add(Step); } }
        }
        last = now;
    }
}
'@
}
[WindowShot]::SetProcessDPIAware() | Out-Null

$ae = [System.Windows.Automation.AutomationElement]
$descendants = [System.Windows.Automation.TreeScope]::Descendants

# A window's handle or an element: where a search starts.
function Get-Root($from) {
    if ($from -is [IntPtr]) { $ae::FromHandle($from) } else { $from }
}

# The controls named $text. "Open diff#2" is the second one.
function Find-ByText($from, [string] $text, [switch] $Optional) {
    $index = 0
    if ($text -match '^(.*)#(\d+)$') { $text = $Matches[1]; $index = [int] $Matches[2] - 1 }
    $named = New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, $text)
    $all = (Get-Root $from).FindAll($descendants, $named)
    if ($all.Count -le $index) {
        if ($Optional) { return $null }
        throw "Found $($all.Count) controls named '$text'."
    }
    $all[$index]
}

# The lowest on screen of the controls named $text, such as the last of several tool tiles on one file, which is the one
# in view when the conversation is scrolled to its end; or null.
function Find-LowestByText($from, [string] $text) {
    $named = New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, $text)
    $lowest = $null
    foreach ($element in (Get-Root $from).FindAll($descendants, $named)) {
        $rect = $element.Current.BoundingRectangle
        if (-not $rect.IsEmpty -and (-not $lowest -or $rect.Top -gt $lowest.Current.BoundingRectangle.Top)) { $lowest = $element }
    }
    $lowest
}

# The first control whose name starts with $prefix, such as History's chip for a project, "starfall, 5 sessions"; or null.
function Find-ByPrefix($from, [string] $prefix) {
    $all = (Get-Root $from).FindAll($descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($element in $all) {
        if ($element.Current.Name.StartsWith($prefix, [StringComparison]::Ordinal)) { return $element }
    }
    $null
}

# The first control of a type, such as 'Tree' or 'Edit', named $text when that's given; or null.
function Find-ByType($from, [string] $type, [string] $text = '') {
    $found = New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, [System.Windows.Automation.ControlType]::($type))
    $condition = $found
    if ($text) {
        $named = New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, $text)
        $condition = New-Object System.Windows.Automation.AndCondition($found, $named)
    }
    (Get-Root $from).FindFirst($descendants, $condition)
}

# The control whose x:Name is $id (UI Automation's AutomationId), such as the composer's text box; or null.
function Find-ById($from, [string] $id) {
    (Get-Root $from).FindFirst($descendants, (New-Object System.Windows.Automation.PropertyCondition($ae::AutomationIdProperty, $id)))
}

# The control around $element.
function Get-Parent($element) {
    [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($element)
}

# The lowest text under $from, such as the last line of a page that doesn't fill its panel; or null.
function Find-Lowest($from) {
    $type = New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
    $lowest = $null
    foreach ($text in (Get-Root $from).FindAll($descendants, $type)) {
        $rect = $text.Current.BoundingRectangle
        if (-not $rect.IsEmpty -and (-not $lowest -or $rect.Bottom -gt $lowest.Current.BoundingRectangle.Bottom)) { $lowest = $text }
    }
    $lowest
}

# Prints the controls under $from, one a line: type, name and id. For finding what to click.
function Show-Tree($from, [int] $depth = 12) {
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $walk = {
        param($element, $level)
        $type = $element.Current.ControlType.ProgrammaticName -replace '^ControlType\.', ''
        $rect = $element.Current.BoundingRectangle
        '{0}{1} "{2}" #{3} [{4:N0},{5:N0} {6:N0}x{7:N0}]' -f ('  ' * $level), $type, $element.Current.Name, $element.Current.AutomationId, $rect.X, $rect.Y, $rect.Width, $rect.Height
        if ($level -lt $depth) {
            for ($child = $walker.GetFirstChild($element); $child; $child = $walker.GetNextSibling($child)) { & $walk $child ($level + 1) }
        }
    }
    & $walk (Get-Root $from) 0
}

# ---- The command channel (DESIGN.md §9, "Taking screenshots without the focus") ----------------------------------------

# The pipe the Claudette Start-Claudette started takes its commands on.
$script:pipeName = $null

# Sends $command to Claudette, which carries it out itself and answers; throws with why if it couldn't.
function Send-Command([hashtable] $command) {
    if (-not $script:pipeName) { throw "Claudette wasn't started with its command channel (Start-Claudette)." }
    $client = New-Object System.IO.Pipes.NamedPipeClientStream('.', $script:pipeName, [System.IO.Pipes.PipeDirection]::InOut)
    try {
        $client.Connect(10000)
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        $writer = New-Object System.IO.StreamWriter($client, $utf8)
        $writer.AutoFlush = $true
        $reader = New-Object System.IO.StreamReader($client, $utf8)
        $writer.WriteLine(($command | ConvertTo-Json -Compress))
        $line = $reader.ReadLine()
    }
    finally {
        $client.Dispose()
    }
    if (-not $line) { throw "Claudette didn't answer '$($command.verb)'." }
    $answer = $line | ConvertFrom-Json
    if (-not $answer.ok) { throw "Claudette couldn't $($command.verb): $($answer.error)" }
    $answer
}

# A command for $element: its window's handle, its name, id and type as UI Automation has them, and where its middle is,
# for when several match.
function Send-ElementCommand([string] $verb, $element, [hashtable] $more = @{}) {
    $current = $element.Current
    $command = @{
        verb = $verb
        window = [long] (Get-ElementWindow $element)
        name = $current.Name
        id = $current.AutomationId
        type = $current.ControlType.ProgrammaticName -replace '^ControlType\.', ''
    }
    $rect = $current.BoundingRectangle
    if (-not $rect.IsEmpty) {
        $command.x = $rect.Left + $rect.Width / 2
        $command.y = $rect.Top + $rect.Height / 2
    }
    foreach ($key in $more.Keys) { $command[$key] = $more[$key] }
    Send-Command $command
}

# Clicks the control named $text, or the nearest one around it that can be clicked.
function Invoke-ByText($from, [string] $text) {
    Invoke-Element (Find-ByText $from $text) $text
}

function Invoke-Element($element, [string] $what) {
    if (-not $element) { throw "Found nothing named '$what' to click." }
    Send-Click $element
}

# The window $element is drawn in: the one around it with a handle, such as Settings or a popup.
function Get-ElementWindow($element) {
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    while ($element -and $element.Current.NativeWindowHandle -eq 0) { $element = $walker.GetParent($element) }
    if (-not $element) { throw 'Found no window around the control.' }
    [IntPtr] $element.Current.NativeWindowHandle
}

# Clicks $element, as a click would: selects it, toggles it, invokes it or opens it, or the nearest control around it that can.
function Send-Click($element) {
    Send-ElementCommand 'click' $element | Out-Null
}

# Selects the item at $index (from 0) of the tree named $tree, such as a node of the agent map, by clicking its header:
# the item's middle can be one of its children's.
function Select-TreeItem([IntPtr] $hwnd, [string] $tree, [int] $index) {
    $found = Find-ByType $hwnd 'Tree' $tree
    if (-not $found) { throw "Found no tree named '$tree'." }
    $type = New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, [System.Windows.Automation.ControlType]::TreeItem)
    $items = $found.FindAll($descendants, $type)
    if ($items.Count -le $index) { throw "The tree '$tree' has $($items.Count) items." }
    $header = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetFirstChild($items[$index])
    Send-Click $(if ($header) { $header } else { $items[$index] })
}

# Scrolls $element into view in the list or conversation that holds it, if that can. With the conversation's scroll pane
# as $scroller, it then scrolls back until the element is $gap device-independent pixels under the pane's top, so what sits
# above the element, such as a card's header, shows too.
function Show-Element([IntPtr] $hwnd, $element, $scroller = $null, [int] $gap = 0) {
    Send-ElementCommand 'show' $element | Out-Null
    if (-not $scroller) { return }
    $least = $gap * [WindowShot]::Scale($hwnd)
    for ($step = 0; $step -lt 20; $step++) {
        Start-Sleep -Milliseconds 200
        if ($element.Current.BoundingRectangle.Top - $scroller.Current.BoundingRectangle.Top -ge $least) { break }
        Send-ElementCommand 'scroll' $scroller @{ lines = -1 } | Out-Null
    }
}

# Puts $text in the text box $element, as typing it would.
function Set-ElementText($element, [string] $text) {
    Send-ElementCommand 'settext' $element @{ text = $text } | Out-Null
}

# Sends $text from the selected tab's composer.
function Send-Message([IntPtr] $hwnd, [string] $text) {
    $box = Find-ById $hwnd 'Composer'
    if (-not $box) { throw 'Found no composer.' }
    Set-ElementText $box $text
    Start-Sleep -Milliseconds 300
    Invoke-ByText $hwnd 'Send'
}

# Shows the tip of $element, or of the nearest control around it that has one, as when the pointer rests on it.
function Show-Tip($element) {
    Send-ElementCommand 'hover' $element | Out-Null
}

# Hides it again, as when the pointer leaves.
function Hide-Tip($element) {
    Send-ElementCommand 'unhover' $element | Out-Null
}

# Presses keys together in the window, such as 'Ctrl+Shift+P', as the keyboard would: to its focused control, or itself.
function Send-Keys([IntPtr] $hwnd, [string] $chord) {
    Send-Command @{ verb = 'key'; window = [long] $hwnd; keys = $chord } | Out-Null
}

# Presses one key, such as 'Enter' or 'Esc', in the window.
function Send-WindowKey([IntPtr] $hwnd, [string] $key) {
    Send-Keys $hwnd $key
}

# What the script is doing, for Stop-Claudette to say if Claudette came to the front meanwhile.
function Set-Step([string] $name) {
    [FocusWatch]::Step = $name
}

# Waits for a window of Claudette's other than $main, such as Settings, and puts it behind the user's windows.
function Get-OtherWindow($claudette, [IntPtr] $main, [string] $what, [int] $seconds = 20) {
    Wait-Until { [WindowShot]::OtherWindow($claudette.Id, $main) -ne [IntPtr]::Zero } $what $seconds
    $other = [WindowShot]::OtherWindow($claudette.Id, $main)
    [WindowShot]::ToBack($other)
    $other
}

# Hides the tool windows showing in the main window, by the Hide button in each one's header, so a picture has the
# window's whole width, as the Files tab's does.
function Hide-ToolWindows([IntPtr] $window) {
    foreach ($title in 'Changed files', 'Agents', 'Project', 'Tasks', 'MCP', 'Scratch pad', 'Processes', 'Games', 'Find') {
        if ($hide = Find-ByText $window "Hide $title" -Optional) {
            Send-Click $hide
            Wait-Until { -not (Find-ByText $window "Hide $title" -Optional) } "$title to hide" 20
        }
    }
}

# Clicks $element to open its menu and returns where to find the menu's entries, once $entry is there: a window of its
# own, as a context menu has, or the window it was opened from, whose automation tree has the header's menu's entries
# (its button is in the title bar), which a click reaches as it does the others.
function Open-Menu($claudette, [IntPtr] $window, $element, [string] $entry) {
    Send-Click $element
    $script:menuRoot = $null
    Wait-Until {
        $other = [WindowShot]::OtherWindow($claudette.Id, $window)
        if ($other -ne [IntPtr]::Zero -and (Find-ByText $other $entry -Optional)) { $script:menuRoot = $other }
        elseif (Find-ByText $window $entry -Optional) { $script:menuRoot = $window }
        $null -ne $script:menuRoot
    } "the menu with $entry" 20
    $script:menuRoot
}

# Waits while the pointer is over Claudette, where a window of it shows past the user's: what's under it would show lit.
function Wait-PointerAway([IntPtr] $hwnd, [string] $path) {
    $deadline = (Get-Date).AddSeconds(5)
    while ([WindowShot]::PointerOver($hwnd)) {
        if ((Get-Date) -gt $deadline) {
            Write-Warning "The pointer is over Claudette, so what's under it may show lit in $(Split-Path $path -Leaf)."
            return
        }
        Start-Sleep -Milliseconds 250
    }
}

# The window as $path, down to $bottom on the screen when that isn't zero.
function Save-Window([IntPtr] $hwnd, [string] $path, [int] $bottom = 0) {
    Wait-PointerAway $hwnd $path
    [WindowShot]::Save($hwnd, $path, $bottom)
}

# The part of the screen the controls cover.
function Get-Bounds($elements) {
    $union = [System.Windows.Rect]::Empty
    foreach ($element in $elements) { $union = [System.Windows.Rect]::Union($union, $element.Current.BoundingRectangle) }
    $union
}

# $rect of the window, with $margin device-independent pixels around it, as $path. $margin is one number, or the left, top,
# right and bottom's, for a card that UI Automation doesn't show the edges of.
function Save-Rect([IntPtr] $hwnd, [string] $path, [System.Windows.Rect] $rect, [double[]] $margin = @(8)) {
    if ($rect.IsEmpty -or $rect.Width -lt 1) { throw "Nothing to capture for $path." }
    if ($margin.Count -eq 1) { $margin = $margin * 4 }
    $scale = [WindowShot]::Scale($hwnd)
    Wait-PointerAway $hwnd $path
    [WindowShot]::SaveRegion($hwnd, $path, [int] ($rect.Left - $margin[0] * $scale), [int] ($rect.Top - $margin[1] * $scale),
        [int] ($rect.Right + $margin[2] * $scale), [int] ($rect.Bottom + $margin[3] * $scale))
}

# The part of the window the controls cover, with $margin around it (Save-Rect).
function Save-Elements([IntPtr] $hwnd, [string] $path, $elements, [double[]] $margin = @(8)) {
    Save-Rect $hwnd $path (Get-Bounds $elements) $margin
}

function Save-Element([IntPtr] $hwnd, [string] $path, $element, [double[]] $margin = @(8)) {
    Save-Rect $hwnd $path $element.Current.BoundingRectangle $margin
}

# Closes a window of Claudette's, such as Settings, as its close button does.
function Close-Window([IntPtr] $hwnd) {
    Send-Command @{ verb = 'close'; window = [long] $hwnd } | Out-Null
}

# Opens the context menu of $element, or of the nearest control around it that has one, as a right-click would.
function Send-RightClick($element) {
    Send-ElementCommand 'rightclick' $element | Out-Null
}

# Turns the switch or check box named $text on or off: the control itself, not a title beside it named the same.
function Set-Toggle($from, [string] $text, [bool] $on) {
    $named = New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, $text)
    $togglable = New-Object System.Windows.Automation.PropertyCondition($ae::IsTogglePatternAvailableProperty, $true)
    $box = (Get-Root $from).FindFirst($descendants, (New-Object System.Windows.Automation.AndCondition($named, $togglable)))
    if (-not $box) { throw "Found no switch named '$text'." }
    $toggle = $box.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if (($toggle.Current.ToggleState -eq 'On') -ne $on) {
        Send-ElementCommand 'toggle' $box @{ on = $on } | Out-Null
        Wait-Until { ($toggle.Current.ToggleState -eq 'On') -eq $on } "'$text' to change" 10
    }
}

# Selects the list item named $text, such as one of Settings' pages or a search result.
function Select-ListItem($from, [string] $text) {
    $item = Find-ByType $from 'ListItem' $text
    if (-not $item) { throw "Found no list item named '$text'." }
    Send-Click $item
}

function Wait-Until([scriptblock] $condition, [string] $what, [int] $seconds = 60) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while (-not (& $condition)) {
        if ((Get-Date) -gt $deadline) { throw "Timed out waiting for $what." }
        Start-Sleep -Milliseconds 250
    }
}

# Builds Claudette and the demo; returns where their programs are.
function Build-Claudette([string] $repo) {
    Write-Host 'Building Claudette and the demo...'
    & dotnet build (Join-Path $repo 'src\Claudette.App\Claudette.App.csproj') -v q -nologo
    if ($LASTEXITCODE -ne 0) { throw 'The build failed.' }
    & dotnet build (Join-Path $repo 'tools\Claudette.Demo\Claudette.Demo.csproj') -v q -nologo
    if ($LASTEXITCODE -ne 0) { throw 'The build failed.' }
    [pscustomobject] @{
        App = Join-Path $repo 'src\Claudette.App\bin\Debug\net10.0\Claudette.exe'
        Demo = Join-Path $repo 'tools\Claudette.Demo\bin\Debug\net10.0\claudette-demo.exe'
    }
}

# Makes the demo's content in $work afresh, for the style and theme: still, and without the OS's notifications, which
# Claudette would send from behind the user's windows.
function New-DemoContent([string] $demo, [string] $work, [string] $projects, [string] $style, [string] $theme) {
    & $demo $work --theme $theme --style $style --projects $projects --still --quiet | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'claudette-demo failed.' }
}

# Starts Claudette on the demo in $work (fake-claude as Claude Code, so no account and no tokens), once it has a window
# and takes commands, behind the user's windows and without the focus. $environment adds to what it starts with, such as
# FAKE_CLAUDE_LOGGED_IN = '0' for the sign-in screen.
function Start-Claudette([string] $app, [string] $work, [hashtable] $environment = @{}) {
    $start = New-Object System.Diagnostics.ProcessStartInfo $app
    $start.UseShellExecute = $false
    $start.EnvironmentVariables['CLAUDETTE_HOME'] = Join-Path $work 'home'
    $start.EnvironmentVariables['CLAUDE_CONFIG_DIR'] = Join-Path $work 'claude-config'
    $start.EnvironmentVariables['FAKE_CLAUDE_USAGE'] = Join-Path $work 'usage.json'
    $start.EnvironmentVariables['CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC'] = '1'
    # A source build otherwise copies itself to its data folder and runs from there.
    $start.EnvironmentVariables['CLAUDETTE_RUN_IN_PLACE'] = '1'
    $start.EnvironmentVariables['CLAUDETTE_UNFOCUSED'] = '1'
    # Its own pipe each time, so a Claudette left over from a run can't answer for this one.
    $script:pipeName = "claudette-automation-$([guid]::NewGuid().ToString('N'))"
    $start.EnvironmentVariables['CLAUDETTE_AUTOMATION_PIPE'] = $script:pipeName
    foreach ($name in $environment.Keys) { $start.EnvironmentVariables[$name] = $environment[$name] }
    $claudette = [System.Diagnostics.Process]::Start($start)
    [FocusWatch]::Start($claudette.Id)
    Wait-Until { $claudette.Refresh(); $claudette.MainWindowHandle -ne [IntPtr]::Zero } 'the window'
    [WindowShot]::ToBack($claudette.MainWindowHandle)
    Wait-Until { try { Send-Command @{ verb = 'ping' } | Out-Null; $true } catch { $false } } 'the command channel' 30
    $claudette
}

# Closed by its window, so it stops each tab's Claude Code as it would for a user. Says if it came to the front meanwhile.
function Stop-Claudette($claudette) {
    $taken = [FocusWatch]::Stop()
    if ($taken) { Write-Warning "Claudette came to the front: $($taken -join ', ')." }
    $claudette.CloseMainWindow() | Out-Null
    if (-not $claudette.WaitForExit(30000)) { Write-Warning "Claudette (pid $($claudette.Id)) didn't close." }
}

Export-ModuleMember -Function Get-Root, Get-Parent, Find-ByText, Find-ByPrefix, Find-ByType, Find-ById, Find-Lowest, Find-LowestByText, Show-Tree, Invoke-ByText, Invoke-Element,
    Select-TreeItem, Select-ListItem, Show-Element, Set-ElementText, Set-Toggle, Send-Click, Send-RightClick, Send-Message, Show-Tip, Hide-Tip, Send-Keys,
    Send-WindowKey, Send-Command, Send-ElementCommand, Set-Step, Get-OtherWindow, Open-Menu, Hide-ToolWindows, Wait-PointerAway, Save-Window, Get-Bounds, Save-Rect, Save-Elements, Save-Element, Close-Window,
    Wait-Until, Build-Claudette, New-DemoContent, Start-Claudette, Stop-Claudette
