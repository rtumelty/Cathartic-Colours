// Written from docs/wiki/systems/merge-rules.md ("Spawning") and docs/wiki/systems/ecs-frame-order.md
// ("Known issues": CC-020 and CC-021) plus the CC-006.2 API contract, without reading the simulation
// implementation.
//
// These tests PIN TODAY'S BUGGY BEHAVIOUR so the extraction into the pure simulation is a faithful copy.
// CC-006.4 turns them into Fixes_CC020_... / Fixes_CC021_... tests that assert the corrected behaviour.
//
//  CC-020: the colour roll draws from a copy of the random state that is thrown away, so the stored random is
//          not advanced by it and the spawn position is drawn from the same un-advanced state (colour and
//          position are correlated).
//  CC-021: the spawn makes at most Width*Height*2 random tries (x then y each) and fails, with empty cells
//          still on the board, when none of them hits an empty cell (about 13% for one empty cell on 6x6).
using NUnit.Framework;
using Unity.Mathematics;

namespace Simulation.Tests
{
    public class KnownBugCharacterisationTests
    {
        const string CentreIndicator3x3 = ". . .|. i .|. . .";

        // ------------------------------------------------------------------ CC-020

        [Test]
        public void Pins_CC020_SpawningDisabled_ColourRollDoesNotAdvanceTheStoredRandom()
        {
            for (uint seed = 1; seed <= 25; seed++)
            {
                using (var run = StepRun.Execute(GameMode.Standard, "i . . .", TestBoards.Right, TestBoards.NoSpawn, seed))
                {
                    Assert.AreEqual(1, run.Of(SimEventType.IndicatorResolved).Count, "precondition: an indicator was resolved (seed " + seed + ")");
                    Assert.AreEqual(run.InitialState, run.Random.state, "seed " + seed + ": random state must be unchanged by the colour roll");
                }
            }
        }

        [Test]
        public void Pins_CC020_ResolvedColourIsTheFirstRollOfAFreshRandom_NextInt0To3()
        {
            foreach (var settings in new[] { TestBoards.NoSpawn, TestBoards.WithSpawn })
            {
                for (uint seed = 1; seed <= 200; seed++)
                {
                    var fresh = new Unity.Mathematics.Random(seed);
                    var expected = TestBoards.ColorFromRoll(fresh.NextInt(0, 3)); // 0 Red, 1 Green, 2 Blue

                    using (var run = StepRun.Execute(GameMode.Standard, CentreIndicator3x3, TestBoards.Right, settings, seed))
                    {
                        Assert.AreEqual(expected, run.Board[1, 1].Color, "seed " + seed + " spawn=" + settings.SpawnEnabled);
                        Assert.AreEqual(BlockSize.Small, run.Board[1, 1].Size);
                        Assert.IsFalse(run.Board[1, 1].IsIndicator);
                        var resolved = run.Of(SimEventType.IndicatorResolved);
                        Assert.AreEqual(1, resolved.Count);
                        Assert.AreEqual(expected, resolved[0].Color, "event colour, seed " + seed);
                        Assert.AreEqual(new int2(1, 1), resolved[0].Position);
                    }
                }
            }
        }

        [Test]
        public void Pins_CC020_SpawnPositionIsDrawnFromTheSameUnadvancedStateAsTheColourRoll()
        {
            int checkedSeeds = 0;
            for (uint seed = 1; seed <= 200; seed++)
            {
                var replay = TestBoards.ReplaySpawn(seed, 3, 3, c => !(c.x == 1 && c.y == 1));
                Assert.IsTrue(replay.Found, "test setup: 8 of 9 cells free, 18 tries (seed " + seed + ")");

                using (var run = StepRun.Execute(GameMode.Standard, CentreIndicator3x3, TestBoards.Right, TestBoards.WithSpawn, seed))
                {
                    Assert.IsFalse(run.Result.SpawnFailed, "seed " + seed);
                    var spawned = run.Of(SimEventType.IndicatorSpawned);
                    Assert.AreEqual(1, spawned.Count, "seed " + seed);
                    Assert.AreEqual(replay.Cell, spawned[0].Position,
                        "seed " + seed + ": spawn cell must come from tries drawn from the fresh Random(seed), not from a state advanced by the colour roll");

                    // The colour roll and the first x try are both NextInt(0, 3) on the same un-advanced state, so they are equal.
                    if (!(replay.FirstX == 1 && replay.FirstY == 1))
                    {
                        checkedSeeds++;
                        int colourIndex = run.Board[1, 1].Color == BlockColor.Red ? 0 : run.Board[1, 1].Color == BlockColor.Green ? 1 : 2;
                        Assert.AreEqual(colourIndex, spawned[0].Position.x, "seed " + seed + ": colour index equals the first x try");
                        Assert.AreEqual(replay.FirstX, spawned[0].Position.x);
                    }
                }
            }
            Assert.Greater(checkedSeeds, 100, "test setup: most seeds should land their first try on a free cell");
        }

