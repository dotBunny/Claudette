using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Claudette.App.Games;
using Claudette.App.Services;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.App.ViewModels.ToolWindows;
using Claudette.App.Views;

namespace Claudette.App.UiTests;

/// <summary>
/// Games with Claudette rendered (DESIGN.md §18): the Games tool window with each board and the game's menu in its
/// header, the keyboard while the page has focus, and the ways in: her menu and the usage limit bar.
/// </summary>
public class GamesUiTests
{
    [AvaloniaFact]
    public async Task The_Games_page_is_made_only_while_a_game_shows_and_plays_tic_tac_toe()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        h.Services.Random = new Random(4);
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        Assert.Empty(window.GetVisualDescendants().OfType<GamesView>());
        Assert.DoesNotContain(PageTabs(window), t => t == "Games");

        tab.PlayGame(GameKind.TicTacToe);
        UiText.Settle(window);

        Assert.Contains("Games", PageTabs(window));
        var page = window.GetVisualDescendants().OfType<GamesView>().Single();
        var squares = Squares(page);
        Assert.Equal(9, squares.Count);
        Assert.Equal("Top left, empty", AutomationProperties.GetName(squares[0]));

        Click(window, squares[4]);
        h.Time.Advance(GameViewModel.LongestThought);
        UiText.Settle(window);

        Assert.Equal("Middle, yours", AutomationProperties.GetName(Squares(page)[4]));
        Assert.Single(Squares(page), s => AutomationProperties.GetName(s)!.EndsWith(", Claudette's", StringComparison.Ordinal));
        var shown = UiText.Describe(page);

        // Another window of its group, and the game's controls go; it's still open, and shows again in its window.
        h.ShowToolWindow(ToolWindowKinds.ScratchPad);
        UiText.Settle(window);
        Assert.Empty(window.GetVisualDescendants().OfType<GamesView>());
        h.ShowToolWindow(ToolWindowKinds.Games);
        UiText.Settle(window);
        page = window.GetVisualDescendants().OfType<GamesView>().Single();
        Assert.Single(Squares(page), s => AutomationProperties.GetName(s)!.EndsWith(", yours", StringComparison.Ordinal));

        // Done playing: the page and its tab go.
        Click(window, page.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Done playing"));
        Assert.DoesNotContain(PageTabs(window), t => t == "Games");
        window.Close();

        await Verify(shown);
    }

    [AvaloniaFact]
    public async Task Hangman_takes_letters_typed_on_the_page_and_none_reach_the_composer()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        h.Services.Random = new Random(9);
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        tab.PlayGame(GameKind.Hangman);
        UiText.Settle(window);
        h.Time.Advance(GameViewModel.LongestThought);
        UiText.Settle(window);
        var page = window.GetVisualDescendants().OfType<GamesView>().Single();
        var game = (HangmanViewModel)h.Services.Games.Game!;
        // Started just now, the page took the keyboard.
        await UiText.SettleUntilAsync(window, () => page.IsKeyboardFocusWithin, "the page to take the keyboard");

        var hit = game.Word![0];
        var miss = Enumerable.Range('A', 26).Select(c => (char)c).First(c => !game.Word.Contains(c));
        window.KeyPressQwerty(Physical(hit), RawInputModifiers.None);
        window.KeyPressQwerty(Physical(miss), RawInputModifiers.None);
        UiText.Settle(window);

        Assert.Equal(hit, game.Pattern[0]);
        Assert.Equal(1, game.Misses);
        Assert.Equal("", tab.ComposerText ?? "");
        Assert.Equal($"{miss}, not in the word", AutomationProperties.GetName(Letter(page, miss)));
        Assert.Equal("The gallows, 1 of 6 misses drawn", AutomationProperties.GetName(page.GetVisualDescendants().OfType<Canvas>().Single()));
        var shown = UiText.Describe(page, (game.Word, "WORD"));
        window.Close();

        await Verify(shown);

        static PhysicalKey Physical(char letter) => PhysicalKey.A + (letter - 'A');

