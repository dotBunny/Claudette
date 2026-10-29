using System.Collections.ObjectModel;
using Claudette.Usage;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Claudette.App.ViewModels;

/// <summary>One meter on the header's usage chip: a short label, a thin bar and the percent (DESIGN.md §6, "Header meters").</summary>
public sealed partial class UsageChipMeterViewModel(string label, string shortLabel) : ObservableObject
{
    /// <summary>The meter's whole name: Session, Weekly or a model's.</summary>
    public string Label { get; } = label;

    /// <summary>S for the session, W for the weekly limit, otherwise the model's first letter (more when that's taken).</summary>
    public string ShortLabel { get; } = shortLabel;

    [ObservableProperty]
    public partial double Percent { get; set; }

    [ObservableProperty]
    public partial string PercentText { get; set; } = "";

    [ObservableProperty]
    public partial string? ResetText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWarning), nameof(IsCritical))]
    public partial UsageLevel Level { get; set; }

    public bool IsWarning => Level == UsageLevel.Warning;

    public bool IsCritical => Level == UsageLevel.Critical;

    /// <summary>"Session 9%, resets in 3h 18m": what a screen reader says of the meter.</summary>
    public string Spoken => ResetText is { } reset ? $"{Label} {PercentText}, {reset}" : $"{Label} {PercentText}";
}

/// <summary>
/// The usage chip at the right of the header (DESIGN.md §6, "Header meters"): one line of short labels, thin bars and
/// percents, whose click opens a popover with the full detail. This is only its presentation, made from the header's
/// meters (<see cref="UsageViewModel"/>), which still read and keep the usage.
/// </summary>
public sealed partial class UsageChipViewModel : ObservableObject
{
    /// <summary>Below this window width the chip leaves out the bars, so it never crowds the header's other content.</summary>
    public const double BarsMinWidth = 820;

    /// <summary>The session meter, then the weekly limit, then each model's.</summary>
    public ObservableCollection<UsageChipMeterViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial bool ShowBars { get; set; } = true;

    /// <summary>What a screen reader says of the chip: every meter with its reset.</summary>
    [ObservableProperty]
    public partial string AccessibleName { get; set; } = "Usage details";

    /// <summary>"Not enough data yet.", "No recent usage.", or where the session is headed; the popover's note.</summary>
    [ObservableProperty]
    public partial string? Note { get; set; }

    [ObservableProperty]
    public partial string? AsOfText { get; set; }

    /// <summary>Called by the window as it resizes (at its zoom): a narrow window drops the bars before anything else.</summary>
    public void SetWidth(double width) => ShowBars = width >= BarsMinWidth;

    /// <summary>The meter's label as short as the chip has it: S, W, then a model's first letter.</summary>
    public static string ShortLabelOf(string label, IReadOnlyCollection<string> taken)
    {
        if (label == "Session")
        {
            return "S";
        }
        if (label == "Weekly")
        {
            return "W";
        }
        var text = label.Trim();
        if (text.Length == 0)
        {
            return "?";
        }
        // The first letter, or more when another meter has it already (Sonnet beside the session).
        for (var length = 1; length < text.Length; length++)
        {
            var candidate = char.ToUpperInvariant(text[0]) + text[1..length];
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
        return text;
    }

    /// <summary>Takes in the header's meters as they are now.</summary>
    internal void Update(bool hasSession, MeterViewModel session, IReadOnlyList<MeterViewModel> weekly, string? note, string? asOf)
    {
        var sources = new List<MeterViewModel>();
        if (hasSession)
        {
            sources.Add(session);
        }
        sources.AddRange(weekly);

        // Keep the meters' objects while the set is the same, so the chip doesn't flicker.
        if (!Items.Select(i => i.Label).SequenceEqual(sources.Select(s => s.Label)))
        {
            Items.Clear();
            var taken = new List<string>();
            foreach (var source in sources)
            {
                var shortLabel = ShortLabelOf(source.Label, taken);
                taken.Add(shortLabel);
                Items.Add(new UsageChipMeterViewModel(source.Label, shortLabel));
            }
        }
        for (var i = 0; i < sources.Count; i++)
        {
            var (item, source) = (Items[i], sources[i]);
            item.Percent = source.Percent;
            item.PercentText = source.PercentText;
            item.ResetText = source.ResetText;
            item.Level = source.Level;
        }
        Note = note;
        AsOfText = asOf;
        AccessibleName = Items.Count == 0 ? "Usage details" : "Usage details: " + string.Join("; ", Items.Select(i => i.Spoken));
    }
}
