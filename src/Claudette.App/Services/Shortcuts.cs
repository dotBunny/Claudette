using Avalonia.Input;
using Claudette.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Claudette.App.Services;

/// <summary>Matches key presses against the shortcuts in Settings → Keyboard (DESIGN.md §14).</summary>
public static class Shortcuts
{
    public static bool IsMac { get; } = OperatingSystem.IsMacOS();

    /// <summary>
    /// The chord for a key press, or null for a modifier key on its own. Avalonia reports Cmd as Meta on macOS, which is
    /// Primary there; elsewhere Ctrl is Primary.
    /// </summary>
    public static KeyChord? FromKeyPress(Key key, KeyModifiers modifiers)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin or Key.None or Key.System)
        {
            return null;
        }
        return new KeyChord(Modifiers(modifiers), KeyName(key));
    }

    /// <summary>Does this key press trigger the command's shortcut?</summary>
    public static bool Matches(KeyboardSettings settings, string commandId, Key key, KeyModifiers modifiers) =>
        Matches(KeyboardShortcuts.Resolve(settings, commandId), key, modifiers);

    public static bool Matches(KeyChord? bound, Key key, KeyModifiers modifiers)
    {
        if (bound is null || !Enum.TryParse<Key>(bound.Key, ignoreCase: true, out var boundKey)
            || KeyChord.Normalize(bound.Modifiers, IsMac) != KeyChord.Normalize(Modifiers(modifiers), IsMac))
        {
            return false;
        }
        return bound.IsDigitRange ? Digit(key) is not null : boundKey == key || NumberPadTwin(key) == boundKey;
    }

    /// <summary>The main keyboard's key for a number pad key: <c>NumPad0</c> is <c>D0</c>, <c>Add</c> is <c>OemPlus</c>.</summary>
    private static Key? NumberPadTwin(Key key) => key switch
    {
        >= Key.NumPad0 and <= Key.NumPad9 => Key.D0 + (key - Key.NumPad0),
        Key.Add => Key.OemPlus,
        Key.Subtract => Key.OemMinus,
        _ => null,
    };

    /// <summary>1–9 for the digit keys, on the main row or the number pad.</summary>
    public static int? Digit(Key key) => key switch
    {
        >= Key.D1 and <= Key.D9 => key - Key.D0,
        >= Key.NumPad1 and <= Key.NumPad9 => key - Key.NumPad0,
        _ => null,
    };

    private static ChordModifiers Modifiers(KeyModifiers modifiers)
    {
        var chord = ChordModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            chord |= ChordModifiers.Shift;
        }
        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            chord |= ChordModifiers.Alt;
        }
        if (IsMac)
        {
            chord |= (modifiers.HasFlag(KeyModifiers.Meta) ? ChordModifiers.Primary : 0) | (modifiers.HasFlag(KeyModifiers.Control) ? ChordModifiers.Ctrl : 0);
        }
        else if (modifiers.HasFlag(KeyModifiers.Control))
        {
            chord |= ChordModifiers.Primary;
        }
        return chord;
    }

    /// <summary>One name per key, although Avalonia gives some keys two (Enter and Return, Oem1 and OemSemicolon).</summary>
    private static string KeyName(Key key) => key switch
    {
        Key.Enter => "Enter",
        Key.Back => "Back",
        Key.OemSemicolon => "OemSemicolon",
        Key.OemQuestion => "OemQuestion",
        Key.OemTilde => "OemTilde",
        Key.OemOpenBrackets => "OemOpenBrackets",
        Key.OemPipe => "OemPipe",
        Key.OemCloseBrackets => "OemCloseBrackets",
        Key.OemQuotes => "OemQuotes",
        Key.PageUp => "PageUp",
        Key.PageDown => "PageDown",
        >= Key.NumPad0 and <= Key.NumPad9 => $"D{key - Key.NumPad0}",
        Key.Add => "OemPlus",
        Key.Subtract => "OemMinus",
        _ => key.ToString(),
    };
}

/// <summary>
/// Tooltips that name a shortcut, such as "New tab (Ctrl+T)", kept current when shortcuts are rebound. Shared by the
/// views through <see cref="AppServices.Tips"/>.
/// </summary>
public sealed class ShortcutTips(AppSettings settings) : ObservableObject
{
    public string NewTab => Tip("New tab", KeyboardShortcuts.NewTab);

    public string Settings => Tip("Settings", KeyboardShortcuts.Settings);

    public string History => Tip("Resume a past session", KeyboardShortcuts.History);

    public string Suffixes => Tip("Quick suffixes", KeyboardShortcuts.Suffixes);

    public string Stop => Tip("Stop", KeyboardShortcuts.Stop);

    public string Stash => Text(KeyboardShortcuts.Stash) is { } shortcut
        ? $"Messages put aside with Stash ({shortcut}), which every tab shares"
        : "Messages put aside with Stash, which every tab shares";

    /// <summary>The Project Files tab's <b>Find file</b> (DESIGN.md §18, "Project files").</summary>
    public string GoToFile => Tip("Find a file by name", KeyboardShortcuts.GoToFile);