        [Test]
        public void Pins_CC020_SpawnFirstXTryEqualsNewRandomSeedNextInt0ToWidth()
        {
            // 5-wide, 2-high board with the indicator in the middle of the bottom row and everything else free.
            for (uint seed = 1; seed <= 100; seed++)
            {
                var fresh = new Unity.Mathematics.Random(seed);
                int firstX = fresh.NextInt(0, 5);
                int firstY = fresh.NextInt(0, 2);
                if (firstX == 2 && firstY == 0) continue; // first try would hit the resolved block; the spawn moves on to a later try

                using (var run = StepRun.Execute(GameMode.Standard, ". . . . .|. . i . .", TestBoards.Right, TestBoards.WithSpawn, seed))
                {
                    var spawned = run.Of(SimEventType.IndicatorSpawned);
                    Assert.AreEqual(1, spawned.Count);
                    Assert.AreEqual(new int2(firstX, firstY), spawned[0].Position, "seed " + seed + ": first try is (x then y) from the fresh state");
                }
            }
        }

        [Test]
        public void Pins_CC020_RandomStateAfterAStepIsAdvancedOnlyByTheSpawnTries_NotByTheColourRoll()
        {
            for (uint seed = 1; seed <= 100; seed++)
            {
                var replay = TestBoards.ReplaySpawn(seed, 3, 3, c => !(c.x == 1 && c.y == 1));
                using (var run = StepRun.Execute(GameMode.Standard, CentreIndicator3x3, TestBoards.Right, TestBoards.WithSpawn, seed))
                {
                    Assert.AreEqual(replay.FinalState, run.Random.state,
                        "seed " + seed + ": final state equals a fresh Random after only the spawn's draws");
                }
            }
        }

        [Test]
        public void Pins_CC020_StepWithNoIndicatorAndSpawningDisabled_LeavesRandomUntouched()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs . . .", TestBoards.Right, TestBoards.NoSpawn, 77u))
                Assert.AreEqual(run.InitialState, run.Random.state);
        }

        // ------------------------------------------------------------------ CC-021

        // 6x6 board of alternating Rs/Gs (nothing can merge in any mode) with (0,0) cleared. A Right move changes
        // nothing: row 0 starts with the gap, every block in it is blocked by a neighbour it cannot merge with.
        static BoardState SixBySixWithOneGapAtOrigin()
        {
            var board = TestBoards.Checkerboard(6, 6);
            board[0, 0] = Block.Empty;
            return board;
        }

        /// <summary>Index (0-based) of the first try that hits the free cell, or -1 if none within maxTries.</summary>
        static int FirstHitIndex(uint seed, int maxTries)
        {
            var r = new Unity.Mathematics.Random(seed);
            for (int i = 0; i < maxTries; i++)
            {
                int x = r.NextInt(0, 6);
                int y = r.NextInt(0, 6);
                if (x == 0 && y == 0) return i;
            }
            return -1;
        }

        static uint FindSeed(System.Func<int, bool> predicateOnFirstHitIndex)
        {
            for (uint seed = 1; seed <= 20000; seed++)
                if (predicateOnFirstHitIndex(FirstHitIndex(seed, 600))) return seed;
            return 0;
        }

