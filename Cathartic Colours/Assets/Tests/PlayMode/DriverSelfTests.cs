// CC-046.2: tests of the GameDriver itself. Each one checks that a driver step does what its name says on a board whose
// outcome is known, so a later playthrough test that fails points at the game, not at the driver:
//  - ResumeWith reads back exactly the board, move count, score and mode that were written;
//  - every input kind (arrow key, WASD, touch swipe, gamepad d-pad, gamepad stick) makes one move the right way;
//  - a swipe shorter than the game's threshold makes no move;
//  - a screenshot file is written and isn't blank;
//  - a Small pair merge raises the move and merge sounds, and scores.
// Spawning is off in the move cases, so the boards are exact.
using System.Collections;
using System.IO;
using NUnit.Framework;
using Simulation;
using TestSupport;
using UnityEngine.TestTools;

namespace PlayModeTests
{
    [Category("CC046")]
    public class DriverSelfTests : PlaythroughTest
    {
        // A lone block at (1,1) on a 4x4 board, and where each move takes it.
        const string Lone = ". . . .|. . . .|. Rs . .|. . . .";
        const string LoneUp = ". . . .|. Rs . .|. . . .|. . . .";
        const string LoneDown = ". . . .|. . . .|. . . .|. Rs . .";
        const string LoneLeft = ". . . .|. . . .|Rs . . .|. . . .";
        const string LoneRight = ". . . .|. . . .|. . Rs .|. . . .";

        static string ExpectedBoard(Direction d)
        {
            switch (d)
            {
                case Direction.Up: return LoneUp;
                case Direction.Down: return LoneDown;
                case Direction.Left: return LoneLeft;
                default: return LoneRight;
            }
        }

        [UnityTest]
        public IEnumerator ResumeWith_ReadsBackTheBoardMoveCountScoreAndMode()
        {
            const string board = ". . . .|. Rs . Gm|. . i .|Bl . . .";
            yield return Driver.ResumeWith(board, GameMode.Standard, moveCount: 5, score: 30);

            Assert.AreEqual(board, Driver.Board());
            Assert.AreEqual(5, Driver.MoveCount);
            Assert.AreEqual(30, Driver.Score);
            Assert.AreEqual("Moves: 5", Driver.MovesText);
            Assert.AreEqual("Score: 30", Driver.ScoreText);
            Assert.AreEqual(GameMode.Standard, Driver.Mode);
            Assert.AreEqual(4, Driver.GridWidth);
            Assert.AreEqual(4, Driver.GridHeight);
            Assert.IsTrue(Driver.WaitingForInput);
            Assert.IsFalse(Driver.IsGameOver);
            Assert.IsFalse(Driver.IsGameOverPanelShown);
        }

        [UnityTest]
        public IEnumerator ResumeWith_TakesTheBoardSizeFromTheBoardAndTheModeFromTheArgument()
        {
            const string board = ". . . . .|. . . . .|Rs . . . Bs";
            yield return Driver.ResumeWith(board, GameMode.ColorMerge);

            Assert.AreEqual(board, Driver.Board());
            Assert.AreEqual(GameMode.ColorMerge, Driver.Mode);
            Assert.AreEqual(5, Driver.GridWidth);
            Assert.AreEqual(3, Driver.GridHeight);
            Assert.AreEqual(0, Driver.MoveCount);
            Driver.AssertBoard(". . . . .|. . . . .|Rs . . . Bs");
        }

        [Test]
        public void WriteSave_RejectsAQuestionMarkToken()
        {
            Assert.Throws<System.ArgumentException>(() => Driver.WriteSave(". . .|. ? .|. . .", GameMode.Standard));
        }

        [UnityTest]
        public IEnumerator AssertBoard_FailsWithTheDifferingCellNamed()
        {
            yield return Driver.ResumeWith(Lone, GameMode.Standard);

            Driver.AssertBoard(Lone);
            var failure = Assert.Throws<AssertionException>(() => Driver.AssertBoard(LoneUp, "wrong board"));
            StringAssert.Contains("wrong board", failure.Message);
            StringAssert.Contains("cell (1,", failure.Message);
        }

