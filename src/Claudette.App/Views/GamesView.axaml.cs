using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Claudette.App.Services;
using Claudette.App.ViewModels;

namespace Claudette.App.Views;

/// <summary>
/// The Games page (DESIGN.md §18, "Games with Claudette"): what it needs beyond bindings, which is the keyboard and
/// Minesweeper's right-click. While the page has focus, the number keys play tic-tac-toe's squares and Connect Four's
/// columns, the letters guess in hangman, and F flags the Minesweeper square with the focus. It takes focus when a game
/// is started, so the keys reach it rather than the composer.
/// </summary>
public partial class GamesView : UserControl
{
    private GameService? _games;

    public GamesView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_games is not null)
        {
            _games.PropertyChanged -= OnGamesChanged;
        }
        _games = DataContext as GameService;
        if (_games is not null && this.IsAttachedToVisualTree())
        {
            _games.PropertyChanged += OnGamesChanged;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_games is not null)
        {
            _games.PropertyChanged -= OnGamesChanged;
            _games.PropertyChanged += OnGamesChanged;
        }
        FocusIfStarted();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_games is not null)
        {
            _games.PropertyChanged -= OnGamesChanged;
        }
    }

    private void OnGamesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameService.Game))
        {
            FocusIfStarted();
        }
    }

    /// <summary>A game was just started: the page takes the keyboard, once whatever started it (a menu) has let go.</summary>
    private void FocusIfStarted()
    {
        if (_games is { IsOpen: true } games && IsEffectivelyVisible && games.TakeFocusRequest())
        {
            Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Background);
        }
    }

    /// <summary>A right-click on one of Minesweeper's squares flags it.</summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed && MinesweeperSquareAt(e.Source) is { } square)
        {
            square.FlagCommand.Execute(null);
            e.Handled = true;
        }
    }

    private static MinesweeperSquare? MinesweeperSquareAt(object? source) =>
        (source as Visual)?.FindAncestorOfType<Button>(includeSelf: true)?.DataContext as MinesweeperSquare;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || (e.KeyModifiers & ~KeyModifiers.Shift) != KeyModifiers.None || _games?.Game is not { } game
            // The level's list has its own keys.
            || (e.Source as Visual)?.FindAncestorOfType<ComboBox>(includeSelf: true) is not null)
        {
            return;
        }
        // F flags the Minesweeper square with the focus.
        if (e.Key == Key.F && MinesweeperSquareAt(e.Source) is { } square)
        {
            square.FlagCommand.Execute(null);
            e.Handled = true;
            return;
        }
        var key = e.Key switch
        {
            >= Key.A and <= Key.Z => (char)('A' + (e.Key - Key.A)),
            >= Key.D1 and <= Key.D9 => (char)('1' + (e.Key - Key.D1)),
            >= Key.NumPad1 and <= Key.NumPad9 => (char)('1' + (e.Key - Key.NumPad1)),
            _ => '\0',
        };
        if (key != '\0' && game.Press(key))
        {
            e.Handled = true;
        }
    }
}
