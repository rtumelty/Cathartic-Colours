// Written from docs/wiki/systems/merge-rules.md ("Spawning") and the CC-006.2 API contract,
// without reading the simulation implementation.
//
// Spawn positions are predicted by replaying the spec's tries on a fresh Unity.Mathematics.Random(seed)
// (see TestBoards.ReplaySpawn), so the tests do not depend on what the implementation does internally.
using NUnit.Framework;
using Unity.Mathematics;

namespace Simulation.Tests
{
    public class StepResolveSpawnTests
    {
        // ------------------------------------------------------------------ resolve

        [Test]
        public void Resolve_IndicatorBecomesSmallPrimary_AndLosesIndicatorFlag()
        {
            for (uint seed = 1; seed <= 30; seed++)
            {
                using (var run = StepRun.Execute(GameMode.Standard, ". . . .|. i . .", TestBoards.Right, TestBoards.NoSpawn, seed))
                {
                    var block = run.Board[1, 0];
                    Assert.IsFalse(block.IsIndicator, "seed " + seed + ": indicator flag must be cleared");
                    Assert.AreEqual(BlockSize.Small, block.Size, "seed " + seed);
                    CollectionAssert.Contains(new[] { BlockColor.Red, BlockColor.Green, BlockColor.Blue }, block.Color, "seed " + seed);
                    Assert.AreEqual(0, TestBoards.CountIndicators(run.Board), "seed " + seed + ": no indicator remains (spawning off)");
                }
            }
        }

        [Test]
        public void Resolve_AllThreePrimariesAreReachable()
        {
            var seen = new System.Collections.Generic.HashSet<BlockColor>();
            // Spread the seeds: Unity.Mathematics.Random's first draw from a small seed is small, so seeds
            // 1..200 all roll index 0 (Red). The game seeds from DateTime ticks, which are large.
            for (uint i = 1; i <= 200; i++)
            {
                uint seed = i * 2654435761u;
                using (var run = StepRun.Execute(GameMode.Standard, ". i .", TestBoards.Right, TestBoards.NoSpawn, seed))
                    seen.Add(run.Board[1, 0].Color);
            }
            CollectionAssert.AreEquivalent(new[] { BlockColor.Red, BlockColor.Green, BlockColor.Blue }, seen);
        }

        [Test]
        public void Resolve_RaisesIndicatorResolvedEvent_WithCellAndRolledColour()
        {
            using (var run = StepRun.Execute(GameMode.Standard, ". . . .|. i . .", TestBoards.Right, TestBoards.NoSpawn, 5u))
            {
                var resolved = run.Of(SimEventType.IndicatorResolved);
                Assert.AreEqual(1, resolved.Count);
                Assert.AreEqual(new int2(1, 0), resolved[0].Position);
                Assert.AreEqual(run.Board[1, 0].Color, resolved[0].Color, "event colour is the colour the block became");
            }
        }

