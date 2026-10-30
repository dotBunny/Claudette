using Claudette.App.Games;
using Claudette.App.ViewModels;
using Claudette.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Claudette.App.Services;

/// <summary>
/// Games with Claudette (DESIGN.md §18): the one game open, for the whole app, on whichever tab's Games page shows it.
/// Keeps each game's score and level on this machine, tells Claudette what happens, and says over the board when Claude
/// needs the user. Nothing here touches Claude Code.
/// </summary>
public sealed partial class GameService : ObservableObject, IGameHost, IDisposable
{
    private readonly AppServices _services;

    /// <summary>The last words hangman used, newest last, so they don't come up again soon.</summary>
    private readonly List<string> _recentWords = [];

    private Action? _answer;

    /// <summary>
    /// Until when the page takes the keyboard as it shows: a moment after a game is started, long enough for the page to
    /// be made, and not so long that showing it later, from another tab, takes the keyboard from the composer.
    /// </summary>
    private DateTimeOffset _focusUntil = DateTimeOffset.MinValue;

    private static readonly TimeSpan FocusWithin = TimeSpan.FromSeconds(1);

    public GameService(AppServices services) => _services = services;

    /// <summary>Settings → Appearance → <b>Games with Claudette</b>.</summary>
    public bool IsEnabled => _services.Settings.Appearance.GamesWithClaudette;

    /// <summary>The game open, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen), nameof(IsTicTacToe), nameof(IsConnectFour), nameof(IsHangman), nameof(IsMinesweeper))]
    public partial GameViewModel? Game { get; private set; }

    public bool IsOpen => Game is not null;

    public bool IsTicTacToe => Game?.Kind == GameKind.TicTacToe;

    public bool IsConnectFour => Game?.Kind == GameKind.ConnectFour;

    public bool IsHangman => Game?.Kind == GameKind.Hangman;

    public bool IsMinesweeper => Game?.Kind == GameKind.Minesweeper;

    /// <summary>A game opened or closed: the tabs' Games pages come or go.</summary>
    public event Action? OpenChanged;

    /// <summary>The shell's live region for screen readers.</summary>
    public Action<string>? Announcer { get; set; }

    /// <summary>
    /// Starts <paramref name="kind"/>, in place of another game, or goes back to it if it's the one open. Null while
    /// games are turned off.
    /// </summary>
    public GameViewModel? Start(GameKind kind)
    {
        if (!IsEnabled)
        {
            return null;
        }
        _focusUntil = _services.Time.GetUtcNow() + FocusWithin;
        if (Game is { } open && open.Kind == kind)
        {
            return open;
        }
        var old = Game;
        Game = kind switch
        {
            GameKind.ConnectFour => new ConnectFourViewModel(this),
            GameKind.Hangman => new HangmanViewModel(this),
            GameKind.Minesweeper => new MinesweeperViewModel(this),
            _ => new TicTacToeViewModel(this),
        };
        old?.Dispose();
        if (old is null)
        {
            OpenChanged?.Invoke();
        }
        return Game;
    }

    /// <summary>The game's menu in the window's header: another game, the one in progress dropped uncounted.</summary>
    [RelayCommand]
    private void Switch(GameKind kind) => Start(kind);

    /// <summary><b>Done playing</b>: the game ends, and the Games page goes.</summary>
    [RelayCommand]
    public void Close()
    {
        if (Game is not { } game)
        {
            return;
        }
        Game = null;
        game.Dispose();
        OpenChanged?.Invoke();
    }

    /// <summary>Whether the page should take the keyboard as it shows: once, just after a game is started.</summary>
    public bool TakeFocusRequest()
    {
        var wants = _services.Time.GetUtcNow() <= _focusUntil;
        _focusUntil = DateTimeOffset.MinValue;
        return wants;
    }

    /// <summary>Turning games off ends the one open.</summary>
    public void OnSettingsChanged()
    {
        OnPropertyChanged(nameof(IsEnabled));
        if (!IsEnabled)
        {
            Close();
        }
    }

    // ---- When Claude needs the user ------------------------------------------------------------------------------------

    /// <summary>"Claude needs you in auth refactor" over the board while a tab waits on the user; null while none does.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCall))]
    public partial string? Call { get; private set; }

    public bool HasCall => Call is not null;

    /// <summary>The call has <b>Go there</b>: the tab waiting isn't the one in front.</summary>
    [ObservableProperty]
    public partial bool CanAnswerCall { get; private set; }

    /// <summary>The shell says which tabs wait, and how to go to them.</summary>
    public void SetCall(string? call, Action? answer)
    {
        Call = call;
        _answer = call is null ? null : answer;
        CanAnswerCall = _answer is not null;
    }

    /// <summary><b>Go there</b>.</summary>
    [RelayCommand]
    private void AnswerCall() => _answer?.Invoke();

    // ---- What the games get --------------------------------------------------------------------------------------------

    public TimeProvider Time => _services.Time;

    public IUiDispatcher Dispatcher => _services.Dispatcher;

    // Read each time: tests set it after the services are made.
    public Random Random => _services.Random;

    private GamesState State => _services.State.Games;

    public GameScore Score(GameKind kind) => State.Scores.TryGetValue(kind.ToString(), out var score) ? score : new GameScore();

    public GameLevel Level(GameKind kind) =>
        State.Levels.TryGetValue(kind.ToString(), out var name) && Enum.TryParse<GameLevel>(name, out var level) && Enum.IsDefined(level)
            ? level
            : GameLevel.Normal;

    public void SetLevel(GameKind kind, GameLevel level)
    {
        State.Levels[kind.ToString()] = level.ToString();
        _services.SaveState();
    }

    public void ResetScore(GameKind kind)
    {
        if (State.Scores.Remove(kind.ToString()))
        {
            _services.SaveState();
        }
    }

    public HangmanWord NextWord()
    {
        var word = HangmanWords.Pick(Random, _recentWords);
        _recentWords.Add(word.Word);
        if (_recentWords.Count > HangmanWords.Recent)
        {
            _recentWords.RemoveAt(0);
        }
        return word;
    }

    public void Moved() => _services.Mascot.Nudge();

    public void Thinking() => _services.Mascot.GameThinking();

    public void CloseCall() => _services.Mascot.GameCloseCall();

    public void Finished(GameKind kind, GameOutcome outcome)
    {
        if (outcome == GameOutcome.Playing)
        {
            return;
        }
        var score = Score(kind);
        switch (outcome)
        {
            case GameOutcome.Won:
                score.Wins++;
                _services.Mascot.GameLost();
                break;
            case GameOutcome.Lost:
                score.Losses++;
                _services.Mascot.GameWon();
                break;
            default:
                score.Draws++;
                _services.Mascot.GameDrawn();
                break;
        }
        State.Scores[kind.ToString()] = score;
        _services.SaveState();
    }

    public void Announce(string text) => Announcer?.Invoke(text);

    public void Dispose()
    {
        Game?.Dispose();
        Game = null;
    }
}
