using Unity.Collections;
using Unity.Mathematics;

namespace Simulation
{
    /// <summary>
    /// One player move as a pure function of its inputs: board, direction, rules, settings and random state.
    /// Same inputs give the same board, events and random state. Burst-compatible.
    /// </summary>
    public static class BoardSimulation
    {
        /// <summary>
        /// Runs one move: slide and merge, resolve the indicator, spawn the next indicator, check for game over.
        /// Rejected (nothing changes, no events) if <paramref name="direction"/> is not one of the four unit
        /// directions or the board is already game over. Events are appended to <paramref name="events"/>.
        /// </summary>
        public static StepResult Step<TRules>(ref BoardState board, int2 direction, in TRules rules,
            in StepSettings settings, ref Random random, NativeList<SimEvent> events)
            where TRules : struct, IMergeRules
        {
            var result = new StepResult();

            if (!IsUnitDirection(direction) || IsGameOver(board, rules))
                return result;

            result.Accepted = true;
            result.ScoreDelta = Slide(ref board, direction, rules, settings.Points, events);
            ResolveIndicators(ref board, ref random, events);

            if (settings.SpawnEnabled)
                result.SpawnFailed = !SpawnIndicator(ref board, ref random, events);

            result.GameOver = IsGameOver(board, rules);
            if (result.GameOver)
                events.Add(new SimEvent { Type = SimEventType.GameOver });

            return result;
        }

        public static bool HasEmptyCell(in BoardState board)
        {
            for (int i = 0; i < board.Cells.Length; i++)
            {
                if (board.Cells[i].IsEmpty)
                    return true;
            }
            return false;
        }

