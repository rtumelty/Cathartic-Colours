using Unity.Collections;
using Unity.Mathematics;

namespace Simulation
{
    /// <summary>
    /// One player move as a pure function of its inputs: board, direction, <see cref="GameRules"/> and random state.
    /// Same inputs give the same board, events and random state. Burst-compatible.
    /// </summary>
    public static class BoardSimulation
    {
        /// <summary>
        /// Runs one move with the rules of <paramref name="rules"/>. Picks the merge rule struct by
        /// <see cref="GameRules.Merge"/> and calls the generic step, so the per-cell merge loop is not dispatched.
        /// </summary>
        public static StepResult Step(ref BoardState board, int2 direction, in GameRules rules,
            ref Random random, NativeList<SimEvent> events)
        {
            switch (rules.Merge)
            {
                case MergeRuleKind.AdvancedColorMerge:
                    return Step(ref board, direction, new AdvancedColorMergeRules(), rules, ref random, events);
                case MergeRuleKind.ColorMerge:
                    return Step(ref board, direction, new ColorMergeRules(), rules, ref random, events);
                default:
                    return Step(ref board, direction, new StandardMergeRules(), rules, ref random, events);
            }
        }

        /// <summary>
        /// Runs one move in a fixed order of phases (ADR-005):
        /// 1. slide and merge (merge rule <paramref name="merge"/>, scoring rule);
        /// 2. resolve the indicators (spawn rule);
        /// 3. grid change (grid rule);
        /// 4. spawn (spawn rule);
        /// 5. end check (end condition).
        /// <see cref="GameRules.Merge"/> is ignored here: the caller passes the merge rule struct itself.
        /// Rejected (nothing changes, no events) if <paramref name="direction"/> is not one of the four unit
        /// directions or the game is already over. Events are appended to <paramref name="events"/>.
        /// </summary>
        public static StepResult Step<TMerge>(ref BoardState board, int2 direction, in TMerge merge,
            in GameRules rules, ref Random random, NativeList<SimEvent> events)
            where TMerge : struct, IMergeRules
        {
            var result = new StepResult();

            if (!IsUnitDirection(direction) || EndConditions.IsGameOver(rules.End, board, merge))
                return result;

            result.Accepted = true;
            result.ScoreDelta = Slide(ref board, direction, merge, rules, events);
            SpawnRules.ResolveIndicators(rules.Spawn, ref board, ref random, events);
            GridMutations.Apply(rules.Grid, ref board, ref random, events);
            result.SpawnFailed = SpawnRules.Spawn(rules.Spawn, ref board, ref random, events);

            result.GameOver = EndConditions.IsGameOver(rules.End, board, merge);
            if (result.GameOver)
                events.Add(new SimEvent { Type = SimEventType.GameOver });

            return result;
        }

        /// <summary>
        /// Time passing, for modes with a timer (CC-010). Hands over to the timer rule; with
        /// <see cref="TimerRuleKind.None"/> nothing changes, nothing is drawn and the result is not accepted.
        /// </summary>
        public static StepResult Advance(ref BoardState board, float elapsedSeconds, in GameRules rules,
            ref Random random, NativeList<SimEvent> events)
        {
            return TimerRules.Advance(rules.Timer, ref board, elapsedSeconds, ref random, events);
        }

        /// <summary>Whether the game is over under the end condition and merge rule of <paramref name="rules"/>.</summary>
        public static bool IsGameOver(in BoardState board, in GameRules rules)
        {
            switch (rules.Merge)
            {
                case MergeRuleKind.AdvancedColorMerge:
                    return EndConditions.IsGameOver(rules.End, board, new AdvancedColorMergeRules());
                case MergeRuleKind.ColorMerge:
                    return EndConditions.IsGameOver(rules.End, board, new ColorMergeRules());
                default:
                    return EndConditions.IsGameOver(rules.End, board, new StandardMergeRules());
            }
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
        /// when the merge rule allows it and that cell has not been merged into already this move. Each merge
        /// is scored by the scoring rule of <paramref name="rules"/>. Returns the points.
        /// </summary>
        private static int Slide<TMerge>(ref BoardState board, int2 direction, in TMerge merge,
            in GameRules rules, NativeList<SimEvent> events)
            where TMerge : struct, IMergeRules
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
                    if (mergedInto[targetIndex] || !merge.CanMerge(mover, target))
                        continue;

                    var result = merge.Merge(mover, target);
                    int mergePoints = ScoringRules.PointsFor(rules, result);
                    score += mergePoints;

                    board[from] = Block.Empty;
                    board[to] = result.DestroysBoth ? Block.Empty : Block.Of(result.Color, result.Size);
                    mergedInto[targetIndex] = true;

                    events.Add(new SimEvent
                    {
                        Type = SimEventType.Merged,
                        From = from,
                        Position = to,
                        Color = result.Color,
                        Size = result.Size,
                        MoverSize = mover.Size,
                        Tier = result.Tier,
                        Points = mergePoints,
                        DestroysBoth = result.DestroysBoth
                    });
                }
            }

            mergedInto.Dispose();
            return score;
        }
    }
}
