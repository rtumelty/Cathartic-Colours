// CC-046.3: the Color Merge (CM) cases of docs/wiki/test-plan.md, TP-CM-01 to TP-CM-07, one test each.
// The steps follow the test plan. Merge cases start from a known 4x4 board with spawning off; the plan's one-row examples
// ("[Rs Gs . .]" pressing left) are the bottom row. TP-CM-01, TP-CM-06 and TP-CM-07 are about the white block, so they spawn.
using System.Collections;
using NUnit.Framework;
using Simulation;
using TestSupport;
using UnityEngine.TestTools;

namespace PlayModeTests
{
    [Category("TP_CM")]
    public class ColorMergeCases : PlaythroughTest
    {
        const GameMode Mode = GameMode.ColorMerge;
        static string Row(string row) => BoardNotation.BottomRow(row);

        [UnityTest, Category("TP_CM_01")]
        public IEnumerator TP_CM_01_FourByFourBoardWhiteBlockAtTwoTwoAndFirstMove()
        {
            yield return CaseChecks.FirstMove(Driver, Mode, 4, 4, "TP-CM-01");
        }

        [UnityTest, Category("TP_CM_02")]
        public IEnumerator TP_CM_02_TwoDifferentPrimariesMakeASecondary()
        {
            yield return CaseChecks.Merge(Driver, Mode, Row("Rs Gs . ."), Direction.Left, Row("Ys . . ."), 10, "TP-CM-02-yellow");
            yield return CaseChecks.Merge(Driver, Mode, Row("Rs Bs . ."), Direction.Left, Row("Ms . . ."), 10, "TP-CM-02-magenta");
            yield return CaseChecks.Merge(Driver, Mode, Row("Gs Bs . ."), Direction.Left, Row("Cs . . ."), 10, "TP-CM-02-cyan");
        }

        [UnityTest, Category("TP_CM_03")]
        public IEnumerator TP_CM_03_EqualColoursDoNotMerge()
        {
            yield return CaseChecks.Merge(Driver, Mode, Row("Rs Rs . ."), Direction.Left, Row("Rs Rs . ."), 0, "TP-CM-03");
        }

        [UnityTest, Category("TP_CM_04")]
        public IEnumerator TP_CM_04_SecondaryPlusItsMissingPrimaryClearsBoth()
        {
            yield return CaseChecks.Merge(Driver, Mode, Row("Ys Bs . ."), Direction.Left, Row(". . . ."), 100, "TP-CM-04-yellow-blue");
            yield return CaseChecks.Merge(Driver, Mode, Row("Ms Gs . ."), Direction.Left, Row(". . . ."), 100, "TP-CM-04-magenta-green");
            yield return CaseChecks.Merge(Driver, Mode, Row("Cs Rs . ."), Direction.Left, Row(". . . ."), 100, "TP-CM-04-cyan-red");
        }

        [UnityTest, Category("TP_CM_05")]
        public IEnumerator TP_CM_05_PairsThatDoNotMergeStayAsTheyAre()
        {
            yield return CaseChecks.Merge(Driver, Mode, Row("Ys Ms . ."), Direction.Left, Row("Ys Ms . ."), 0, "TP-CM-05-two-secondaries");
            yield return CaseChecks.Merge(Driver, Mode, Row("Ys Rs . ."), Direction.Left, Row("Ys Rs . ."), 0, "TP-CM-05-secondary-and-contained-primary");
        }

        [UnityTest, Category("TP_CM_06")]
        public IEnumerator TP_CM_06_WhiteBlockNeverMerges()
        {
            yield return Driver.ResumeWith(Row(". Rs i ."), Mode, spawnNextColour: true, moveCount: 3, score: 40);

            yield return Driver.Move(Direction.Right);

            string after = Driver.Board();
            Assert.AreEqual("Rs", BoardNotation.TokenAt(after, 1, 0), "The Red block does not move into or merge with the white block. Board: " + after);
            Assert.IsTrue(BoardNotation.IsResolvedPrimary(BoardNotation.ParseCell(BoardNotation.TokenAt(after, 2, 0))),
                "The white block became a coloured block in place. Board: " + after);
            Assert.AreEqual(1, BoardNotation.Count(after, "i"), "A new white block appears. Board: " + after);
            Assert.AreEqual(3, Driver.Blocks(), "Two coloured blocks and the new white one.");
            Assert.AreEqual(40, Driver.Score, "Score does not change.");
            Assert.AreEqual(4, Driver.MoveCount);
            yield return Driver.Screenshot("TP-CM-06");
        }

        [UnityTest, Category("TP_CM_07")]
        public IEnumerator TP_CM_07_RegressionExtendedPlayFollowsTheColorMergeRules()
        {
            yield return Driver.StartFromMenu(Mode, 4, 4, true);
            var report = new ExtendedPlayReport();

            yield return ExtendedPlay.Run(Driver, Mode, 150, report);

            TestContext.WriteLine("TP-CM-07 " + report.Summary());
            yield return Driver.Screenshot("TP-CM-07-end");
        }
    }
}
