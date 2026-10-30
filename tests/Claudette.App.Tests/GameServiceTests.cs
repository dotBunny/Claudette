using Claudette.App.Games;
using Claudette.App.Tests.Support;
using Claudette.App.ViewModels;
using Claudette.App.ViewModels.ToolWindows;
using Claudette.Core.Settings;

namespace Claudette.App.Tests;

/// <summary>
/// Games with Claudette (DESIGN.md §18): one game for the whole app on the tabs' Games page, her turn after a moment's
/// thought, the score kept on this machine, and the line over the board while Claude needs the user.
/// </summary>
public class GameServiceTests
{
    [Fact]
    public async Task Starting_a_game_shows_the_Games_window_which_goes_when_the_game_ends()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        var games = h.ToolWindow(ToolWindowKinds.Games);
        Assert.False(games.IsAvailable);

        tab.PlayGame(GameKind.TicTacToe);

        Assert.True(h.IsShowing(ToolWindowKinds.Games));
        Assert.True(games.IsAvailable);
        Assert.Same(h.Services.Games, games.Content);
        var game = Assert.IsType<TicTacToeViewModel>(h.Services.Games.Game);

        // Asked for again, it's the same game; another switches to it.
        tab.PlayGame(GameKind.TicTacToe);
        Assert.Same(game, h.Services.Games.Game);
        tab.PlayGame(GameKind.Hangman);
        Assert.IsType<HangmanViewModel>(h.Services.Games.Game);
        Assert.True(h.Services.Games.IsHangman);

        // Another window of its group in its place: the game stays.
        h.ShowToolWindow(ToolWindowKinds.ScratchPad);
        Assert.False(h.IsShowing(ToolWindowKinds.Games));
        Assert.True(h.Services.Games.IsOpen);

        h.Services.Games.CloseCommand.Execute(null);

