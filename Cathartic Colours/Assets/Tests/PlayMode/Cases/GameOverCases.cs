// CC-046.3: the Game over (OVER) cases of docs/wiki/test-plan.md, TP-OVER-07 then TP-OVER-01 to TP-OVER-06 (the plan's order).
// The steps follow the test plan, but the board is set up directly instead of played until it fills: a Standard 4x4 board
// (the plan's "small board fills fastest") written as a save and resumed. The boards are checkerboards of Red and Green
// Small blocks, which never merge, so "full with no merge available" is certain.
//
// Not covered here: the part of TP-OVER-07 that says the new white block "appears in different cells over many moves". That is
// statistical; the extended regression cases (TP-STD-11, TP-CM-07, TP-ACM-08) check after every move that a new white
// block appears whenever a cell is free.
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Simulation;
using TestSupport;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace PlayModeTests
{
    [Category("TP_OVER")]
    public class GameOverCases : PlaythroughTest
    {
        const GameMode Mode = GameMode.Standard;

        // A full 4x4 board with the white block on it and one pair of Blue blocks (top row) that can merge.
        const string FullWithAMerge = "Bs Bs Gs Rs|Rs Gs Rs Gs|Gs Rs Gs Rs|Rs Gs Rs i";

        VisualElement GameOverPanel => Driver.Find<VisualElement>("game-over-panel");

        [UnityTest, Category("TP_OVER_07")]
        public IEnumerator TP_OVER_07_AFreeCellAlwaysGetsTheNewWhiteBlock()
        {
            // Step 1 and 2: one empty cell, nothing slides, so the new white block must land in that cell.
            yield return Driver.ResumeWith(CaseChecks.AlmostFullCheckerboard, Mode, spawnNextColour: true);
            yield return Driver.Move(Direction.Right);
            Driver.AssertBoard(CaseChecks.GameOverBoard, "The new white block is in the one free cell");
            Assert.AreEqual(1, Driver.MoveCount);

            // Step 3: a full board with the white block on it and a merge still possible. A press that merges nothing
            // (up: no equal pair is in a column) resolves the white block and spawns nothing; the game goes on.
            yield return Driver.ResumeWith(FullWithAMerge, Mode, spawnNextColour: true, moveCount: 5, score: 20);
            yield return Driver.Move(Direction.Up);

            string after = Driver.Board();
            Driver.AssertBoard("Bs Bs Gs Rs|Rs Gs Rs Gs|Gs Rs Gs Rs|Rs Gs Rs ?", "The white block turns coloured in place");
            Assert.AreEqual(0, BoardNotation.Count(after, "i"), "There is no free cell, so no new white block. Board: " + after);
            Assert.AreEqual(6, Driver.MoveCount, "The move still counts.");
            Assert.AreEqual(20, Driver.Score);
            Assert.IsFalse(Driver.IsGameOver, "A merge is still possible.");
            yield return GameDriver.Frames(2);
            Assert.IsFalse(Driver.IsGameOverPanelShown);
            yield return Driver.Screenshot("TP-OVER-07");
        }

        [UnityTest, Category("TP_OVER_01")]
        public IEnumerator TP_OVER_01_FullBoardWithAMergeAvailableKeepsPlaying()
        {
            yield return Driver.ResumeWith(FullWithAMerge, Mode, moveCount: 5, score: 20);
            Assert.IsFalse(Driver.IsGameOver, "The board is full but a pair can merge.");
            Assert.IsFalse(Driver.IsGameOverPanelShown, "No game-over panel.");
            yield return Driver.Screenshot("TP-OVER-01-before");

            yield return Driver.Move(Direction.Left);

            // The Blue pair merges, then the blocks behind it close the gap; the white block resolves in place.
            Driver.AssertBoard("Bm Gs Rs .|Rs Gs Rs Gs|Gs Rs Gs Rs|Rs Gs Rs ?", "The merging move is accepted");
            Assert.AreEqual(6, Driver.MoveCount);
            Assert.AreEqual(30, Driver.Score);
            yield return GameDriver.Frames(2);
            Assert.IsFalse(Driver.IsGameOverPanelShown, "Still no game-over panel.");
            yield return Driver.Screenshot("TP-OVER-01-after");
        }

        [UnityTest, Category("TP_OVER_02")]
        public IEnumerator TP_OVER_02_GameOverAppearsWhenFullAndNoMergeExists()
        {
            yield return Driver.ResumeWith(CaseChecks.AlmostFullCheckerboard, Mode, spawnNextColour: true, moveCount: 7, score: 30);
            Assert.IsFalse(Driver.IsGameOverPanelShown);

            yield return Driver.Move(Direction.Right);
            yield return GameDriver.Frames(2);

            Driver.AssertBoard(CaseChecks.GameOverBoard);
            Assert.IsTrue(Driver.IsGameOver);
            Assert.IsTrue(Driver.IsGameOverPanelShown, "The game-over panel appears.");
            Assert.IsTrue(Shown(GameOverPanel.Q<Button>("gameover-restart-button")), "The panel has a Restart button.");
            Assert.IsTrue(Shown(GameOverPanel.Q<Button>("main-menu-button")), "The panel has a Main Menu button.");
            Assert.AreEqual("Restart", GameOverPanel.Q<Button>("gameover-restart-button").text);
            Assert.AreEqual("Main Menu", GameOverPanel.Q<Button>("main-menu-button").text);
            Assert.AreEqual("Score: 30", Driver.ScoreText, "Score keeps its final value.");
            Assert.AreEqual("Moves: 8", Driver.MovesText, "Moves keeps its final value, counting the game-over move.");
            yield return Driver.Screenshot("TP-OVER-02");
        }

        [UnityTest, Category("TP_OVER_03")]
        public IEnumerator TP_OVER_03_NothingChangesAfterGameOverWhateverIsPressed()
        {
            yield return CaseChecks.ReachGameOver(Driver, score: 30, moves: 7);
            yield return GameDriver.Frames(2);
            string board = Driver.Board();
            int score = Driver.Score, moves = Driver.MoveCount;
            Driver.AudioEvents(); // the game-over move's own sounds

            foreach (var d in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
            {
                yield return Driver.Move(d);
                AssertUnchanged(board, score, moves, "arrow " + d);
            }
            foreach (var d in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
            {
                yield return Driver.MoveWasd(d);
                AssertUnchanged(board, score, moves, "WASD " + d);
            }
            foreach (var d in new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right })
            {
                yield return Driver.Swipe(d);
                AssertUnchanged(board, score, moves, "swipe " + d);
            }
            CollectionAssert.IsEmpty(Driver.AudioEvents(), "No move sound or any other sound is raised.");
            Assert.IsTrue(Driver.IsGameOverPanelShown, "The panel stays up.");
            yield return Driver.Screenshot("TP-OVER-03");
        }

        void AssertUnchanged(string board, int score, int moves, string what)
        {
            Assert.AreEqual(board, Driver.Board(), "The board after " + what);
            Assert.AreEqual(score, Driver.Score, "Score after " + what);
            Assert.AreEqual(moves, Driver.MoveCount, "Moves after " + what + ": a rejected move is not counted");
        }

        [UnityTest, Category("TP_OVER_04")]
        public IEnumerator TP_OVER_04_RestartStartsAFreshGame()
        {
            yield return CaseChecks.ReachGameOver(Driver, score: 30, moves: 7);
            yield return GameDriver.Frames(2);
            Assert.IsTrue(Driver.IsGameOverPanelShown);

            yield return Driver.Click(GameOverPanel.Q<Button>("gameover-restart-button"));
            yield return GameDriver.Frames(3);

            Assert.IsFalse(Driver.IsGameOverPanelShown, "The panel disappears.");
            Driver.AssertBoard(BoardNotation.Build(4, 4, (2, 2, "i")), "Only the white block at (2,2)");
            Assert.AreEqual("Score: 0", Driver.ScoreText);
            Assert.AreEqual("Moves: 0", Driver.MovesText);
            Assert.AreEqual(0, Driver.Score);
            Assert.AreEqual(0, Driver.MoveCount);
            Assert.IsFalse(Driver.IsGameOver);
            yield return Driver.Screenshot("TP-OVER-04");

            yield return Driver.Move(Direction.Left);
            Assert.AreEqual(1, Driver.MoveCount, "The game accepts moves again.");
        }

        [UnityTest, Category("TP_OVER_05")]
        public IEnumerator TP_OVER_05_RestartDoesNotDuplicateGridCells()
        {
            yield return CaseChecks.ReachGameOver(Driver);
            yield return GameDriver.Frames(2);
            Assert.AreEqual(16, CountCells(), "Before the restart there are 16 cells.");

            yield return Driver.Click(GameOverPanel.Q<Button>("gameover-restart-button"));
            yield return GameDriver.Frames(5); // destroyed objects go at the end of the frame

            Assert.AreEqual(16, CountCells(), "Exactly one Cell_<x>_<y> object per cell of the 4x4 board.");
        }

        [UnityTest, Category("TP_OVER_06")]
        public IEnumerator TP_OVER_06_NoResumeIsOfferedAfterAFinishedGame()
        {
            yield return CaseChecks.ReachGameOver(Driver, score: 30, moves: 7);
            yield return GameDriver.Frames(2);
            yield return Driver.Click(GameOverPanel.Q<Button>("gameover-restart-button"));
            yield return GameDriver.Frames(3);

            yield return Driver.ReturnToMenu();

            Assert.IsFalse(Driver.IsShown("resume-game-button"), "The save was deleted when the game ended.");
            CollectionAssert.IsEmpty(Driver.SaveFiles().Where(f => Regex.IsMatch(f, @"^savegame(\.\d)?\.json(\.tmp)?$")), "No save file is left.");
            yield return Driver.Screenshot("TP-OVER-06");
        }

        static int CountCells() => Object.FindObjectsByType<GameObject>().Count(g => Regex.IsMatch(g.name, @"^Cell_\d+_\d+$"));

        static bool Shown(VisualElement element) =>
            element != null && element.resolvedStyle.display != DisplayStyle.None && element.worldBound.width > 0 && element.worldBound.height > 0;
    }
}
