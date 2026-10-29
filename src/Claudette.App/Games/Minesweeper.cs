namespace Claudette.App.Games;

/// <summary>
/// A minefield (DESIGN.md §18, "Games with Claudette"): nine squares by nine, row by row from the top left, with
/// Claudette's mines under some. She hides them as the user opens the first square, never in it or next to it, so the
/// first square always opens onto some room. Opening every square without a mine wins; opening a mine loses.
/// </summary>
public sealed class Minesweeper
{
    public const int Columns = 9;
    public const int Rows = 9;
    public const int Size = Columns * Rows;

    private readonly bool[] _mines = new bool[Size];
    private readonly bool[] _open = new bool[Size];
    private readonly bool[] _flagged = new bool[Size];

    public Minesweeper(int mines)
    {
        if (mines is < 1 or > Size - 9)
        {
            throw new ArgumentOutOfRangeException(nameof(mines), mines, "A field has room for 1 to 72 mines.");
        }
        Mines = mines;
    }

    /// <summary>How many mines she hides at each level: about one square in eight, six and five.</summary>
    public static int MinesFor(GameLevel level) => level switch
    {
        GameLevel.Easy => 10,
        GameLevel.Hard => 16,
        _ => 13,
    };

    public int Mines { get; }

    /// <summary>She's hidden her mines: the user has opened a square.</summary>
    public bool HasMines { get; private set; }

    /// <summary>The mine the user opened, which lost the round.</summary>
    public int? Exploded { get; private set; }

    public int Flags => _flagged.Count(f => f);

    /// <summary>The squares without a mine still to open.</summary>
    public int SafeLeft { get; private set; } = Size;

    public GameOutcome Outcome => Exploded is not null ? GameOutcome.Lost : SafeLeft == 0 ? GameOutcome.Won : GameOutcome.Playing;

    public bool IsOver => Outcome != GameOutcome.Playing;

    public bool IsMine(int square) => _mines[square];

    public bool IsOpen(int square) => _open[square];

    public bool IsFlagged(int square) => _flagged[square];

    /// <summary>How many of the squares around <paramref name="square"/> have a mine.</summary>
    public int MinesAround(int square) => Around(square).Count(s => _mines[s]);

    /// <summary>How many of the squares around <paramref name="square"/> are flagged.</summary>
    public int FlagsAround(int square) => Around(square).Count(s => _flagged[s]);

    /// <summary>The up to eight squares touching <paramref name="square"/>.</summary>
    public static IEnumerable<int> Around(int square)
    {
        var (row, column) = (square / Columns, square % Columns);
        for (var r = Math.Max(row - 1, 0); r <= Math.Min(row + 1, Rows - 1); r++)
        {
            for (var c = Math.Max(column - 1, 0); c <= Math.Min(column + 1, Columns - 1); c++)
            {
                if (r != row || c != column)
                {
                    yield return r * Columns + c;
                }
            }
        }
    }

    /// <summary>Whether <paramref name="square"/> can be opened: it's on the field, shut and not flagged, and the round goes on.</summary>
    public bool CanOpen(int square) => square is >= 0 and < Size && !_open[square] && !_flagged[square] && !IsOver;

    /// <summary>
    /// Opens <paramref name="square"/>, hiding her mines first if it's the first; one with no mines around it opens those
    /// around it too, and so on. Returns the squares opened, none when it can't be.
    /// </summary>
    public IReadOnlyList<int> Open(int square, Random random)
    {
        if (!CanOpen(square))
        {
            return [];
        }
        if (!HasMines)
        {
            Hide(square, random);
        }
        if (_mines[square])
        {
            _open[square] = true;
            Exploded = square;
            return [square];
        }
        var opened = new List<int>();
        var next = new Stack<int>([square]);
        while (next.TryPop(out var at))
        {
            if (_open[at] || _flagged[at])
            {
                continue;
            }
            _open[at] = true;
            SafeLeft--;
            opened.Add(at);
            if (MinesAround(at) == 0)
            {
                foreach (var around in Around(at))
                {
                    next.Push(around);
                }
            }
        }
        return opened;
    }

    /// <summary>
    /// An open square with as many flags around it as mines: the rest around it open at once. Returns the squares opened,
    /// none when it can't; a flag in the wrong place opens a mine.
    /// </summary>
    public IReadOnlyList<int> OpenAround(int square, Random random)
    {
        if (IsOver || square is < 0 or >= Size || !_open[square])
        {
            return [];
        }
        var mines = MinesAround(square);
        if (mines == 0 || FlagsAround(square) != mines)
        {
            return [];
        }
        var opened = new List<int>();
        foreach (var around in Around(square))
        {
            opened.AddRange(Open(around, random));
        }
        return opened;
    }

    /// <summary>Flags a shut square, or takes its flag away. False when it can't: it's open, or the round is over.</summary>
    public bool ToggleFlag(int square)
    {
        if (square is < 0 or >= Size || _open[square] || IsOver)
        {
            return false;
        }
        _flagged[square] = !_flagged[square];
        return true;
    }

    /// <summary>
    /// Hides her mines anywhere but <paramref name="first"/> and the squares around it. There's always room: a field has
    /// at most 72, and at most nine squares are kept clear.
    /// </summary>
    private void Hide(int first, Random random)
    {
        var clear = Around(first).Append(first).ToHashSet();
        var places = Enumerable.Range(0, Size).Where(s => !clear.Contains(s)).ToArray();
        // The first few of a shuffle.
        for (var i = 0; i < Mines; i++)
        {
            var pick = i + random.Next(places.Length - i);
            (places[i], places[pick]) = (places[pick], places[i]);
            _mines[places[i]] = true;
        }
        SafeLeft = Size - Mines;
        HasMines = true;
    }

    /// <summary>Hides the mines in <paramref name="squares"/>, for tests: the first square opened may then be one.</summary>
    internal void Lay(IEnumerable<int> squares)
    {
        foreach (var square in squares)
        {
            _mines[square] = true;
        }
        SafeLeft = Size - _mines.Count(m => m);
        HasMines = true;
    }
}
