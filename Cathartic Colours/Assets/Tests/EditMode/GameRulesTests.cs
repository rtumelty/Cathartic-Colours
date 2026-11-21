// Unit tests for the rule families and GameRules (CC-008.2, ADR-005).
// The recorded games (RecordedGamesTests) prove whole games are unchanged; these pin each rule on its own.
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Simulation.Tests
{
    public class GameRulesTests
    {
        // ------------------------------------------------------------------ GameRules.For

        [TestCase(GameMode.Standard, MergeRuleKind.Standard)]
        [TestCase(GameMode.ColorMerge, MergeRuleKind.ColorMerge)]
        [TestCase(GameMode.AdvancedColorMerge, MergeRuleKind.AdvancedColorMerge)]
        [TestCase((GameMode)99, MergeRuleKind.Standard)]
        public void For_PicksTheModesMergeRule_AndTheSharedRulesForEverythingElse(GameMode mode, MergeRuleKind merge)
        {
            var rules = GameRules.For(mode, StepSettings.Default);

            Assert.AreEqual(merge, rules.Merge);
            Assert.AreEqual(SpawnRuleKind.UniformPrimary, rules.Spawn);
            Assert.AreEqual(ScoringRuleKind.TierPoints, rules.Scoring);
            Assert.AreEqual(EndConditionKind.NoMovesLeft, rules.End);
            Assert.AreEqual(GridMutationKind.None, rules.Grid);
            Assert.AreEqual(TimerRuleKind.None, rules.Timer);
        }

        [Test]
        public void For_SpawnToggleOff_UsesNoSpawn()
        {
            Assert.AreEqual(SpawnRuleKind.None, GameRules.For(GameMode.ColorMerge, TestBoards.NoSpawn).Spawn);
        }

        [Test]
        public void For_CopiesTheTierPoints()
        {
            var settings = StepSettings.Default;
            settings.Points = new TierPoints { Tier1 = 1, Tier2 = 2, Tier3 = 3, Tier4 = 4 };

            var points = GameRules.For(GameMode.Standard, settings).Points;

            Assert.AreEqual(new[] { 1, 2, 3, 4 }, new[] { points.Tier1, points.Tier2, points.Tier3, points.Tier4 });
        }

        // ------------------------------------------------------------------ spawn rules

        [Test]
        public void UniformPrimarySpawn_Resolve_DrawsOncePerIndicatorInIndexOrder()
        {
            // Indicators at index 1 and 6 on a 4x2 board ("first row listed is the top row").
            using (var board = TestBoards.Parse(". . i .|. i . ."))
            {
                var b = board;
                var events = new NativeList<SimEvent>(8, Allocator.Temp);
                try
                {
                    var random = new Random(21u);
                    var expected = new Random(21u);
                    var first = TestBoards.ColorFromRoll(expected.NextInt(0, 3));
                    var second = TestBoards.ColorFromRoll(expected.NextInt(0, 3));

                    new UniformPrimarySpawn().ResolveIndicators(ref b, ref random, events);

                    Assert.AreEqual(Block.Of(first, BlockSize.Small), b[1, 0], "index 1 resolves first");
                    Assert.AreEqual(Block.Of(second, BlockSize.Small), b[2, 1], "index 6 resolves second");
                    Assert.AreEqual(expected.state, random.state, "exactly one draw per indicator");
                    Assert.AreEqual(2, events.Length);
                    Assert.AreEqual(SimEventType.IndicatorResolved, events[0].Type);
                    Assert.AreEqual(new int2(1, 0), events[0].Position);
                }
                finally
                {
                    events.Dispose();
                }
            }
        }

        [Test]
        public void UniformPrimarySpawn_Spawn_OneDrawAmongEmptyCellsInIndexOrder()
        {
            using (var board = TestBoards.Parse("Rs . Gs .|. Bs . ."))
            {
                var b = board;
                var events = new NativeList<SimEvent>(8, Allocator.Temp);
                try
                {
                    var random = new Random(5u);
                    var replay = TestBoards.ReplaySpawn(5u, 4, 2, p => b[p].IsEmpty);

                    bool failed = new UniformPrimarySpawn().Spawn(ref b, ref random, events);

                    Assert.IsFalse(failed);
                    Assert.IsTrue(b[replay.Cell].IsIndicator, "indicator lands on the replayed cell");
                    Assert.AreEqual(replay.FinalState, random.state, "exactly one draw");
                    Assert.AreEqual(1, events.Length);
                    Assert.AreEqual(SimEventType.IndicatorSpawned, events[0].Type);
                    Assert.AreEqual(replay.Cell, events[0].Position);
                }
                finally
                {
                    events.Dispose();
                }
            }
        }

        [Test]
        public void UniformPrimarySpawn_Spawn_FullBoard_FailsWithoutDrawing()
        {
            using (var board = TestBoards.Parse("Rs Gs|Bs Rs"))
            {
                var b = board;
                var events = new NativeList<SimEvent>(8, Allocator.Temp);
                try
                {
                    var random = new Random(5u);
                    uint before = random.state;

                    Assert.IsTrue(new UniformPrimarySpawn().Spawn(ref b, ref random, events));
                    Assert.AreEqual(before, random.state);
                    Assert.AreEqual(0, events.Length);
                }
                finally
                {
                    events.Dispose();
                }
            }
        }

        [Test]
        public void NoSpawn_ResolvesLikeUniformPrimary_ButNeverSpawnsOrDraws()
        {
            using (var viaNoSpawn = TestBoards.Parse("i . . .|. . . i"))
            using (var viaUniform = TestBoards.Parse("i . . .|. . . i"))
            {
                var a = viaNoSpawn;
                var u = viaUniform;
                var events = new NativeList<SimEvent>(8, Allocator.Temp);
                try
                {
                    var randomA = new Random(77u);
                    var randomU = new Random(77u);
                    new NoSpawn().ResolveIndicators(ref a, ref randomA, events);
                    new UniformPrimarySpawn().ResolveIndicators(ref u, ref randomU, events);
                    Assert.AreEqual(TestBoards.Format(u), TestBoards.Format(a));
                    Assert.AreEqual(randomU.state, randomA.state);

                    events.Clear();
                    uint before = randomA.state;
                    Assert.IsFalse(new NoSpawn().Spawn(ref a, ref randomA, events), "nothing was due, so nothing failed");
                    Assert.AreEqual(before, randomA.state);
                    Assert.AreEqual(0, events.Length);
                    Assert.AreEqual(0, TestBoards.CountIndicators(a));
                }
                finally
                {
                    events.Dispose();
                }
            }
        }

        [Test]
        public void SpawnRules_DispatchToTheNamedRule()
        {
            using (var board = TestBoards.Parse(". . . ."))
            {
                var b = board;
                var events = new NativeList<SimEvent>(8, Allocator.Temp);
                try
                {
                    var random = new Random(3u);
                    Assert.IsFalse(SpawnRules.Spawn(SpawnRuleKind.None, ref b, ref random, events));
                    Assert.AreEqual(0, TestBoards.CountIndicators(b));

                    Assert.IsFalse(SpawnRules.Spawn(SpawnRuleKind.UniformPrimary, ref b, ref random, events));
                    Assert.AreEqual(1, TestBoards.CountIndicators(b));
                }
                finally
                {
                    events.Dispose();
                }
            }
        }

        // ------------------------------------------------------------------ scoring

        [TestCase(ScoreTier.None, 0)]
        [TestCase(ScoreTier.Tier1, 3)]
        [TestCase(ScoreTier.Tier2, 5)]
        [TestCase(ScoreTier.Tier3, 7)]
        [TestCase(ScoreTier.Tier4, 9)]
        public void TierPointsScoring_ScoresTheTiersPoints(ScoreTier tier, int points)
        {
            var tierPoints = new TierPoints { Tier1 = 3, Tier2 = 5, Tier3 = 7, Tier4 = 9 };
            var merge = new MergeResult(BlockColor.Red, BlockSize.Medium, tier);

            Assert.AreEqual(points, new TierPointsScoring(tierPoints).PointsFor(merge));

            var settings = StepSettings.Default;
            settings.Points = tierPoints;
            Assert.AreEqual(points, ScoringRules.PointsFor(GameRules.For(GameMode.Standard, settings), merge), "via GameRules");
        }

        // ------------------------------------------------------------------ end condition

        [TestCase(GameMode.Standard, "Rs Gs|Gs Rs", true)]
        [TestCase(GameMode.Standard, "Rs Rs|Gs Bs", false)]
        [TestCase(GameMode.Standard, "Rs .|Gs Bs", false)]
        [TestCase(GameMode.AdvancedColorMerge, "Rm Rm|Rm Rm", true)]
        [TestCase(GameMode.AdvancedColorMerge, "Rm Bm|Bm Rm", false)]
        public void NoMovesLeftEnd_MatchesBoardSimulationIsGameOver(GameMode mode, string text, bool over)
        {
            using (var board = TestBoards.Parse(text))
            {
                var rules = GameRules.For(mode, StepSettings.Default);
                Assert.AreEqual(over, BoardSimulation.IsGameOver(board, rules), "via GameRules");

                if (mode == GameMode.Standard)
                    Assert.AreEqual(over, new NoMovesLeftEnd().IsGameOver(board, new StandardMergeRules()));
                else
                    Assert.AreEqual(over, new NoMovesLeftEnd().IsGameOver(board, new AdvancedColorMergeRules()));
            }
        }

        // ------------------------------------------------------------------ the "none" rules

        [Test]
        public void NoGridMutation_ChangesNothingAndDrawsNothing()
        {
            using (var board = TestBoards.Parse("Rs . i .|. Bm . Gl"))
            {
                var b = board;
                var events = new NativeList<SimEvent>(8, Allocator.Temp);
                try
                {
                    string before = TestBoards.Format(b);
                    var random = new Random(9u);
                    uint state = random.state;

                    GridMutations.Apply(GridMutationKind.None, ref b, ref random, events);

                    Assert.AreEqual(before, TestBoards.Format(b));
                    Assert.AreEqual(state, random.state);
                    Assert.AreEqual(0, events.Length);
                }
                finally
                {
                    events.Dispose();
                }
            }
        }

        [TestCase(GameMode.Standard)]
        [TestCase(GameMode.ColorMerge)]
        [TestCase(GameMode.AdvancedColorMerge)]
        public void Advance_WithNoTimer_ChangesNothing(GameMode mode)
        {
            using (var board = TestBoards.Parse("Rs . i .|. Bm . Gl"))
            {
                var b = board;
                var events = new NativeList<SimEvent>(8, Allocator.Temp);
                try
                {
                    string before = TestBoards.Format(b);
                    var random = new Random(9u);
                    uint state = random.state;

                    var result = BoardSimulation.Advance(ref b, 1.5f, GameRules.For(mode, StepSettings.Default), ref random, events);

                    Assert.IsFalse(result.Accepted, "time changed nothing");
                    Assert.AreEqual(0, result.ScoreDelta);
                    Assert.IsFalse(result.GameOver);
                    Assert.AreEqual(before, TestBoards.Format(b));
                    Assert.AreEqual(state, random.state);
                    Assert.AreEqual(0, events.Length);
                }
                finally
                {
                    events.Dispose();
                }
            }
        }

        // ------------------------------------------------------------------ step phases

        [Test]
        public void Step_ResolvesBeforeSpawning_SoTheSpawnDrawsFromTheStateAfterTheColourRoll()
        {
            // One indicator, spawn on: colour roll first, then the spawn's cell draw (D4 order, CC-020).
            using (var run = StepRun.Execute(GameMode.Standard, ". . . .|i . . .", TestBoards.Up, TestBoards.WithSpawn, 41u))
            {
                var resolved = run.Of(SimEventType.IndicatorResolved);
                var spawned = run.Of(SimEventType.IndicatorSpawned);
                Assert.AreEqual(1, resolved.Count);
                Assert.AreEqual(1, spawned.Count);

                var replay = TestBoards.ReplaySpawn(41u, 4, 2, p => !(p.x == 0 && p.y == 0), colourDraws: 1);
                Assert.AreEqual(replay.Cell, spawned[0].Position);
                Assert.AreEqual(replay.FinalState, run.Random.state);

                int resolvedIndex = -1, spawnedIndex = -1;
                for (int i = 0; i < run.Events.Length; i++)
                {
                    if (run.Events[i].Type == SimEventType.IndicatorResolved) resolvedIndex = i;
                    if (run.Events[i].Type == SimEventType.IndicatorSpawned) spawnedIndex = i;
                }
                Assert.Less(resolvedIndex, spawnedIndex, "resolve events come before the spawn event");
            }
        }
    }
}
