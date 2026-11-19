// Test helpers for the CC-006.2 characterisation tests.
// Written from docs/wiki/systems/merge-rules.md and the CC-006.2 API contract, without reading the
// simulation implementation.
//
// Board notation (as in the wiki worked examples): cells separated by spaces, rows separated by '|',
// the FIRST row listed is the TOP row (highest y), so x grows to the right and y grows upwards.
//   "."  empty cell
//   "i"  the white next-colour indicator (Block.Indicator)
//   "Rs" colour letter (R G B Y M C W) + size letter (s m l)
//   "?"  (expected boards only) a freshly resolved indicator: Small Red/Green/Blue, not an indicator
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Simulation.Tests
{
    internal struct SpawnReplay
    {
        public bool Found;
        public int2 Cell;
        public uint FinalState;
    }

    internal static class TestBoards
    {
        public static readonly int2 Right = new int2(1, 0);
        public static readonly int2 Left = new int2(-1, 0);
        public static readonly int2 Up = new int2(0, 1);
        public static readonly int2 Down = new int2(0, -1);

        public static StepSettings NoSpawn
        {
            get
            {
                var s = StepSettings.Default;
                s.SpawnEnabled = false;
                return s;
            }
        }

        public static StepSettings WithSpawn
        {
            get
            {
                var s = StepSettings.Default;
                s.SpawnEnabled = true;
                return s;
            }
        }

        public static char ColorLetter(BlockColor color)
        {
            switch (color)
            {
                case BlockColor.Red: return 'R';
                case BlockColor.Green: return 'G';
                case BlockColor.Yellow: return 'Y';
                case BlockColor.Blue: return 'B';
                case BlockColor.Magenta: return 'M';
                case BlockColor.Cyan: return 'C';
                case BlockColor.White: return 'W';
                default: return '?';
            }
        }

        public static char SizeLetter(BlockSize size)
        {
            switch (size)
            {
                case BlockSize.Small: return 's';
                case BlockSize.Medium: return 'm';
                case BlockSize.Large: return 'l';
                default: return '?';
            }
        }

        static BlockColor ParseColor(char c)
        {
            switch (c)
            {
                case 'R': return BlockColor.Red;
                case 'G': return BlockColor.Green;
                case 'Y': return BlockColor.Yellow;
                case 'B': return BlockColor.Blue;
                case 'M': return BlockColor.Magenta;
                case 'C': return BlockColor.Cyan;
                case 'W': return BlockColor.White;
                default: throw new System.ArgumentException("Unknown colour letter '" + c + "'");
            }
        }

        static BlockSize ParseSize(char c)
        {
            switch (c)
            {
                case 's': return BlockSize.Small;
                case 'm': return BlockSize.Medium;
                case 'l': return BlockSize.Large;
                default: throw new System.ArgumentException("Unknown size letter '" + c + "'");
            }
        }

        public static string FormatBlock(Block b)
        {
            if (b.IsEmpty) return ".";
            if (b.IsIndicator) return "i";
            return "" + ColorLetter(b.Color) + SizeLetter(b.Size);
        }

        public static Block ParseBlock(string token)
        {
            if (token == ".") return Block.Empty;
            if (token == "i") return Block.Indicator;
            if (token.Length != 2) throw new System.ArgumentException("Bad block token '" + token + "'");
            return Block.Of(ParseColor(token[0]), ParseSize(token[1]));
        }

        public static string Describe(Block b)
        {
            return b.Color + "/" + b.Size + (b.IsIndicator ? "/indicator" : "");
        }

        /// <summary>Parses the notation above into a new board (caller disposes).</summary>
        public static BoardState Parse(string text, Allocator allocator = Allocator.Persistent)
        {
            var rows = text.Split('|');
            int height = rows.Length;
            int width = -1;
            var parsed = new string[height][];
            for (int r = 0; r < height; r++)
            {
                parsed[r] = rows[r].Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (width < 0) width = parsed[r].Length;
                else if (width != parsed[r].Length)
                    throw new System.ArgumentException("Ragged board: " + text);
            }

            var board = new BoardState(width, height, allocator);
            for (int r = 0; r < height; r++)
            {
                int y = height - 1 - r;
                for (int x = 0; x < width; x++)
                    board[x, y] = ParseBlock(parsed[r][x]);
            }
            return board;
        }

        /// <summary>Formats a board in the notation above (top row first).</summary>
        public static string Format(in BoardState board)
        {
            var sb = new StringBuilder();
            for (int y = board.Height - 1; y >= 0; y--)
            {
                if (y != board.Height - 1) sb.Append('|');
                for (int x = 0; x < board.Width; x++)
                {
                    if (x > 0) sb.Append(' ');
                    sb.Append(FormatBlock(board[x, y]));
                }
            }
            return sb.ToString();
        }

        public static bool IsResolvedPrimary(Block b)
        {
            return !b.IsIndicator
                   && b.Size == BlockSize.Small
                   && (b.Color == BlockColor.Red || b.Color == BlockColor.Green || b.Color == BlockColor.Blue);
        }

        /// <summary>Asserts the board equals the expected notation ("?" = any resolved primary Small block).</summary>
        public static void AssertBoard(string expected, in BoardState actual, string message = null)
        {
            var rows = expected.Split('|');
            int height = rows.Length;
            Assert.AreEqual(height, actual.Height, "board height. " + message);
            for (int r = 0; r < height; r++)
            {
                var tokens = rows[r].Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                Assert.AreEqual(tokens.Length, actual.Width, "board width. " + message);
                int y = height - 1 - r;
                for (int x = 0; x < tokens.Length; x++)
                {
                    var cell = actual[x, y];
                    if (tokens[x] == "?")
                    {
                        Assert.IsTrue(IsResolvedPrimary(cell),
                            "cell (" + x + "," + y + ") should be a resolved Small primary but was " + Describe(cell)
                            + ". Board: " + Format(actual) + ". " + message);
                    }
                    else
                    {
                        Assert.AreEqual(tokens[x], FormatBlock(cell),
                            "cell (" + x + "," + y + ") of expected '" + expected + "' vs actual '" + Format(actual) + "'. " + message);
                    }
                }
            }
        }

        public static int CountIndicators(in BoardState board)
        {
            int n = 0;
            for (int i = 0; i < board.Cells.Length; i++)
                if (!board.Cells[i].IsEmpty && board.Cells[i].IsIndicator) n++;
            return n;
        }

        public static int CountOccupied(in BoardState board)
        {
            int n = 0;
            for (int i = 0; i < board.Cells.Length; i++)
                if (!board.Cells[i].IsEmpty) n++;
            return n;
        }

        /// <summary>A board of alternating Rs/Gs ((x+y) even = Red) in which no two neighbours can merge in any mode.</summary>
        public static BoardState Checkerboard(int width, int height, Allocator allocator = Allocator.Persistent)
        {
            var board = new BoardState(width, height, allocator);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    board[x, y] = ((x + y) % 2 == 0) ? Block.Of(BlockColor.Red, BlockSize.Small)
                                                     : Block.Of(BlockColor.Green, BlockSize.Small);
            return board;
        }

        public static BlockColor ColorFromRoll(int roll)
        {
            switch (roll)
            {
                case 0: return BlockColor.Red;
                case 1: return BlockColor.Green;
                case 2: return BlockColor.Blue;
                default: throw new System.ArgumentOutOfRangeException(nameof(roll));
            }
        }

        /// <summary>
        /// Replays the spawn of the spec from a fresh <c>Random(seed)</c>: first <paramref name="colourDraws"/>
        /// colour rolls (NextInt(0, 3)), then one draw NextInt(0, emptyCount) picking among the free cells in
        /// index order (x + y * width). Found is false when there is no free cell (and nothing is drawn).
        /// </summary>
        public static SpawnReplay ReplaySpawn(uint seed, int width, int height, System.Func<int2, bool> isFree, int colourDraws = 0)
        {
            var r = new Unity.Mathematics.Random(seed);
            for (int i = 0; i < colourDraws; i++)
                r.NextInt(0, 3);

            var free = new List<int2>();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (isFree(new int2(x, y))) free.Add(new int2(x, y));

            var res = new SpawnReplay();
            if (free.Count > 0)
            {
                res.Found = true;
                res.Cell = free[r.NextInt(0, free.Count)];
            }
            res.FinalState = r.state;
            return res;
        }

        public static string DescribeEvent(SimEvent e)
        {
            return e.Type + " from=" + e.From + " pos=" + e.Position + " col=" + e.Color + " size=" + e.Size
                   + " mover=" + e.MoverSize + " tier=" + e.Tier + " pts=" + e.Points + " both=" + e.DestroysBoth;
        }
    }

    /// <summary>One executed step: owns the board and the event list; dispose when done.</summary>
    internal sealed class StepRun : System.IDisposable
    {
        public BoardState Board;
        public NativeList<SimEvent> Events;
        public StepResult Result;
        public Unity.Mathematics.Random Random;
        public uint InitialState;

        StepRun() { }

        static StepRun Create(BoardState board, uint seed)
        {
            var run = new StepRun();
            run.Board = board;
            run.Events = new NativeList<SimEvent>(Allocator.Persistent);
            run.Random = new Unity.Mathematics.Random(seed);
            run.InitialState = run.Random.state;
            return run;
        }

        /// <summary>Parses the board and runs one step through MergeRulesFactory.Step.</summary>
        public static StepRun Execute(GameMode mode, string board, int2 direction, StepSettings settings, uint seed = 1u)
        {
            return ExecuteOn(mode, TestBoards.Parse(board), direction, settings, seed);
        }

        /// <summary>Takes ownership of <paramref name="board"/> and runs one step through MergeRulesFactory.Step.</summary>
        public static StepRun ExecuteOn(GameMode mode, BoardState board, int2 direction, StepSettings settings, uint seed = 1u)
        {
            var run = Create(board, seed);
            run.Result = MergeRulesFactory.Step(mode, ref run.Board, direction, in settings, ref run.Random, run.Events);
            return run;
        }

        /// <summary>Runs one step through the generic BoardSimulation.Step with the rule struct for the mode.</summary>
        public static StepRun ExecuteGeneric(GameMode mode, string board, int2 direction, StepSettings settings, uint seed = 1u)
        {
            var run = Create(TestBoards.Parse(board), seed);
            switch (mode)
            {
                case GameMode.ColorMerge:
                {
                    var rules = new ColorMergeRules();
                    run.Result = BoardSimulation.Step(ref run.Board, direction, in rules, in settings, ref run.Random, run.Events);
                    break;
                }
                case GameMode.AdvancedColorMerge:
                {
                    var rules = new AdvancedColorMergeRules();
                    run.Result = BoardSimulation.Step(ref run.Board, direction, in rules, in settings, ref run.Random, run.Events);
                    break;
                }
                default:
                {
                    var rules = new StandardMergeRules();
                    run.Result = BoardSimulation.Step(ref run.Board, direction, in rules, in settings, ref run.Random, run.Events);
                    break;
                }
            }
            return run;
        }

        public string BoardText
        {
            get { return TestBoards.Format(Board); }
        }

        public List<SimEvent> Of(SimEventType type)
        {
            var list = new List<SimEvent>();
            for (int i = 0; i < Events.Length; i++)
                if (Events[i].Type == type) list.Add(Events[i]);
            return list;
        }

        public List<string> EventDescriptions()
        {
            var list = new List<string>();
            for (int i = 0; i < Events.Length; i++) list.Add(TestBoards.DescribeEvent(Events[i]));
            return list;
        }

        public void Dispose()
        {
            if (Board.IsCreated) Board.Dispose();
            if (Events.IsCreated) Events.Dispose();
        }
    }
}
