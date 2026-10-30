using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Claudette.App.ViewModels;

public static class Converters
{
    public static readonly IValueConverter BoldIfTrue = new FuncValueConverter<bool, FontWeight>(b => b ? FontWeight.SemiBold : FontWeight.Normal);

    /// <summary>A color as a brush of it, such as a group's on its panes' headers; none for none.</summary>
    public static readonly IValueConverter ColorBrush = new FuncValueConverter<Color?, IBrush?>(color => color is { } c ? new SolidColorBrush(c) : null);

    /// <summary>
    /// The room a list keeps at its right for its scroll viewer's vertical bar, while it can scroll (bound to
    /// <c>ScrollBarMaximum.Y</c>): the bar is drawn over the list, so it would otherwise cover the list's right edge.
    /// </summary>
    public static readonly IValueConverter ScrollBarGutter = new FuncValueConverter<double, Thickness>(max => max > 0 ? new Thickness(0, 0, 12, 0) : default);

    /// <summary>An indent, for tree rows.</summary>
    public static readonly IValueConverter LeftMargin = new FuncValueConverter<double, Thickness>(left => new Thickness(left, 0, 0, 0));

    /// <summary>Marks a process that left the tab's tree (DESIGN.md §4).</summary>
    public static readonly IValueConverter DetachedText = new FuncValueConverter<bool, string>(detached => detached ? "· detached" : "");

    /// <summary>A file's letter in the Project Files tab's explorer is for one added: green (DESIGN.md §18, "Project files").</summary>
    public static readonly IValueConverter IsAddedLetter = new FuncValueConverter<string?, bool>(letter => letter is "A" or "U");

    /// <summary>A tab's square in the collapsed sidebar: the first letter or digit of its name (DESIGN.md §4).</summary>
    public static readonly IValueConverter Initial = new FuncValueConverter<string?, string>(Initials);

    public static string Initials(string? name) =>
        name?.FirstOrDefault(char.IsLetterOrDigit) is { } first and not '\0' ? char.ToUpperInvariant(first).ToString() : "·";
}
