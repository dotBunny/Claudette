using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Claudette.App.Controls;
using Claudette.App.Services;
using Claudette.App.Tests.Support;
using Claudette.App.Themes;
using Claudette.App.ViewModels;
using Claudette.App.Views;
using Claudette.Core.Settings;

namespace Claudette.App.UiTests;

/// <summary>Settings → Appearance → Style, applied to a running app (DESIGN.md §3, "Visual style").</summary>
public class ClaudeStyleTests
{
    [AvaloniaFact]
    public void The_Claude_style_repaints_the_window_the_tokens_and_the_accent_and_Standard_puts_them_back()
    {
        var app = (App)Application.Current!;
        var before = app.RequestedThemeVariant;
        var window = new Window { Width = 200, Height = 100 };
        window.Show();
        try
        {
            app.RequestedThemeVariant = ThemeVariant.Light;
            UiText.Settle(window);

            app.Colors.Apply(AppStyle.Claude);
            UiText.Settle(window);

            // Fluent's window background, Claudette's own tokens and the accent all change, while the app runs.
            Assert.Equal(Color.Parse("#FAF9F5"), BackgroundOf(window));
            Assert.Equal(Color.Parse("#F5F4ED"), Token(window, "SidebarBrush"));
            Assert.Equal(AppColors.ClaudeAccent, (Color)window.FindResource("SystemAccentColor")!);
            // The cards' colors too (DESIGN.md §18, "Conversation cards"): your cards warm grey, Claude's orange.
            Assert.Equal(Color.Parse("#B0AEA5"), Token(window, "CardYouStripeBrush"));
            Assert.Equal(Color.Parse("#D97757"), Token(window, "CardClaudeStripeBrush"));

            app.RequestedThemeVariant = ThemeVariant.Dark;
            UiText.Settle(window);

            Assert.Equal(Color.Parse("#262624"), BackgroundOf(window));
            Assert.Equal(Color.Parse("#1F1E1D"), Token(window, "SidebarBrush"));
            Assert.Equal(Color.Parse("#8A887F"), Token(window, "CardYouStripeBrush"));

            app.Colors.Apply(AppStyle.Standard);
            app.RequestedThemeVariant = ThemeVariant.Light;
            UiText.Settle(window);

            // Standard's are Rider's (DESIGN.md §3, "Visual style"): its frame, its greys and its blue.
            Assert.Equal(Color.Parse("#EBECF0"), BackgroundOf(window));
            Assert.Equal(Color.Parse("#F7F8FA"), Token(window, "SidebarBrush"));
            Assert.Equal(Color.Parse("#E87BA4"), Token(window, "CardYouStripeBrush"));
            Assert.Equal(AppColors.RiderAccent, (Color)window.FindResource("SystemAccentColor")!);
        }
        finally
        {
            app.Colors.Apply(AppStyle.Standard);
            app.RequestedThemeVariant = before;
            window.Close();
        }
    }

    private static Color BackgroundOf(Window window) => Assert.IsAssignableFrom<ISolidColorBrush>(window.Background).Color;

    private static Color Token(Window window, string key) =>
        Assert.IsAssignableFrom<ISolidColorBrush>(window.FindResource(window.ActualThemeVariant, key)).Color;

    [AvaloniaFact]
    public async Task Send_is_a_round_arrow_in_both_styles_and_the_Claude_style_rounds_the_cards_and_code_and_paints_yours_warm()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        tab.ComposerText = "Why does the parser drop the last line?";
        await tab.SendCommand.ExecuteAsync(null);
        h.Transport.EmitTurn("It stops at the last newline:\n\n```\nwhile (reader.Peek() >= 0)\n```");
        // Not working until the send's Working is applied: the turn's footer is what says the reply is in.
        await TabTestHarness.Eventually(() => !tab.IsWorking && tab.Items.OfType<Conversation.TurnSummaryItem>().Any(), "the reply");
        var window = new MainWindow { DataContext = new MainWindowViewModel(h.Services) { CurrentPage = h.Shell }, Width = 1000, Height = 700 };
        window.Show();
        await UiText.SettleUntilAsync(window, () => YourCard(window) is not null && CodeFrame(window) is not null, "the message and the reply's code");

        // Standard: your card in the categorical palette's magenta, full width (DESIGN.md §18, "Conversation cards"),
        // and Send a round arrow in the accent, as in the Claude style (DESIGN.md §3).
        Assert.Equal(Token(window, "CardYouStripeBrush"), Stripe(YourCard(window)!));
        Assert.Equal(HorizontalAlignment.Stretch, YourCard(window)!.HorizontalAlignment);
        Assert.Equal(new CornerRadius(8), YourCard(window)!.CornerRadius);
        // The code in Claude's reply keeps Standard's corners: its card's "claude" class says who wrote it, and the
        // style's shapes look for the class on the AppearanceScope alone.
        Assert.Equal(new CornerRadius(6), CodeFrame(window)!.CornerRadius);
        AssertRound(window);

        h.Services.Settings.Appearance.Style = AppStyle.Claude;
        h.Services.SaveSettings();
        UiText.Settle(window);

        // Claude: rounder. Its warm grey comes with the colors (The_Claude_style_repaints_the_window_the_tokens_and_the_accent…).
        Assert.Equal(new CornerRadius(14), YourCard(window)!.CornerRadius);
        Assert.Equal(new CornerRadius(12), CodeFrame(window)!.CornerRadius);
        AssertRound(window);

        // Stop too, while a turn runs.
        tab.ComposerText = "And the first line?";
        await tab.SendCommand.ExecuteAsync(null);
        await UiText.SettleUntilAsync(window, () => RoundButton(window, "stop") is { IsEffectivelyVisible: true }, "Stop");
        AssertDrawnAboutTheMiddle(RoundButton(window, "stop")!);
        h.Transport.EmitTurn("It keeps it.");
        await TabTestHarness.Eventually(() => !tab.IsWorking, "the reply");
        window.Close();
    }

    /// <summary>Send: a 32-pixel circle with its arrow drawn about its middle, named for a screen reader.</summary>
    private static void AssertRound(Window window)
    {
        var send = window.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("send"));
        Assert.Equal(32, send.Bounds.Width);
        Assert.Equal(32, send.Bounds.Height);
        Assert.Equal(new CornerRadius(16), send.CornerRadius);
        Assert.Contains("accent", send.Classes);
        Assert.Equal("Send", Avalonia.Automation.AutomationProperties.GetName(send));
        Assert.DoesNotContain(send.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible);
        AssertDrawnAboutTheMiddle(send);
    }

    private static Button? RoundButton(Window window, string name) =>
        window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Classes.Contains(name));

    // The icon fills the button and is drawn about its middle: one centered by layout is rounded to a whole pixel, which
    // at 150% puts a 10 or 14 wide icon half a pixel off.
    private static void AssertDrawnAboutTheMiddle(Button button)
    {
        var icon = button.GetVisualDescendants().OfType<Path>().Single(p => p.IsEffectivelyVisible);
        Assert.Equal(new Point(0, 0), icon.TranslatePoint(new Point(0, 0), button));
        Assert.Equal(button.Bounds.Size, icon.Bounds.Size);
        Assert.Equal(new Point(16, 16), icon.Data!.Bounds.Center);
    }

    private static Card? YourCard(Window window) =>
        window.GetVisualDescendants().OfType<Card>().FirstOrDefault(c => c.Classes.Contains("you"));

    private static Color Stripe(Card card) => Assert.IsAssignableFrom<ISolidColorBrush>(card.StripeBrush).Color;

    private static Border? CodeFrame(Window window) =>
        window.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("codeframe"));

}