        static Button Letter(GamesView page, char letter) =>
            page.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("gameletter") && b.Content as string == letter.ToString());
    }

    [AvaloniaFact]
    public async Task Connect_four_drops_a_piece_in_the_column_clicked()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        h.Services.Random = new Random(6);
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        tab.PlayGame(GameKind.ConnectFour);
        UiText.Settle(window);
        var page = window.GetVisualDescendants().OfType<GamesView>().Single();
        var columns = page.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("gamecolumn")).ToList();
        Assert.Equal(7, columns.Count);
        Assert.Equal("Column 3, 0 of 6 filled", AutomationProperties.GetName(columns[2]));

        Click(window, columns[2]);
        h.Time.Advance(GameViewModel.LongestThought);
        UiText.Settle(window);

        Assert.Equal("Column 3, 1 of 6 filled", AutomationProperties.GetName(columns[2]));
        var pieces = columns[2].GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().ToList();
        Assert.Contains("yours", pieces[^1].Classes);
        Assert.Single(page.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>(), e => e.Classes.Contains("hers"));
        var shown = UiText.Describe(page);
        window.Close();

        await Verify(shown);
    }

    [AvaloniaFact]
    public async Task The_windows_header_names_the_game_and_its_menu_switches_to_another()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        tab.PlayGame(GameKind.TicTacToe);
        UiText.Settle(window);

        var choice = GameChoice(window);
        Assert.Equal("Game: Tic-tac-toe", AutomationProperties.GetName(choice));
        Assert.Contains("Tic-tac-toe", choice.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
        // It's in the header, beside the window's title, and the page under it has no row of games.
        var header = choice.GetVisualAncestors().OfType<Grid>().First(g => g.Classes.Contains("toolheader"));
        Assert.Contains(header.GetVisualDescendants().OfType<TextBlock>(), t => t.Classes.Contains("tooltitle") && t.Text == "Games");
        var page = window.GetVisualDescendants().OfType<GamesView>().Single();
        Assert.DoesNotContain(page.GetVisualDescendants().OfType<Button>(), b => b.Content is "Connect Four" or TextBlock { Text: "Connect Four" });

        var menu = Assert.IsType<MenuFlyout>(choice.Flyout);
        var items = menu.Items.OfType<MenuItem>().ToList();
        Assert.Equal(["Tic-tac-toe", "Connect Four", "Hangman", "Minesweeper"], items.Select(i => i.Header as string));
        menu.ShowAt(choice);
        await UiText.SettleUntilAsync(window, () => items[0].IsEffectivelyVisible, "the open menu");
        Assert.Equal([true, false, false, false], items.Select(i => i.IsChecked));

        items[3].Command!.Execute(items[3].CommandParameter);
        menu.Hide();
        UiText.Settle(window);

        Assert.IsType<MinesweeperViewModel>(h.Services.Games.Game);
        Assert.Equal("Game: Minesweeper", AutomationProperties.GetName(GameChoice(window)));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Minesweeper_opens_with_a_click_flags_with_a_right_click_or_F_and_the_arrows_move_between_squares()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        h.Services.Random = new Random(5);
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        tab.PlayGame(GameKind.Minesweeper);
        UiText.Settle(window);
        var page = window.GetVisualDescendants().OfType<GamesView>().Single();
        var game = (MinesweeperViewModel)h.Services.Games.Game!;
        var squares = MineSquares(page);
        Assert.Equal(81, squares.Count);
        // Nine to a row, in the right side at its narrowest.
        Point At(int square) => squares[square].TranslatePoint(default, page)!.Value;
        Assert.Equal(At(0).Y, At(8).Y);
        Assert.True(At(9).Y > At(0).Y);
        Assert.True(At(8).X + squares[8].Bounds.Width <= ToolDockViewModel.MinRightWidth - 16);

        Click(window, squares[40]);

        Assert.True(game.Squares[40].IsOpen);
        Assert.Contains("open", squares[40].Classes);
        var shut = game.Squares.Where(s => s.State == MinesweeperSquareState.Shut).Select(s => s.Index).ToList();

        // A right-click flags a shut square.
        var center = squares[shut[0]].TranslatePoint(new Point(squares[shut[0]].Bounds.Width / 2, squares[shut[0]].Bounds.Height / 2), window)!.Value;
        window.MouseMove(center);
        window.MouseDown(center, MouseButton.Right);
        window.MouseUp(center, MouseButton.Right);
        UiText.Settle(window);
        Assert.EndsWith(", flagged", AutomationProperties.GetName(MineSquares(page)[shut[0]]));

        // F flags the one with the focus, and again takes the flag away.
        var square = MineSquares(page)[shut[1]];
        square.Focus();
        UiText.Settle(window);
        window.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.None);
        UiText.Settle(window);
        Assert.True(game.Squares[shut[1]].ShowsFlag);
        window.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.None);
        UiText.Settle(window);
        Assert.False(game.Squares[shut[1]].ShowsFlag);
        Assert.Equal("", tab.ComposerText ?? "");

        // The arrow keys move the focus from square to square.
        MineSquares(page)[0].Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        UiText.Settle(window);
        Assert.Same(MineSquares(page)[10], window.FocusManager!.GetFocusedElement());
        // Tab leaves the field, rather than going through its 81 squares.
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        UiText.Settle(window);
        var next = Assert.IsAssignableFrom<Control>(window.FocusManager.GetFocusedElement());
        Assert.DoesNotContain("minesquare", next.Classes);
        var shown = UiText.Describe(page);
        window.Close();

        await Verify(shown);
    }

    [AvaloniaFact]
    public async Task Her_menu_offers_the_games_while_theyre_on()
    {
        await using var h = new TabTestHarness(dispatcher: new AvaloniaUiDispatcher());
        var tab = await h.OpenTabAsync();
        var window = UiText.Show(new ShellView { DataContext = h.Shell });
        var layer = window.GetVisualDescendants().OfType<MascotLayer>().Single(l => !l.IsFront);
        var menu = layer.Figure.ContextMenu!;
        Assert.Equal(["Play tic-tac-toe", "Play Connect Four", "Play hangman", "Play Minesweeper", "Hide Claudette", "Claudette settings…"],
            menu.Items.OfType<MenuItem>().Where(i => i.IsVisible).Select(i => i.Header as string));

        menu.Items.OfType<MenuItem>().Single(i => i.Header as string == "Play Connect Four").Command!.Execute(GameKind.ConnectFour);
        UiText.Settle(window);
        Assert.IsType<ConnectFourViewModel>(h.Services.Games.Game);
        Assert.True(h.IsShowing(ToolWindowKinds.Games));

        h.Services.Settings.Appearance.GamesWithClaudette = false;
        h.Services.SaveSettings();
        UiText.Settle(window);
        Assert.Equal(["Hide Claudette", "Claudette settings…"], menu.Items.OfType<MenuItem>().Where(i => i.IsVisible).Select(i => i.Header as string));
        Assert.DoesNotContain(menu.Items.OfType<Separator>(), s => s.IsVisible);
        Assert.Empty(window.GetVisualDescendants().OfType<GamesView>());
        window.Close();
    }

    /// <summary>The tool windows' icons on the strip, by name.</summary>
    private static List<string?> PageTabs(Window window) =>
        [.. window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("stripicon") && b.IsEffectivelyVisible).Select(AutomationProperties.GetName)];

    private static List<Button> Squares(Control page) => [.. page.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("gamesquare"))];

    private static List<Button> MineSquares(Control page) => [.. page.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("minesquare"))];

    /// <summary>The game's menu in the Games window's header.</summary>
    private static Button GameChoice(Window window) =>
        window.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("toolchoice") && b.IsEffectivelyVisible);

    private static void Click(Window window, Control target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseMove(center);
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        UiText.Settle(window);
    }
}
