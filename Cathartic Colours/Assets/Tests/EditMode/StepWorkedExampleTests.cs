// Written from the "Worked examples" in docs/wiki/systems/merge-rules.md and the CC-006.2 API contract,
// without reading the simulation implementation.
//
// Each example is one row of width 4, no indicator on the board and spawning disabled, so the row after
// the step is exactly what the wiki shows. Every example runs twice: through MergeRulesFactory.Step and
// through the generic BoardSimulation.Step<TRules> with the matching rule struct.
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;

namespace Simulation.Tests
{
    public class StepWorkedExampleTests
    {
        static TestCaseData Example(string name, GameMode mode, string before, int dx, int dy, string after, ScoreTier tier, int points)
        {
            return new TestCaseData(mode, before, dx, dy, after, tier, points).SetName(name);
        }

        static IEnumerable<TestCaseData> Examples()
        {
            // Standard
            yield return Example("Standard_NoMergeWhenCellAheadWasEmpty_BlocksJustSlide",
                GameMode.Standard, "Rs Rs Bs .", 1, 0, ". Rs Rs Bs", ScoreTier.None, 0);
            yield return Example("Standard_TwoSmallSameColour_MergeToMedium_Tier1",
                GameMode.Standard, ". . Rs Rs", 1, 0, ". . . Rm", ScoreTier.Tier1, 10);
            yield return Example("Standard_ThreeSmallInARow_OnlyOneMerge_Tier1",
                GameMode.Standard, ". Rs Rs Rs", 1, 0, ". . Rs Rm", ScoreTier.Tier1, 10);
            yield return Example("Standard_TwoLargeSameColour_BothDestroyed_Tier3",
                GameMode.Standard, ". . Rl Rl", 1, 0, ". . . .", ScoreTier.Tier3, 100);

            // Color Merge
            yield return Example("ColorMerge_RedGreen_MakeYellow_Tier1",
                GameMode.ColorMerge, "Rs Gs . .", -1, 0, "Ys . . .", ScoreTier.Tier1, 10);
            yield return Example("ColorMerge_YellowBlue_BothDestroyed_Tier3",
                GameMode.ColorMerge, "Ys Bs . .", -1, 0, ". . . .", ScoreTier.Tier3, 100);
            yield return Example("ColorMerge_EqualColours_DoNotMerge",
                GameMode.ColorMerge, "Rs Rs . .", -1, 0, "Rs Rs . .", ScoreTier.None, 0);
            yield return Example("ColorMerge_TwoSecondaries_DoNotMerge",
                GameMode.ColorMerge, "Ys Ms . .", -1, 0, "Ys Ms . .", ScoreTier.None, 0);

            // Advanced Color Merge
            yield return Example("Advanced_TwoSmallSameColour_MergeToMedium_Tier1",
                GameMode.AdvancedColorMerge, ". . Rs Rs", 1, 0, ". . . Rm", ScoreTier.Tier1, 10);
            yield return Example("Advanced_SmallDifferentColours_DoNotMerge",
                GameMode.AdvancedColorMerge, ". . Rs Gs", 1, 0, ". . Rs Gs", ScoreTier.None, 0);
            yield return Example("Advanced_MediumRedMediumBlue_MakeLargeMagenta_Tier2",
                GameMode.AdvancedColorMerge, ". . Rm Bm", 1, 0, ". . . Ml", ScoreTier.Tier2, 50);
            yield return Example("Advanced_MediumGreenIntoLargeMagenta_MakeLargeWhite_Tier3",
                GameMode.AdvancedColorMerge, ". . Gm Ml", 1, 0, ". . . Wl", ScoreTier.Tier3, 100);
            yield return Example("Advanced_LargeMagentaIntoMediumGreen_MirrorAlsoMerges_Tier3",
                GameMode.AdvancedColorMerge, ". . Ml Gm", 1, 0, ". . . Wl", ScoreTier.Tier3, 100);
            yield return Example("Advanced_TwoLargeWhite_BothDestroyed_Tier4",
                GameMode.AdvancedColorMerge, ". . Wl Wl", 1, 0, ". . . .", ScoreTier.Tier4, 200);
            yield return Example("Advanced_TwoMediumSameColour_DoNotMerge",
                GameMode.AdvancedColorMerge, ". . Rm Rm", 1, 0, ". . Rm Rm", ScoreTier.None, 0);
        }

        static void AssertExample(StepRun run, string after, ScoreTier tier, int points)
        {
            Assert.IsTrue(run.Result.Accepted, "the step should be accepted");
            TestBoards.AssertBoard(after, run.Board);
            Assert.AreEqual(points, run.Result.ScoreDelta, "ScoreDelta");
            var merged = run.Of(SimEventType.Merged);
            if (tier == ScoreTier.None)
            {
                Assert.AreEqual(0, merged.Count, "no merge expected");
            }
            else
            {
                Assert.AreEqual(1, merged.Count, "exactly one merge expected");
                Assert.AreEqual(tier, merged[0].Tier, "tier");
                Assert.AreEqual(points, merged[0].Points, "points of the merge event");
            }
        }

        [TestCaseSource(nameof(Examples))]
        public void WorkedExample_ViaFactory(GameMode mode, string before, int dx, int dy, string after, ScoreTier tier, int points)
        {
            using (var run = StepRun.Execute(mode, before, new int2(dx, dy), TestBoards.NoSpawn))
                AssertExample(run, after, tier, points);
        }

        [TestCaseSource(nameof(Examples))]
        public void WorkedExample_ViaGenericStep(GameMode mode, string before, int dx, int dy, string after, ScoreTier tier, int points)
        {
            using (var run = StepRun.ExecuteGeneric(mode, before, new int2(dx, dy), TestBoards.NoSpawn))
                AssertExample(run, after, tier, points);
        }

        [Test]
        public void WorkedExample_StandardMergeTargetIsTheCellAhead_MergedEventFields()
        {
            using (var run = StepRun.Execute(GameMode.Standard, ". . Rs Rs", TestBoards.Right, TestBoards.NoSpawn))
            {
                var merged = run.Of(SimEventType.Merged);
                Assert.AreEqual(1, merged.Count);
                Assert.AreEqual(new int2(2, 0), merged[0].From, "mover's cell before the move");
                Assert.AreEqual(new int2(3, 0), merged[0].Position, "target cell");
                Assert.AreEqual(BlockColor.Red, merged[0].Color);
                Assert.AreEqual(BlockSize.Medium, merged[0].Size);
                Assert.AreEqual(BlockSize.Small, merged[0].MoverSize);
                Assert.IsFalse(merged[0].DestroysBoth);
            }
        }
    }
}
