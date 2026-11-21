// CC-046.3: the Advanced Color Merge (ACM) cases of docs/wiki/test-plan.md, TP-ACM-01 to TP-ACM-08, one test each.
// The steps follow the test plan. Merge cases start from a known 4x4 board with spawning off (a 4-wide board: the plan's
// one-row examples, "[. . Rs Rs]" pressing right, are its bottom row); TP-ACM-01 and TP-ACM-08 start a 6x6 game from the menu.
using System.Collections;
using NUnit.Framework;
using Simulation;
using TestSupport;
using UnityEngine.TestTools;

namespace PlayModeTests
{
    [Category("TP_ACM")]
    public class AdvancedColorMergeCases : PlaythroughTest
    {
        const GameMode Mode = GameMode.AdvancedColorMerge;
        static string Row(string row) => BoardNotation.BottomRow(row);

        [UnityTest, Category("TP_ACM_01")]
        public IEnumerator TP_ACM_01_SixBySixBoardAndFirstMove()
        {
            yield return CaseChecks.FirstMove(Driver, Mode, 6, 6, "TP-ACM-01");
        }

        [UnityTest, Category("TP_ACM_02")]
        public IEnumerator TP_ACM_02_SmallPairOfOneColourGrowsToMedium()
        {
            yield return CaseChecks.Merge(Driver, Mode, Row(". . Rs Rs"), Direction.Right, Row(". . . Rm"), 10, "TP-ACM-02");
        }

        [UnityTest, Category("TP_ACM_03")]
        public IEnumerator TP_ACM_03_SmallBlocksThatDoNotMergeStayAsTheyAre()
        {
            yield return CaseChecks.Merge(Driver, Mode, Row(". . Rs Gs"), Direction.Right, Row(". . Rs Gs"), 0, "TP-ACM-03-colours");
            yield return CaseChecks.Merge(Driver, Mode, Row(". . Rs Rm"), Direction.Right, Row(". . Rs Rm"), 0, "TP-ACM-03-sizes");
        }

        [UnityTest, Category("TP_ACM_04")]
        public IEnumerator TP_ACM_04_TwoDifferentMediumPrimariesMakeALargeSecondary()
        {
            yield return CaseChecks.Merge(Driver, Mode, Row(". . Rm Bm"), Direction.Right, Row(". . . Ml"), 50, "TP-ACM-04-magenta");
            yield return CaseChecks.Merge(Driver, Mode, Row(". . Rm Gm"), Direction.Right, Row(". . . Yl"), 50, "TP-ACM-04-yellow");
            yield return CaseChecks.Merge(Driver, Mode, Row(". . Gm Bm"), Direction.Right, Row(". . . Cl"), 50, "TP-ACM-04-cyan");
        }

        [UnityTest, Category("TP_ACM_05")]
        public IEnumerator TP_ACM_05_SameColourMediumBlocksDoNotMerge()
        {
            yield return CaseChecks.Merge(Driver, Mode, Row(". . Rm Rm"), Direction.Right, Row(". . Rm Rm"), 0, "TP-ACM-05");
        }

        [UnityTest, Category("TP_ACM_06")]
        public IEnumerator TP_ACM_06_LargeSecondaryPlusItsMissingMediumPrimaryMakesWhite()
        {
            yield return CaseChecks.Merge(Driver, Mode, Row(". . Gm Ml"), Direction.Right, Row(". . . Wl"), 100, "TP-ACM-06-gm-ml");
            yield return CaseChecks.Merge(Driver, Mode, Row(". . Ml Gm"), Direction.Right, Row(". . . Wl"), 100, "TP-ACM-06-ml-gm");
            yield return CaseChecks.Merge(Driver, Mode, Row(". . Yl Bm"), Direction.Right, Row(". . . Wl"), 100, "TP-ACM-06-yl-bm");
            yield return CaseChecks.Merge(Driver, Mode, Row(". . Cl Rm"), Direction.Right, Row(". . . Wl"), 100, "TP-ACM-06-cl-rm");
        }

        [UnityTest, Category("TP_ACM_07")]
        public IEnumerator TP_ACM_07_TwoLargeWhiteBlocksClear()
        {
            yield return CaseChecks.Merge(Driver, Mode, Row(". . Wl Wl"), Direction.Right, Row(". . . ."), 200, "TP-ACM-07");
        }

        [UnityTest, Category("TP_ACM_08")]
        public IEnumerator TP_ACM_08_RegressionExtendedPlayFollowsTheAdvancedColorMergeRules()
        {
            yield return Driver.StartFromMenu(Mode, 6, 6, true);
            var report = new ExtendedPlayReport();

            yield return ExtendedPlay.Run(Driver, Mode, 150, report);

            TestContext.WriteLine("TP-ACM-08 " + report.Summary());
            yield return Driver.Screenshot("TP-ACM-08-end");
        }
    }
}
