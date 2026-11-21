using Unity.Collections;
using Unity.Mathematics;

namespace Simulation
{
    /// <summary>Which spawn rule a game uses. Held in <see cref="GameRules"/>, never saved.</summary>
    public enum SpawnRuleKind : byte
    {
        /// <summary>Indicators become Small Red, Green or Blue blocks; a new indicator appears after every move.</summary>
        UniformPrimary = 0,

        /// <summary>The spawn toggle off: indicators already on the board still resolve, no new ones appear.</summary>
        None = 1
    }

    /// <summary>
    /// A spawn rule: turns indicators into blocks after the slide, and places new indicators after the grid change
    /// (ADR-005). A rule owns every random draw it makes, in a documented order. Changing that order changes
    /// replays and resumed games, so it needs new recorded-game hashes (CC-008.1) and a deliberate decision.
    /// </summary>
    public interface ISpawnRule
    {
        /// <summary>Phase 2: turns every indicator on the board into a block.</summary>
        void ResolveIndicators(ref BoardState board, ref Random random, NativeList<SimEvent> events);

        /// <summary>Phase 4: places new indicators. Returns true when a spawn was due but the board had no room.</summary>
        bool Spawn(ref BoardState board, ref Random random, NativeList<SimEvent> events);
    }

    /// <summary>
    /// The spawn rule of the three original modes. Draw order: one <c>NextInt(0, 3)</c> per indicator, in cell index
    /// order (0 Red, 1 Green, 2 Blue); then one <c>NextInt(0, emptyCount)</c> picking the new indicator's cell among
    /// the empty cells counted in index order (x + y * Width). Nothing is drawn for the spawn when no cell is empty
    /// (CC-006.4, CC-020, CC-021).
    /// </summary>
    public struct UniformPrimarySpawn : ISpawnRule
    {
        public void ResolveIndicators(ref BoardState board, ref Random random, NativeList<SimEvent> events)
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

        public bool Spawn(ref BoardState board, ref Random random, NativeList<SimEvent> events)
        {
            int emptyCount = board.CountEmpty();
            if (emptyCount == 0)
                return true;

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
                return false;
            }
            return true;
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

    /// <summary>
    /// The spawn toggle off (<c>GameConfiguration.spawnNextColourSystem</c> false). Indicators already on the board
    /// still resolve exactly as in <see cref="UniformPrimarySpawn"/>, as they did before CC-008. Nothing new is
    /// placed and nothing more is drawn.
    /// </summary>
    public struct NoSpawn : ISpawnRule
    {
        public void ResolveIndicators(ref BoardState board, ref Random random, NativeList<SimEvent> events) =>
            new UniformPrimarySpawn().ResolveIndicators(ref board, ref random, events);

        public bool Spawn(ref BoardState board, ref Random random, NativeList<SimEvent> events) => false;
    }

    /// <summary>Calls the spawn rule a <see cref="SpawnRuleKind"/> names. Unknown kinds use <see cref="UniformPrimarySpawn"/>.</summary>
    public static class SpawnRules
    {
        public static void ResolveIndicators(SpawnRuleKind kind, ref BoardState board, ref Random random,
            NativeList<SimEvent> events)
        {
            switch (kind)
            {
                case SpawnRuleKind.None:
                    new NoSpawn().ResolveIndicators(ref board, ref random, events);
                    break;
                default:
                    new UniformPrimarySpawn().ResolveIndicators(ref board, ref random, events);
                    break;
            }
        }

        public static bool Spawn(SpawnRuleKind kind, ref BoardState board, ref Random random, NativeList<SimEvent> events)
        {
            switch (kind)
            {
                case SpawnRuleKind.None: return new NoSpawn().Spawn(ref board, ref random, events);
                default: return new UniformPrimarySpawn().Spawn(ref board, ref random, events);
            }
        }
    }
}
