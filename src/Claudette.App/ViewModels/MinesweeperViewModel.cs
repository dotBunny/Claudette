using System.Globalization;
using Claudette.App.Games;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary>
/// Minesweeper against Claudette (DESIGN.md Â§18, "Games with Claudette"): she hides her mines in a field of nine by
/// nine, and the user opens every square without one. A click opens a square, a right-click or F flags one, and a click
/// on a number with as many flags around it opens the rest around it. The level is how many mines she hides.
/// </summary>
public sealed class MinesweeperViewModel : GameViewModel
{
    private Minesweeper _field = new(Minesweeper.MinesFor(GameLevel.Normal));

    /// <summary>She's wiped her brow this round: one square was left to open.</summary>
    private bool _closeCall;

    public MinesweeperViewModel(IGameHost host) : base(host, GameKind.Minesweeper)
    {
        Squares = [.. Enumerable.Range(0, Minesweeper.Size).Select(i => new MinesweeperSquare(this, i))];
        NewRound();
    }

    public override string Title => "Minesweeper";

    public override IReadOnlyList<GameLevelChoice> Levels { get; } =
        [new(GameLevel.Easy, "Easy"), new(GameLevel.Normal, "Normal"), new(GameLevel.Hard, "Hard")];

    protected override bool HasDraws => false;

    /// <summary>The field's squares, row by row from the top left.</summary>
    public IReadOnlyList<MinesweeperSquare> Squares { get; }

    /// <summary>Where her mines are, for tests.</summary>
    internal Minesweeper Field => _field;

    protected override void StartRound(bool youStart)
    {
        // She hides her mines as the user opens the first square; who starts doesn't apply.
        _field = new Minesweeper(Minesweeper.MinesFor(CurrentLevel));
        _closeCall = false;
        Refresh();
        Status = $"Claudette hid {_field.Mines} mines. Open a square.";
    }

    /// <summary>Another level is another number of mines: a new field, the one in progress dropped uncounted.</summary>
    protected override void OnLevelChanged() => NewRound();

    /// <summary>
    /// The user opens <paramref name="square"/>, or on a number with its mines flagged, the rest around it. Nothing
    /// happens on a flag, or once the round is over.
    /// </summary>
    public void Open(int square)
    {
        var opened = _field.IsOpen(square) ? _field.OpenAround(square, Host.Random) : _field.Open(square, Host.Random);
        if (opened.Count == 0)
        {
            return;
        }
        Host.Moved();
        Refresh();
        switch (_field.Outcome)
        {
            case GameOutcome.Won:
                Finish(GameOutcome.Won, "You cleared the field: you win!");
                return;
            case GameOutcome.Lost:
                Finish(GameOutcome.Lost, "Boom! Claudette wins.");
                return;
        }
        if (_field.SafeLeft == 1 && !_closeCall)
        {
            _closeCall = true;
            Host.CloseCall();
        }
        Status = MinesLeft();
        Host.Announce(opened.Count == 1 ? Squares[opened[0]].Name : $"{opened.Count} squares opened.");
    }

    /// <summary>The user flags <paramref name="square"/>, or takes its flag away. Nothing happens on an open square.</summary>
    public void Flag(int square)
    {
        if (!_field.ToggleFlag(square))
        {
            return;
        }
        Host.Moved();
        Refresh();
        Status = MinesLeft();
        Host.Announce(Squares[square].Name);
    }

    /// <summary>The number keys and letters don't play Minesweeper: its squares take the keyboard themselves.</summary>
    public override bool Press(char key) => false;

    /// <summary>"7 mines left to flag": the mines less the flags, as the classic counter has it.</summary>
    private string MinesLeft() => (_field.Mines - _field.Flags) switch
    {
        1 => "1 mine left to flag.",
        < 0 => "More flags than mines.",
        var left => $"{left} mines left to flag.",
    };

    private void Refresh()
    {
        var (over, lost) = (_field.IsOver, _field.Outcome == GameOutcome.Lost);
        foreach (var square in Squares)
        {
            var at = square.Index;
            square.Count = _field.HasMines ? _field.MinesAround(at) : 0;
            square.State = (_field.IsOpen(at), _field.IsMine(at), _field.IsFlagged(at)) switch
            {
                (true, true, _) => MinesweeperSquareState.Exploded,
                (true, false, _) => MinesweeperSquareState.Open,
                // Once she's won, her mines show and a flag on none is marked wrong; once the user has, every mine is flagged.
                (_, true, false) when over => lost ? MinesweeperSquareState.Mine : MinesweeperSquareState.Flagged,
                (_, false, true) when lost => MinesweeperSquareState.WrongFlag,
                (_, _, true) => MinesweeperSquareState.Flagged,
                _ => MinesweeperSquareState.Shut,
            };
        }
    }
}

public enum MinesweeperSquareState
{
    Shut,
    Flagged,
    Open,
    /// <summary>One of her mines, shown once she's won.</summary>
    Mine,
    /// <summary>The mine the user opened.</summary>
    Exploded,
    /// <summary>A flag where there was no mine, shown once she's won.</summary>
    WrongFlag,
}

/// <summary>A square of the minefield: a click opens it, a right-click or F flags it.</summary>
public sealed partial class MinesweeperSquare(MinesweeperViewModel game, int index) : ObservableObject
{
    public int Index => index;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen), nameof(ShowsFlag), nameof(ShowsMine), nameof(IsExploded), nameof(IsWrongFlag), nameof(Text), nameof(What),
        nameof(Name))]
    public partial MinesweeperSquareState State { get; internal set; }

    /// <summary>How many mines are around it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text), nameof(What), nameof(Name))]
    public partial int Count { get; internal set; }

    /// <summary>Opened, the mine that lost too: it's drawn flat.</summary>
    public bool IsOpen => State is MinesweeperSquareState.Open or MinesweeperSquareState.Exploded;

    public bool ShowsFlag => State is MinesweeperSquareState.Flagged or MinesweeperSquareState.WrongFlag;

    public bool ShowsMine => State is MinesweeperSquareState.Mine or MinesweeperSquareState.Exploded;

    public bool IsExploded => State == MinesweeperSquareState.Exploded;

    public bool IsWrongFlag => State == MinesweeperSquareState.WrongFlag;

    /// <summary>The number on it once it's open, which its color follows; blank with no mines around it.</summary>
    public string Text => State == MinesweeperSquareState.Open && Count > 0 ? Count.ToString(CultureInfo.InvariantCulture) : "";

    /// <summary>What's on it: "2 mines around", "flagged".</summary>
    public string What => State switch
    {
        MinesweeperSquareState.Open => Count switch
        {
            0 => "empty",
            1 => "1 mine around",
            _ => $"{Count} mines around",
        },
        MinesweeperSquareState.Flagged => "flagged",
        MinesweeperSquareState.Mine => "a mine",
        MinesweeperSquareState.Exploded => "the mine you opened",
        MinesweeperSquareState.WrongFlag => "flagged, with no mine",
        _ => "not opened",
    };

    /// <summary>"Row 1, column 3, 2 mines around", for screen readers.</summary>
    public string Name => $"Row {index / Minesweeper.Columns + 1}, column {index % Minesweeper.Columns + 1}, {What}";

    [RelayCommand]
    private void Open() => game.Open(index);

    [RelayCommand]
    private void Flag() => game.Flag(index);
}
