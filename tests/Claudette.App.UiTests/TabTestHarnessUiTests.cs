using Avalonia.Headless.XUnit;
using Claudette.App.Services;
using Claudette.App.Tests.Support;

namespace Claudette.App.UiTests;

/// <summary>The harness on Avalonia's UI thread, as the rendered tests run it.</summary>
public class TabTestHarnessUiTests
{
    [AvaloniaFact]
    public async Task A_second_tab_starts_beside_the_first_with_no_setup()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var first = await h.OpenTabAsync();

        var second = await h.OpenTabAsync();

        Assert.NotSame(first, second);
        Assert.True(first.IsProcessRunning);
        Assert.True(second.IsProcessRunning);
    }
}
