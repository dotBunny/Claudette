using Claudette.App.Games;
using Claudette.App.Services;
using Claudette.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.ViewModels;

/// <summary>What a game against Claudette gets from <see cref="GameService"/> (DESIGN.md §18, "Games with Claudette").</summary>
public interface IGameHost
{
    TimeProvider Time { get; }

    IUiDispatcher Dispatcher { get; }

    Random Random { get; }

    /// <summary>The game's rounds so far, kept on this machine.</summary>
    GameScore Score(GameKind kind);

    GameLevel Level(GameKind kind);

    void SetLevel(GameKind kind, GameLevel level);

    void ResetScore(GameKind kind);

    /// <summary>A word for hangman that hasn't come up lately.</summary>
    HangmanWord NextWord();

    /// <summary>The user made a move: Claudette stays awake.</summary>
    void Moved();

    /// <summary>Claudette thinks about her move: a "?" over her head.</summary>
    void Thinking();

    /// <summary>A close call: she blocked a move that would have won, or the user has one miss left. She wipes her brow.</summary>
    void CloseCall();

    /// <summary>A round ended: it's counted, and she reacts.</summary>
    void Finished(GameKind kind, GameOutcome outcome);

    /// <summary>Has screen readers say <paramref name="text"/>.</summary>
    void Announce(string text);
}

/// <summary>A level in a game's list, named as that game names it.</summary>
public sealed record GameLevelChoice(GameLevel Level, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// A game against Claudette in the Games tool window (DESIGN.md §18, "Games with Claudette"): its rounds, who
/// starts each, Claudette's turn after a moment's thought on the injected clock, its level and its score.
/// </summary>
public abstract partial class GameViewModel : ObservableObject, IDisposable
{
    private readonly UiTimeout _herTurn;

    /// <summary>The user starts the next round: whoever didn't start the last one does.</summary>
    private bool _youStartNext = true;

    protected GameViewModel(IGameHost host, GameKind kind)
    {
        Host = host;
        Kind = kind;
        _herTurn = new UiTimeout(host.Time, host.Dispatcher);
    }

    /// <summary>How long she thinks before her move: a second or so.</summary>
    public static TimeSpan ThinkingTime(Random random) => TimeSpan.FromMilliseconds(800 + random.Next(500));

    /// <summary>The longest she thinks, for tests.</summary>
    public static readonly TimeSpan LongestThought = TimeSpan.FromMilliseconds(1300);

    protected IGameHost Host { get; }

    public GameKind Kind { get; }

    public abstract string Title { get; }

    /// <summary>What's happening: whose move it is, or how the round ended.</summary>
    [ObservableProperty]
    public partial string Status { get; protected set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOver), nameof(NewRoundText))]
    public partial GameOutcome Outcome { get; protected set; }

    public bool IsOver => Outcome != GameOutcome.Playing;

    /// <summary>Claudette is thinking about her move: the user's moves wait.</summary>
    [ObservableProperty]
    public partial bool IsHerTurn { get; private set; }

    /// <summary><b>Play again</b> once a round is over; <b>New game</b> during one, which drops it uncounted.</summary>
    public string NewRoundText => IsOver ? "Play again" : "New game";

    /// <summary>The levels to choose from; none for a game without.</summary>
    public virtual IReadOnlyList<GameLevelChoice> Levels => [];

    public bool HasLevels => Levels.Count > 0;

    public GameLevelChoice? Level
    {
        get => Levels.FirstOrDefault(l => l.Level == Host.Level(Kind));
        set
        {
            if (value is not null && value.Level != Host.Level(Kind))
            {
                Host.SetLevel(Kind, value.Level);
                OnPropertyChanged();
                OnLevelChanged();
            }
        }
    }

    protected GameLevel CurrentLevel => Host.Level(Kind);

    /// <summary>The user chose another level. Claudette plays it from her next move; a game that sets up by it starts again.</summary>
    protected virtual void OnLevelChanged()
    {
    }

    /// <summary>"You 3 · Claudette 2 · Draws 1"; null before the first round is over.</summary>
    public string? ScoreText
    {
        get
        {
            var score = Host.Score(Kind);
            if (score.Wins + score.Losses + score.Draws == 0)
            {
                return null;
            }
            var text = $"You {score.Wins} · Claudette {score.Losses}";
            return HasDraws ? $"{text} · Draws {score.Draws}" : text;
        }
    }

    public bool HasScore => ScoreText is not null;

    /// <summary>Whether a round can end in a draw, so the score counts them.</summary>
    protected virtual bool HasDraws => true;

    /// <summary>Starts a round: the first, <b>Play again</b>, or <b>New game</b> part-way through one, which isn't counted.</summary>
    [RelayCommand]
    public void NewRound()
    {
        _herTurn.Cancel();
        IsHerTurn = false;
        Outcome = GameOutcome.Playing;
        var youStart = _youStartNext;
        _youStartNext = !youStart;
        StartRound(youStart);
    }

    /// <summary><b>Reset score</b>: back to nothing.</summary>
    [RelayCommand]
    private void ResetScore()
    {
        Host.ResetScore(Kind);
        OnScoreChanged();
    }

    /// <summary>Sets the board up for a new round, with the user or Claudette to move first.</summary>
    protected abstract void StartRound(bool youStart);

    /// <summary>A key pressed while the page has focus: a number or a letter. True when the game took it.</summary>
    public abstract bool Press(char key);

    /// <summary>Claudette thinks for a moment, then <paramref name="move"/> makes her move.</summary>
    protected void HerTurn(Action move, string thinking = "Claudette is thinking…")
    {
        IsHerTurn = true;
        Status = thinking;
        Host.Thinking();
        _herTurn.Restart(ThinkingTime(Host.Random), () =>
        {
            IsHerTurn = false;
            move();
        });
    }

    /// <summary>The round is over: it's counted, she reacts, and screen readers hear <paramref name="status"/>.</summary>
    protected void Finish(GameOutcome outcome, string status)
    {
        Outcome = outcome;
        Status = status;
        Host.Finished(Kind, outcome);
        Host.Announce(status);
        OnScoreChanged();
    }

    private void OnScoreChanged()
    {
        OnPropertyChanged(nameof(ScoreText));
        OnPropertyChanged(nameof(HasScore));
    }

    public void Dispose()
    {
        _herTurn.Dispose();
        GC.SuppressFinalize(this);
    }
}
