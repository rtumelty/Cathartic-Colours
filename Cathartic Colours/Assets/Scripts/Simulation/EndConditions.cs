namespace Simulation
{
    /// <summary>Which end condition a game uses.</summary>
    public enum EndConditionKind : byte
    {
        /// <summary>Game over when no cell is empty and no neighbours can merge.</summary>
        NoMovesLeft = 0
    }

    /// <summary>
    /// An end condition: checked before a move (a finished game rejects moves) and after it (ADR-005). Generic over
    /// the merge rule, because whether anything can still merge depends on it.
    /// </summary>
    public interface IEndCondition
    {
        bool IsGameOver<TMerge>(in BoardState board, in TMerge merge) where TMerge : struct, IMergeRules;
    }

    /// <summary>No empty cell (an indicator counts as occupied) and no mergeable 4-neighbour pair.</summary>
    public struct NoMovesLeftEnd : IEndCondition
    {
        public bool IsGameOver<TMerge>(in BoardState board, in TMerge merge) where TMerge : struct, IMergeRules =>
            BoardSimulation.IsGameOver(board, merge);
    }

    /// <summary>Calls the end condition an <see cref="EndConditionKind"/> names. Unknown kinds use <see cref="NoMovesLeftEnd"/>.</summary>
    public static class EndConditions
    {
        public static bool IsGameOver<TMerge>(EndConditionKind kind, in BoardState board, in TMerge merge)
            where TMerge : struct, IMergeRules
        {
            switch (kind)
            {
                default: return new NoMovesLeftEnd().IsGameOver(board, merge);
            }
        }
    }
}
