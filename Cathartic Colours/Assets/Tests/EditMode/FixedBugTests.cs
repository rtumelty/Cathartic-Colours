// Regression tests for the two bugs fixed in CC-006.4 (they pinned the old behaviour in CC-006.2/CC-006.3).
//
//  CC-020: the colour roll now advances the stored random state, so the spawn that follows draws from the next
//          state: colour and spawn position come from successive draws and are no longer correlated.
//  CC-021: the spawn counts the empty cells and makes exactly one draw NextInt(0, emptyCount), placing the
//          indicator on that empty cell (cells counted in index order, x + y * Width). It fails only when the
//          board has no empty cell.
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Simulation.Tests
{
    public class FixedBugTests
    {
        const string CentreIndicator3x3 = ". . .|. i .|. . .";

        static int ColourIndex(Block b) =>
            b.Color == BlockColor.Red ? 0 : b.Color == BlockColor.Green ? 1 : 2;

        // ------------------------------------------------------------------ CC-020

        [Test]
        public void Fixes_CC020_SpawningDisabled_ColourRollAdvancesTheStoredRandomByExactlyOneDraw()
        {
            for (uint seed = 1; seed <= 25; seed++)
            {
                using (var run = StepRun.Execute(GameMode.Standard, "i . . .", TestBoards.Right, TestBoards.NoSpawn, seed))
                {
                    Assert.AreEqual(1, run.Of(SimEventType.IndicatorResolved).Count, "precondition: an indicator was resolved (seed " + seed + ")");
                    var replay = new Unity.Mathematics.Random(seed);
                    replay.NextInt(0, 3);
                    Assert.AreEqual(replay.state, run.Random.state, "seed " + seed + ": exactly one NextInt(0, 3) draw");
                    Assert.AreNotEqual(run.InitialState, run.Random.state, "seed " + seed);
                }
            }
        }

        [Test]
        public void Fixes_CC020_ResolvedColourIsTheFirstDrawAndSpawnCellIsTheSecondDraw()
        {
            for (uint seed = 1; seed <= 200; seed++)
            {
                var fresh = new Unity.Mathematics.Random(seed);
                var expectedColour = TestBoards.ColorFromRoll(fresh.NextInt(0, 3));
                var replay = TestBoards.ReplaySpawn(seed, 3, 3, c => !(c.x == 1 && c.y == 1), colourDraws: 1);
                Assert.IsTrue(replay.Found);

                using (var run = StepRun.Execute(GameMode.Standard, CentreIndicator3x3, TestBoards.Right, TestBoards.WithSpawn, seed))
                {
                    Assert.AreEqual(expectedColour, run.Board[1, 1].Color, "seed " + seed);
                    Assert.AreEqual(BlockSize.Small, run.Board[1, 1].Size);
                    var resolved = run.Of(SimEventType.IndicatorResolved);
                    Assert.AreEqual(1, resolved.Count);
                    Assert.AreEqual(expectedColour, resolved[0].Color, "event colour, seed " + seed);
                    Assert.AreEqual(new int2(1, 1), resolved[0].Position);

                    Assert.IsFalse(run.Result.SpawnFailed, "seed " + seed);
                    var spawned = run.Of(SimEventType.IndicatorSpawned);
                    Assert.AreEqual(1, spawned.Count, "seed " + seed);
                    Assert.AreEqual(replay.Cell, spawned[0].Position, "seed " + seed + ": spawn cell from the second draw");
                    Assert.AreEqual(replay.FinalState, run.Random.state, "seed " + seed + ": two draws in total");
                }
            }
        }

        [Test]
        public void Fixes_CC020_RandomStateAfterAStepDiffersFromTheStateAfterTheColourRollAlone()
        {
            for (uint seed = 1; seed <= 100; seed++)
            {
                var afterColourRoll = new Unity.Mathematics.Random(seed);
                afterColourRoll.NextInt(0, 3);

                using (var run = StepRun.Execute(GameMode.Standard, CentreIndicator3x3, TestBoards.Right, TestBoards.WithSpawn, seed))
                    Assert.AreNotEqual(afterColourRoll.state, run.Random.state, "seed " + seed + ": the spawn drew as well");
            }
        }

        [Test]
        public void Fixes_CC020_ColourAndSpawnPositionAreNotCorrelated()
        {
            // Old behaviour: spawn x came from the same draw as the colour index, so colour index == spawn x always.
            int equal = 0;
            const int seeds = 200;
            for (uint i = 0; i < seeds; i++)
            {
                uint seed = (i + 1) * 2654435761u;
                using (var run = StepRun.Execute(GameMode.Standard, CentreIndicator3x3, TestBoards.Right, TestBoards.WithSpawn, seed))
                {
                    var spawned = run.Of(SimEventType.IndicatorSpawned)[0].Position;
                    if (ColourIndex(run.Board[1, 1]) == spawned.x) equal++;
                }
            }
            Assert.Less(equal, seeds, "colour index must not always equal the spawn column");
        }

        [Test]
        public void StepWithNoIndicatorAndSpawningDisabled_LeavesRandomUntouched()
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

        [Test]
        public void Fixes_CC021_SixBySixBoardWithOneEmptyCell_AlwaysSpawnsIntoThatCell()
        {
            for (uint i = 0; i < 1000; i++)
            {
                uint seed = (i + 1) * 2654435761u;
                using (var run = StepRun.ExecuteOn(GameMode.Standard, SixBySixWithOneGapAtOrigin(), TestBoards.Right, TestBoards.WithSpawn, seed))
                {
                    Assert.IsTrue(run.Result.Accepted, "seed " + seed);
                    Assert.IsFalse(run.Result.SpawnFailed, "seed " + seed);
                    var spawned = run.Of(SimEventType.IndicatorSpawned);
                    Assert.AreEqual(1, spawned.Count, "seed " + seed);
                    Assert.AreEqual(new int2(0, 0), spawned[0].Position, "seed " + seed);
                    Assert.IsTrue(run.Board[0, 0] == Block.Indicator, "seed " + seed);
                    Assert.AreEqual(1, TestBoards.CountIndicators(run.Board));
                    Assert.AreEqual(36, TestBoards.CountOccupied(run.Board));
                    Assert.IsTrue(run.Result.GameOver, "the board is now full and the indicator never merges");
                }
            }
        }

        [Test]
        public void Fixes_CC021_FullBoardWithAMergeablePair_ReportsSpawnFailed_AndSpawnsNothing()
        {
            // Full board, the two reds in the top row can merge in Standard, so it is not game over. Moving up
            // changes nothing (everything is against the top wall or cannot merge with the block above it).
            using (var run = StepRun.Execute(GameMode.Standard, "Rs Rs|Gs Bs", TestBoards.Up, TestBoards.WithSpawn, 5u))
            {
                Assert.IsTrue(run.Result.Accepted);
                Assert.IsTrue(run.Result.SpawnFailed);
                Assert.AreEqual(0, run.Of(SimEventType.IndicatorSpawned).Count);
                Assert.AreEqual(0, TestBoards.CountIndicators(run.Board));
                Assert.IsFalse(run.Result.GameOver, "a mergeable pair remains");
                Assert.AreEqual(0, run.Of(SimEventType.GameOver).Count);
                Assert.AreEqual(run.InitialState, run.Random.state, "no draw is made when there is no empty cell");
            }
        }

        [Test]
        public void Fixes_CC021_SpawnFailedIsFalseWhenSpawningIsDisabled_EvenOnAFullBoard()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs Rs|Gs Bs", TestBoards.Up, TestBoards.NoSpawn, 5u))
                Assert.IsFalse(run.Result.SpawnFailed);
        }

        [Test]
        public void Fixes_CC021_EveryEmptyCellOfASmallBoardIsReachableOverManySeeds()
        {
            // A Left move on "Rs Gs . . ." moves nothing (Rs is at the wall, Gs cannot merge into it), so the free cells stay x = 2..4.
            var seen = new HashSet<int>();
            for (uint i = 0; i < 300; i++)
            {
                uint seed = (i + 1) * 2654435761u;
                using (var run = StepRun.Execute(GameMode.Standard, "Rs Gs . . .", TestBoards.Left, TestBoards.WithSpawn, seed))
                {
                    var spawned = run.Of(SimEventType.IndicatorSpawned);
                    Assert.AreEqual(1, spawned.Count);
                    Assert.AreEqual(0, spawned[0].Position.y);
                    Assert.GreaterOrEqual(spawned[0].Position.x, 2);
                    seen.Add(spawned[0].Position.x);
                }
            }
            CollectionAssert.AreEquivalent(new[] { 2, 3, 4 }, seen);
        }

        [Test]
        public void Fixes_CC021_SpawnMakesExactlyOneDraw_AndPicksTheEmptyCellInIndexOrder()
        {
            const string board = ". . . Rs|. . . Gs|. . . Bs|. . . Rs"; // free cells are exactly x < 3, a Right move changes nothing
            // Index order x + y * 4 over the cells with x < 3.
            int[] freeIndices = { 0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14 };
            for (uint i = 0; i < 200; i++)
            {
                uint seed = (i + 1) * 2654435761u;
                var replay = new Unity.Mathematics.Random(seed);
                int index = freeIndices[replay.NextInt(0, freeIndices.Length)];
                var expected = new int2(index % 4, index / 4);

                using (var run = StepRun.Execute(GameMode.Standard, board, TestBoards.Right, TestBoards.WithSpawn, seed))
                {
                    var spawned = run.Of(SimEventType.IndicatorSpawned);
                    Assert.AreEqual(1, spawned.Count, "seed " + seed);
                    Assert.AreEqual(expected, spawned[0].Position, "seed " + seed);
                    Assert.AreEqual(replay.state, run.Random.state, "seed " + seed + ": exactly one draw");
                }
            }
        }
    }
}
