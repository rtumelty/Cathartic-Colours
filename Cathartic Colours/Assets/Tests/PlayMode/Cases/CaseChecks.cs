// Checks shared by the test-plan cases (CC-046.3): the merge case (start from a known board, make one move, check the board,
// the score and the move counter), the first-move case, and the boards the game-over cases share.
using System.Collections;
using NUnit.Framework;
using Simulation;
using TestSupport;

namespace PlayModeTests
{
    static class CaseChecks
    {
        /// <summary>Score and move count the merge cases start from, so "Score +10" is a real change from a non-zero value.</summary>
        public const int StartScore = 40, StartMoves = 3;

        /// <summary>
        /// A merge case: starts from <paramref name="start"/> (spawning off, no indicator, so the board is exact), presses
        /// <paramref name="direction"/> once and checks the board, that the score rose by <paramref name="points"/>, that the move
        /// counter rose by one, and that the HUD labels show both. Saves a screenshot named <paramref name="shot"/> after the move.
        /// </summary>
        public static IEnumerator Merge(GameDriver driver, GameMode mode, string start, Direction direction, string expected, int points, string shot = null, string beforeShot = null)
        {
            yield return driver.ResumeWith(start, mode, moveCount: StartMoves, score: StartScore);
            driver.AudioEvents();
            Assert.AreEqual(start, driver.Board(), "The game starts from the known board.");
            if (beforeShot != null)
                yield return driver.Screenshot(beforeShot);

            yield return driver.Move(direction);

            driver.AssertBoard(expected, $"{direction} from '{start}'");
            Assert.AreEqual(StartScore + points, driver.Score, $"Score after {direction} from '{start}'");
            Assert.AreEqual(StartMoves + 1, driver.MoveCount, "The move counts, merge or not.");
            Assert.AreEqual($"Score: {StartScore + points}", driver.ScoreText);
            Assert.AreEqual($"Moves: {StartMoves + 1}", driver.MovesText);
            if (shot != null)
                yield return driver.Screenshot(shot);
        }

        /// <summary>
        /// The first-move case of every mode: a new game has one indicator at the centre cell (width / 2, height / 2) and
        /// nothing else; the first move resolves it in place to a Small Red, Green or Blue block, a new indicator appears
        /// in a different cell, and the counters read 1 and 0.
        /// </summary>
        public static IEnumerator FirstMove(GameDriver driver, GameMode mode, int width, int height, string shotPrefix)
        {
            yield return driver.StartFromMenu(mode, width, height, true);
            int cx = width / 2, cy = height / 2;
            Assert.AreEqual(mode, driver.Mode);
            Assert.AreEqual(width, driver.GridWidth);
            Assert.AreEqual(height, driver.GridHeight);
            Assert.AreEqual(BoardNotation.Build(width, height, (cx, cy, "i")), driver.Board(), "A new game has one white block at the centre.");
            Assert.AreEqual(0, driver.MoveCount);
            yield return driver.Screenshot(shotPrefix + "-before");

            yield return driver.Move(Direction.Left);

            string after = driver.Board();
            Assert.IsTrue(BoardNotation.IsResolvedPrimary(BoardNotation.ParseCell(BoardNotation.TokenAt(after, cx, cy))),
                $"The white block at ({cx},{cy}) stays and becomes a Small Red, Green or Blue block. Board: {after}");
            Assert.AreEqual(1, BoardNotation.Count(after, "i"), "A new white block appears. Board: " + after);
            Assert.AreNotEqual("i", BoardNotation.TokenAt(after, cx, cy), "The new white block is in a different cell.");
            Assert.AreEqual(2, driver.Blocks(), "Only the resolved block and the new white block are on the board.");
            Assert.AreEqual(1, driver.MoveCount);
            Assert.AreEqual(0, driver.Score);
            Assert.AreEqual("Moves: 1", driver.MovesText);
            Assert.AreEqual("Score: 0", driver.ScoreText);
            yield return driver.Screenshot(shotPrefix + "-after");
        }

        /// <summary>
        /// A 4x4 Standard board with one empty cell (bottom left), no indicator and a Red/Green checkerboard, so no two
        /// neighbours are equal. Spawning on, any move that slides nothing puts the new indicator in the empty cell and the
        /// board is full with no merge: game over. Pressing Right or Up slides nothing (the empty cell is at the wall they push away from).
        /// </summary>
        public const string AlmostFullCheckerboard = "Gs Rs Gs Rs|Rs Gs Rs Gs|Gs Rs Gs Rs|. Gs Rs Gs";

        /// <summary>The checkerboard with the indicator in the free cell: full, no merge possible.</summary>
        public const string GameOverBoard = "Gs Rs Gs Rs|Rs Gs Rs Gs|Gs Rs Gs Rs|i Gs Rs Gs";

        /// <summary>Plays the move that ends the game on <see cref="AlmostFullCheckerboard"/> and checks the board is the game over board.</summary>
        public static IEnumerator ReachGameOver(GameDriver driver, int score = 0, int moves = 0)
        {
            yield return driver.ResumeWith(AlmostFullCheckerboard, GameMode.Standard, spawnNextColour: true, moveCount: moves, score: score);
            yield return driver.Move(Direction.Right);
            Assert.AreEqual(GameOverBoard, driver.Board(), "Right slides nothing, so the new white block fills the one free cell.");
            Assert.IsTrue(driver.IsGameOver, "A full board with no merge is game over.");
        }
    }

    static class DriverExtensions
    {
        /// <summary>The number of blocks on the board, indicators included.</summary>
        public static int Blocks(this GameDriver driver) => PlayModeTestSupport.Blocks().Length;
    }
}