        [Test]
        public void Pins_CC021_Spawn_CanFailWhileAnEmptyCellRemains_OnASixBySixBoard()
        {
            // Seed for which none of the 72 tries (6*6*2) hits the single free cell.
            uint seed = FindSeed(hit => hit < 0 || hit >= 72);
            Assert.AreNotEqual(0u, seed, "test setup: a failing seed exists in 1..20000");

            using (var run = StepRun.ExecuteOn(GameMode.Standard, SixBySixWithOneGapAtOrigin(), TestBoards.Right, TestBoards.WithSpawn, seed))
            {
                Assert.IsTrue(run.Result.Accepted);
                Assert.IsTrue(run.Result.SpawnFailed, "the bounded tries missed the only free cell");
                Assert.AreEqual(0, run.Of(SimEventType.IndicatorSpawned).Count);
                Assert.IsTrue(run.Board[0, 0].IsEmpty, "the cell stays empty");
                Assert.AreEqual(0, TestBoards.CountIndicators(run.Board));
                Assert.AreEqual(35, TestBoards.CountOccupied(run.Board));
                Assert.IsFalse(run.Result.GameOver, "an empty cell remains, so it is not game over");
                Assert.AreEqual(0, run.Of(SimEventType.GameOver).Count);

                // 72 tries of (x, y) were drawn and the random was advanced by all of them.
                var r = new Unity.Mathematics.Random(seed);
                for (int i = 0; i < 72; i++)
                {
                    r.NextInt(0, 6);
                    r.NextInt(0, 6);
                }
                Assert.AreEqual(r.state, run.Random.state, "random advanced by exactly 72 x/y tries");
            }
        }

        [Test]
        public void Pins_CC021_Spawn_SucceedsWhenOneOfTheTriesHitsTheOnlyFreeCell()
        {
            uint seed = FindSeed(hit => hit >= 0 && hit < 72);
            Assert.AreNotEqual(0u, seed, "test setup: a succeeding seed exists in 1..20000");

            using (var run = StepRun.ExecuteOn(GameMode.Standard, SixBySixWithOneGapAtOrigin(), TestBoards.Right, TestBoards.WithSpawn, seed))
            {
                Assert.IsTrue(run.Result.Accepted);
                Assert.IsFalse(run.Result.SpawnFailed);
                var spawned = run.Of(SimEventType.IndicatorSpawned);
                Assert.AreEqual(1, spawned.Count);
                Assert.AreEqual(new int2(0, 0), spawned[0].Position);
                Assert.IsTrue(run.Board[0, 0] == Block.Indicator);
                Assert.IsTrue(run.Result.GameOver, "the board is now full and the indicator never merges");
                Assert.AreEqual(1, run.Of(SimEventType.GameOver).Count);
            }
        }

        [Test]
        public void Pins_CC021_Spawn_LastPermittedTryIsNumber72_ASeedHittingOnTry72Succeeds()
        {
            uint seed = FindSeed(hit => hit == 71);
            Assert.AreNotEqual(0u, seed, "test setup: a seed whose first hit is the 72nd try exists in 1..20000");

            using (var run = StepRun.ExecuteOn(GameMode.Standard, SixBySixWithOneGapAtOrigin(), TestBoards.Right, TestBoards.WithSpawn, seed))
            {
                Assert.IsFalse(run.Result.SpawnFailed, "try 72 (index 71) is still within Width*Height*2");
                Assert.IsTrue(run.Board[0, 0] == Block.Indicator);
            }
        }

        [Test]
        public void Pins_CC021_Spawn_TryNumber73IsOneTooLate_ASeedFirstHittingThereFails()
        {
            uint seed = FindSeed(hit => hit == 72);
            Assert.AreNotEqual(0u, seed, "test setup: a seed whose first hit is the 73rd try exists in 1..20000");

            using (var run = StepRun.ExecuteOn(GameMode.Standard, SixBySixWithOneGapAtOrigin(), TestBoards.Right, TestBoards.WithSpawn, seed))
            {
                Assert.IsTrue(run.Result.SpawnFailed, "only Width*Height*2 = 72 tries are made");
                Assert.IsTrue(run.Board[0, 0].IsEmpty);
            }
        }

        [Test]
        public void ReplayHelper_FailureRateIsRoughlyThirteenPercentForOneEmptyCellOnSixBySix()
        {
            int failures = 0;
            const int seeds = 3000;
            for (uint seed = 1; seed <= seeds; seed++)
            {
                int hit = FirstHitIndex(seed, 72);
                if (hit < 0) failures++;
            }
            // The wiki says about 13% ((35/36)^72); this only guards the replay helper used by the tests above.
            Assert.That(failures / (double)seeds, Is.InRange(0.08, 0.18));
        }
    }
}
