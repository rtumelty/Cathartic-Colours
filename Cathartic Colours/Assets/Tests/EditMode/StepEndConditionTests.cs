// Written from docs/wiki/systems/merge-rules.md ("End conditions") and the CC-006.2 API contract,
// without reading the simulation implementation.
//
// Game over = no empty cell AND no block with a 4-neighbour for which the current mode's CanMerge(block, neighbour)
// is true. The indicator counts as an occupied cell. Diagonal neighbours do not count.
using NUnit.Framework;
using Unity.Mathematics;

namespace Simulation.Tests
{
    public class StepEndConditionTests
    {
        static bool HasPair(GameMode mode, in BoardState board)
        {
            switch (mode)
            {
                case GameMode.ColorMerge:
                {
                    var r = new ColorMergeRules();
                    return BoardSimulation.HasMergeablePair(in board, in r);
                }
                case GameMode.AdvancedColorMerge:
                {
                    var r = new AdvancedColorMergeRules();
                    return BoardSimulation.HasMergeablePair(in board, in r);
                }
                default:
                {
                    var r = new StandardMergeRules();
                    return BoardSimulation.HasMergeablePair(in board, in r);
                }
            }
        }

        static bool IsOverGeneric(GameMode mode, in BoardState board)
        {
            switch (mode)
            {
                case GameMode.ColorMerge:
                {
                    var r = new ColorMergeRules();
                    return BoardSimulation.IsGameOver(in board, in r);
                }
                case GameMode.AdvancedColorMerge:
                {
                    var r = new AdvancedColorMergeRules();
                    return BoardSimulation.IsGameOver(in board, in r);
                }
                default:
                {
                    var r = new StandardMergeRules();
                    return BoardSimulation.IsGameOver(in board, in r);
                }
            }
        }

