using Claudette.App.Games;

namespace Claudette.App.Tests;

/// <summary>Games with Claudette (DESIGN.md §18): the rules, and how she plays.</summary>
public class GamesTests
{
    private const Player You = Player.You;
    private const Player Her = Player.Claudette;

    // ---- Tic-tac-toe ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(3, 4, 5)]
    [InlineData(6, 7, 8)]
    [InlineData(0, 3, 6)]
    [InlineData(2, 5, 8)]
    [InlineData(0, 4, 8)]
    [InlineData(2, 4, 6)]
    public void Three_in_a_line_wins_tic_tac_toe(int a, int b, int c)
    {
        var board = new TicTacToe();
        board.Play(a, You);
        board.Play(b, You);
        Assert.Equal(GameOutcome.Playing, board.Outcome);
        Assert.Equal(c, board.WinningMove(You));

        board.Play(c, You);

        Assert.Equal(GameOutcome.Won, board.Outcome);
        Assert.Equal([a, b, c], board.WinningLine!);
        Assert.False(board.CanPlay(Enumerable.Range(0, 9).First(s => s != a && s != b && s != c)));
    }

    [Fact]
    public void A_square_is_taken_once_and_a_full_board_without_a_line_is_a_draw()
    {
        var board = new TicTacToe();
        board.Play(4, Her);
        Assert.False(board.CanPlay(4));
        Assert.Throws<InvalidOperationException>(() => board.Play(4, You));

        // ✕ ○ ✕ / ✕ ○ ○ / ○ ✕ ✕
        var draw = new TicTacToe();
        int[] yours = [0, 2, 3, 7, 8], hers = [1, 4, 5, 6];
        foreach (var square in yours)
        {
            draw.Play(square, You);
        }
        foreach (var square in hers)
        {
            draw.Play(square, Her);
        }
        Assert.Equal(GameOutcome.Draw, draw.Outcome);
        Assert.Null(draw.WinningLine);
    }

    [Fact]
    public void Unbeatable_never_loses_whoever_starts_and_whatever_the_user_plays()
    {
        var games = 0;
        Play(new TicTacToe(), You);
        Play(new TicTacToe(), Her);
        Assert.True(games > 100, $"only {games} games");

        // Every move the user can make, and every move she might choose, to the end of the game.
        void Play(TicTacToe board, Player toMove)
        {
            if (board.IsOver)
            {
                Assert.NotEqual(GameOutcome.Won, board.Outcome);
                games++;
                return;
            }
            var moves = toMove == You ? board.Free.ToArray() : [.. TicTacToeOpponent.BestMoves(board)];
            foreach (var square in moves)
            {
                board.Set(square, toMove);
                Play(board, toMove.Other());
                board.Set(square, Player.None);
            }
        }
    }

    [Theory]
    [InlineData(GameLevel.Easy)]
    [InlineData(GameLevel.Normal)]
    [InlineData(GameLevel.Hard)]
    public void On_every_level_she_takes_a_win_in_front_of_her(GameLevel level)
    {
        for (var seed = 0; seed < 20; seed++)
        {
            // ○ ○ _ / ✕ ✕ _ / ✕ _ _: the top right wins for her, and she'd block the user's middle right otherwise.
            var board = new TicTacToe();
            board.Play(0, Her);
            board.Play(1, Her);
            board.Play(3, You);
            board.Play(4, You);
            board.Play(6, You);
            Assert.Equal(2, TicTacToeOpponent.Choose(board, level, new Random(seed)));
        }
    }

    [Fact]
    public void Normal_blocks_the_users_win_and_easy_sometimes_doesnt()
    {
        var easyMissed = false;
        for (var seed = 0; seed < 30; seed++)
        {
            // ✕ ✕ _ / ○ _ _ / _ _ _: the top right wins for the user.
            var board = new TicTacToe();
            board.Play(0, You);
            board.Play(1, You);
            board.Play(3, Her);
            Assert.Equal(2, TicTacToeOpponent.Choose(board, GameLevel.Normal, new Random(seed)));
            Assert.Equal(2, TicTacToeOpponent.Choose(board, GameLevel.Hard, new Random(seed)));
            easyMissed |= TicTacToeOpponent.Choose(board, GameLevel.Easy, new Random(seed)) != 2;
        }
        Assert.True(easyMissed);
    }

