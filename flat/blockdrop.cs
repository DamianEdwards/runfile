#!/usr/bin/env dotnet
#:package Hex1b@0.165.0
#:package System.CommandLine@2.0.2

using System.Diagnostics;
using System.CommandLine;
using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

var levelOption = new Option<int>("--level", "-l")
{
    Description = "Starting level from 1 through 20.",
    DefaultValueFactory = _ => 1,
};
levelOption.Validators.Add(result =>
{
    var level = result.GetValueOrDefault<int>();
    if (level is < 1 or > BlockdropGame.MaxLevel)
    {
        result.AddError("--level must be a whole number from 1 through 20.");
    }
});

var rootCommand = new RootCommand(
    """
    Blockdrop - a signal-bright falling block game powered by Hex1b.

    Controls:
      A/D or Left/Right   Move
      S or Down           Soft drop
      W or Up             Rotate clockwise
      Ctrl/Alt + A/Left   Rotate counter-clockwise
      Ctrl/Alt + D/Right  Rotate clockwise
      Space               Hard drop
      P                   Pause
      R                   Restart
      Q or Escape         Quit
    """);
rootCommand.Options.Add(levelOption);
rootCommand.SetAction((parseResult, cancellationToken) =>
    RunGameAsync(parseResult.GetValue(levelOption), cancellationToken));

return await rootCommand.Parse(args).InvokeAsync();

static async Task<int> RunGameAsync(int level, CancellationToken cancellationToken)
{
    var game = new BlockdropGame(level);
    var clock = Stopwatch.StartNew();
    var previousFrame = clock.Elapsed;

    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithHex1bApp(
            appOptions => appOptions.FrameRateLimitMs = 16,
            app => context =>
            {
                var now = clock.Elapsed;
                game.Advance(now - previousFrame);
                previousFrame = now;

                return context
                    .Surface(layer => [
                        layer.Layer(surface => BlockdropRenderer.Draw(surface, game))
                    ])
                    .RedrawAfter(game.IsRunning ? 33 : 150)
                    .InputBindings(bindings => BlockdropBindings.Configure(bindings, game, app));
            })
        .Build();

    await terminal.RunAsync(cancellationToken);
    return 0;
}

static class BlockdropBindings
{
    public static void Configure(InputBindingsBuilder bindings, BlockdropGame game, Hex1bApp app)
    {
        Bind(bindings.Key(Hex1bKey.A), () => game.MoveHorizontal(-1), "Move left");
        Bind(bindings.Key(Hex1bKey.LeftArrow), () => game.MoveHorizontal(-1), "Move left");
        Bind(bindings.Key(Hex1bKey.D), () => game.MoveHorizontal(1), "Move right");
        Bind(bindings.Key(Hex1bKey.RightArrow), () => game.MoveHorizontal(1), "Move right");
        Bind(bindings.Key(Hex1bKey.S), game.SoftDrop, "Soft drop");
        Bind(bindings.Key(Hex1bKey.DownArrow), game.SoftDrop, "Soft drop");
        Bind(bindings.Key(Hex1bKey.W), () => game.Rotate(1), "Rotate clockwise");
        Bind(bindings.Key(Hex1bKey.UpArrow), () => game.Rotate(1), "Rotate clockwise");
        Bind(bindings.Key(Hex1bKey.Spacebar), game.HardDrop, "Hard drop");

        Bind(bindings.Ctrl().Key(Hex1bKey.A), () => game.Rotate(-1), "Rotate counter-clockwise");
        Bind(bindings.Ctrl().Key(Hex1bKey.LeftArrow), () => game.Rotate(-1), "Rotate counter-clockwise");
        Bind(bindings.Ctrl().Key(Hex1bKey.D), () => game.Rotate(1), "Rotate clockwise");
        Bind(bindings.Ctrl().Key(Hex1bKey.RightArrow), () => game.Rotate(1), "Rotate clockwise");
        Bind(bindings.Alt().Key(Hex1bKey.A), () => game.Rotate(-1), "Rotate counter-clockwise");
        Bind(bindings.Alt().Key(Hex1bKey.LeftArrow), () => game.Rotate(-1), "Rotate counter-clockwise");
        Bind(bindings.Alt().Key(Hex1bKey.D), () => game.Rotate(1), "Rotate clockwise");
        Bind(bindings.Alt().Key(Hex1bKey.RightArrow), () => game.Rotate(1), "Rotate clockwise");

        Bind(bindings.Ctrl().Key(Hex1bKey.Z), () => game.Rotate(-1), "Rotate counter-clockwise");
        Bind(bindings.Ctrl().Key(Hex1bKey.X), () => game.Rotate(1), "Rotate clockwise");
        Bind(bindings.Alt().Key(Hex1bKey.Z), () => game.Rotate(-1), "Rotate counter-clockwise");
        Bind(bindings.Alt().Key(Hex1bKey.X), () => game.Rotate(1), "Rotate clockwise");

        Bind(bindings.Key(Hex1bKey.P), game.TogglePause, "Pause");
        Bind(bindings.Key(Hex1bKey.R), game.Restart, "Restart");
        bindings.Key(Hex1bKey.Q).Global().Action(app.RequestStop, "Quit");
        bindings.Key(Hex1bKey.Escape).Global().Action(app.RequestStop, "Quit");

        void Bind(KeyStepBuilder builder, Action action, string description)
        {
            builder.Global().Action(() =>
            {
                action();
                app.Invalidate();
            }, description);
        }
    }
}

