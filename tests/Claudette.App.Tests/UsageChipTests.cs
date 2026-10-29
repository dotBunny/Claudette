using Claudette.App.ViewModels;
using Claudette.Usage;

namespace Claudette.App.Tests;

/// <summary>The usage chip at the header's right (DESIGN.md §6, "Header meters"): short labels, levels, the spoken name and the bars.</summary>
public class UsageChipTests
{
    [Fact]
    public void Short_labels_are_S_and_W_then_a_models_first_letter_or_more_when_taken()
    {
        Assert.Equal("S", UsageChipViewModel.ShortLabelOf("Session", []));
        Assert.Equal("W", UsageChipViewModel.ShortLabelOf("Weekly", []));
        Assert.Equal("F", UsageChipViewModel.ShortLabelOf("Fable", ["S", "W"]));
        Assert.Equal("So", UsageChipViewModel.ShortLabelOf("sonnet", ["S", "W"]));
        Assert.Equal("Fa", UsageChipViewModel.ShortLabelOf("Fable", ["F"]));
    }

    [Fact]
    public void The_chip_follows_the_meters_with_their_levels_and_says_each_one_and_its_reset()
    {
        var chip = new UsageChipViewModel();
        var session = new MeterViewModel("Session") { Percent = 9, PercentText = "9%", ResetText = "resets in 3h 18m", Level = UsageLevel.Normal };
        var weekly = new[]
        {
            new MeterViewModel("Weekly") { Percent = 47, PercentText = "47%", ResetText = "resets Tue 23:00", Level = UsageLevel.Normal },
            new MeterViewModel("Fable") { Percent = 84, PercentText = "84%", ResetText = null, Level = UsageLevel.Warning },
        };

        chip.Update(true, session, weekly, "Not enough data yet.", null);

        Assert.Equal(["S", "W", "F"], chip.Items.Select(i => i.ShortLabel));
        Assert.Equal(["9%", "47%", "84%"], chip.Items.Select(i => i.PercentText));
        Assert.Equal([false, false, true], chip.Items.Select(i => i.IsWarning));
        Assert.Equal("Not enough data yet.", chip.Note);
        Assert.Equal("Usage details: Session 9%, resets in 3h 18m; Weekly 47%, resets Tue 23:00; Fable 84%", chip.AccessibleName);

        // The same meters keep their objects, and a critical one says so.
        var first = chip.Items[0];
        weekly[1].Level = UsageLevel.Critical;
        session.Percent = 12;
        session.PercentText = "12%";
        chip.Update(true, session, weekly, null, "as of 14:02");
        Assert.Same(first, chip.Items[0]);
        Assert.Equal(12, first.Percent);
        Assert.True(chip.Items[2].IsCritical);
        Assert.Null(chip.Note);
        Assert.Equal("as of 14:02", chip.AsOfText);
    }

    [Fact]
    public void Without_a_session_reading_the_weekly_meters_stand_alone_and_nothing_gives_the_plain_name()
    {
        var chip = new UsageChipViewModel();
        Assert.Equal("Usage details", chip.AccessibleName);

        chip.Update(false, new MeterViewModel("Session"), [new MeterViewModel("Weekly") { PercentText = "5%" }], null, null);

        Assert.Equal(["W"], chip.Items.Select(i => i.ShortLabel));
    }

    [Fact]
    public void A_narrow_window_drops_the_bars()
    {
        var chip = new UsageChipViewModel();
        Assert.True(chip.ShowBars);

        chip.SetWidth(UsageChipViewModel.BarsMinWidth - 1);
        Assert.False(chip.ShowBars);

        chip.SetWidth(UsageChipViewModel.BarsMinWidth);
        Assert.True(chip.ShowBars);
    }
}
