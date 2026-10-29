namespace Claudette.App.Games;

/// <summary>The games against Claudette (DESIGN.md §18, "Games with Claudette").</summary>
public enum GameKind
{
    TicTacToe,
    ConnectFour,
    Hangman,
    Minesweeper,
}

/// <summary>
/// How well Claudette plays, or in Minesweeper, how many mines she hides. Tic-tac-toe calls the top level Unbeatable, the
/// others Hard.
/// </summary>
public enum GameLevel
{
    Easy,
    Normal,
    Hard,
}

/// <summary>Whose a square or a piece is.</summary>
public enum Player
{
    None,
    You,
    Claudette,
}

/// <summary>How a round stands, from the user's side.</summary>
public enum GameOutcome
{
    Playing,
    Won,
    Lost,
    Draw,
}

public static class Players
{
    /// <summary>The one who moves after <paramref name="player"/>.</summary>
    public static Player Other(this Player player) => player == Player.You ? Player.Claudette : Player.You;
}
