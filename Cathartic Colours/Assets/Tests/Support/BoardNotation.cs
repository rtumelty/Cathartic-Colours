// Board notation shared by the test plan, the PlayMode driver and the EditMode TestBoards helper (CC-046).
//
// Cells are separated by spaces and rows by '|'. The FIRST row listed is the TOP row (highest y), so x grows to the
// right and y grows upwards, as in the test plan ("(column, row)" with row 0 at the bottom).
//   "."   empty cell
//   "i"   the white next-colour indicator
//   "Rs"  colour letter (R G B Y M C W) + size letter (s m l)
//   "?"   (expected boards only) a freshly resolved indicator: a Small Red, Green or Blue block
using System;
using System.Text;
using Simulation;

namespace TestSupport
{
    /// <summary>One parsed cell of a board in test-plan notation.</summary>
    public readonly struct NotationCell
    {
        public readonly bool IsEmpty;
        public readonly bool IsIndicator;
        public readonly BlockColor Color;
        public readonly BlockSize Size;

        NotationCell(bool isEmpty, bool isIndicator, BlockColor color, BlockSize size)
        {
            IsEmpty = isEmpty;
            IsIndicator = isIndicator;
            Color = color;
            Size = size;
        }

        public static NotationCell Empty => new NotationCell(true, false, default, default);
        public static NotationCell Indicator => new NotationCell(false, true, BlockColor.White, BlockSize.Small);
        public static NotationCell Of(BlockColor color, BlockSize size) => new NotationCell(false, false, color, size);
    }

    public static class BoardNotation
    {
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

        public static BlockColor ParseColor(char c)
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
                default: throw new ArgumentException("Unknown colour letter '" + c + "'");
            }
        }

        public static BlockSize ParseSize(char c)
        {
            switch (c)
            {
                case 's': return BlockSize.Small;
                case 'm': return BlockSize.Medium;
                case 'l': return BlockSize.Large;
                default: throw new ArgumentException("Unknown size letter '" + c + "'");
            }
        }

        /// <summary>The token for one cell: ".", "i" or colour and size letters.</summary>
        public static string FormatCell(NotationCell cell)
        {
            if (cell.IsEmpty) return ".";
            if (cell.IsIndicator) return "i";
            return "" + ColorLetter(cell.Color) + SizeLetter(cell.Size);
        }

        /// <summary>Parses one token ("." , "i" or "Rs"); "?" is not a cell and throws.</summary>
        public static NotationCell ParseCell(string token)
        {
            if (token == ".") return NotationCell.Empty;
            if (token == "i") return NotationCell.Indicator;
            if (token.Length != 2) throw new ArgumentException("Bad block token '" + token + "'");
            return NotationCell.Of(ParseColor(token[0]), ParseSize(token[1]));
        }

        /// <summary>Splits a board into tokens, indexed [x, y] with y = 0 the bottom row.</summary>
        public static string[,] Tokens(string text)
        {
            var rows = text.Split('|');
            int height = rows.Length;
            int width = -1;
            var parsed = new string[height][];
            for (int r = 0; r < height; r++)
            {
                parsed[r] = rows[r].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (width < 0) width = parsed[r].Length;
                else if (width != parsed[r].Length)
                    throw new ArgumentException("Ragged board: " + text);
            }

            var tokens = new string[width, height];
            for (int r = 0; r < height; r++)
            {
                int y = height - 1 - r;
                for (int x = 0; x < width; x++)
                    tokens[x, y] = parsed[r][x];
            }
            return tokens;
        }

        /// <summary>Parses a board into cells, indexed [x, y] with y = 0 the bottom row.</summary>
        public static NotationCell[,] Parse(string text)
        {
            var tokens = Tokens(text);
            int width = tokens.GetLength(0), height = tokens.GetLength(1);
            var cells = new NotationCell[width, height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    cells[x, y] = ParseCell(tokens[x, y]);
            return cells;
        }

        /// <summary>Formats cells indexed [x, y] (y = 0 the bottom row), top row first.</summary>
        public static string Format(NotationCell[,] cells)
        {
            return Format(cells.GetLength(0), cells.GetLength(1), (x, y) => cells[x, y]);
        }

        /// <summary>Formats a board of the given size from a cell lookup, top row first.</summary>
        public static string Format(int width, int height, Func<int, int, NotationCell> cellAt)
        {
            var sb = new StringBuilder();
            for (int y = height - 1; y >= 0; y--)
            {
                if (y != height - 1) sb.Append('|');
                for (int x = 0; x < width; x++)
                {
                    if (x > 0) sb.Append(' ');
                    sb.Append(FormatCell(cellAt(x, y)));
                }
            }
            return sb.ToString();
        }

        /// <summary>True for a freshly resolved indicator: a Small Red, Green or Blue block that is not an indicator.</summary>
        public static bool IsResolvedPrimary(NotationCell cell)
        {
            return !cell.IsEmpty && !cell.IsIndicator && cell.Size == BlockSize.Small &&
                   (cell.Color == BlockColor.Red || cell.Color == BlockColor.Green || cell.Color == BlockColor.Blue);
        }

        /// <summary>
        /// Compares a board with an expected board ("?" matches any resolved primary). Returns null when they match,
        /// otherwise a message naming the first differing cell and both boards.
        /// </summary>
        public static string Compare(string expected, string actual)
        {
            var want = Tokens(expected);
            var got = Tokens(actual);
            if (want.GetLength(0) != got.GetLength(0) || want.GetLength(1) != got.GetLength(1))
                return $"board size {got.GetLength(0)}x{got.GetLength(1)}, expected {want.GetLength(0)}x{want.GetLength(1)}. Expected '{expected}', actual '{actual}'";
            for (int y = want.GetLength(1) - 1; y >= 0; y--)
                for (int x = 0; x < want.GetLength(0); x++)
                {
                    string w = want[x, y], g = got[x, y];
                    bool ok = w == "?" ? IsResolvedPrimary(ParseCell(g)) : w == g;
                    if (!ok)
                        return $"cell ({x},{y}) is '{g}', expected '{w}'. Expected '{expected}', actual '{actual}'";
                }
            return null;
        }
    }
}