enum Tetromino
{
    I = 1,
    J,
    L,
    O,
    S,
    T,
    Z,
}

enum BlockdropState
{
    Playing,
    Paused,
    GameOver,
}

readonly record struct GridPoint(int X, int Y)
{
    public static implicit operator GridPoint((int X, int Y) point)
        => new(point.X, point.Y);
}

readonly record struct FallingPiece(Tetromino Kind, int Rotation, int X, int Y);

sealed class BlockdropGame
{
    public const int BoardWidth = 10;
    public const int BoardHeight = 20;
    public const int MaxLevel = 20;

    private static readonly GridPoint[][][] Shapes =
    [
        [],
        [
            [(0, 1), (1, 1), (2, 1), (3, 1)],
            [(2, 0), (2, 1), (2, 2), (2, 3)],
            [(0, 2), (1, 2), (2, 2), (3, 2)],
            [(1, 0), (1, 1), (1, 2), (1, 3)],
        ],
        [
            [(0, 0), (0, 1), (1, 1), (2, 1)],
            [(1, 0), (2, 0), (1, 1), (1, 2)],
            [(0, 1), (1, 1), (2, 1), (2, 2)],
            [(1, 0), (1, 1), (0, 2), (1, 2)],
        ],
        [
            [(2, 0), (0, 1), (1, 1), (2, 1)],
            [(1, 0), (1, 1), (1, 2), (2, 2)],
            [(0, 1), (1, 1), (2, 1), (0, 2)],
            [(0, 0), (1, 0), (1, 1), (1, 2)],
        ],
        [
            [(1, 0), (2, 0), (1, 1), (2, 1)],
            [(1, 0), (2, 0), (1, 1), (2, 1)],
            [(1, 0), (2, 0), (1, 1), (2, 1)],
            [(1, 0), (2, 0), (1, 1), (2, 1)],
        ],
        [
            [(1, 0), (2, 0), (0, 1), (1, 1)],
            [(1, 0), (1, 1), (2, 1), (2, 2)],
            [(1, 1), (2, 1), (0, 2), (1, 2)],
            [(0, 0), (0, 1), (1, 1), (1, 2)],
        ],
        [
            [(1, 0), (0, 1), (1, 1), (2, 1)],
            [(1, 0), (1, 1), (2, 1), (1, 2)],
            [(0, 1), (1, 1), (2, 1), (1, 2)],
            [(1, 0), (0, 1), (1, 1), (1, 2)],
        ],
        [
            [(0, 0), (1, 0), (1, 1), (2, 1)],
            [(2, 0), (1, 1), (2, 1), (1, 2)],
            [(0, 1), (1, 1), (1, 2), (2, 2)],
            [(1, 0), (0, 1), (1, 1), (0, 2)],
        ],
    ];

    private static readonly GridPoint[] KickTests =
    [
        (0, 0),
        (-1, 0),
        (1, 0),
        (-2, 0),
        (2, 0),
        (0, -1),
        (-1, -1),
        (1, -1),
        (0, -2),
    ];

    private readonly Random _random = new();
    private readonly Queue<Tetromino> _next = new();
    private int[] _flashingRows = [];
    private double _gravityAccumulatorMilliseconds;
    private double _lineFlashRemainingMilliseconds;

    private const double LineFlashDurationMilliseconds = 225;
    private const double LineFlashFrameMilliseconds = 45;

    public BlockdropGame(int startingLevel)
    {
        StartingLevel = startingLevel;
        Restart();
    }

    public int[,] Board { get; } = new int[BoardHeight, BoardWidth];

    public int StartingLevel { get; }

    public FallingPiece Current { get; private set; }

    public Tetromino NextKind => _next.Peek();

    public long Score { get; private set; }

    public int Lines { get; private set; }

    public int Level => Math.Min(MaxLevel, StartingLevel + Lines / 10);

    public int LinesUntilNextLevel => Level == MaxLevel ? 0 : 10 - Lines % 10;

    public int LevelProgress => Level == MaxLevel ? 10 : Lines % 10;

    public BlockdropState State { get; private set; }

    public bool IsRunning => State == BlockdropState.Playing;

    public string StatusMessage { get; private set; } = "CHANNEL OPEN";

    public IReadOnlyList<int> FlashingRows => _flashingRows;

    public bool IsLineFlashVisible =>
        _lineFlashRemainingMilliseconds > 0 &&
        LineFlashFrame % 2 == 0;

