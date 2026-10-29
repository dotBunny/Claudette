namespace Claudette.Core.Settings;

/// <summary>
/// Which of Claudette's settings sync through the session library (DESIGN.md §14, "Settings sync"), by their paths in
/// <c>settings.json</c>, such as <c>appearance.theme</c>. Settings sync copies these; Settings marks every other setting as
/// this computer's.
/// </summary>
public static class SyncedSettings
{
    /// <summary>
    /// The sections that sync. The paths to claude and git, the machine name, the library folder, the diff tool, folders
    /// and tabs stay per machine.
    /// </summary>
    public static IReadOnlyList<string> Sections { get; } = ["appearance", "newTabs", "usage", "checkIns", "quickSuffixes", "processes", "notifications", "keyboard"];

    /// <summary>
    /// Within the synced sections, what stays per machine: the zoom suits this machine's screen, and the title bar its
    /// OS.
    /// </summary>
    public static IReadOnlyList<string> PerMachine { get; } = ["appearance.zoom", "appearance.systemTitleBar"];

    /// <summary>Synced as one value each: the shortcut overrides come and go by command id.</summary>
    public static IReadOnlyList<string> Leaves { get; } = ["keyboard.bindings"];

    /// <summary>
    /// Whether the setting at <paramref name="path"/> syncs: its section does, and it isn't one of the few in it that stay
    /// per machine. A path outside <c>settings.json</c>, such as <c>state.detailedUsageHeader</c>, never does.
    /// </summary>
    public static bool Syncs(string path)
    {
        var section = path.Split('.')[0];
        return Sections.Contains(section, StringComparer.Ordinal)
            && !PerMachine.Any(p => path == p || path.StartsWith(p + ".", StringComparison.Ordinal));
    }
}