        [Test]
        public void Resolve_EveryIndicatorOnTheBoardIsResolved()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "i . i .", TestBoards.Right, TestBoards.NoSpawn, 3u))
            {
                TestBoards.AssertBoard("? . ? .", run.Board);
                Assert.AreEqual(2, run.Of(SimEventType.IndicatorResolved).Count);
                Assert.AreEqual(0, TestBoards.CountIndicators(run.Board));
            }
        }

        [TestCase(1, 0, TestName = "Resolve_DoesNotMoveOnItsResolveMove_Right")]
        [TestCase(-1, 0, TestName = "Resolve_DoesNotMoveOnItsResolveMove_Left")]
        [TestCase(0, 1, TestName = "Resolve_DoesNotMoveOnItsResolveMove_Up")]
        [TestCase(0, -1, TestName = "Resolve_DoesNotMoveOnItsResolveMove_Down")]
        public void Resolve_NewlyResolvedBlockDoesNotMoveOnTheMoveThatResolvesIt(int dx, int dy)
        {
            using (var run = StepRun.Execute(GameMode.Standard, ". . .|. i .|. . .", new int2(dx, dy), TestBoards.NoSpawn, 9u))
            {
                TestBoards.AssertBoard(". . .|. ? .|. . .", run.Board);
                Assert.AreEqual(0, run.Of(SimEventType.Moved).Count);
            }
        }

        [Test]
        public void Resolve_NewlyResolvedBlockDoesNotMergeOnTheMoveThatResolvesIt()
        {
            // Standard: a resolved Small could match the Small at x3 if it were allowed to move this step.
            // It must stay put (and the indicator behind a mover is not a mover).
            for (uint seed = 1; seed <= 40; seed++)
            {
                using (var run = StepRun.Execute(GameMode.Standard, ". . i Rs", TestBoards.Right, TestBoards.NoSpawn, seed))
                {
                    Assert.AreEqual(0, run.Of(SimEventType.Merged).Count, "seed " + seed);
                    Assert.AreEqual("Rs", TestBoards.FormatBlock(run.Board[3, 0]), "seed " + seed);
                    Assert.IsTrue(TestBoards.IsResolvedPrimary(run.Board[2, 0]), "seed " + seed);
                }
            }
        }

        [Test]
        public void Resolve_NoIndicator_NothingResolved()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs . . .", TestBoards.Right, TestBoards.NoSpawn))
                Assert.AreEqual(0, run.Of(SimEventType.IndicatorResolved).Count);
        }

        // ------------------------------------------------------------------ spawn disabled

        [Test]
        public void SpawnDisabled_NoIndicatorIsSpawned_AndSpawnFailedIsFalse()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "i . . .", TestBoards.Right, TestBoards.NoSpawn, 11u))
            {
                Assert.AreEqual(0, run.Of(SimEventType.IndicatorSpawned).Count);
                Assert.AreEqual(0, TestBoards.CountIndicators(run.Board));
                Assert.AreEqual(1, TestBoards.CountOccupied(run.Board), "only the resolved block is on the board");
                Assert.IsFalse(run.Result.SpawnFailed, "spawning was not attempted, so it cannot have failed");
            }
        }

        [Test]
        public void SpawnDisabled_SingleIndicatorResolvesOnFirstMove_ThenNothingEverAppears()
        {
            var settings = TestBoards.NoSpawn;
            var board = TestBoards.Parse("i . . .");
            var events = new Unity.Collections.NativeList<SimEvent>(Unity.Collections.Allocator.Persistent);
            try
            {
                var random = new Unity.Mathematics.Random(13u);
                var first = MergeRulesFactory.Step(GameMode.Standard, ref board, TestBoards.Right, in settings, ref random, events);
                Assert.IsTrue(first.Accepted);
                Assert.IsTrue(TestBoards.IsResolvedPrimary(board[0, 0]), "resolved on the first move");

                for (int i = 0; i < 3; i++)
                {
                    events.Clear();
                    var next = MergeRulesFactory.Step(GameMode.Standard, ref board, TestBoards.Right, in settings, ref random, events);
                    Assert.IsTrue(next.Accepted);
                    Assert.AreEqual(1, TestBoards.CountOccupied(board), "still exactly one block after move " + (i + 2));
                    Assert.AreEqual(0, TestBoards.CountIndicators(board));
                }
                TestBoards.AssertBoard(". . . ?", board, "the lone block marches to the wall one cell per move");
            }
            finally
            {
                events.Dispose();
                board.Dispose();
            }
        }

        // ------------------------------------------------------------------ spawn enabled

        // Column x = 3 holds four blocks that cannot move or merge (wall on the right, different colours),
        // so a Right move leaves the board unchanged and the free cells are exactly x < 3.
        const string WallColumnBoard = ". . . Rs|. . . Gs|. . . Bs|. . . Rs";

        [Test]
        public void Spawn_ExactlyOneIndicatorIsPlaced_OnAnEmptyCell()
        {
            for (uint seed = 1; seed <= 100; seed++)
            {
                var replay = TestBoards.ReplaySpawn(seed, 4, 4, c => c.x < 3);
                Assert.IsTrue(replay.Found, "test setup: 12 of 16 cells are free, so a 32-try search cannot fail for seed " + seed);

                using (var run = StepRun.Execute(GameMode.Standard, WallColumnBoard, TestBoards.Right, TestBoards.WithSpawn, seed))
                {
                    Assert.IsTrue(run.Result.Accepted, "seed " + seed);
                    Assert.IsFalse(run.Result.SpawnFailed, "seed " + seed);
                    var spawned = run.Of(SimEventType.IndicatorSpawned);
                    Assert.AreEqual(1, spawned.Count, "seed " + seed);
                    Assert.AreEqual(replay.Cell, spawned[0].Position, "seed " + seed + ": spawn cell from the spec's tries");
                    Assert.AreEqual(BlockColor.White, spawned[0].Color, "seed " + seed);
                    Assert.IsTrue(run.Board[spawned[0].Position] == Block.Indicator, "seed " + seed + ": the new block is Block.Indicator");
                    Assert.AreEqual(1, TestBoards.CountIndicators(run.Board), "seed " + seed);
                    Assert.AreEqual(5, TestBoards.CountOccupied(run.Board), "seed " + seed + ": 4 originals + 1 indicator");
                    Assert.Less(spawned[0].Position.x, 3, "seed " + seed + ": never on an occupied cell");
                }
            }
        }

        [Test]
        public void Spawn_OnlyFreeCellLeftByTheMove_ReceivesTheIndicator()
        {
            // Left move: Rs Gs Bs . -> nothing can move left except Bs... no: Bs at x2 is blocked by Gs; only the gap at x3 is free.
            for (uint seed = 1; seed <= 300; seed++)
            {
                var replay = TestBoards.ReplaySpawn(seed, 4, 1, c => c.x == 3);
                if (!replay.Found) continue; // bounded-retry failures are covered by the CC-021 pin tests

                using (var run = StepRun.Execute(GameMode.Standard, "Rs Gs Bs .", TestBoards.Left, TestBoards.WithSpawn, seed))
                {
                    TestBoards.AssertBoard("Rs Gs Bs i", run.Board, "seed " + seed);
                    Assert.IsFalse(run.Result.SpawnFailed);
                }
            }
        }

        [Test]
        public void Spawn_NewIndicatorNeverLandsOnTheJustResolvedBlock()
        {
            for (uint seed = 1; seed <= 200; seed++)
            {
                using (var run = StepRun.Execute(GameMode.Standard, ". . .|. i .|. . .", TestBoards.Right, TestBoards.WithSpawn, seed))
                {
                    Assert.IsTrue(TestBoards.IsResolvedPrimary(run.Board[1, 1]), "seed " + seed + ": centre stays the resolved block");
                    Assert.LessOrEqual(TestBoards.CountIndicators(run.Board), 1, "seed " + seed);
                }
            }
        }

        [Test]
        public void Spawn_IndicatorSpawnedEventMatchesTheBoard()
        {
            using (var run = StepRun.Execute(GameMode.Standard, WallColumnBoard, TestBoards.Right, TestBoards.WithSpawn, 4u))
            {
                var spawned = run.Of(SimEventType.IndicatorSpawned);
                Assert.AreEqual(1, spawned.Count);
                var cell = spawned[0].Position;
                Assert.IsTrue(run.Board.InBounds(cell));
                Assert.IsTrue(run.Board[cell].IsIndicator);
            }
        }

        [Test]
        public void Spawn_EventOrder_MovedAndMergedBeforeResolvedBeforeSpawned()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs Rs . .|. . . .|. . . .|. . . i", TestBoards.Left, TestBoards.WithSpawn, 2u))
            {
                int lastSlide = -1, resolved = -1, spawned = -1;
                for (int i = 0; i < run.Events.Length; i++)
                {
                    var t = run.Events[i].Type;
                    if (t == SimEventType.Moved || t == SimEventType.Merged) lastSlide = i;
                    else if (t == SimEventType.IndicatorResolved) resolved = i;
                    else if (t == SimEventType.IndicatorSpawned) spawned = i;
                }
                Assert.GreaterOrEqual(lastSlide, 0, "a merge happened");
                Assert.Greater(resolved, lastSlide, "resolve comes after sliding");
                if (spawned >= 0) Assert.Greater(spawned, resolved, "spawn comes after resolve");
            }
        }
    }
}