        /// <summary>True if any block has a 4-neighbour it can merge into under <paramref name="rules"/>.</summary>
        public static bool HasMergeablePair<TRules>(in BoardState board, in TRules rules)
            where TRules : struct, IMergeRules
        {
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var block = board[x, y];
                    if (block.IsEmpty)
                        continue;

                    if (CanMergeWith(board, rules, block, new int2(x + 1, y)) ||
                        CanMergeWith(board, rules, block, new int2(x - 1, y)) ||
                        CanMergeWith(board, rules, block, new int2(x, y + 1)) ||
                        CanMergeWith(board, rules, block, new int2(x, y - 1)))
                        return true;
                }
            }
            return false;
        }

        /// <summary>No empty cell (an indicator counts as occupied) and no mergeable neighbour pair.</summary>
        public static bool IsGameOver<TRules>(in BoardState board, in TRules rules)
            where TRules : struct, IMergeRules
        {
            return !HasEmptyCell(board) && !HasMergeablePair(board, rules);
        }

        private static bool CanMergeWith<TRules>(in BoardState board, in TRules rules, Block block, int2 neighbour)
            where TRules : struct, IMergeRules
        {
            if (!board.InBounds(neighbour))
                return false;

            var other = board[neighbour];
            return !other.IsEmpty && rules.CanMerge(block, other);
        }

        private static bool IsUnitDirection(int2 d) =>
            (math.abs(d.x) == 1 && d.y == 0) || (d.x == 0 && math.abs(d.y) == 1);

        /// <summary>
        /// Every non-indicator block moves at most one cell, furthest along the direction first, so a block
        /// can move into a cell vacated earlier in the same move. A block merges into the occupied cell ahead
        /// when the rules allow it and that cell has not been merged into already this move. Returns the points.
        /// </summary>
        private static int Slide<TRules>(ref BoardState board, int2 direction, in TRules rules,
            in TierPoints points, NativeList<SimEvent> events)
            where TRules : struct, IMergeRules
        {
            int width = board.Width;
            int height = board.Height;
            int score = 0;
            var mergedInto = new NativeArray<bool>(width * height, Allocator.Temp, NativeArrayOptions.ClearMemory);

            // Visiting cells in order of decreasing progress along the direction processes each original block
            // exactly once: a block only ever moves into a cell that has already been visited.
            bool horizontal = direction.x != 0;
            int lineCount = horizontal ? width : height;
            int crossCount = horizontal ? height : width;
            bool positive = horizontal ? direction.x > 0 : direction.y > 0;

            for (int line = 0; line < lineCount; line++)
            {
                int along = positive ? lineCount - 1 - line : line;
                for (int cross = 0; cross < crossCount; cross++)
                {
                    int2 from = horizontal ? new int2(along, cross) : new int2(cross, along);
                    var mover = board[from];
                    if (mover.IsEmpty || mover.IsIndicator)
                        continue;

                    int2 to = from + direction;
                    if (!board.InBounds(to))
                        continue;

                    var target = board[to];
                    if (target.IsEmpty)
                    {
                        board[to] = mover;
                        board[from] = Block.Empty;
                        events.Add(new SimEvent { Type = SimEventType.Moved, From = from, Position = to });
                        continue;
                    }

                    int targetIndex = board.IndexOf(to);
                    if (mergedInto[targetIndex] || !rules.CanMerge(mover, target))
                        continue;

                    var merge = rules.Merge(mover, target);
                    int mergePoints = points.For(merge.Tier);
                    score += mergePoints;

                    board[from] = Block.Empty;
                    board[to] = merge.DestroysBoth ? Block.Empty : Block.Of(merge.Color, merge.Size);
                    mergedInto[targetIndex] = true;

                    events.Add(new SimEvent
                    {
                        Type = SimEventType.Merged,
                        From = from,
                        Position = to,
                        Color = merge.Color,
                        Size = merge.Size,
                        MoverSize = mover.Size,
                        Tier = merge.Tier,
                        Points = mergePoints,
                        DestroysBoth = merge.DestroysBoth
                    });
                }
            }

            mergedInto.Dispose();
            return score;
        }

        /// <summary>
        /// Every indicator becomes a Small Red, Green or Blue block, one random draw each. The draws advance the
        /// stored random state, so the spawn that follows draws from the next state (fixed in CC-006.4, CC-020).
        /// </summary>
        private static void ResolveIndicators(ref BoardState board, ref Random random, NativeList<SimEvent> events)
        {
            for (int i = 0; i < board.Cells.Length; i++)
            {
                if (!board.Cells[i].IsIndicator)
                    continue;

                var color = PrimaryFromIndex(random.NextInt(0, 3));
                board.Cells[i] = Block.Of(color, BlockSize.Small);
                events.Add(new SimEvent
                {
                    Type = SimEventType.IndicatorResolved,
                    Position = new int2(i % board.Width, i / board.Width),
                    Color = color,
                    Size = BlockSize.Small
                });
            }
        }

        /// <summary>
        /// Places a new indicator on a uniformly random empty cell using exactly one draw, counting empty cells
        /// in index order (x + y * Width). Returns false, drawing nothing, only when the board has no empty cell
        /// (fixed in CC-006.4, CC-021).
        /// </summary>
        private static bool SpawnIndicator(ref BoardState board, ref Random random, NativeList<SimEvent> events)
        {
            int emptyCount = 0;
            for (int i = 0; i < board.Cells.Length; i++)
            {
                if (board.Cells[i].IsEmpty)
                    emptyCount++;
            }
            if (emptyCount == 0)
                return false;

            int pick = random.NextInt(0, emptyCount);
            for (int i = 0; i < board.Cells.Length; i++)
            {
                if (!board.Cells[i].IsEmpty)
                    continue;
                if (pick-- > 0)
                    continue;

                board.Cells[i] = Block.Indicator;
                events.Add(new SimEvent
                {
                    Type = SimEventType.IndicatorSpawned,
                    Position = new int2(i % board.Width, i / board.Width),
                    Color = BlockColor.White,
                    Size = BlockSize.Small
                });
                return true;
            }
            return false;
        }

        private static BlockColor PrimaryFromIndex(int index)
        {
            switch (index)
            {
                case 0: return BlockColor.Red;
                case 1: return BlockColor.Green;
                default: return BlockColor.Blue;
            }
        }
    }
}
