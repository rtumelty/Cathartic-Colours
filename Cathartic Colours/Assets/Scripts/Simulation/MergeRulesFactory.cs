using Unity.Collections;
using Unity.Mathematics;

namespace Simulation
{
    /// <summary>
    /// Picks the rule set for a <see cref="GameMode"/> with a switch and calls the generic code with that struct,
    /// so there is no boxing or managed interface reference (Burst-compatible). Unknown modes use Standard.
    /// </summary>
    public static class MergeRulesFactory
    {
        public static StepResult Step(GameMode mode, ref BoardState board, int2 direction,
            in StepSettings settings, ref Random random, NativeList<SimEvent> events)
        {
            switch (mode)
            {
                case GameMode.AdvancedColorMerge:
                    return BoardSimulation.Step(ref board, direction, new AdvancedColorMergeRules(), settings, ref random, events);
                case GameMode.ColorMerge:
                    return BoardSimulation.Step(ref board, direction, new ColorMergeRules(), settings, ref random, events);
                default:
                    return BoardSimulation.Step(ref board, direction, new StandardMergeRules(), settings, ref random, events);
            }
        }

        public static bool IsGameOver(GameMode mode, in BoardState board)
        {
            switch (mode)
            {
                case GameMode.AdvancedColorMerge:
                    return BoardSimulation.IsGameOver(board, new AdvancedColorMergeRules());
                case GameMode.ColorMerge:
                    return BoardSimulation.IsGameOver(board, new ColorMergeRules());
                default:
                    return BoardSimulation.IsGameOver(board, new StandardMergeRules());
            }
        }

        public static bool CanMerge(GameMode mode, Block mover, Block target)
        {
            switch (mode)
            {
                case GameMode.AdvancedColorMerge: return new AdvancedColorMergeRules().CanMerge(mover, target);
                case GameMode.ColorMerge: return new ColorMergeRules().CanMerge(mover, target);
                default: return new StandardMergeRules().CanMerge(mover, target);
            }
        }

        public static MergeResult Merge(GameMode mode, Block mover, Block target)
        {
            switch (mode)
            {
                case GameMode.AdvancedColorMerge: return new AdvancedColorMergeRules().Merge(mover, target);
                case GameMode.ColorMerge: return new ColorMergeRules().Merge(mover, target);
                default: return new StandardMergeRules().Merge(mover, target);
            }
        }
    }
}