        [UnityTest]
        public IEnumerator ArrowKey_MovesABlockOneCell([Values] Direction direction)
        {
            yield return Driver.ResumeWith(Lone, GameMode.Standard);
            yield return Driver.Move(direction);
            Driver.AssertBoard(ExpectedBoard(direction), "arrow key " + direction);
            Assert.AreEqual(1, Driver.MoveCount);
        }

        [UnityTest]
        public IEnumerator WasdKey_MovesABlockOneCell([Values] Direction direction)
        {
            yield return Driver.ResumeWith(Lone, GameMode.Standard);
            yield return Driver.MoveWasd(direction);
            Driver.AssertBoard(ExpectedBoard(direction), "WASD " + direction);
            Assert.AreEqual(1, Driver.MoveCount);
        }

        [UnityTest]
        public IEnumerator Swipe_MovesABlockOneCell([Values] Direction direction)
        {
            yield return Driver.ResumeWith(Lone, GameMode.Standard);
            yield return Driver.Swipe(direction);
            Driver.AssertBoard(ExpectedBoard(direction), "swipe " + direction);
            Assert.AreEqual(1, Driver.MoveCount);
        }

        [UnityTest]
        public IEnumerator GamepadDpad_MovesABlockOneCell([Values] Direction direction)
        {
            yield return Driver.ResumeWith(Lone, GameMode.Standard);
            yield return Driver.GamepadDpad(direction);
            Driver.AssertBoard(ExpectedBoard(direction), "d-pad " + direction);
            Assert.AreEqual(1, Driver.MoveCount);
        }

        [UnityTest]
        public IEnumerator GamepadStick_MovesABlockOneCell([Values] Direction direction)
        {
            yield return Driver.ResumeWith(Lone, GameMode.Standard);
            yield return Driver.GamepadStick(direction);
            Driver.AssertBoard(ExpectedBoard(direction), "stick " + direction);
            Assert.AreEqual(1, Driver.MoveCount);
        }

        [UnityTest]
        public IEnumerator GamepadStick_HeldDownMakesOnlyOneMove()
        {
            yield return Driver.ResumeWith(Lone, GameMode.Standard);
            yield return Driver.GamepadStick(Direction.Right, holdSeconds: 0.3f);
            Driver.AssertBoard(LoneRight);
            Assert.AreEqual(1, Driver.MoveCount, "One tilt is one move, however long the stick is held.");
        }

        [UnityTest]
        public IEnumerator ShortSwipe_MakesNoMove()
        {
            yield return Driver.ResumeWith(Lone, GameMode.Standard);
            yield return Driver.Swipe(Direction.Right, distance: 10f);
            Driver.AssertBoard(Lone);
            Assert.AreEqual(0, Driver.MoveCount);
        }

        [UnityTest]
        public IEnumerator Screenshot_WritesAFileThatIsNotBlank()
        {
            yield return Driver.ResumeWith(". . . .|. Rs . Gm|. . . .|Bl . . .", GameMode.Standard);
            yield return Driver.Screenshot("driver-self-test");

            Assert.IsTrue(File.Exists(Driver.LastScreenshotPath), "The screenshot was saved to the report folder.");
            StringAssert.StartsWith(Driver.ReportFolder, Driver.LastScreenshotPath);
            Assert.Greater(new FileInfo(Driver.LastScreenshotPath).Length, 10000, "A rendered board and HUD compress to more than a blank image.");
        }

        [UnityTest]
        public IEnumerator SmallPairMerge_RaisesTheMoveAndMergeSoundsAndScores()
        {
            yield return Driver.ResumeWith(". . . .|. . . .|. . Rs Rs|. . . .", GameMode.Standard);
            Driver.AudioEvents(); // nothing yet

            yield return Driver.Move(Direction.Right);

            Driver.AssertBoard(". . . .|. . . .|. . . Rm|. . . .");
            Assert.AreEqual(10, Driver.Score, "A Small merge is a tier 1 merge: 10 points.");
            Assert.AreEqual("Score: 10", Driver.ScoreText);
            var events = Driver.AudioEvents();
            CollectionAssert.Contains(events, "event:/Block_Merge_Small");
            CollectionAssert.Contains(events, "event:/Block_Move");
        }
    }
}
