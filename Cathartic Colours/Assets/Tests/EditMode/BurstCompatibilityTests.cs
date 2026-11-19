using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Simulation.Tests
{
    /// <summary>
    /// Proves that the step and all three rule sets compile and run under Burst (project rule: favour
    /// Burst-compatible ECS code; ADR-001 §5), and that the Burst result matches the managed one.
    /// </summary>
    public class BurstCompatibilityTests
    {
        [TestCase(GameMode.Standard)]
        [TestCase(GameMode.ColorMerge)]
        [TestCase(GameMode.AdvancedColorMerge)]
        public void Step_CompilesAndRunsUnderBurst_MatchingManagedResult(GameMode mode)
        {
            Assert.IsTrue(BurstCompiler.IsEnabled, "Burst compilation is disabled in this editor.");

            var burst = BurstCompiler.CompileFunctionPointer<BurstStepProbe.RunDelegate>(BurstStepProbe.Run);

            var burstResult = new BurstStepProbe.ProbeResult();
            burst.Invoke((int)mode, 12345u, ref burstResult);

            // RunManaged has no [BurstCompile], so this call runs as ordinary C#. (A direct call to Run would
            // itself be Burst-compiled.)
            var managedResult = new BurstStepProbe.ProbeResult();
            BurstStepProbe.RunManaged((int)mode, 12345u, ref managedResult);

            Assert.AreEqual(0, burstResult.RanManaged, "The function pointer ran as managed code, not Burst.");
            Assert.AreEqual(1, managedResult.RanManaged, "Sanity check: a direct call should run managed.");
            Assert.AreEqual(managedResult.Checksum, burstResult.Checksum, "Burst and managed steps differ.");
            Assert.AreEqual(managedResult.EventCount, burstResult.EventCount);
            Assert.AreEqual(managedResult.Score, burstResult.Score);
            Assert.Greater(burstResult.EventCount, 0, "The probe should produce events.");
        }
    }

    [BurstCompile]
    public static class BurstStepProbe
    {
        public struct ProbeResult
        {
            public int RanManaged; // int, not bool: function pointer arguments must be blittable
            public int Score;
            public int EventCount;
            public uint Checksum;
        }

        public delegate void RunDelegate(int mode, uint seed, ref ProbeResult result);

        /// <summary>Runs a few steps on a small board in every direction and summarises the outcome.</summary>
        [BurstCompile(CompileSynchronously = true)]
        public static void Run(int mode, uint seed, ref ProbeResult result) => RunCore(mode, seed, ref result);

        public static void RunManaged(int mode, uint seed, ref ProbeResult result) => RunCore(mode, seed, ref result);

        private static void RunCore(int mode, uint seed, ref ProbeResult result)
        {
            MarkManaged(ref result.RanManaged);

            var board = new BoardState(4, 4, Allocator.Temp);
            var events = new NativeList<SimEvent>(64, Allocator.Temp);
            var random = new Random(seed);
            var settings = StepSettings.Default;

            board[0, 0] = Block.Of(BlockColor.Red, BlockSize.Small);
            board[1, 0] = Block.Of(BlockColor.Red, BlockSize.Small);
            board[2, 0] = Block.Of(BlockColor.Green, BlockSize.Small);
            board[0, 1] = Block.Of(BlockColor.Blue, BlockSize.Medium);
            board[1, 1] = Block.Of(BlockColor.Red, BlockSize.Medium);
            board[3, 3] = Block.Indicator;

            var directions = new NativeArray<int2>(4, Allocator.Temp);
            directions[0] = new int2(-1, 0);
            directions[1] = new int2(0, -1);
            directions[2] = new int2(1, 0);
            directions[3] = new int2(0, 1);

            for (int i = 0; i < 8; i++)
            {
                var step = MergeRulesFactory.Step((GameMode)mode, ref board, directions[i % 4], settings, ref random, events);
                result.Score += step.ScoreDelta;
            }

            // Also exercise the generic entry point directly.
            var direct = BoardSimulation.Step(ref board, new int2(-1, 0), new StandardMergeRules(), settings, ref random, events);
            result.Score += direct.ScoreDelta;

            uint checksum = random.state;
            for (int i = 0; i < board.Cells.Length; i++)
            {
                var cell = board.Cells[i];
                checksum = checksum * 31u + (uint)cell.Color * 7u + (uint)cell.Size * 3u + (cell.IsIndicator ? 1u : 0u);
            }

            result.EventCount = events.Length;
            result.Checksum = checksum;

            directions.Dispose();
            events.Dispose();
            board.Dispose();
        }

        [BurstDiscard]
        private static void MarkManaged(ref int ranManaged)
        {
            ranManaged = 1;
        }
    }
}
