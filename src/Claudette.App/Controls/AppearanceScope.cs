using Avalonia.Controls;

namespace Claudette.App.Controls;

/// <summary>
/// Carries Settings → Appearance's classes for everything inside it (DESIGN.md §3, "Visual style"; §14): "claude" for the
/// Claude style, "compact" for Density, "fullwidth", "quietsteps", "indentyou" and "indentsteps". The shell, the Settings,
/// About and file windows, and Appearance's preview each have one, and the styles those classes change name it first:
/// <c>controls|AppearanceScope.claude Border.composer</c>, never <c>.claude Border.composer</c>. A selector that starts
/// with a bare class could match any ancestor, so Avalonia watches the classes of every ancestor of every control it
/// might style: 1.5 million watchers for six tabs, most of the memory those tabs' controls took. Named by its type, only
/// this one is watched. <c>AppearanceScopeTests</c> keeps every selector that way.
/// </summary>
/// <remarks>
/// A grid, so a view whose root is a grid can make it this one; anywhere else it holds the root as its only child.
/// </remarks>
public sealed class AppearanceScope : Grid;
