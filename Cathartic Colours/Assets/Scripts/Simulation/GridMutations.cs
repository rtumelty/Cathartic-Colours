using Unity.Collections;
using Unity.Mathematics;

namespace Simulation
{
    /// <summary>Which grid change a game uses. A slot for shifting size (CC-011) and hazards (CC-012).</summary>
    public enum GridMutationKind : byte
    {
        None = 0
    }

    /// <summary>
    /// A grid change: runs once per move after the indicators resolve and before the spawn (ADR-005), so the next
    /// indicator is placed on the changed board.
    /// </summary>
    public interface IGridMutation
    {
        void Apply(ref BoardState board, ref Random random, NativeList<SimEvent> events);
    }

    /// <summary>Changes nothing and draws nothing.</summary>
    public struct NoGridMutation : IGridMutation
    {
        public void Apply(ref BoardState board, ref Random random, NativeList<SimEvent> events) { }
    }

    /// <summary>Calls the grid change a <see cref="GridMutationKind"/> names. Unknown kinds change nothing.</summary>
    public static class GridMutations
    {
        public static void Apply(GridMutationKind kind, ref BoardState board, ref Random random, NativeList<SimEvent> events)
        {
            switch (kind)
            {
                default:
                    new NoGridMutation().Apply(ref board, ref random, events);
                    break;
            }
        }
    }
}
