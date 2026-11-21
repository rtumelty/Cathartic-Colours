// Recorded games (CC-008.1): plays seeded random games through the simulation and reduces each to a
// move count and a 64-bit hash of everything observable (boards, results, events, random state).
// The committed results in RecordedGames/recorded-games.txt pin today's behaviour exactly, so a refactor
// of the step code can prove it changed nothing. See docs/wiki/systems/merge-rules.md.
using Unity.Collections;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Simulation.Tests
{
    internal struct RecordedGame
    {
        public int Moves;
        public ulong Hash;
    }

    internal static class RecordedGames
    {
        public const int BoardSize = 6;
        public const int MaxMoves = 300;
        public const int SeedCount = 50;
        public const int StartingBlocks = 12;
        public const string RelativePath = "Tests/EditMode/RecordedGames/recorded-games.txt";

        static readonly int2[] Directions = { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) };

        static readonly BlockColor[] StartColors =
        {
            BlockColor.Red, BlockColor.Green, BlockColor.Blue,
            BlockColor.Yellow, BlockColor.Magenta, BlockColor.Cyan
        };

        static readonly BlockSize[] StartSizes = { BlockSize.Small, BlockSize.Medium, BlockSize.Large };

        /// <summary>
        /// THE SEAM: the only place the recorded games call the step code. When the step API changes
        /// (CC-008.2), retarget this one method; the recorded hashes must stay the same.
        /// </summary>
        public static StepResult StepOnce(GameMode mode, ref BoardState board, int2 dir, bool spawnEnabled,
            ref Random random, NativeList<SimEvent> events)
        {
            var settings = StepSettings.Default;
            settings.SpawnEnabled = spawnEnabled;
            return BoardSimulation.Step(ref board, dir, GameRules.For(mode, settings), ref random, events);
        }

        public static RecordedGame PlayGame(GameMode mode, bool spawnEnabled, uint seed)
        {
            // Game randomness (spawn rolls) and setup randomness (start board, move choice) are separate
            // streams, so choosing moves never consumes game draws.
            var random = new Random(seed);
            var setup = new Random(seed * 7919u + 1u);

            var board = new BoardState(BoardSize, BoardSize, Allocator.Temp);
            var events = new NativeList<SimEvent>(64, Allocator.Temp);
            try
            {
                FillStartingBoard(ref board, ref setup);

                ulong hash = Fnv.Offset;
                hash = Fnv.Add(hash, seed);
                HashBoard(ref hash, board);

                int moves = 0;
                for (int i = 0; i < MaxMoves; i++)
                {
                    var dir = Directions[setup.NextInt(0, 4)];
                    events.Clear();
                    var result = StepOnce(mode, ref board, dir, spawnEnabled, ref random, events);
                    if (!result.Accepted)
                        break;
                    moves++;

                    hash = Fnv.Add(hash, i);
                    hash = Fnv.Add(hash, dir.x);
                    hash = Fnv.Add(hash, dir.y);
                    hash = Fnv.Add(hash, result.Accepted ? 1 : 0);
                    hash = Fnv.Add(hash, result.ScoreDelta);
                    hash = Fnv.Add(hash, result.GameOver ? 1 : 0);
                    hash = Fnv.Add(hash, result.SpawnFailed ? 1 : 0);
                    HashBoard(ref hash, board);
                    hash = Fnv.Add(hash, random.state);
                    hash = Fnv.Add(hash, events.Length);
                    for (int e = 0; e < events.Length; e++)
                        HashEvent(ref hash, events[e]);

                    if (result.GameOver)
                        break;
                }

                return new RecordedGame { Moves = moves, Hash = hash };
            }
            finally
            {
                events.Dispose();
                board.Dispose();
            }
        }

        static void FillStartingBoard(ref BoardState board, ref Random setup)
        {
            int cellCount = board.Cells.Length;
            for (int placed = 0; placed < StartingBlocks;)
            {
                int index = setup.NextInt(0, cellCount);
                if (!board.Cells[index].IsEmpty)
                    continue;
                board.Cells[index] = Block.Of(StartColors[setup.NextInt(0, StartColors.Length)],
                    StartSizes[setup.NextInt(0, StartSizes.Length)]);
                placed++;
            }

            while (true)
            {
                int index = setup.NextInt(0, cellCount);
                if (!board.Cells[index].IsEmpty)
                    continue;
                board.Cells[index] = Block.Indicator;
                break;
            }
        }

        static void HashBoard(ref ulong hash, BoardState board)
        {
            for (int i = 0; i < board.Cells.Length; i++)
            {
                var b = board.Cells[i];
                hash = Fnv.Add(hash, (int)b.Color);
                hash = Fnv.Add(hash, (int)b.Size);
                hash = Fnv.Add(hash, b.IsIndicator ? 1 : 0);
            }
        }

        static void HashEvent(ref ulong hash, SimEvent e)
        {
            hash = Fnv.Add(hash, (int)e.Type);
            hash = Fnv.Add(hash, e.From.x);
            hash = Fnv.Add(hash, e.From.y);
            hash = Fnv.Add(hash, e.Position.x);
            hash = Fnv.Add(hash, e.Position.y);
            hash = Fnv.Add(hash, (int)e.Color);
            hash = Fnv.Add(hash, (int)e.Size);
            hash = Fnv.Add(hash, (int)e.MoverSize);
            hash = Fnv.Add(hash, (int)e.Tier);
            hash = Fnv.Add(hash, e.Points);
            hash = Fnv.Add(hash, e.DestroysBoth ? 1 : 0);
        }

        /// <summary>FNV-1a, 64-bit, fed four bytes at a time (little endian) from explicit field values.</summary>
        static class Fnv
        {
            public const ulong Offset = 14695981039346656037UL;
            const ulong Prime = 1099511628211UL;

            public static ulong Add(ulong hash, int value) => Add(hash, unchecked((uint)value));

            public static ulong Add(ulong hash, uint value)
            {
                for (int i = 0; i < 4; i++)
                {
                    hash ^= (value >> (i * 8)) & 0xFFu;
                    hash *= Prime;
                }
                return hash;
            }
        }
    }
}