    public int LineFlashFrame =>
        (int)((LineFlashDurationMilliseconds - _lineFlashRemainingMilliseconds) /
              LineFlashFrameMilliseconds);

    public int DropDistance
    {
        get
        {
            var distance = 0;
            while (CanPlace(Current with { Y = Current.Y + distance + 1 }))
            {
                distance++;
            }

            return distance;
        }
    }

    public void Restart()
    {
        Array.Clear(Board);
        _next.Clear();
        _flashingRows = [];
        _gravityAccumulatorMilliseconds = 0;
        _lineFlashRemainingMilliseconds = 0;
        Score = 0;
        Lines = 0;
        State = BlockdropState.Playing;
        StatusMessage = $"LEVEL {StartingLevel:00} LINKED";
        FillQueue();
        SpawnNextPiece();
    }

    public void Advance(TimeSpan elapsed)
    {
        var elapsedMilliseconds = Math.Min(250, elapsed.TotalMilliseconds);
        if (_lineFlashRemainingMilliseconds > 0)
        {
            _lineFlashRemainingMilliseconds = Math.Max(
                0,
                _lineFlashRemainingMilliseconds - elapsedMilliseconds);

            if (_lineFlashRemainingMilliseconds == 0)
            {
                _flashingRows = [];
            }
        }

        if (State != BlockdropState.Playing)
        {
            return;
        }

        _gravityAccumulatorMilliseconds += elapsedMilliseconds;
        var gravity = GravityMilliseconds(Level);

        while (_gravityAccumulatorMilliseconds >= gravity)
        {
            _gravityAccumulatorMilliseconds -= gravity;

            if (!TryMove(0, 1))
            {
                LockCurrentPiece();
                break;
            }
        }
    }

    public void MoveHorizontal(int direction)
    {
        if (State == BlockdropState.Playing)
        {
            TryMove(direction, 0);
        }
    }

    public void SoftDrop()
    {
        if (State != BlockdropState.Playing)
        {
            return;
        }

        if (TryMove(0, 1))
        {
            Score++;
            _gravityAccumulatorMilliseconds = 0;
        }
        else
        {
            LockCurrentPiece();
        }
    }

    public void HardDrop()
    {
        if (State != BlockdropState.Playing)
        {
            return;
        }

        var distance = DropDistance;
        Current = Current with { Y = Current.Y + distance };
        Score += distance * 2L;
        StatusMessage = distance == 0 ? "FAST LOCK" : $"BURST DROP +{distance * 2}";
        LockCurrentPiece();
    }

    public void Rotate(int direction)
    {
        if (State != BlockdropState.Playing || Current.Kind == Tetromino.O)
        {
            return;
        }

        var rotation = (Current.Rotation + direction + 4) % 4;

        foreach (var kick in KickTests)
        {
            var candidate = Current with
            {
                Rotation = rotation,
                X = Current.X + kick.X,
                Y = Current.Y + kick.Y,
            };

            if (CanPlace(candidate))
            {
                Current = candidate;
                StatusMessage = direction < 0 ? "PHASE -90" : "PHASE +90";
                return;
            }
        }
    }

    public void TogglePause()
    {
        if (State == BlockdropState.GameOver)
        {
            return;
        }

        State = State == BlockdropState.Paused
            ? BlockdropState.Playing
            : BlockdropState.Paused;
        StatusMessage = State == BlockdropState.Paused ? "FIELD SUSPENDED" : "CHANNEL OPEN";
        _gravityAccumulatorMilliseconds = 0;
    }

    public IEnumerable<GridPoint> Cells(FallingPiece piece)
    {
        foreach (var point in Shapes[(int)piece.Kind][piece.Rotation])
        {
            yield return new GridPoint(piece.X + point.X, piece.Y + point.Y);
        }
    }

    public IEnumerable<GridPoint> PreviewCells(Tetromino kind)
        => Shapes[(int)kind][0];

    private bool TryMove(int deltaX, int deltaY)
    {
        var candidate = Current with
        {
            X = Current.X + deltaX,
            Y = Current.Y + deltaY,
        };

        if (!CanPlace(candidate))
        {
            return false;
        }

        Current = candidate;
        return true;
    }

