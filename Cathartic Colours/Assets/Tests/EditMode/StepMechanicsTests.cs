// Written from docs/wiki/systems/merge-rules.md ("Sliding and merging") and the CC-006.2 API contract,
// without reading the simulation implementation.
//
// Boards use the notation in TestBoards.cs (first row = top row). Spawning is disabled unless a test says
// otherwise; "?" in an expected board means a freshly resolved indicator (Small Red/Green/Blue).
using NUnit.Framework;
using Unity.Mathematics;

namespace Simulation.Tests
{
    public class StepMechanicsTests
    {
        static StepSettings NoSpawn { get { return TestBoards.NoSpawn; } }

        // ------------------------------------------------------------------ walls and one cell per move

        [TestCase(". . . Rs", 1, 0, TestName = "Wall_Right")]
        [TestCase("Rs . . .", -1, 0, TestName = "Wall_Left")]
        public void Wall_BlockAtTheEdgeDoesNotMove_NoEvents(string board, int dx, int dy)
        {
            using (var run = StepRun.Execute(GameMode.Standard, board, new int2(dx, dy), NoSpawn))
            {
                Assert.IsTrue(run.Result.Accepted, "a move that changes nothing is still accepted");
                TestBoards.AssertBoard(board, run.Board);
                Assert.AreEqual(0, run.Events.Length, "no Moved/Merged events for a blocked block");
                Assert.AreEqual(0, run.Result.ScoreDelta);
            }
        }

        [TestCase("Rs . .|. . .|. . .", 0, 1, TestName = "Wall_Up_TopLeftCorner")]
        [TestCase("Rs . .|. . .|. . .", -1, 0, TestName = "Wall_Left_TopLeftCorner")]
        [TestCase(". . .|. . .|. . Rs", 0, -1, TestName = "Wall_Down_BottomRightCorner")]
        [TestCase(". . .|. . .|. . Rs", 1, 0, TestName = "Wall_Right_BottomRightCorner")]
        public void Wall_CornerBlockDoesNotMoveIntoTheWall(string board, int dx, int dy)
        {
            using (var run = StepRun.Execute(GameMode.Standard, board, new int2(dx, dy), NoSpawn))
            {
                Assert.IsTrue(run.Result.Accepted);
                TestBoards.AssertBoard(board, run.Board);
                Assert.AreEqual(0, run.Events.Length);
            }
        }

        [Test]
        public void OneCellPerMove_BlockDoesNotSlideToTheWall()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs . . .", TestBoards.Right, NoSpawn))
            {
                TestBoards.AssertBoard(". Rs . .", run.Board);
                var moved = run.Of(SimEventType.Moved);
                Assert.AreEqual(1, moved.Count);
                Assert.AreEqual(new int2(0, 0), moved[0].From);
                Assert.AreEqual(new int2(1, 0), moved[0].Position);
                Assert.AreEqual(0, run.Of(SimEventType.Merged).Count);
            }
        }