        Assert.False(h.Services.Games.IsOpen);
        Assert.False(games.IsAvailable);
        Assert.False(h.IsShowing(ToolWindowKinds.Games));
    }

    [Fact]
    public async Task The_game_is_the_apps_so_every_tab_shows_the_same_one()
    {
        await using var h = new TabTestHarness();
        h.Services.Settings.Sessions.RestoreUnpinnedTabs = true;
        h.Services.State.Tabs = [new TabState { Folder = h.WorkFolder }, new TabState { Folder = h.WorkFolder }];
        h.Shell.Restore(null);
        var (first, second) = (h.Shell.AllTabs.ElementAt(0), h.Shell.AllTabs.ElementAt(1));

        second.PlayGame(GameKind.ConnectFour);

        // Its window shows the same game whichever tab has the focus.
        var games = h.ToolWindow(ToolWindowKinds.Games);
        InlineDispatcher.Run(() => h.Shell.SelectedTab = first);
        Assert.True(games.IsAvailable);
        Assert.Same(h.Services.Games, games.Content);

        // Ending it takes the window away.
        h.Services.Games.Close();
        Assert.False(games.IsAvailable);
    }

    [Fact]
    public async Task She_thinks_for_a_moment_before_her_move_and_the_users_moves_wait_for_it()
    {
        await using var h = new TabTestHarness();
        h.Services.Random = new Random(3);
        var tab = await h.OpenTabAsync();
        var announced = new List<string>();
        h.Services.Games.Announcer = announced.Add;
        tab.PlayGame(GameKind.TicTacToe);
        var game = (TicTacToeViewModel)h.Services.Games.Game!;
        Assert.Equal("Your move. You're ✕.", game.Status);

        game.Squares[4].PlayCommand.Execute(null);

        Assert.True(game.IsHerTurn);
        Assert.Equal("Claudette is thinking…", game.Status);
        Assert.Equal(Player.You, game.Squares[4].Owner);
        Assert.Equal("Middle, yours", game.Squares[4].Name);
        // While she thinks, a square the user clicks stays empty.
        game.Squares[0].PlayCommand.Execute(null);
        Assert.Equal(Player.None, game.Squares[0].Owner);

        h.Time.Advance(GameViewModel.LongestThought);

        Assert.False(game.IsHerTurn);
        Assert.Equal("Your move.", game.Status);
        var hers = Assert.Single(game.Squares, s => s.IsHers);
        Assert.Equal($"Claudette took the {TicTacToeViewModel.SquareName(hers.Index)}.", announced.Last());
        Assert.EndsWith(", Claudette's", hers.Name);
    }

    [Fact]
    public async Task Rounds_are_counted_on_this_machine_whoever_starts_takes_turns_and_reset_score_clears_it()
    {
        await using var h = new TabTestHarness();
        h.Services.Random = new Random(5);
        var tab = await h.OpenTabAsync();
        tab.PlayGame(GameKind.TicTacToe);
        var game = (TicTacToeViewModel)h.Services.Games.Game!;
        game.Level = game.Levels.Single(l => l.Level == GameLevel.Hard);
        Assert.Equal("Hard", h.Services.State.Games.Levels["TicTacToe"]);
        Assert.Equal("Unbeatable", game.Level!.Label);
        Assert.Null(game.ScoreText);

        // Unbeatable: the user never wins, so the round ends in her win or a draw.
        PlayOut(h, game);
        Assert.True(game.IsOver);
        Assert.Contains(game.Outcome, new[] { GameOutcome.Lost, GameOutcome.Draw });
        var score = h.Services.State.Games.Scores["TicTacToe"];
        Assert.Equal(1, score.Losses + score.Draws);
        Assert.Equal(0, score.Wins);
        Assert.StartsWith("You 0 · Claudette ", game.ScoreText);
        Assert.Equal("Play again", game.NewRoundText);

        // The next round she starts.
        game.NewRoundCommand.Execute(null);
        Assert.True(game.IsHerTurn);
        h.Time.Advance(GameViewModel.LongestThought);
        Assert.Single(game.Squares, s => s.IsHers);
        Assert.Equal("New game", game.NewRoundText);

        // A new game part-way through isn't counted; the user starts it.
        game.NewRoundCommand.Execute(null);
        Assert.All(game.Squares, s => Assert.Equal(Player.None, s.Owner));
        Assert.False(game.IsHerTurn);
        Assert.Equal(1, h.Services.State.Games.Scores["TicTacToe"].Losses + h.Services.State.Games.Scores["TicTacToe"].Draws);

        game.ResetScoreCommand.Execute(null);
        Assert.Null(game.ScoreText);
        Assert.False(h.Services.State.Games.Scores.ContainsKey("TicTacToe"));
    }

    [Fact]
    public async Task Hangman_takes_typed_letters_says_how_they_went_and_shows_the_word_when_she_wins()
    {
        await using var h = new TabTestHarness();
        h.Services.Random = new Random(11);
        var tab = await h.OpenTabAsync();
        var announced = new List<string>();
        h.Services.Games.Announcer = announced.Add;
        tab.PlayGame(GameKind.Hangman);
        var game = (HangmanViewModel)h.Services.Games.Game!;
        Assert.Equal("Claudette is thinking of a word…", game.Status);
        // A letter while she thinks of the word is taken, so it doesn't go anywhere else, but guesses nothing.
        Assert.True(game.Press('E'));
        Assert.All(game.Letters, l => Assert.False(l.IsGuessed));

        h.Time.Advance(GameViewModel.LongestThought);

        var word = game.Word!;
        Assert.Equal("Guess a letter.", game.Status);
        Assert.Matches("^(A programming|An everyday) word, \\d+ letters$", game.Hint);
        Assert.Equal(string.Join(' ', Enumerable.Repeat('_', word.Length)), game.Pattern);
        Assert.Equal($"{game.Hint}.", announced.Last());

        var hit = word[0];
        Assert.True(game.Press(char.ToLowerInvariant(hit)));
        Assert.StartsWith("Yes, there", game.Status);
        Assert.Equal(hit, game.Pattern[0]);
        Assert.True(game.Letters.Single(l => l.Letter == hit).IsFound);
        Assert.Equal($"{hit}, in the word", game.Letters.Single(l => l.Letter == hit).Name);
        Assert.False(game.Press('1'));

        var misses = Enumerable.Range('A', 26).Select(c => (char)c).Where(c => !word.Contains(c)).Take(6).ToArray();
        foreach (var letter in misses[..5])
        {
            game.Press(letter);
        }
        Assert.Equal(5, game.Misses);
        Assert.True(game.ShowsLeftLeg);
        Assert.False(game.ShowsRightLeg);
        Assert.Equal($"No {misses[4]}. One miss left.", game.Status);

        game.Press(misses[5]);

        Assert.Equal(GameOutcome.Lost, game.Outcome);
        Assert.Equal($"Claudette wins! It was {word}.", game.Status);
        Assert.Equal(string.Join(' ', word.ToCharArray()), game.Pattern);
        Assert.Equal(1, h.Services.State.Games.Scores["Hangman"].Losses);
        Assert.Equal("You 0 · Claudette 1", game.ScoreText);
    }

    [Fact]
    public async Task Connect_four_drops_by_column_number_and_she_answers()
    {
        await using var h = new TabTestHarness();
        h.Services.Random = new Random(2);
        var tab = await h.OpenTabAsync();
        tab.PlayGame(GameKind.ConnectFour);
        var game = (ConnectFourViewModel)h.Services.Games.Game!;

        Assert.True(game.Press('4'));
        Assert.False(game.Press('8'));
        Assert.Equal("Column 4, 1 of 6 filled", game.Columns[3].Name);
        Assert.True(game.Columns[3].Cells[^1].IsYours);
        h.Time.Advance(GameViewModel.LongestThought);

        Assert.Equal(2, game.Columns.Sum(c => c.Filled));
        Assert.Single(game.Columns.SelectMany(c => c.Cells), c => c.IsHers);
        Assert.Single(game.Columns.SelectMany(c => c.Cells), c => c.IsLast && c.IsHers);
    }

    [Fact]
    public async Task Minesweeper_opens_and_flags_squares_and_counts_a_cleared_field_as_a_win()
    {
        await using var h = new TabTestHarness();
        h.Services.Random = new Random(8);
        var tab = await h.OpenTabAsync();
        var announced = new List<string>();
        h.Services.Games.Announcer = announced.Add;
        tab.PlayGame(GameKind.Minesweeper);
        var game = Assert.IsType<MinesweeperViewModel>(h.Services.Games.Game);
        Assert.Equal("Claudette hid 13 mines. Open a square.", game.Status);
        Assert.Equal("Row 1, column 1, not opened", game.Squares[0].Name);
        // The number keys and letters are the squares' own: the page doesn't take them.
        Assert.False(game.Press('1'));

        game.Squares[40].OpenCommand.Execute(null);

        Assert.True(game.Squares[40].IsOpen);
        Assert.Equal("Row 5, column 5, empty", game.Squares[40].Name);
        Assert.Equal("13 mines left to flag.", game.Status);
        Assert.Matches("^\\d+ squares opened\\.$", announced.Last());
        var mines = game.Squares.Where(s => game.Field.IsMine(s.Index)).ToList();
        Assert.Equal(13, mines.Count);

        // A flag on a mine: one fewer to flag, and the square stays shut.
        mines[0].FlagCommand.Execute(null);
        Assert.Equal("12 mines left to flag.", game.Status);
        Assert.EndsWith(", flagged", announced.Last());
        mines[0].OpenCommand.Execute(null);
        Assert.True(mines[0].ShowsFlag);
        Assert.False(game.IsOver);

        // Every square without a mine opened: the user wins, and her mines show flagged.
        foreach (var square in game.Squares.Where(s => !game.Field.IsMine(s.Index) && !s.IsOpen))
        {
            square.OpenCommand.Execute(null);
        }

        Assert.Equal(GameOutcome.Won, game.Outcome);
        Assert.Equal("You cleared the field: you win!", game.Status);
        Assert.All(mines, m => Assert.True(m.ShowsFlag));
        Assert.Equal("You 1 · Claudette 0", game.ScoreText);
        Assert.Equal(1, h.Services.State.Games.Scores["Minesweeper"].Wins);
    }

    [Fact]
    public async Task Opening_one_of_her_mines_loses_and_shows_where_they_were_and_the_wrong_flags()
    {
        await using var h = new TabTestHarness();
        h.Services.Random = new Random(12);
        var tab = await h.OpenTabAsync();
        tab.PlayGame(GameKind.Minesweeper);
        var game = (MinesweeperViewModel)h.Services.Games.Game!;
        game.Squares[0].OpenCommand.Execute(null);
        var mines = game.Squares.Where(s => game.Field.IsMine(s.Index)).ToList();
        var wrong = game.Squares.First(s => !game.Field.IsMine(s.Index) && !s.IsOpen);
        wrong.FlagCommand.Execute(null);

        mines[0].OpenCommand.Execute(null);

        Assert.Equal(GameOutcome.Lost, game.Outcome);
        Assert.Equal("Boom! Claudette wins.", game.Status);
        Assert.True(mines[0].IsExploded);
        Assert.EndsWith(", the mine you opened", mines[0].Name);
        Assert.All(mines.Skip(1), m => Assert.Equal(MinesweeperSquareState.Mine, m.State));
        Assert.True(wrong.IsWrongFlag);
        Assert.Equal(1, h.Services.State.Games.Scores["Minesweeper"].Losses);
        // Over, the field takes no more.
        var shut = game.Squares.First(s => s.State == MinesweeperSquareState.Mine);
        shut.OpenCommand.Execute(null);
        Assert.Equal(MinesweeperSquareState.Mine, shut.State);
    }

    [Fact]
    public async Task Another_level_in_minesweeper_is_another_number_of_mines_on_a_new_field()
    {
        await using var h = new TabTestHarness();
        h.Services.Random = new Random(1);
        var tab = await h.OpenTabAsync();
        tab.PlayGame(GameKind.Minesweeper);
        var game = (MinesweeperViewModel)h.Services.Games.Game!;
        game.Squares[40].OpenCommand.Execute(null);

        game.Level = game.Levels.Single(l => l.Level == GameLevel.Hard);

        Assert.Equal("Claudette hid 16 mines. Open a square.", game.Status);
        Assert.All(game.Squares, s => Assert.Equal(MinesweeperSquareState.Shut, s.State));
        Assert.False(game.Field.HasMines);
        // The round dropped wasn't counted.
        Assert.False(h.Services.State.Games.Scores.ContainsKey("Minesweeper"));
    }

    [Fact]
    public async Task While_tabs_wait_on_the_user_a_line_over_the_board_says_where_with_Go_there()
    {
        await using var h = new TabTestHarness();
        h.Services.Settings.Sessions.RestoreUnpinnedTabs = true;
        h.Services.State.Tabs = [new TabState { Folder = h.WorkFolder, IsPinned = true, UserName = "auth refactor" }, new TabState { Folder = h.WorkFolder, UserName = "docs" },
            new TabState { Folder = h.WorkFolder, UserName = "tests" }];
        h.Shell.Restore(null);
        var (a, b, c) = (h.Shell.AllTabs.ElementAt(0), h.Shell.AllTabs.ElementAt(1), h.Shell.AllTabs.ElementAt(2));
        await TabTestHarness.Eventually(() => a.Status == TabStatus.Idle && a.IsSettled, "the selected tab to start");
        var games = h.Services.Games;
        a.PlayGame(GameKind.TicTacToe);
        Assert.False(games.HasCall);

        c.Status = TabStatus.NeedsInput;
        Assert.Equal("Claude needs you in tests", games.Call);
        Assert.True(games.CanAnswerCall);
        b.Status = TabStatus.NeedsInput;
        Assert.Equal("Claude needs you in 2 tabs", games.Call);

        games.AnswerCallCommand.Execute(null);
        Assert.Same(b, h.Shell.SelectedTab);
        Assert.Equal("Claude needs you in tests", games.Call);
        // Selected, the restored tab starts, which sets its status; it waits again once started.
        await TabTestHarness.Eventually(() => b.Status == TabStatus.Idle && b.IsSettled, "the tab gone to to start");
        b.Status = TabStatus.NeedsInput;

        // The tab in front waiting: nowhere to go.
        c.Status = TabStatus.Idle;
        Assert.Equal("Claude is waiting for you in this tab", games.Call);
        Assert.False(games.CanAnswerCall);
        b.Status = TabStatus.Idle;
        Assert.False(games.HasCall);
        // The game never moved.
        Assert.IsType<TicTacToeViewModel>(games.Game);
    }

    [Fact]
    public async Task Turning_games_off_ends_the_one_open_and_takes_every_way_in()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        tab.PlayGame(GameKind.Hangman);
        Assert.Contains(h.Shell.PaletteEntries(), e => e.Label == "Play hangman with Claudette");

        h.Services.Settings.Appearance.GamesWithClaudette = false;
        h.Services.SaveSettings();

        Assert.False(h.Services.Games.IsOpen);
        Assert.False(h.ToolWindow(ToolWindowKinds.Games).IsAvailable);
        Assert.DoesNotContain(h.Shell.PaletteEntries(), e => e.Label.StartsWith("Play ", StringComparison.Ordinal));
        tab.PlayGame(GameKind.TicTacToe);
        Assert.False(h.Services.Games.IsOpen);
        Assert.False(h.IsShowing(ToolWindowKinds.Games));
    }

    [Fact]
    public async Task The_palette_starts_each_game()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        string[] labels = ["Play tic-tac-toe with Claudette", "Play Connect Four with Claudette", "Play hangman with Claudette", "Play Minesweeper with Claudette"];
        foreach (var (label, type) in labels.Zip(new[] { typeof(TicTacToeViewModel), typeof(ConnectFourViewModel), typeof(HangmanViewModel), typeof(MinesweeperViewModel) }))
        {
            await h.Shell.PaletteEntries().Single(e => e.Label == label).Run();
            Assert.IsType(type, h.Services.Games.Game);
            Assert.True(h.IsShowing(ToolWindowKinds.Games));
        }
    }

    [Fact]
    public async Task The_usage_limit_bar_offers_a_game_until_the_limit_resets()
    {
        await using var h = new TabTestHarness();
        var tab = await h.OpenTabAsync();
        Assert.False(tab.CanPlayWhileWaiting);
        var changed = new List<string?>();
        tab.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        var reset = h.Time.GetUtcNow() + TimeSpan.FromHours(2);
        await AutoContinueTests.HitLimitAsync(h, tab, reset);

        Assert.True(tab.CanPlayWhileWaiting);
        Assert.Contains(nameof(TabViewModel.CanPlayWhileWaiting), changed);
        // Left stopped, the limit still holds; once it resets, there's nothing to wait for.
        tab.DontContinueCommand.Execute(null);
        Assert.True(tab.CanPlayWhileWaiting);
        await AutoContinueTests.AdvanceToAsync(h, reset + TimeSpan.FromMinutes(1));
        await TabTestHarness.Eventually(() => tab.LimitWait is { HasReset: true }, "the reset");
        Assert.False(tab.CanPlayWhileWaiting);
    }

    [Fact]
    public async Task Her_tip_offers_a_game_only_while_theres_a_wait()
    {
        await using var h = new TabTestHarness();
        var mascot = h.Services.Mascot;
        string? Tips(int seed) => string.Join("|", Enumerable.Range(0, 200).Select(i => mascot.Tip(new Random(seed + i))).Distinct());

        Assert.DoesNotContain("Right-click me for a game while you wait", Tips(0));
        mascot.Follow(new Mascot.MascotSituation { Mood = Mascot.MascotMood.Working });
        Assert.Contains("Right-click me for a game while you wait", Tips(0));
        h.Services.Settings.Appearance.GamesWithClaudette = false;
        Assert.DoesNotContain("Right-click me for a game while you wait", Tips(0));
    }

    /// <summary>Plays the user's side, the first free square each time, until the round ends.</summary>
    private static void PlayOut(TabTestHarness h, TicTacToeViewModel game)
    {
        while (!game.IsOver)
        {
            if (game.IsHerTurn)
            {
                h.Time.Advance(GameViewModel.LongestThought);
                continue;
            }
            game.Squares.First(s => s.Owner == Player.None).PlayCommand.Execute(null);
        }
    }
}