        // mode, board, hasEmptyCell, hasMergeablePair, isGameOver
        [TestCase(GameMode.Standard, "Rs Gs|Gs Rs", false, false, true, TestName = "Standard_FullBoard_OnlyDiagonalMatches_GameOver")]
        [TestCase(GameMode.Standard, "Rs Rs|Gs Bs", false, true, false, TestName = "Standard_FullBoard_HorizontalPair_NotGameOver")]
        [TestCase(GameMode.Standard, "Rs Gs|Rs Bs", false, true, false, TestName = "Standard_FullBoard_VerticalPair_NotGameOver")]
        [TestCase(GameMode.Standard, "Rl Rl|Gs Bs", false, true, false, TestName = "Standard_FullBoard_LargePair_NotGameOver")]
        [TestCase(GameMode.Standard, "Rs Rm|Rm Rs", false, false, true, TestName = "Standard_FullBoard_SameColourDifferentSize_GameOver")]
        [TestCase(GameMode.Standard, "Rs i|Gs Bs", false, false, true, TestName = "Standard_FullBoard_IndicatorCountsAsOccupied_GameOver")]
        [TestCase(GameMode.Standard, "Ws i|Gs Bs", false, false, true, TestName = "Standard_FullBoard_WhiteNextToIndicatorDoesNotMerge_GameOver")]
        [TestCase(GameMode.Standard, ". Gs|Gs Rs", true, false, false, TestName = "Standard_EmptyCell_NotGameOver")]
        [TestCase(GameMode.Standard, "i .", true, false, false, TestName = "Standard_IndicatorAndEmptyCell_NotGameOver")]
        [TestCase(GameMode.Standard, ". . .", true, false, false, TestName = "Standard_EmptyBoard_NotGameOver")]
        [TestCase(GameMode.Standard, "Rs", false, false, true, TestName = "Standard_FullOneByOne_GameOver")]
        [TestCase(GameMode.Standard, ".", true, false, false, TestName = "Standard_EmptyOneByOne_NotGameOver")]
        [TestCase(GameMode.Standard, "Rm Bm|Bm Rm", false, false, true, TestName = "Standard_MediumPrimaries_DoNotMerge_GameOver")]
        [TestCase(GameMode.ColorMerge, "Rs Rs|Rs Rs", false, false, true, TestName = "ColorMerge_FullBoardEqualColours_GameOver")]
        [TestCase(GameMode.ColorMerge, "Rs Gs|Gs Rs", false, true, false, TestName = "ColorMerge_FullBoardRedGreen_NotGameOver")]
        [TestCase(GameMode.ColorMerge, "Ys Ms|Ms Ys", false, false, true, TestName = "ColorMerge_FullBoardSecondaries_GameOver")]
        [TestCase(GameMode.ColorMerge, "Ys Bs|Bs Ys", false, true, false, TestName = "ColorMerge_FullBoardYellowBlue_NotGameOver")]
        [TestCase(GameMode.ColorMerge, "Ws Rs|Rs Ws", false, false, true, TestName = "ColorMerge_FullBoardWhite_GameOver")]
        [TestCase(GameMode.ColorMerge, "Rs i|Rs Rs", false, false, true, TestName = "ColorMerge_FullBoardIndicatorAndEqualColours_GameOver")]
        [TestCase(GameMode.ColorMerge, "Rl Gs|Bs Bs", false, true, false, TestName = "ColorMerge_FullBoardSizeIgnored_NotGameOver")]
        [TestCase(GameMode.ColorMerge, "Rs .|Rs Rs", true, false, false, TestName = "ColorMerge_EmptyCell_NotGameOver")]
        [TestCase(GameMode.AdvancedColorMerge, "Rs Gs|Gs Rs", false, false, true, TestName = "Advanced_FullBoardSmallDifferentColours_GameOver")]
        [TestCase(GameMode.AdvancedColorMerge, "Rs Rs|Gs Bs", false, true, false, TestName = "Advanced_FullBoardSmallSamePair_NotGameOver")]
        [TestCase(GameMode.AdvancedColorMerge, "Rm Bm|Bm Rm", false, true, false, TestName = "Advanced_FullBoardMediumDifferentPrimaries_NotGameOver")]
        [TestCase(GameMode.AdvancedColorMerge, "Rm Rm|Rm Rm", false, false, true, TestName = "Advanced_FullBoardMediumSameColour_GameOver")]
        [TestCase(GameMode.AdvancedColorMerge, "Rs Rm|Rm Rs", false, false, true, TestName = "Advanced_FullBoardSmallAndMedium_GameOver")]
        [TestCase(GameMode.AdvancedColorMerge, "Ml Gm|Gl Gl", false, true, false, TestName = "Advanced_FullBoardLargeSecondaryNextToMediumPrimary_NotGameOver")]
        [TestCase(GameMode.AdvancedColorMerge, "Wl Wl|Gs Bs", false, true, false, TestName = "Advanced_FullBoardWhiteLargePair_NotGameOver")]
        [TestCase(GameMode.AdvancedColorMerge, "Wl Wm|Wm Wl", false, false, true, TestName = "Advanced_FullBoardWhiteMixedSizes_GameOver")]
        [TestCase(GameMode.AdvancedColorMerge, "Rs i|Gs Bm", false, false, true, TestName = "Advanced_FullBoardIndicator_GameOver")]
        public void EndCondition_Direct(GameMode mode, string board, bool hasEmpty, bool hasPair, bool isOver)
        {
            using (var b = TestBoards.Parse(board))
            {
                Assert.AreEqual(hasEmpty, BoardSimulation.HasEmptyCell(in b), "HasEmptyCell");
                Assert.AreEqual(hasPair, HasPair(mode, in b), "HasMergeablePair (generic)");
                Assert.AreEqual(isOver, IsOverGeneric(mode, in b), "IsGameOver (generic)");
                Assert.AreEqual(isOver, MergeRulesFactory.IsGameOver(mode, in b), "IsGameOver (factory)");
            }
        }