        [Test]
        public void OneCellPerMove_AdjacentBlocksOfSameColourMoveRatherThanMerge_WhenCellAheadIsEmpty()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs Rs . .", TestBoards.Right, NoSpawn))
            {
                TestBoards.AssertBoard(". Rs Rs .", run.Board);
                Assert.AreEqual(0, run.Of(SimEventType.Merged).Count);
                Assert.AreEqual(2, run.Of(SimEventType.Moved).Count);
            }
        }

        // ------------------------------------------------------------------ processing order

        [Test]
        public void FurthestFirst_BlockBehindMovesIntoJustVacatedCell_Right()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs Bs . .", TestBoards.Right, NoSpawn))
            {
                TestBoards.AssertBoard(". Rs Bs .", run.Board);
                var moved = run.Of(SimEventType.Moved);
                Assert.AreEqual(2, moved.Count);
                // Furthest along the direction of travel is processed first.
                Assert.AreEqual(new int2(1, 0), moved[0].From);
                Assert.AreEqual(new int2(2, 0), moved[0].Position);
                Assert.AreEqual(new int2(0, 0), moved[1].From);
                Assert.AreEqual(new int2(1, 0), moved[1].Position);
            }
        }

        [Test]
        public void FurthestFirst_BlockBehindMovesIntoJustVacatedCell_Left()
        {
            using (var run = StepRun.Execute(GameMode.Standard, ". . Bs Rs", TestBoards.Left, NoSpawn))
            {
                TestBoards.AssertBoard(". Bs Rs .", run.Board);
                var moved = run.Of(SimEventType.Moved);
                Assert.AreEqual(2, moved.Count);
                Assert.AreEqual(new int2(2, 0), moved[0].From, "the block furthest along the direction (left) goes first");
                Assert.AreEqual(new int2(1, 0), moved[0].Position);
            }
        }

        [Test]
        public void FurthestFirst_ThreeBlocksAllAdvanceOneCell_Worked()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs Rs Bs .", TestBoards.Right, NoSpawn))
            {
                TestBoards.AssertBoard(". Rs Rs Bs", run.Board);
                var moved = run.Of(SimEventType.Moved);
                Assert.AreEqual(3, moved.Count);
                Assert.AreEqual(new int2(2, 0), moved[0].From);
                Assert.AreEqual(new int2(1, 0), moved[1].From);
                Assert.AreEqual(new int2(0, 0), moved[2].From);
            }
        }

        [Test]
        public void BlockedBlockDoesNotAllowFollowerToMove()
        {
            // Bs at the wall, Gs blocked behind it (different colours), Rs blocked behind Gs.
            using (var run = StepRun.Execute(GameMode.Standard, ". Rs Gs Bs", TestBoards.Right, NoSpawn))
            {
                // Bs at the wall stays; Gs cannot merge into Bs; Rs cannot merge into Gs.
                TestBoards.AssertBoard(". Rs Gs Bs", run.Board);
                Assert.AreEqual(0, run.Events.Length);
            }
        }

        // ------------------------------------------------------------------ merges: one per target, results stay

        [Test]
        public void MergeResultDoesNotMoveAgain_SameStep_ThreeInARow()
        {
            using (var run = StepRun.Execute(GameMode.Standard, ". Rs Rs Rs", TestBoards.Right, NoSpawn))
            {
                // x2 merges into x3 (Rm); x1 finds x2 empty and moves. The merge result stays at x3.
                TestBoards.AssertBoard(". . Rs Rm", run.Board);
                Assert.AreEqual(1, run.Of(SimEventType.Merged).Count, "one merge only");
                Assert.AreEqual(1, run.Of(SimEventType.Moved).Count);
                // Processing order: the merge (x2 -> x3) happens before the move (x1 -> x2).
                Assert.AreEqual(SimEventType.Merged, run.Events[0].Type);
                Assert.AreEqual(SimEventType.Moved, run.Events[1].Type);
                Assert.AreEqual(new int2(1, 0), run.Events[1].From);
                Assert.AreEqual(new int2(2, 0), run.Events[1].Position);
            }
        }

        [Test]
        public void OneMergePerTarget_FourInARow_Right()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs Rs Rs Rs", TestBoards.Right, NoSpawn))
            {
                // x2 merges into x3 (Rm); x1 moves into x2; x0 moves into x1.
                TestBoards.AssertBoard(". Rs Rs Rm", run.Board);
                Assert.AreEqual(1, run.Of(SimEventType.Merged).Count);
                Assert.AreEqual(10, run.Result.ScoreDelta);
            }
        }

        [Test]
        public void OneMergePerTarget_FourInARow_Left()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs Rs Rs Rs", TestBoards.Left, NoSpawn))
            {
                TestBoards.AssertBoard("Rm Rs Rs .", run.Board);
                Assert.AreEqual(1, run.Of(SimEventType.Merged).Count);
            }
        }

        [Test]
        public void DestroyedPairLeavesBothCellsFreeForFollowersInTheSameStep()
        {
            // Left: x0 is the wall; Rl at x1 merges into Rl at x0 and both vanish; Bs at x2 then finds x1 empty.
            using (var run = StepRun.Execute(GameMode.Standard, "Rl Rl Bs .", TestBoards.Left, NoSpawn))
            {
                TestBoards.AssertBoard(". Bs . .", run.Board);
                Assert.AreEqual(100, run.Result.ScoreDelta);
            }
        }

        // ------------------------------------------------------------------ indicator

        [TestCase(GameMode.Standard, TestName = "Indicator_StaysWhereItIs_Standard")]
        [TestCase(GameMode.ColorMerge, TestName = "Indicator_StaysWhereItIs_ColorMerge")]
        [TestCase(GameMode.AdvancedColorMerge, TestName = "Indicator_StaysWhereItIs_Advanced")]
        public void Indicator_IsImmovable_ItOnlyResolvesInPlace(GameMode mode)
        {
            foreach (var dir in new[] { TestBoards.Right, TestBoards.Left })
            {
                using (var run = StepRun.Execute(mode, ". . . .|. i . .", dir, NoSpawn))
                {
                    Assert.IsTrue(run.Result.Accepted);
                    TestBoards.AssertBoard(". . . .|. ? . .", run.Board, "direction " + dir);
                    Assert.AreEqual(0, run.Of(SimEventType.Moved).Count, "the indicator must not move");
                }
            }
        }

        [TestCase(GameMode.Standard)]
        [TestCase(GameMode.ColorMerge)]
        [TestCase(GameMode.AdvancedColorMerge)]
        public void Indicator_BlocksMovement_BlockStopsInFrontOfIt(GameMode mode)
        {
            using (var run = StepRun.Execute(mode, ". Rs i .", TestBoards.Right, NoSpawn))
            {
                TestBoards.AssertBoard(". Rs ? .", run.Board);
                Assert.AreEqual(0, run.Of(SimEventType.Moved).Count);
                Assert.AreEqual(0, run.Of(SimEventType.Merged).Count);
            }
        }

        [Test]
        public void Indicator_IsNotMergedInto_EvenByAWhiteSmallInStandard()
        {
            // In Standard a White Small would otherwise merge with another White Small (same colour and size).
            using (var run = StepRun.Execute(GameMode.Standard, ". . Ws i", TestBoards.Right, NoSpawn))
            {
                TestBoards.AssertBoard(". . Ws ?", run.Board);
                Assert.AreEqual(0, run.Of(SimEventType.Merged).Count);
                Assert.AreEqual(0, run.Result.ScoreDelta);
            }
        }

        [Test]
        public void Indicator_DoesNotMergeAsMover_EvenIntoAWhiteSmallInStandard()
        {
            using (var run = StepRun.Execute(GameMode.Standard, ". . i Ws", TestBoards.Right, NoSpawn))
            {
                TestBoards.AssertBoard(". . ? Ws", run.Board);
                Assert.AreEqual(0, run.Of(SimEventType.Merged).Count);
            }
        }

        [Test]
        public void Indicator_DoesNotBlockOtherRowsOrColumns()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs . . .|. i . .", TestBoards.Right, NoSpawn))
            {
                TestBoards.AssertBoard(". Rs . .|. ? . .", run.Board);
            }
        }

        // ------------------------------------------------------------------ rows and columns are independent

        [Test]
        public void Rows_AreIndependent_Right()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs Rs . .|. . Gs Gs", TestBoards.Right, NoSpawn))
            {
                TestBoards.AssertBoard(". Rs Rs .|. . . Gm", run.Board);
                Assert.AreEqual(1, run.Of(SimEventType.Merged).Count);
            }
        }

        [Test]
        public void Columns_AreIndependent_Up()
        {
            // 2 wide, 3 high. Column 0: Rs Rs stacked at the top; column 1: Bs at the bottom.
            using (var run = StepRun.Execute(GameMode.Standard, "Rs .|Rs .|. Bs", TestBoards.Up, NoSpawn))
            {
                TestBoards.AssertBoard("Rm .|. Bs|. .", run.Board);
                var merged = run.Of(SimEventType.Merged);
                Assert.AreEqual(1, merged.Count);
                Assert.AreEqual(new int2(0, 1), merged[0].From);
                Assert.AreEqual(new int2(0, 2), merged[0].Position);
            }
        }

        [Test]
        public void Columns_AreIndependent_Down()
        {
            using (var run = StepRun.Execute(GameMode.Standard, "Rs .|Rs .|. Bs", TestBoards.Down, NoSpawn))
            {
                // Down: wall is y=0. Col 0: Rs (y1) moves to y0, Rs (y2) moves to y1. Col 1: Bs already at the wall.
                TestBoards.AssertBoard(". .|Rs .|Rs Bs", run.Board);
                Assert.AreEqual(0, run.Of(SimEventType.Merged).Count);
            }
        }

        [Test]
        public void AllFourDirections_MoveASingleBlockOneCell()
        {
            const string start = ". . .|. Rs .|. . .";
            var cases = new[]
            {
                (dir: TestBoards.Right, expected: ". . .|. . Rs|. . ."),
                (dir: TestBoards.Left, expected: ". . .|Rs . .|. . ."),
                (dir: TestBoards.Up, expected: ". Rs .|. . .|. . ."),
                (dir: TestBoards.Down, expected: ". . .|. . .|. Rs .")
            };
            foreach (var c in cases)
            {
                using (var run = StepRun.Execute(GameMode.Standard, start, c.dir, NoSpawn))
                {
                    Assert.IsTrue(run.Result.Accepted, "direction " + c.dir);
                    TestBoards.AssertBoard(c.expected, run.Board, "direction " + c.dir);
                }
            }
        }

        [Test]
        public void AllFourDirections_MergeIntoTheCellAhead()
        {
            // A Standard pair next to a wall in each direction; the mover is the block further from the wall.
            var cases = new[]
            {
                (dir: TestBoards.Right, before: ". . .|. Rs Rs|. . .", after: ". . .|. . Rm|. . ."),
                (dir: TestBoards.Left, before: ". . .|Rs Rs .|. . .", after: ". . .|Rm . .|. . ."),
                (dir: TestBoards.Up, before: ". Rs .|. Rs .|. . .", after: ". Rm .|. . .|. . ."),
                (dir: TestBoards.Down, before: ". . .|. Rs .|. Rs .", after: ". . .|. . .|. Rm .")
            };
            foreach (var c in cases)
            {
                using (var run = StepRun.Execute(GameMode.Standard, c.before, c.dir, NoSpawn))
                {
                    TestBoards.AssertBoard(c.after, run.Board, "direction " + c.dir);
                    var merged = run.Of(SimEventType.Merged);
                    Assert.AreEqual(1, merged.Count, "direction " + c.dir);
                    Assert.AreEqual(ScoreTier.Tier1, merged[0].Tier);
                }
            }
        }

        // ------------------------------------------------------------------ rejected steps

        [TestCase(0, 0, TestName = "InvalidDirection_Zero")]
        [TestCase(1, 1, TestName = "InvalidDirection_Diagonal")]
        [TestCase(-1, -1, TestName = "InvalidDirection_NegativeDiagonal")]
        [TestCase(2, 0, TestName = "InvalidDirection_TwoCells")]
        [TestCase(0, -2, TestName = "InvalidDirection_TwoCellsDown")]
        public void InvalidDirection_IsRejected_NothingChanges(int dx, int dy)
        {
            const string board = "Rs . .|. i .|. . Gs";
            using (var run = StepRun.Execute(GameMode.Standard, board, new int2(dx, dy), TestBoards.WithSpawn, 42u))
            {
                Assert.IsFalse(run.Result.Accepted);
                Assert.AreEqual(0, run.Result.ScoreDelta);
                Assert.AreEqual(board, run.BoardText, "board must be untouched (the indicator must not even resolve)");
                Assert.AreEqual(0, run.Events.Length, "no events");
                Assert.AreEqual(run.InitialState, run.Random.state, "random must be untouched");
            }
        }

        [Test]
        public void InvalidDirection_IsRejected_ViaGenericStepToo()
        {
            using (var run = StepRun.ExecuteGeneric(GameMode.Standard, "Rs . .|. i .|. . Gs", new int2(0, 0), TestBoards.WithSpawn, 42u))
            {
                Assert.IsFalse(run.Result.Accepted);
                Assert.AreEqual(0, run.Events.Length);
                Assert.AreEqual(run.InitialState, run.Random.state);
            }
        }

        [TestCase(GameMode.Standard, "Rs Gs|Gs Rs")]
        [TestCase(GameMode.ColorMerge, "Rs Rs|Rs Rs")]
        [TestCase(GameMode.AdvancedColorMerge, "Rs Gs|Gs Rs")]
        public void GameOverBoard_RejectsEveryDirection(GameMode mode, string board)
        {
            foreach (var dir in new[] { TestBoards.Right, TestBoards.Left, TestBoards.Up, TestBoards.Down })
            {
                using (var run = StepRun.Execute(mode, board, dir, TestBoards.WithSpawn, 7u))
                {
                    Assert.IsFalse(run.Result.Accepted, mode + " " + dir);
                    Assert.AreEqual(board, run.BoardText);
                    Assert.AreEqual(0, run.Events.Length, "no events, not even a repeated GameOver event");
                    Assert.AreEqual(0, run.Result.ScoreDelta);
                    Assert.AreEqual(run.InitialState, run.Random.state);
                }
            }
        }

        [Test]
        public void FullBoardWithAMergeablePair_StillAcceptsMoves_AndIsNotGameOver()
        {
            // Full 2x2 board with a mergeable pair (top row). Moving up changes nothing, but the move is legal.
            using (var run = StepRun.Execute(GameMode.Standard, "Rs Rs|Gs Bs", TestBoards.Up, NoSpawn))
            {
                Assert.IsTrue(run.Result.Accepted);
                Assert.IsFalse(run.Result.GameOver);
                Assert.AreEqual(0, run.Of(SimEventType.GameOver).Count);
                TestBoards.AssertBoard("Rs Rs|Gs Bs", run.Board);
            }
        }

        // ------------------------------------------------------------------ events

        [Test]
        public void MovedEvent_CarriesFromAndPosition_Vertical()
        {
            using (var run = StepRun.Execute(GameMode.Standard, ". . .|. . .|Rs . .", TestBoards.Up, NoSpawn))
            {
                var moved = run.Of(SimEventType.Moved);
                Assert.AreEqual(1, moved.Count);
                Assert.AreEqual(new int2(0, 0), moved[0].From);
                Assert.AreEqual(new int2(0, 1), moved[0].Position);
            }
        }

        [Test]
        public void MergedEvent_CarriesAllFields_AdvancedLargeIntoMedium()
        {
            using (var run = StepRun.Execute(GameMode.AdvancedColorMerge, ". . Ml Gm", TestBoards.Right, NoSpawn))
            {
                var merged = run.Of(SimEventType.Merged);
                Assert.AreEqual(1, merged.Count);
                var e = merged[0];
                Assert.AreEqual(new int2(2, 0), e.From);
                Assert.AreEqual(new int2(3, 0), e.Position);
                Assert.AreEqual(BlockColor.White, e.Color);
                Assert.AreEqual(BlockSize.Large, e.Size);
                Assert.AreEqual(BlockSize.Large, e.MoverSize, "the mover was the Large Magenta");
                Assert.AreEqual(ScoreTier.Tier3, e.Tier);
                Assert.AreEqual(100, e.Points);
                Assert.IsFalse(e.DestroysBoth);
            }
        }

        [Test]
        public void MergedEvent_MoverSizeIsTheMoversSizeBeforeMerging_ColorMerge()
        {
            using (var run = StepRun.Execute(GameMode.ColorMerge, ". . Gl Rs", TestBoards.Right, NoSpawn))
            {
                var merged = run.Of(SimEventType.Merged);
                Assert.AreEqual(1, merged.Count);
                Assert.AreEqual(BlockSize.Large, merged[0].MoverSize);
                Assert.AreEqual(BlockColor.Yellow, merged[0].Color);
                Assert.AreEqual(BlockSize.Large, merged[0].Size, "result size is the mover's size");
                TestBoards.AssertBoard(". . . Yl", run.Board);
            }
        }

        [Test]
        public void MergedEvent_DestroyingMerge_SetsDestroysBothAndEmptiesBothCells()
        {
            using (var run = StepRun.Execute(GameMode.Standard, ". . Rl Rl", TestBoards.Right, NoSpawn))
            {
                var merged = run.Of(SimEventType.Merged);
                Assert.AreEqual(1, merged.Count);
                Assert.IsTrue(merged[0].DestroysBoth);
                Assert.AreEqual(ScoreTier.Tier3, merged[0].Tier);
                Assert.AreEqual(100, merged[0].Points);
                Assert.AreEqual(new int2(2, 0), merged[0].From);
                Assert.AreEqual(new int2(3, 0), merged[0].Position);
                TestBoards.AssertBoard(". . . .", run.Board);
            }
        }

        [Test]
        public void NoOpMove_OnAnEmptyBoard_IsAcceptedWithNoEvents()
        {
            using (var run = StepRun.Execute(GameMode.Standard, ". . . .", TestBoards.Right, NoSpawn))
            {
                Assert.IsTrue(run.Result.Accepted);
                Assert.AreEqual(0, run.Events.Length);
                Assert.IsFalse(run.Result.GameOver);
            }
        }
    }
}
