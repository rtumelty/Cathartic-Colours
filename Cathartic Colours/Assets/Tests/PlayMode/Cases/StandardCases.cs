// CC-046.3: the Standard mode (STD) cases of docs/wiki/test-plan.md, TP-STD-01 to TP-STD-11, one test each.
// The steps follow the test plan. Where the plan says "play until it happens" the test starts from a known board
// (ResumeWith) with spawning off, so the board is exact; TP-STD-01 and TP-STD-11 start a new game from the menu with spawning on.
// The plan's one-row examples ("[. . Rs Rs]") are the bottom row of a 4x4 board.
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Simulation;
using TestSupport;
using UnityEngine;
using UnityEngine.TestTools;

namespace PlayModeTests
{
    [Category("TP_STD")]
    public class StandardCases : PlaythroughTest
    {
        const GameMode Mode = GameMode.Standard;

        // A lone Red Small at (2,2) of a 6x6 board, and where each move takes it.
        static string Lone(int x, int y) => BoardNotation.Build(6, 6, (x, y, "Rs"));

        static (int x, int y) Step(int x, int y, Direction d)
        {
            switch (d)
            {
                case Direction.Up: return (x, y + 1);
                case Direction.Down: return (x, y - 1);
                case Direction.Left: return (x - 1, y);
                default: return (x + 1, y);
            }
        }

        [UnityTest, Category("TP_STD_01")]
        public IEnumerator TP_STD_01_FirstMoveConvertsTheWhiteBlock()
        {
            yield return CaseChecks.FirstMove(Driver, Mode, 6, 6, "TP-STD-01");
        }

        [UnityTest, Category("TP_STD_02")]
        public IEnumerator TP_STD_02_ABlockMovesOneCellPerPressInAllFourDirectionsWithArrowsAndWasd()
        {
            yield return Driver.ResumeWith(Lone(2, 2), Mode);
            int x = 2, y = 2;
            var keys = new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

            foreach (var d in keys)
            {
                (x, y) = Step(x, y, d);
                yield return Driver.Move(d);
                Driver.AssertBoard(Lone(x, y), "arrow " + d);
            }
            foreach (var d in keys)
            {
                (x, y) = Step(x, y, d);
                yield return Driver.MoveWasd(d);
                Driver.AssertBoard(Lone(x, y), "WASD " + d);
            }
            Assert.AreEqual(8, Driver.MoveCount);

            // The block that was white changes colour but does not move; a coloured block beside it does.
            string start = BoardNotation.Build(4, 4, (1, 2, "Rs"), (2, 2, "i"));
            yield return Driver.ResumeWith(start, Mode);
            yield return Driver.Move(Direction.Up);
            Driver.AssertBoard(BoardNotation.Build(4, 4, (1, 3, "Rs"), (2, 2, "?")), "The white block resolves in place while the Red block moves up");
            yield return Driver.Screenshot("TP-STD-02");
        }

        [UnityTest, Category("TP_STD_03")]
        public IEnumerator TP_STD_03_WallsAndTheWhiteBlockStopMovement()
        {
            // A block at the wall stays, and the press still counts.
            string atWall = BoardNotation.Build(4, 4, (3, 1, "Rs"));
            yield return Driver.ResumeWith(atWall, Mode);
            yield return Driver.Move(Direction.Right);
            Driver.AssertBoard(atWall, "The block is against the right wall");
            Assert.AreEqual(1, Driver.MoveCount, "The extra press counts.");
            Assert.AreEqual("Moves: 1", Driver.MovesText);

            // A coloured block with the white block ahead: no move, no merge; the white block resolves in place.
            yield return Driver.ResumeWith(BoardNotation.BottomRow(". . Rs i"), Mode);
            yield return Driver.Move(Direction.Right);
            Driver.AssertBoard(BoardNotation.BottomRow(". . Rs ?"), "The Red block neither moves into nor merges with the white block");
            Assert.AreEqual(1, Driver.MoveCount);
            Assert.AreEqual(0, Driver.Score);
            yield return Driver.Screenshot("TP-STD-03");
        }

        [UnityTest, Category("TP_STD_04")]
        public IEnumerator TP_STD_04_SmallPairMergesToMedium()
        {
            yield return CaseChecks.Merge(Driver, Mode, BoardNotation.BottomRow(". . Rs Rs"), Direction.Right,
                BoardNotation.BottomRow(". . . Rm"), 10, "TP-STD-04-after", "TP-STD-04-before");
        }

        [UnityTest, Category("TP_STD_05")]
        public IEnumerator TP_STD_05_BlocksThatMustNotMergeStayAsTheyAre()
        {
            string differentColour = BoardNotation.BottomRow(". . Rs Bs");
            yield return CaseChecks.Merge(Driver, Mode, differentColour, Direction.Right, differentColour, 0, "TP-STD-05-colour");
            string differentSize = BoardNotation.BottomRow(". . Rs Rm");
            yield return CaseChecks.Merge(Driver, Mode, differentSize, Direction.Right, differentSize, 0, "TP-STD-05-size");
        }