    /// <summary>The editor's find box's <b>Find in files</b> (DESIGN.md §18, "Finding in files").</summary>
    public string FindInFiles => Tip("Find in files", KeyboardShortcuts.FindInFiles);

    /// <summary>Find in files' chevron, which shows the replacement, or hides it.</summary>
    public string ShowReplace => Tip("Show Replace", KeyboardShortcuts.ReplaceInFiles);

    public string HideReplace => Tip("Hide Replace", KeyboardShortcuts.FindInFiles);

    // ---- The keys alone, for a list of commands beside them: a Files tab with nothing open ---------------------------

    public string GoToFileKeys => Text(KeyboardShortcuts.GoToFile) ?? "";

    public string FindInFilesKeys => Text(KeyboardShortcuts.FindInFiles) ?? "";

    public string ReplaceInFilesKeys => Text(KeyboardShortcuts.ReplaceInFiles) ?? "";

    public string CommandPaletteKeys => Text(KeyboardShortcuts.CommandPalette) ?? "";

    /// <summary>A key with no command of its own, as this OS writes it: Esc is ⎋ on macOS, Enter ↩.</summary>
    public static string Key(string key) => new KeyChord(ChordModifiers.None, key).Display(Shortcuts.IsMac);

    /// <summary>The peek's arrows: in a Files tab, the previous and next message keys go from change to change.</summary>
    public string PreviousChange => Tip("Previous change", KeyboardShortcuts.PreviousMessage);

    public string NextChange => Tip("Next change", KeyboardShortcuts.NextMessage);

    public string CollapseSidebar => Tip("Collapse the sidebar", KeyboardShortcuts.ToggleSidebar);

    public string ExpandSidebar => Tip("Expand the sidebar", KeyboardShortcuts.ToggleSidebar);

    public string Allow => Text(KeyboardShortcuts.AllowPrompt) ?? "";

    public string Deny => Text(KeyboardShortcuts.DenyPrompt) ?? "";

    /// <summary>The composer's placeholder: which key sends, which starts a new line (Settings → Keyboard → Send with), and Stop.</summary>
    public string ComposerPlaceholder
    {
        get
        {
            var keys = SendsWithPrimaryEnter ? $"{PrimaryEnter} to send, Enter for a new line" : "Enter to send, Shift+Enter for a new line";
            return Text(KeyboardShortcuts.Stop) is { } stop ? $"Message Claude…  ({keys}, {stop} to stop)" : $"Message Claude…  ({keys})";
        }
    }

    // ---- Send with (DESIGN.md §14), on the composer's send key chip and Send (DESIGN.md §5, "Composer") ------------

    /// <summary>
    /// The glyph the send key chip shows for Enter, on every OS. It's drawn from Inter, which Claudette ships: the OS's
    /// own interface font may not have it (Segoe UI hasn't), and fallback would take it from whichever font has.
    /// </summary>
    public const string ReturnGlyph = "⏎";

    /// <summary>The key that sends, as the app writes keys: Enter, or Ctrl+Enter (⌘↩ on macOS).</summary>
    public string SendKeyText => SendsWithPrimaryEnter ? PrimaryEnter : "Enter";

    /// <summary>
    /// What the send key chip shows before <see cref="ReturnGlyph"/>, so it reads <c>⏎ sends</c>, <c>Ctrl+⏎ sends</c> or
    /// <c>⌘⏎ sends</c>: nothing, or the modifier as the app writes it. It's the chord's display without its key's, which
    /// comes last.
    /// </summary>
    public string SendKeyModifiers => SendsWithPrimaryEnter ? PrimaryEnter[..^new KeyChord(ChordModifiers.None, "Enter").Display(Shortcuts.IsMac).Length] : "";

    /// <summary>The chip's name, in words for a screen reader: Enter sends, or Ctrl+Enter sends.</summary>
    public string SendKeyName => $"{SendKeyText} sends";

    /// <summary>The chip's tooltip: what each key does, and what a click does.</summary>
    public string SendKeyTip => SendsWithPrimaryEnter
        ? $"{PrimaryEnter} sends, Enter starts a new line. Click to send with Enter instead."
        : $"Enter sends, Shift+Enter starts a new line. Click to send with {PrimaryEnter} instead.";

    /// <summary>Send's tooltip, which names the key that sends.</summary>
    public string Send => $"Send ({SendKeyText})";

    private bool SendsWithPrimaryEnter => settings.Keyboard.SendKey == SendKey.PrimaryEnter;

    /// <summary>Ctrl+Enter, or ⌘↩ on macOS.</summary>
    private static string PrimaryEnter => new KeyChord(ChordModifiers.Primary, "Enter").Display(Shortcuts.IsMac);

    /// <summary>The shortcut as it reads on this OS, or null when it has been removed.</summary>
    public string? Text(string commandId) => KeyboardShortcuts.Resolve(settings.Keyboard, commandId)?.Display(Shortcuts.IsMac);

    public void Refresh() => OnPropertyChanged(string.Empty);

    private string Tip(string label, string commandId) => Text(commandId) is { } shortcut ? $"{label} ({shortcut})" : label;
}