    // ---- Connect Four --------------------------------------------------------------------------------------------------

    [Fact]
    public void Pieces_fall_to_the_bottom_and_a_full_column_takes_no_more()
    {
        var board = new ConnectFour();
        Assert.Equal(0, board.Drop(3, You));
        Assert.Equal(1, board.Drop(3, Her));
        Assert.Equal((3, 1), board.LastMove);
        for (var row = 2; row < ConnectFour.Rows; row++)
        {
            board.Drop(3, row % 2 == 0 ? You : Her);
        }
        Assert.Equal(ConnectFour.Rows, board.Height(3));
        Assert.False(board.CanDrop(3));
        Assert.Throws<InvalidOperationException>(() => board.Drop(3, You));
        Assert.Equal(GameOutcome.Playing, board.Outcome);
    }

    [Fact]
    public void Four_across_up_or_diagonally_wins_connect_four_and_ringed_are_the_four()
    {
        var across = new ConnectFour();
        foreach (var column in new[] { 0, 1, 2 })
        {
            across.Drop(column, You);
        }
        Assert.Equal(3, across.WinningMove(You));
        across.Drop(3, You);
        Assert.Equal(GameOutcome.Won, across.Outcome);
        Assert.Equal([(0, 0), (1, 0), (2, 0), (3, 0)], across.WinningCells);

        var up = new ConnectFour();
        for (var i = 0; i < 4; i++)
        {
            up.Drop(5, Her);
        }
        Assert.Equal(GameOutcome.Lost, up.Outcome);
        Assert.Equal([(5, 0), (5, 1), (5, 2), (5, 3)], up.WinningCells);

        // A rising diagonal from the bottom left: each column propped up with the user's pieces.
        var diagonal = new ConnectFour();
        for (var column = 0; column < 4; column++)
        {
            for (var below = 0; below < column; below++)
            {
                diagonal.Drop(column, You);
            }
            diagonal.Drop(column, Her);
        }
        Assert.Equal(Her, diagonal.Winner);
        Assert.Equal([(0, 0), (1, 1), (2, 2), (3, 3)], diagonal.WinningCells);
    }

    [Fact]
    public void A_full_board_without_four_in_a_row_is_a_draw()
    {
        var board = new ConnectFour();
        for (var column = 0; column < ConnectFour.Columns; column++)
        {
            for (var row = 0; row < ConnectFour.Rows; row++)
            {
                // Pairs up each column, turning over from one column to the next: never four of one anywhere.
                board.Drop(column, (row / 2 + column) % 2 == 0 ? You : Her);
            }
        }
        Assert.True(board.IsFull);
        Assert.Equal(GameOutcome.Draw, board.Outcome);
    }