    private bool CanPlace(FallingPiece piece)
    {
        foreach (var cell in Cells(piece))
        {
            if (cell.X < 0 || cell.X >= BoardWidth || cell.Y >= BoardHeight)
            {
                return false;
            }

            if (cell.Y >= 0 && Board[cell.Y, cell.X] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private void LockCurrentPiece()
    {
        var lockedAboveField = false;

        foreach (var cell in Cells(Current))
        {
            if (cell.Y < 0)
            {
                lockedAboveField = true;
                continue;
            }

            Board[cell.Y, cell.X] = (int)Current.Kind;
        }

        if (lockedAboveField)
        {
            EndGame();
            return;
        }

        var clearedRows = ClearCompletedLines();
        var cleared = clearedRows.Length;
        if (cleared > 0)
        {
            BeginLineFlash(clearedRows);

            var points = cleared switch
            {
                1 => 100,
                2 => 300,
                3 => 500,
                _ => 800,
            };

            Score += points * (long)Level;
            Lines += cleared;
            StatusMessage = cleared switch
            {
                1 => $"SINGLE PULSE +{points * Level}",
                2 => $"DOUBLE PULSE +{points * Level}",
                3 => $"TRIPLE PULSE +{points * Level}",
                _ => $"QUAD BURST +{points * Level}",
            };
        }

        _gravityAccumulatorMilliseconds = 0;
        SpawnNextPiece();
    }

    private int[] ClearCompletedLines()
    {
        var writeRow = BoardHeight - 1;
        var clearedRows = new List<int>(4);

        for (var readRow = BoardHeight - 1; readRow >= 0; readRow--)
        {
            var complete = true;
            for (var x = 0; x < BoardWidth; x++)
            {
                if (Board[readRow, x] == 0)
                {
                    complete = false;
                    break;
                }
            }

            if (complete)
            {
                clearedRows.Add(readRow);
                continue;
            }

            if (writeRow != readRow)
            {
                for (var x = 0; x < BoardWidth; x++)
                {
                    Board[writeRow, x] = Board[readRow, x];
                }
            }

            writeRow--;
        }

        while (writeRow >= 0)
        {
            for (var x = 0; x < BoardWidth; x++)
            {
                Board[writeRow, x] = 0;
            }

            writeRow--;
        }

        return [.. clearedRows];
    }

    private void BeginLineFlash(int[] rows)
    {
        _flashingRows = rows;
        _lineFlashRemainingMilliseconds = LineFlashDurationMilliseconds;
    }

    private void SpawnNextPiece()
    {
        FillQueue();
        var kind = _next.Dequeue();
        FillQueue();
        Current = new FallingPiece(kind, 0, 3, -1);

        if (!CanPlace(Current))
        {
            EndGame();
        }
    }

    private void FillQueue()
    {
        while (_next.Count < 7)
        {
            var bag = Enum.GetValues<Tetromino>();
            for (var index = bag.Length - 1; index > 0; index--)
            {
                var swapIndex = _random.Next(index + 1);
                (bag[index], bag[swapIndex]) = (bag[swapIndex], bag[index]);
            }

            foreach (var kind in bag)
            {
                _next.Enqueue(kind);
            }
        }
    }

    private void EndGame()
    {
        State = BlockdropState.GameOver;
        StatusMessage = "SIGNAL LOST";
    }

    private static double GravityMilliseconds(int level)
        => Math.Max(48, 850 - (level - 1) * 42);
}

static class BlockdropRenderer
{
    private static readonly Hex1bColor Backdrop = Hex1bColor.FromRgb(5, 8, 18);
    private static readonly Hex1bColor BackdropAccent = Hex1bColor.FromRgb(15, 22, 39);
    private static readonly Hex1bColor Panel = Hex1bColor.FromRgb(11, 17, 31);
    private static readonly Hex1bColor PanelStrong = Hex1bColor.FromRgb(17, 27, 46);
    private static readonly Hex1bColor GridA = Hex1bColor.FromRgb(8, 14, 25);
    private static readonly Hex1bColor GridB = Hex1bColor.FromRgb(10, 17, 29);
    private static readonly Hex1bColor Text = Hex1bColor.FromRgb(207, 223, 235);
    private static readonly Hex1bColor Muted = Hex1bColor.FromRgb(101, 124, 148);
    private static readonly Hex1bColor Cyan = Hex1bColor.FromRgb(69, 226, 255);
    private static readonly Hex1bColor Pink = Hex1bColor.FromRgb(255, 83, 181);
    private static readonly Hex1bColor Lime = Hex1bColor.FromRgb(151, 255, 132);
    private static readonly Hex1bColor Warning = Hex1bColor.FromRgb(255, 203, 92);
    private static readonly Hex1bColor Danger = Hex1bColor.FromRgb(255, 91, 105);

    private static readonly Hex1bColor[] PieceColors =
    [
        Muted,
        Hex1bColor.FromRgb(54, 218, 255),
        Hex1bColor.FromRgb(91, 113, 255),
        Hex1bColor.FromRgb(255, 153, 71),
        Hex1bColor.FromRgb(255, 218, 92),
        Hex1bColor.FromRgb(105, 235, 139),
        Hex1bColor.FromRgb(207, 101, 255),
        Hex1bColor.FromRgb(255, 86, 122),
    ];

    private static readonly Hex1bColor[] PieceHighlights =
    [
        Text,
        Hex1bColor.FromRgb(154, 244, 255),
        Hex1bColor.FromRgb(171, 182, 255),
        Hex1bColor.FromRgb(255, 213, 171),
        Hex1bColor.FromRgb(255, 241, 170),
        Hex1bColor.FromRgb(184, 255, 201),
        Hex1bColor.FromRgb(235, 186, 255),
        Hex1bColor.FromRgb(255, 178, 195),
    ];

    public static void Draw(Surface surface, BlockdropGame game)
    {
        surface.Clear(new SurfaceCell(" ", null, Backdrop));
        DrawBackdrop(surface);

        if (surface.Width < 14 || surface.Height < 22)
        {
            DrawTooSmall(surface);
            return;
        }

        var blockWidth = surface.Width >= 22 ? 2 : 1;
        var boardWidth = BlockdropGame.BoardWidth * blockWidth + 2;
        const int boardHeight = BlockdropGame.BoardHeight + 2;
        var aspectRatio = surface.Width / (double)surface.Height;

        if (surface.Width >= 76 && surface.Height >= 25 && aspectRatio >= 2.6)
        {
            DrawWideLayout(surface, game, boardWidth, boardHeight, blockWidth);
        }
        else if (surface.Width >= boardWidth + 22 &&
                 surface.Height >= boardHeight &&
                 aspectRatio >= 1.3)
        {
            DrawSplitLayout(surface, game, boardWidth, boardHeight, blockWidth);
        }
        else if (surface.Width >= boardWidth && surface.Height >= boardHeight + 7)
        {
            DrawTallLayout(surface, game, boardWidth, boardHeight, blockWidth);
        }
        else if (surface.Width >= boardWidth && surface.Height >= boardHeight)
        {
            DrawBoardOnlyLayout(surface, game, boardWidth, boardHeight, blockWidth);
        }
        else
        {
            DrawTooSmall(surface);
            return;
        }

        if (game.State != BlockdropState.Playing)
        {
            DrawStateOverlay(surface, game);
        }
    }

    private static void DrawWideLayout(
        Surface surface,
        BlockdropGame game,
        int boardWidth,
        int boardHeight,
        int blockWidth)
    {
        var boardX = (surface.Width - boardWidth) / 2;
        var boardY = Math.Max(2, (surface.Height - boardHeight) / 2);
        var leftWidth = Math.Min(20, boardX - 3);
        var leftX = boardX - leftWidth - 2;
        var rightX = boardX + boardWidth + 2;
        var rightWidth = Math.Min(24, surface.Width - rightX - 2);

        DrawBrand(surface, boardY - 2);
        DrawBoard(surface, game, boardX, boardY, blockWidth);
        DrawStatsPanel(surface, game, leftX, boardY + 1, leftWidth, 12);
        DrawSignalPanel(surface, game, leftX, boardY + 14, leftWidth, Math.Min(7, boardHeight - 14));
        DrawNextPanel(surface, game, rightX, boardY + 1, rightWidth, 8);
        DrawControlsPanel(surface, rightX, boardY + 10, rightWidth, boardHeight - 10);
    }

    private static void DrawSplitLayout(
        Surface surface,
        BlockdropGame game,
        int boardWidth,
        int boardHeight,
        int blockWidth)
    {
        var sideWidth = Math.Min(28, Math.Max(20, surface.Width - boardWidth - 4));
        var totalWidth = boardWidth + 2 + sideWidth;
        var startX = Math.Max(0, (surface.Width - totalWidth) / 2);
        var boardY = Math.Max(0, (surface.Height - boardHeight) / 2);
        var sideX = startX + boardWidth + 2;

        DrawBoard(surface, game, startX, boardY, blockWidth);
        DrawStatsPanel(surface, game, sideX, boardY, sideWidth, 8);
        DrawNextPanel(surface, game, sideX, boardY + 9, sideWidth, 7);

        var controlsHeight = boardHeight - 17;
        if (controlsHeight >= 4)
        {
            DrawControlsPanel(surface, sideX, boardY + 17, sideWidth, controlsHeight);
        }
    }

    private static void DrawTallLayout(
        Surface surface,
        BlockdropGame game,
        int boardWidth,
        int boardHeight,
        int blockWidth)
    {
        var boardX = (surface.Width - boardWidth) / 2;
        var boardY = Math.Max(5, (surface.Height - boardHeight) / 2);

        DrawTallHeader(surface, game, boardX, Math.Max(0, boardY - 5), boardWidth, 5);
        DrawBoard(surface, game, boardX, boardY, blockWidth);

        var footerY = boardY + boardHeight + 1;
        if (footerY < surface.Height)
        {
            CenterText(surface, footerY, "A/D MOVE  W ROTATE  SPACE DROP", Muted, CellAttributes.Dim);
        }
    }

    private static void DrawBoardOnlyLayout(
        Surface surface,
        BlockdropGame game,
        int boardWidth,
        int boardHeight,
        int blockWidth)
    {
        var boardX = (surface.Width - boardWidth) / 2;
        var boardY = (surface.Height - boardHeight) / 2;
        DrawBoard(surface, game, boardX, boardY, blockWidth);

        var score = $" {game.Score:N0} ";
        Write(surface, boardX + Math.Max(1, boardWidth - score.Length - 1), boardY, score, Warning, PanelStrong, CellAttributes.Bold);
    }

    private static void DrawBrand(Surface surface, int y)
    {
        if (y < 0)
        {
            return;
        }

        CenterText(surface, y, "BLOCKDROP // SIGNAL GRID", Cyan, CellAttributes.Bold);
    }

    private static void DrawStatsPanel(
        Surface surface,
        BlockdropGame game,
        int x,
        int y,
        int width,
        int height)
    {
        if (width < 10 || height < 4)
        {
            return;
        }

        DrawPanel(surface, x, y, width, height, "RUN DATA", Cyan);
        var row = y + 2;
        WriteClipped(surface, x + 2, row++, width - 4, $"SCORE  {game.Score:N0}", Text, Panel, CellAttributes.Bold);

        if (row < y + height - 1)
        {
            WriteClipped(surface, x + 2, row++, width - 4, $"LEVEL  {game.Level:00}", Warning, PanelStrong, CellAttributes.Bold);
        }

        if (row < y + height - 1)
        {
            WriteClipped(surface, x + 2, row++, width - 4, $"LINES  {game.Lines:000}", Lime, Panel);
        }

        if (row < y + height - 1)
        {
            var nextLevel = game.Level == BlockdropGame.MaxLevel
                ? "MAX VELOCITY"
                : $"{game.LinesUntilNextLevel} TO LEVEL";
            WriteClipped(surface, x + 2, row++, width - 4, nextLevel, Muted, Panel, CellAttributes.Dim);
        }

        if (height >= 9)
        {
            DrawMeter(surface, x + 2, y + height - 3, width - 4, game.LevelProgress, 10);
            WriteClipped(surface, x + 2, y + height - 2, width - 4, $"START {game.StartingLevel:00}", Muted, Panel);
        }
    }

    private static void DrawSignalPanel(
        Surface surface,
        BlockdropGame game,
        int x,
        int y,
        int width,
        int height)
    {
        if (width < 10 || height < 4)
        {
            return;
        }

        DrawPanel(surface, x, y, width, height, "LINK", Pink);
        WriteClipped(surface, x + 2, y + 2, width - 4, game.StatusMessage, StatusColor(game), PanelStrong, CellAttributes.Bold);

        if (height >= 5)
        {
            WriteClipped(surface, x + 2, y + 3, width - 4, "P PAUSE / R RESET", Muted, Panel, CellAttributes.Dim);
        }
    }

    private static void DrawNextPanel(
        Surface surface,
        BlockdropGame game,
        int x,
        int y,
        int width,
        int height)
    {
        if (width < 10 || height < 5)
        {
            return;
        }

        DrawPanel(surface, x, y, width, height, "NEXT SIGNAL", Pink);
        var scale = width >= 14 ? 2 : 1;
        var previewWidth = 4 * scale;
        var originX = x + Math.Max(2, (width - previewWidth) / 2);
        var originY = y + Math.Max(2, (height - 2) / 2);

        foreach (var cell in game.PreviewCells(game.NextKind))
        {
            DrawBlock(surface, originX + cell.X * scale, originY + cell.Y, scale, game.NextKind);
        }
    }

    private static void DrawControlsPanel(
        Surface surface,
        int x,
        int y,
        int width,
        int height)
    {
        if (width < 12 || height < 4)
        {
            return;
        }

        DrawPanel(surface, x, y, width, height, "CONTROL BUS", Lime);
        var lines = new[]
        {
            "A/D  or arrows",
            "S/DOWN soft drop",
            "W/UP   rotate",
            "CTRL/ALT + A/D",
            "SPACE   burst drop",
            "P pause  R reboot",
            "Q/ESC   disconnect",
        };

        for (var index = 0; index < lines.Length && y + 2 + index < y + height - 1; index++)
        {
            var color = index is 3 or 4 ? Cyan : Muted;
            WriteClipped(surface, x + 2, y + 2 + index, width - 4, lines[index], color, Panel);
        }
    }

    private static void DrawTallHeader(
        Surface surface,
        BlockdropGame game,
        int x,
        int y,
        int width,
        int height)
    {
        DrawPanel(surface, x, y, width, height, "BLOCKDROP", Cyan);
        WriteClipped(surface, x + 2, y + 2, width - 4, $"S {game.Score:N0}  L {game.Level:00}  R {game.Lines:000}", Text, Panel, CellAttributes.Bold);

        var nextLabel = $"NEXT {game.NextKind}";
        WriteClipped(surface, x + Math.Max(2, width - nextLabel.Length - 2), y + 3, Math.Max(0, width - 4), nextLabel, Pink, PanelStrong);
    }

    private static void DrawBoard(
        Surface surface,
        BlockdropGame game,
        int x,
        int y,
        int blockWidth)
    {
        var width = BlockdropGame.BoardWidth * blockWidth + 2;
        var height = BlockdropGame.BoardHeight + 2;
        DrawPanel(surface, x, y, width, height, "DROP FIELD", Cyan);

        for (var boardY = 0; boardY < BlockdropGame.BoardHeight; boardY++)
        {
            for (var boardX = 0; boardX < BlockdropGame.BoardWidth; boardX++)
            {
                DrawEmptyCell(surface, x + 1 + boardX * blockWidth, y + 1 + boardY, blockWidth, boardX, boardY);
            }
        }

        var ghost = game.Current with { Y = game.Current.Y + game.DropDistance };
        if (ghost.Y != game.Current.Y)
        {
            foreach (var cell in game.Cells(ghost))
            {
                if (cell.Y >= 0)
                {
                    DrawGhost(surface, x + 1 + cell.X * blockWidth, y + 1 + cell.Y, blockWidth, ghost.Kind);
                }
            }
        }

        for (var boardY = 0; boardY < BlockdropGame.BoardHeight; boardY++)
        {
            for (var boardX = 0; boardX < BlockdropGame.BoardWidth; boardX++)
            {
                var value = game.Board[boardY, boardX];
                if (value != 0)
                {
                    DrawBlock(surface, x + 1 + boardX * blockWidth, y + 1 + boardY, blockWidth, (Tetromino)value);
                }
            }
        }

        foreach (var cell in game.Cells(game.Current))
        {
            if (cell.Y >= 0)
            {
                DrawBlock(surface, x + 1 + cell.X * blockWidth, y + 1 + cell.Y, blockWidth, game.Current.Kind);
            }
        }

        if (game.IsLineFlashVisible)
        {
            DrawLineFlash(surface, game, x + 1, y + 1, blockWidth);
        }
    }

    private static void DrawEmptyCell(Surface surface, int x, int y, int width, int boardX, int boardY)
    {
        var background = (boardX + boardY) % 2 == 0 ? GridA : GridB;
        for (var offset = 0; offset < width; offset++)
        {
            Put(surface, x + offset, y, " ", Muted, background);
        }
    }

    private static void DrawBlock(Surface surface, int x, int y, int width, Tetromino kind)
    {
        var color = PieceColors[(int)kind];
        var highlight = PieceHighlights[(int)kind];

        if (width == 1)
        {
            Put(surface, x, y, "#", highlight, color, CellAttributes.Bold);
            return;
        }

        Put(surface, x, y, " ", null, highlight);
        for (var offset = 1; offset < width; offset++)
        {
            Put(surface, x + offset, y, " ", null, color);
        }
    }

    private static void DrawGhost(Surface surface, int x, int y, int width, Tetromino kind)
    {
        for (var offset = 0; offset < width; offset++)
        {
            var existing = surface.IsInBounds(x + offset, y) ? surface[x + offset, y] : default;
            Put(surface, x + offset, y, ".", PieceColors[(int)kind], existing.Background, CellAttributes.Dim);
        }
    }

    private static void DrawLineFlash(
        Surface surface,
        BlockdropGame game,
        int boardX,
        int boardY,
        int blockWidth)
    {
        var primary = game.LineFlashFrame < 2 ? Text : Cyan;
        var secondary = game.LineFlashFrame < 4 ? Cyan : Pink;

        foreach (var row in game.FlashingRows)
        {
            for (var column = 0; column < BlockdropGame.BoardWidth; column++)
            {
                var color = (column + game.LineFlashFrame) % 2 == 0
                    ? primary
                    : secondary;

                for (var offset = 0; offset < blockWidth; offset++)
                {
                    var character = blockWidth == 1 ? "*" : " ";
                    Put(
                        surface,
                        boardX + column * blockWidth + offset,
                        boardY + row,
                        character,
                        color,
                        color,
                        CellAttributes.Bold);
                }
            }
        }
    }

    private static void DrawStateOverlay(Surface surface, BlockdropGame game)
    {
        var width = Math.Min(38, surface.Width - 4);
        var height = 7;
        var x = (surface.Width - width) / 2;
        var y = (surface.Height - height) / 2;
        var title = game.State == BlockdropState.Paused ? "FIELD SUSPENDED" : "SIGNAL LOST";
        var color = game.State == BlockdropState.Paused ? Warning : Danger;

        DrawPanel(surface, x, y, width, height, title, color, PanelStrong);
        CenterTextIn(surface, x + 1, y + 2, width - 2, $"SCORE {game.Score:N0}", Text, PanelStrong, CellAttributes.Bold);
        CenterTextIn(
            surface,
            x + 1,
            y + 4,
            width - 2,
            game.State == BlockdropState.Paused ? "P RESUME  |  Q EXIT" : "R REBOOT  |  Q EXIT",
            Muted,
            PanelStrong);
    }

    private static void DrawTooSmall(Surface surface)
    {
        var width = Math.Min(surface.Width - 2, 34);
        var height = Math.Min(surface.Height - 2, 7);

        if (width < 4 || height < 3)
        {
            return;
        }

        var x = (surface.Width - width) / 2;
        var y = (surface.Height - height) / 2;
        DrawPanel(surface, x, y, width, height, "SIGNAL COMPRESSED", Warning);
        CenterTextIn(surface, x + 1, y + 2, width - 2, "MAKE THE TERMINAL LARGER", Text, Panel);

        if (height >= 5)
        {
            CenterTextIn(surface, x + 1, y + 4, width - 2, "MINIMUM 14 x 22", Muted, Panel);
        }
    }

    private static void DrawBackdrop(Surface surface)
    {
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var pulse = (x * 19 + y * 31) % 113;
                if (pulse == 0 || pulse == 7)
                {
                    Put(surface, x, y, ".", BackdropAccent, Backdrop, CellAttributes.Dim);
                }
            }
        }
    }

    private static void DrawPanel(
        Surface surface,
        int x,
        int y,
        int width,
        int height,
        string title,
        Hex1bColor accent,
        Hex1bColor? background = null)
    {
        if (width < 3 || height < 3)
        {
            return;
        }

        var panelBackground = background ?? Panel;
        surface.Fill(new Rect(x, y, width, height), new SurfaceCell(" ", null, panelBackground));

        for (var column = x + 1; column < x + width - 1; column++)
        {
            Put(surface, column, y, "-", accent, panelBackground);
            Put(surface, column, y + height - 1, "-", accent, panelBackground);
        }

        for (var row = y + 1; row < y + height - 1; row++)
        {
            Put(surface, x, row, "|", accent, panelBackground);
            Put(surface, x + width - 1, row, "|", accent, panelBackground);
        }

        Put(surface, x, y, "+", accent, panelBackground);
        Put(surface, x + width - 1, y, "+", accent, panelBackground);
        Put(surface, x, y + height - 1, "+", accent, panelBackground);
        Put(surface, x + width - 1, y + height - 1, "+", accent, panelBackground);

        if (width >= 8)
        {
            var label = $" {title} ";
            WriteClipped(surface, x + 2, y, width - 4, label, accent, panelBackground, CellAttributes.Bold);
        }
    }

    private static void DrawMeter(Surface surface, int x, int y, int width, int value, int maximum)
    {
        if (width < 3)
        {
            return;
        }

        var interior = width - 2;
        var filled = maximum == 0 ? interior : (int)Math.Round(interior * value / (double)maximum);
        Put(surface, x, y, "[", Muted, Panel);
        Put(surface, x + width - 1, y, "]", Muted, Panel);

        for (var index = 0; index < interior; index++)
        {
            var color = index < filled ? Lime : BackdropAccent;
            Put(surface, x + 1 + index, y, index < filled ? "=" : "-", color, Panel);
        }
    }

    private static Hex1bColor StatusColor(BlockdropGame game)
        => game.State switch
        {
            BlockdropState.Paused => Warning,
            BlockdropState.GameOver => Danger,
            _ => game.StatusMessage.Contains("PULSE", StringComparison.Ordinal) ? Lime : Pink,
        };

    private static void CenterText(
        Surface surface,
        int y,
        string text,
        Hex1bColor foreground,
        CellAttributes attributes = CellAttributes.None)
    {
        var x = Math.Max(0, (surface.Width - text.Length) / 2);
        WriteClipped(surface, x, y, surface.Width - x, text, foreground, Backdrop, attributes);
    }

    private static void CenterTextIn(
        Surface surface,
        int x,
        int y,
        int width,
        string text,
        Hex1bColor foreground,
        Hex1bColor background,
        CellAttributes attributes = CellAttributes.None)
    {
        var clipped = text.Length > width ? text[..width] : text;
        Write(surface, x + Math.Max(0, (width - clipped.Length) / 2), y, clipped, foreground, background, attributes);
    }

    private static void WriteClipped(
        Surface surface,
        int x,
        int y,
        int width,
        string text,
        Hex1bColor foreground,
        Hex1bColor? background = null,
        CellAttributes attributes = CellAttributes.None)
    {
        if (width <= 0)
        {
            return;
        }

        Write(surface, x, y, text.Length > width ? text[..width] : text, foreground, background, attributes);
    }

    private static void Write(
        Surface surface,
        int x,
        int y,
        string text,
        Hex1bColor foreground,
        Hex1bColor? background = null,
        CellAttributes attributes = CellAttributes.None)
    {
        for (var index = 0; index < text.Length; index++)
        {
            var targetX = x + index;
            if (!surface.IsInBounds(targetX, y))
            {
                continue;
            }

            var existing = surface[targetX, y];
            Put(surface, targetX, y, text[index].ToString(), foreground, background ?? existing.Background, attributes);
        }
    }

    private static void Put(
        Surface surface,
        int x,
        int y,
        string character,
        Hex1bColor? foreground,
        Hex1bColor? background,
        CellAttributes attributes = CellAttributes.None)
    {
        if (surface.IsInBounds(x, y))
        {
            surface[x, y] = new SurfaceCell(character, foreground, background, attributes);
        }
    }
}
