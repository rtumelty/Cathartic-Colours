// Written from docs/wiki/systems/merge-rules.md ("Scoring") and the CC-006.2 API contract,
// without reading the simulation implementation.
//
// Determinism: the same board, direction and seed must give an identical board, event list and final random state.
// Scoring: every merge scores GameConfiguration-style tier points (default 10/50/100/200, None 0); the step's
// ScoreDelta is the sum over all merges of that step; there is no multiplier, chain or per-move bonus.
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Simulation.Tests
{
    public class StepDeterminismAndScoringTests
    {
        // ------------------------------------------------------------------ determinism

        static IEnumerable<TestCaseData> Scenarios()
        {
            yield return new TestCaseData(GameMode.Standard, "i . . .|. Rs Rs .|. . . Gs|Rs . . .", 1, 0, 17u).SetName("Determinism_Standard_Right");
            yield return new TestCaseData(GameMode.Standard, "i . . .|. Rs Rs .|. . . Gs|Rs . . .", -1, 0, 99u).SetName("Determinism_Standard_Left");
            yield return new TestCaseData(GameMode.ColorMerge, ". . . .|Rs Gs . i|. . Bs .|. . . .", 0, 1, 12345u).SetName("Determinism_ColorMerge_Up");
            yield return new TestCaseData(GameMode.AdvancedColorMerge, ". Rm Bm .|. . . .|i . . .|Ml Gm . .", 0, -1, 7u).SetName("Determinism_Advanced_Down");
            yield return new TestCaseData(GameMode.Standard, ". . . .|. . i .|. . . .|. . . .", 1, 0, 4242u).SetName("Determinism_Standard_LoneIndicator");
        }

        static void AssertSameRuns(StepRun a, StepRun b)
        {
            Assert.AreEqual(a.Result.Accepted, b.Result.Accepted, "Accepted");
            Assert.AreEqual(a.Result.ScoreDelta, b.Result.ScoreDelta, "ScoreDelta");
            Assert.AreEqual(a.Result.GameOver, b.Result.GameOver, "GameOver");
            Assert.AreEqual(a.Result.SpawnFailed, b.Result.SpawnFailed, "SpawnFailed");
            Assert.AreEqual(a.BoardText, b.BoardText, "board");
            Assert.AreEqual(a.Random.state, b.Random.state, "random state");
            CollectionAssert.AreEqual(a.EventDescriptions(), b.EventDescriptions(), "events");
        }

        [TestCaseSource(nameof(Scenarios))]
        public void SameBoardDirectionAndSeed_GiveIdenticalBoardEventsAndRandomState(GameMode mode, string board, int dx, int dy, uint seed)
        {
            using (var first = StepRun.Execute(mode, board, new int2(dx, dy), TestBoards.WithSpawn, seed))
            using (var second = StepRun.Execute(mode, board, new int2(dx, dy), TestBoards.WithSpawn, seed))
            {
                Assert.IsTrue(first.Result.Accepted);
                AssertSameRuns(first, second);
            }
        }

        [TestCaseSource(nameof(Scenarios))]
        public void GameRulesStepAndGenericStep_AgreeExactly(GameMode mode, string board, int dx, int dy, uint seed)
        {
            using (var viaGameRules = StepRun.Execute(mode, board, new int2(dx, dy), TestBoards.WithSpawn, seed))
            using (var viaGeneric = StepRun.ExecuteGeneric(mode, board, new int2(dx, dy), TestBoards.WithSpawn, seed))
                AssertSameRuns(viaGameRules, viaGeneric);
        }

        [Test]
        public void DifferentSeeds_CanProduceDifferentBoards()
        {
            var seen = new HashSet<string>();
            for (uint seed = 1; seed <= 50; seed++)
            {
                using (var run = StepRun.Execute(GameMode.Standard, ". . . .|. . i .|. . . .|. . . .", TestBoards.Right, TestBoards.WithSpawn, seed))
                    seen.Add(run.BoardText);
            }
            Assert.Greater(seen.Count, 1, "the random source must actually influence colour and spawn position");
        }

        [Test]
        public void TwoStepsInARow_ReplayExactlyFromTheSameSeed()
        {
            string Play(uint seed)
            {
                var board = TestBoards.Parse("i . . .|. . . .|. . . .|. . . .");
                var events = new Unity.Collections.NativeList<SimEvent>(Unity.Collections.Allocator.Persistent);
                try
                {
                    var random = new Unity.Mathematics.Random(seed);
                    var settings = TestBoards.WithSpawn;
                    var sb = new System.Text.StringBuilder();
                    foreach (var dir in new[] { TestBoards.Right, TestBoards.Down, TestBoards.Left, TestBoards.Up, TestBoards.Right })
                    {
                        BoardSimulation.Step(ref board, dir, GameRules.For(GameMode.Standard, settings), ref random, events);
                        sb.Append(TestBoards.Format(board)).Append(" #").Append(random.state).Append('\n');
                    }
                    for (int i = 0; i < events.Length; i++) sb.AppendLine(TestBoards.DescribeEvent(events[i]));
                    return sb.ToString();
                }
                finally
                {
                    events.Dispose();
                    board.Dispose();
                }
            }

            Assert.AreEqual(Play(31u), Play(31u));
        }

        // ------------------------------------------------------------------ scoring

        [Test]
        public void TierPoints_Defaults_AreTen_Fifty_Hundred_TwoHundred_AndNoneIsZero()
        {
            var p = TierPoints.Default;
            Assert.AreEqual(10, p.Tier1);
            Assert.AreEqual(50, p.Tier2);
            Assert.AreEqual(100, p.Tier3);
            Assert.AreEqual(200, p.Tier4);
            Assert.AreEqual(0, p.For(ScoreTier.None));
            Assert.AreEqual(10, p.For(ScoreTier.Tier1));
            Assert.AreEqual(50, p.For(ScoreTier.Tier2));
            Assert.AreEqual(100, p.For(ScoreTier.Tier3));
            Assert.AreEqual(200, p.For(ScoreTier.Tier4));
        }

        [Test]
        public void StepSettingsDefault_UsesDefaultPointsAndSpawnsEnabled()
        {
            var s = StepSettings.Default;
            Assert.IsTrue(s.SpawnEnabled);
            Assert.AreEqual(10, s.Points.Tier1);
            Assert.AreEqual(200, s.Points.Tier4);
        }

        [TestCase(GameMode.Standard, ". . Rs Rs", ScoreTier.Tier1, 10, TestName = "Scoring_Standard_SmallPair_Tier1_10")]
        [TestCase(GameMode.Standard, ". . Rm Rm", ScoreTier.Tier2, 50, TestName = "Scoring_Standard_MediumPair_Tier2_50")]
        [TestCase(GameMode.Standard, ". . Rl Rl", ScoreTier.Tier3, 100, TestName = "Scoring_Standard_LargePair_Tier3_100")]
        [TestCase(GameMode.ColorMerge, ". . Rs Gs", ScoreTier.Tier1, 10, TestName = "Scoring_ColorMerge_PrimaryPair_Tier1_10")]
        [TestCase(GameMode.ColorMerge, ". . Ms Gs", ScoreTier.Tier3, 100, TestName = "Scoring_ColorMerge_WhiteClear_Tier3_100")]
        [TestCase(GameMode.AdvancedColorMerge, ". . Rs Rs", ScoreTier.Tier1, 10, TestName = "Scoring_Advanced_SmallToMedium_Tier1_10")]
        [TestCase(GameMode.AdvancedColorMerge, ". . Rm Gm", ScoreTier.Tier2, 50, TestName = "Scoring_Advanced_MediumToSecondaryLarge_Tier2_50")]
        [TestCase(GameMode.AdvancedColorMerge, ". . Yl Bm", ScoreTier.Tier3, 100, TestName = "Scoring_Advanced_ToWhiteLarge_Tier3_100")]
        [TestCase(GameMode.AdvancedColorMerge, ". . Wl Wl", ScoreTier.Tier4, 200, TestName = "Scoring_Advanced_WhiteClear_Tier4_200")]
        public void Scoring_DefaultTierPoints_PerMerge(GameMode mode, string board, ScoreTier tier, int points)
        {
            using (var run = StepRun.Execute(mode, board, TestBoards.Right, TestBoards.NoSpawn))
            {
                var merged = run.Of(SimEventType.Merged);
                Assert.AreEqual(1, merged.Count);
                Assert.AreEqual(tier, merged[0].Tier);
                Assert.AreEqual(points, merged[0].Points);
                Assert.AreEqual(points, run.Result.ScoreDelta);
            }
        }

        [Test]
        public void Scoring_CustomTierPoints_AreUsedForEventAndScoreDelta()
        {
            var settings = TestBoards.NoSpawn;
            settings.Points = new TierPoints { Tier1 = 7, Tier2 = 11, Tier3 = 13, Tier4 = 17 };
            Assert.AreEqual(0, settings.Points.For(ScoreTier.None));
            Assert.AreEqual(13, settings.Points.For(ScoreTier.Tier3));

            var cases = new[]
            {
                (mode: GameMode.Standard, board: ". . Rs Rs", expected: 7),
                (mode: GameMode.Standard, board: ". . Rm Rm", expected: 11),
                (mode: GameMode.Standard, board: ". . Rl Rl", expected: 13),
                (mode: GameMode.AdvancedColorMerge, board: ". . Wl Wl", expected: 17)
            };
            foreach (var c in cases)
            {
                using (var run = StepRun.Execute(c.mode, c.board, TestBoards.Right, settings))
                {
                    Assert.AreEqual(c.expected, run.Result.ScoreDelta, c.mode + " " + c.board);
                    Assert.AreEqual(c.expected, run.Of(SimEventType.Merged)[0].Points, c.mode + " " + c.board);
                }
            }
        }

        [Test]
        public void Scoring_NoMerge_ScoreDeltaIsZero()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs Bs . .", TestBoards.Right, TestBoards.NoSpawn))
                Assert.AreEqual(0, run.Result.ScoreDelta);
        }

        [Test]
        public void Scoring_ScoreDeltaIsTheSumOfAllMergesInTheStep_TwoRows()
        {
            using (var run = StepRun.Execute(GameMode.Standard, ". . Rs Rs|. . Gl Gl", TestBoards.Right, TestBoards.NoSpawn))
            {
                TestBoards.AssertBoard(". . . Rm|. . . .", run.Board);
                Assert.AreEqual(2, run.Of(SimEventType.Merged).Count);
                Assert.AreEqual(110, run.Result.ScoreDelta, "Tier1 10 + Tier3 100");
            }
        }

        [Test]
        public void Scoring_ScoreDeltaIsTheSumOfAllMergesInTheStep_ThreeRows_NoMultiplier()
        {
            using (var run = StepRun.Execute(GameMode.Standard, ". . Rm Rm|. . Bs Bs|. . Gl Gl", TestBoards.Right, TestBoards.NoSpawn))
            {
                int sum = 0;
                foreach (var e in run.Of(SimEventType.Merged)) sum += e.Points;
                Assert.AreEqual(3, run.Of(SimEventType.Merged).Count);
                Assert.AreEqual(50 + 10 + 100, run.Result.ScoreDelta);
                Assert.AreEqual(sum, run.Result.ScoreDelta, "ScoreDelta equals the sum of the event points");
            }
        }

        [Test]
        public void Scoring_OnlyMergesScore_MovesSpawnsAndResolvesDoNot()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs . . .|. i . .|. . . .|. . . .", TestBoards.Right, TestBoards.WithSpawn, 3u))
            {
                Assert.AreEqual(0, run.Of(SimEventType.Merged).Count);
                Assert.AreEqual(0, run.Result.ScoreDelta);
            }
        }

        [Test]
        public void Scoring_RejectedStepScoresNothing()
        {
            using (var run = StepRun.Execute(GameMode.Standard, ". . Rs Rs", new int2(0, 0), TestBoards.NoSpawn))
            {
                Assert.IsFalse(run.Result.Accepted);
                Assert.AreEqual(0, run.Result.ScoreDelta);
            }
        }
    }
}