    [Theory]
    [InlineData(GameLevel.Easy)]
    [InlineData(GameLevel.Normal)]
    [InlineData(GameLevel.Hard)]
    public void On_every_level_she_takes_four_in_a_row_in_front_of_her(GameLevel level)
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var board = new ConnectFour();
            foreach (var column in new[] { 1, 2, 3 })
            {
                board.Drop(column, Her);
                board.Drop(column, You);
            }
            // She has three across the bottom: the first or fifth column wins.
            Assert.Contains(ConnectFourOpponent.Choose(board, level, new Random(seed)), new[] { 0, 4 });
        }
    }

    [Theory]
    [InlineData(GameLevel.Normal)]
    [InlineData(GameLevel.Hard)]
    public void Looking_ahead_she_blocks_the_users_four(GameLevel level)
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var board = new ConnectFour();
            // Three of the user's in the middle column; hers beside them don't make a line.
            board.Drop(3, You);
            board.Drop(0, Her);
            board.Drop(3, You);
            board.Drop(6, Her);
            board.Drop(3, You);
            Assert.Equal(3, ConnectFourOpponent.Choose(board, level, new Random(seed)));
        }
    }

    [Fact]
    public void Hard_doesnt_lose_to_easy()
    {
        for (var seed = 0; seed < 3; seed++)
        {
            var random = new Random(seed);
            var board = new ConnectFour();
            // Easy plays the user's side, starting; Hard is her.
            var toMove = You;
            while (!board.IsOver)
            {
                var column = toMove == You ? EasyForTheUser(board, random) : ConnectFourOpponent.Choose(board, GameLevel.Hard, random);
                board.Drop(column, toMove);
                toMove = toMove.Other();
            }
            Assert.NotEqual(GameOutcome.Won, board.Outcome);
        }

        // Easy's play, for the user's pieces: what she'd do on Easy with the colors swapped.
        static int EasyForTheUser(ConnectFour board, Random random)
        {
            if (board.WinningMove(You) is { } win)
            {
                return win;
            }
            var free = Enumerable.Range(0, ConnectFour.Columns).Where(board.CanDrop).ToArray();
            return free[random.Next(free.Length)];
        }
    }

    // ---- Hangman -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Hangman_shows_the_letters_guessed_and_ignores_repeats_and_anything_else()
    {
        var round = new Hangman(new HangmanWord("COMMIT", HangmanCategory.Programming));
        Assert.Equal("______", round.Pattern);

        Assert.Equal(2, round.Guess('m'));
        Assert.Equal("__MM__", round.Pattern);
        Assert.Null(round.Guess('M'));
        Assert.Null(round.Guess('3'));
        Assert.Null(round.Guess('é'));
        Assert.Equal(0, round.Guess('Z'));
        Assert.Equal(1, round.Misses);
        Assert.Equal(5, round.MissesLeft);

        foreach (var letter in "COIT")
        {
            round.Guess(letter);
        }
        Assert.True(round.IsSolved);
        Assert.Equal(GameOutcome.Won, round.Outcome);
        Assert.Equal("COMMIT", round.Pattern);
        Assert.Null(round.Guess('A'));
    }

    [Fact]
    public void Six_misses_lose_hangman()
    {
        var round = new Hangman(new HangmanWord("QUEUE", HangmanCategory.Programming));
        foreach (var letter in "ABCDF")
        {
            Assert.Equal(0, round.Guess(letter));
        }
        Assert.Equal(GameOutcome.Playing, round.Outcome);
        Assert.Equal(1, round.MissesLeft);

        round.Guess('G');

        Assert.True(round.IsLost);
        Assert.Equal(GameOutcome.Lost, round.Outcome);
        Assert.Null(round.Guess('Q'));
    }

    [Fact]
    public void Hangmans_words_are_capitals_that_fit_and_come_up_once_each()
    {
        string[] all = [.. HangmanWords.Programming, .. HangmanWords.Everyday];
        Assert.All(all, word => Assert.Matches("^[A-Z]{4,12}$", word));
        Assert.Equal(all.Length, all.Distinct().Count());
        Assert.True(HangmanWords.Programming.Count > HangmanWords.Everyday.Count);
    }

    [Fact]
    public void She_picks_programming_words_three_times_in_four_and_not_one_that_came_up_lately()
    {
        var random = new Random(7);
        var picks = Enumerable.Range(0, 800).Select(_ => HangmanWords.Pick(random, [])).ToList();
        var programming = picks.Count(p => p.Category == HangmanCategory.Programming) / (double)picks.Count;
        Assert.InRange(programming, 0.7, 0.8);
        Assert.All(picks, p => Assert.Contains(p.Word, p.Category == HangmanCategory.Programming ? HangmanWords.Programming : HangmanWords.Everyday));

        var recent = HangmanWords.Programming.Concat(HangmanWords.Everyday).Where(w => w != "QUEUE" && w != "ZEBRA").ToList();
        for (var i = 0; i < 20; i++)
        {
            Assert.Contains(HangmanWords.Pick(random, recent).Word, new[] { "QUEUE", "ZEBRA" });
        }
    }

    // ---- Minesweeper ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(40)]
    [InlineData(80)]
    public void She_hides_her_mines_as_the_first_square_opens_never_in_it_or_next_to_it(int first)
    {
        for (var seed = 0; seed < 30; seed++)
        {
            var field = new Minesweeper(Minesweeper.MinesFor(GameLevel.Hard));
            Assert.False(field.HasMines);

            var opened = field.Open(first, new Random(seed));

            Assert.True(field.HasMines);
            Assert.Equal(GameOutcome.Playing, field.Outcome);
            Assert.Equal(16, Enumerable.Range(0, Minesweeper.Size).Count(field.IsMine));
            Assert.Equal(0, field.MinesAround(first));
            // With none around it, the first square opens those around it too.
            Assert.Contains(first, opened);
            Assert.All(Minesweeper.Around(first), s => Assert.True(field.IsOpen(s)));
            Assert.Equal(Minesweeper.Size - 16 - opened.Count, field.SafeLeft);
        }
    }

    [Fact]
    public void A_square_with_no_mines_around_opens_the_squares_around_it_and_opening_every_safe_one_wins()
    {
        var field = Field(0, 80);

        // The middle opens everything but the corners' mines, stopping at the squares that touch them.
        var opened = field.Open(40, new Random(0));

        Assert.Equal(Minesweeper.Size - 2, opened.Count);
        Assert.False(field.IsOpen(0));
        Assert.Equal(1, field.MinesAround(10));
        Assert.Equal(0, field.SafeLeft);
        Assert.Equal(GameOutcome.Won, field.Outcome);
        Assert.Empty(field.Open(0, new Random(0)));
    }

    [Fact]
    public void A_flag_keeps_a_square_shut_and_a_number_with_as_many_flags_opens_the_rest_around_it()
    {
        // Mines at the top left and two along: the square between them has two around it.
        var field = Field(0, 2);
        Assert.Equal([1], field.Open(1, new Random(0)));
        Assert.Equal(2, field.MinesAround(1));
        // Without its flags, a click on the number does nothing.
        Assert.Empty(field.OpenAround(1, new Random(0)));

        Assert.True(field.ToggleFlag(0));
        Assert.Empty(field.Open(0, new Random(0)));
        Assert.True(field.ToggleFlag(2));
        Assert.False(field.ToggleFlag(1));
        Assert.Equal(2, field.Flags);

        Assert.Equal([9, 10, 11], field.OpenAround(1, new Random(0)).Order());
        Assert.Equal(GameOutcome.Playing, field.Outcome);
        // Under them, nothing touches a mine: the rest of the field opens, and the round is won.
        field.OpenAround(10, new Random(0));
        Assert.Equal(GameOutcome.Won, field.Outcome);
    }

    [Fact]
    public void Opening_around_a_flag_in_the_wrong_place_opens_a_mine_and_loses()
    {
        var field = Field(0, 2);
        field.Open(1, new Random(0));
        field.ToggleFlag(0);
        field.ToggleFlag(9);

        field.OpenAround(1, new Random(0));

        Assert.Equal(GameOutcome.Lost, field.Outcome);
        Assert.Equal(2, field.Exploded);
        Assert.False(field.ToggleFlag(20));
        Assert.Empty(field.Open(20, new Random(0)));
    }

    [Fact]
    public void Harder_levels_hide_more_mines()
    {
        int[] mines = [.. new[] { GameLevel.Easy, GameLevel.Normal, GameLevel.Hard }.Select(Minesweeper.MinesFor)];
        Assert.Equal(mines.Order(), mines);
        Assert.Equal(mines.Length, mines.Distinct().Count());
        Assert.Throws<ArgumentOutOfRangeException>(() => new Minesweeper(Minesweeper.Size - 8));
    }

    /// <summary>A field with its mines laid where a test wants them.</summary>
    private static Minesweeper Field(params int[] mines)
    {
        var field = new Minesweeper(mines.Length);
        field.Lay(mines);
        return field;
    }
}