        [Test]
        public void HasEmptyCell_IndicatorCountsAsOccupied()
        {
            using (var b = TestBoards.Parse("Rs Gs|Gs i"))
                Assert.IsFalse(BoardSimulation.HasEmptyCell(in b), "an indicator is an occupied cell");
            using (var b = TestBoards.Parse("Rs Gs|Gs ."))
                Assert.IsTrue(BoardSimulation.HasEmptyCell(in b));
        }

        [Test]
        public void HasMergeablePair_IgnoresEmptyCells()
        {
            using (var b = TestBoards.Parse(". . .|. . ."))
            {
                var rules = new StandardMergeRules();
                Assert.IsFalse(BoardSimulation.HasMergeablePair(in b, in rules));
            }
        }

        [Test]
        public void HasMergeablePair_OnlyCountsFourNeighbours_NotAcrossAGap()
        {
            using (var b = TestBoards.Parse("Rs . Rs"))
            {
                var rules = new StandardMergeRules();
                Assert.IsFalse(BoardSimulation.HasMergeablePair(in b, in rules), "an empty cell between them is not adjacency");
            }
        }

        // ------------------------------------------------------------------ through Step

        [TestCase(GameMode.Standard)]
        [TestCase(GameMode.ColorMerge)]
        [TestCase(GameMode.AdvancedColorMerge)]
        public void Step_FillingTheLastCellWithTheIndicator_WhenNothingCanMerge_IsGameOver(GameMode mode)
        {
            // Row "Rs Gs ." in the mode where Rs and Gs cannot merge (Standard, Advanced) ... for Color Merge use equal colours.
            string board = mode == GameMode.ColorMerge ? "Rs Rs ." : "Rs Gs .";
            // Nothing can move left on this row (x0 at the wall, neighbours cannot merge), so the free cell stays x2.
            const uint seed = 1u;

            using (var run = StepRun.Execute(mode, board, TestBoards.Left, TestBoards.WithSpawn, seed))
            {
                Assert.IsTrue(run.Result.Accepted);
                Assert.IsFalse(run.Result.SpawnFailed);
                Assert.IsTrue(run.Result.GameOver, "full board and the indicator never merges");
                var gameOver = run.Of(SimEventType.GameOver);
                Assert.AreEqual(1, gameOver.Count, "one GameOver event");
                Assert.AreEqual(SimEventType.GameOver, run.Events[run.Events.Length - 1].Type, "GameOver is the last event");
            }
        }

        [Test]
        public void Step_BoardWithAnEmptyCellAfterTheMove_IsNotGameOver_AndEmitsNoGameOverEvent()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs Gs . .", TestBoards.Right, TestBoards.NoSpawn))
            {
                Assert.IsFalse(run.Result.GameOver);
                Assert.AreEqual(0, run.Of(SimEventType.GameOver).Count);
            }
        }

        [Test]
        public void Step_MergingOnAFullBoard_FreesACell_NotGameOver()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs Rs|Gs Bs", TestBoards.Right, TestBoards.NoSpawn))
            {
                TestBoards.AssertBoard(". Rm|Gs Bs", run.Board);
                Assert.IsFalse(run.Result.GameOver);
            }
        }

        [Test]
        public void Step_GameOverFlagMatchesIsGameOverOnTheResultingBoard()
        {
            foreach (GameMode mode in new[] { GameMode.Standard, GameMode.ColorMerge, GameMode.AdvancedColorMerge })
            {
                for (uint seed = 1; seed <= 25; seed++)
                {
                    using (var run = StepRun.Execute(mode, "Rs Gs . .|Bs Rs Gs .|. Bs Rs .", TestBoards.Right, TestBoards.WithSpawn, seed))
                    {
                        Assert.AreEqual(MergeRulesFactory.IsGameOver(mode, run.Board), run.Result.GameOver, mode + " seed " + seed);
                    }
                }
            }
        }
    }
}