        [UnityTest, Category("TP_STD_06")]
        public IEnumerator TP_STD_06_OnlyOneMergePerBlockInAMove()
        {
            yield return CaseChecks.Merge(Driver, Mode, BoardNotation.BottomRow(". Rs Rs Rs"), Direction.Right,
                BoardNotation.BottomRow(". . Rs Rm"), 10, "TP-STD-06");
        }

        [UnityTest, Category("TP_STD_07")]
        public IEnumerator TP_STD_07_MediumPairMakesLargeAndLargePairClears()
        {
            yield return CaseChecks.Merge(Driver, Mode, BoardNotation.BottomRow(". . Rm Rm"), Direction.Right,
                BoardNotation.BottomRow(". . . Rl"), 50, "TP-STD-07-medium");
            yield return CaseChecks.Merge(Driver, Mode, BoardNotation.BottomRow(". . Rl Rl"), Direction.Right,
                BoardNotation.BottomRow(". . . ."), 100, "TP-STD-07-large");
        }

        [UnityTest, Category("TP_STD_08")]
        public IEnumerator TP_STD_08_SwipesMoveLikeArrowKeysAndAShortDragDoesNot()
        {
            yield return Driver.ResumeWith(Lone(2, 2), Mode);
            int x = 2, y = 2;
            int moves = 0;
            foreach (var d in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
            {
                (x, y) = Step(x, y, d);
                yield return Driver.Swipe(d);
                Driver.AssertBoard(Lone(x, y), "swipe " + d);
                Assert.AreEqual(++moves, Driver.MoveCount, "Each long swipe is one move: " + d);
            }

            yield return Driver.Swipe(Direction.Right, distance: 10f);
            Driver.AssertBoard(Lone(x, y), "A short drag moves nothing");
            Assert.AreEqual(moves, Driver.MoveCount, "A short drag makes no move.");
            Assert.AreEqual($"Moves: {moves}", Driver.MovesText);
        }

        [UnityTest, Category("TP_STD_09")]
        public IEnumerator TP_STD_09_GamepadStickAndDpadMoveOneCellPerPush()
        {
            yield return Driver.ResumeWith(Lone(2, 2), Mode);
            int x = 2, y = 2;
            int moves = 0;
            var directions = new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right };

            foreach (var d in directions)
            {
                (x, y) = Step(x, y, d);
                yield return Driver.GamepadStick(d, holdSeconds: 1f);
                Driver.AssertBoard(Lone(x, y), "stick " + d);
                Assert.AreEqual(++moves, Driver.MoveCount, "Holding the stick for a second is still one move: " + d);
            }
            foreach (var d in directions)
            {
                (x, y) = Step(x, y, d);
                yield return Driver.GamepadDpad(d);
                Driver.AssertBoard(Lone(x, y), "d-pad " + d);
                Assert.AreEqual(++moves, Driver.MoveCount, "One d-pad press is one move: " + d);
            }
        }

        [UnityTest, Category("TP_STD_10")]
        public IEnumerator TP_STD_10_AllEightKeysMoveWithNoConsoleErrors()
        {
            var problems = new List<string>();
            void Listen(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Exception && message.Contains("InvalidOperationException"))
                    problems.Add("InvalidOperationException: " + message);
                if (message.Contains("Game saved to") || message.Contains("Save game loaded from"))
                    problems.Add("Unwanted log line: " + message);
            }

            yield return Driver.ResumeWith(Lone(2, 2), Mode);
            Application.logMessageReceived += Listen;
            try
            {
                int x = 2, y = 2;
                foreach (bool wasd in new[] { false, true })
                    foreach (var d in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
                    {
                        (x, y) = Step(x, y, d);
                        yield return wasd ? Driver.MoveWasd(d) : Driver.Move(d);
                        Driver.AssertBoard(Lone(x, y), (wasd ? "WASD " : "arrow ") + d);
                    }
            }
            finally
            {
                Application.logMessageReceived -= Listen;
            }
            Assert.AreEqual(8, Driver.MoveCount);
            CollectionAssert.IsEmpty(problems, "The Console stays free of the up-arrow exception and of save log lines.");
        }

        [UnityTest, Category("TP_STD_11")]
        public IEnumerator TP_STD_11_RegressionExtendedPlayFollowsTheStandardRules()
        {
            yield return Driver.StartFromMenu(Mode, 6, 6, true);
            var report = new ExtendedPlayReport();

            yield return ExtendedPlay.Run(Driver, Mode, 150, report);

            TestContext.WriteLine("TP-STD-11 " + report.Summary());
            yield return Driver.Screenshot("TP-STD-11-end");
        }
    }
}
