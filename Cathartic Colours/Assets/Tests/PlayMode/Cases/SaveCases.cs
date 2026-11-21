// CC-046.3: the Save/Resume (SAVE) cases of docs/wiki/test-plan.md, TP-SAVE-01 to TP-SAVE-11 (except two that stay manual).
// The steps follow the test plan. The driver redirects the save to a temp folder, so "the save folder" of the plan is
// Driver.SaveFolder and the real save is never touched. A "saved game" is written with WriteSave and resumed through the
// menu's real Resume button.
//  - TP-SAVE-06 (force-closing the app keeps the last move) stays manual: it needs a device (CC-047).
//  - TP-SAVE-12 (resume a game saved by the previous build) stays manual: it needs the previous build installed on a
//    device (CC-047).
using System.Collections;
using System.IO;
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
    [Category("TP_SAVE")]
    public class SaveCases : PlaythroughTest
    {
        const GameMode Mode = GameMode.Standard;

        // A Standard 6x6 game with several blocks and the white block at (3,1), as if 7 moves had been played.
        static readonly string SavedBoard = BoardNotation.Build(6, 6,
            (0, 0, "Rs"), (1, 0, "Gm"), (2, 3, "Bl"), (4, 4, "Rs"), (5, 5, "Gs"), (3, 1, "i"));
        const int SavedMoves = 7, SavedScore = 30;

        /// <summary>Saves the game above, opens the menu, switches the menu to Color Merge 4x4 and resumes: the game must come back as Standard 6x6.</summary>
        IEnumerator ResumeTheSavedGame()
        {
            Driver.WriteSave(SavedBoard, Mode, spawnNextColour: true, moveCount: SavedMoves, score: SavedScore);
            yield return Driver.OpenMenu();
            yield return Driver.SelectDropdown("preset-dropdown", "ColorMerge_4x4");
            yield return GameDriver.WaitUntil(() => Driver.IsShown("resume-game-button"), "the Resume button");
            yield return Driver.Click("resume-game-button");
            yield return Driver.WaitForGameplay();
        }

        IEnumerator ResumeFromMenu()
        {
            yield return GameDriver.WaitUntil(() => Driver.IsShown("resume-game-button"), "the Resume button");
            yield return Driver.Click("resume-game-button");
            yield return Driver.WaitForGameplay();
        }

        [UnityTest, Category("TP_SAVE_01")]
        public IEnumerator TP_SAVE_01_ResumeButtonAppearsAfterAMove()
        {
            yield return Driver.StartFromMenu(Mode, 6, 6, true);
            yield return Driver.Move(Direction.Left);

            yield return Driver.ReturnToMenu();

            Assert.IsTrue(Driver.IsShown("resume-game-button"), "The main menu shows a Resume button.");
            yield return Driver.Screenshot("TP-SAVE-01");
        }

        [UnityTest, Category("TP_SAVE_02")]
        public IEnumerator TP_SAVE_02_ResumeRestoresTheSavedGame()
        {
            yield return ResumeTheSavedGame();

            Assert.AreEqual(Mode, Driver.Mode, "The saved game loads as Standard, not Color Merge.");
            Assert.AreEqual(6, Driver.GridWidth);
            Assert.AreEqual(6, Driver.GridHeight);
            Assert.AreEqual(SavedMoves, Driver.MoveCount);
            Assert.AreEqual(SavedScore, Driver.Score);
            Assert.AreEqual($"Moves: {SavedMoves}", Driver.MovesText);
            Assert.AreEqual($"Score: {SavedScore}", Driver.ScoreText);
            Assert.AreEqual(SavedBoard, Driver.Board(), "Every block is where it was, with its colour and size.");
            yield return Driver.Screenshot("TP-SAVE-02");
        }

        [UnityTest, Category("TP_SAVE_03")]
        public IEnumerator TP_SAVE_03_WhiteBlockIsUnchangedAfterResume()
        {
            yield return ResumeTheSavedGame();

            string board = Driver.Board();
            Assert.AreEqual("i", BoardNotation.TokenAt(board, 3, 1), "The white block is in the same cell and still white.");
            Assert.AreEqual(1, BoardNotation.Count(board, "i"), "No additional white block appeared. Board: " + board);
        }

        [UnityTest, Category("TP_SAVE_07")]
        public IEnumerator TP_SAVE_07_NothingChangesAfterResumeUntilTheFirstMove()
        {
            yield return ResumeTheSavedGame();
            Driver.AudioEvents();

            yield return new WaitForSeconds(5f);

            Assert.AreEqual(SavedBoard, Driver.Board(), "Nothing moved while waiting.");
            Assert.AreEqual(SavedMoves, Driver.MoveCount);
            Assert.AreEqual(SavedScore, Driver.Score);
            CollectionAssert.IsEmpty(Driver.AudioEvents(), "No sound plays.");

            yield return Driver.Move(Direction.Left);

            string after = Driver.Board();
            Assert.AreEqual(SavedMoves + 1, Driver.MoveCount, "Moves rises by exactly 1.");
            Assert.IsTrue(BoardNotation.IsResolvedPrimary(BoardNotation.ParseCell(BoardNotation.TokenAt(after, 3, 1))),
                "The white block turned into a coloured block without moving. Board: " + after);
            Assert.AreEqual(1, BoardNotation.Count(after, "i"), "Exactly one new white block appears. Board: " + after);
        }

        [UnityTest, Category("TP_SAVE_04")]
        public IEnumerator TP_SAVE_04_PlayContinuesAndLaterMovesAreSaved()
        {
            yield return ResumeTheSavedGame();

            yield return Driver.Move(Direction.Left);
            Assert.AreEqual(SavedMoves + 1, Driver.MoveCount, "Moves continues from the resumed count.");
            yield return Driver.Move(Direction.Up);
            Assert.AreEqual(SavedMoves + 2, Driver.MoveCount);
            string board = Driver.Board();
            int score = Driver.Score, moves = Driver.MoveCount;

            yield return Driver.ReturnToMenu();
            yield return ResumeFromMenu();

            Assert.AreEqual(board, Driver.Board(), "The second resume shows the state after the two further moves, not the earlier save.");
            Assert.AreEqual(moves, Driver.MoveCount);
            Assert.AreEqual(score, Driver.Score);
            Assert.AreEqual($"Moves: {moves}", Driver.MovesText);
        }

        [UnityTest, Category("TP_SAVE_05")]
        public IEnumerator TP_SAVE_05_ANewGameKeepsTheOldSaveUntilItsFirstMove()
        {
            Driver.WriteSave(SavedBoard, Mode, spawnNextColour: true, moveCount: SavedMoves, score: SavedScore);
            yield return Driver.OpenMenu();
            Assert.IsTrue(Driver.IsShown("resume-game-button"));

            // Start a new game and leave without moving: the old save stays.
            yield return Driver.Click("start-button");
            yield return Driver.WaitForGameplay();
            Assert.AreEqual(0, Driver.MoveCount, "It is a new game.");
            yield return Driver.ReturnToMenu();
            yield return ResumeFromMenu();
            Assert.AreEqual(SavedBoard, Driver.Board(), "Resume shows the old saved game.");
            Assert.AreEqual(SavedMoves, Driver.MoveCount);

            // Start again and make one move: now Resume loads the new game.
            yield return Driver.ReturnToMenu();
            yield return Driver.Click("start-button");
            yield return Driver.WaitForGameplay();
            yield return Driver.Move(Direction.Left);
            yield return Driver.ReturnToMenu();
            yield return ResumeFromMenu();
            Assert.AreEqual(1, Driver.MoveCount, "Resume loads the new game's state (Moves 1).");
            Assert.AreEqual("Moves: 1", Driver.MovesText);
        }

        [UnityTest, Category("TP_SAVE_08")]
        public IEnumerator TP_SAVE_08_AnUnusableSaveIsSetAside()
        {
            string board = BoardNotation.Build(4, 4, (2, 2, "i"), (0, 0, "Rs"));
            string saveFile = Driver.SavePath("savegame.json");

            // 1. The save names a version this build can't read.
            Driver.WriteSave(board, Mode, spawnNextColour: true, moveCount: 1);
            string text = File.ReadAllText(saveFile);
            StringAssert.Contains("\"version\": \"1.0\"", text);
            string edited = text.Replace("\"version\": \"1.0\"", "\"version\": \"99.0\"");
            File.WriteAllText(saveFile, edited);

            LogAssert.Expect(LogType.Warning, new Regex(@"Save rejected: reason=UnknownVersion file=savegame\.json detail=99\.0 movedTo=savegame\.bad\.\d{8}T\d{9}Z.json", RegexOptions.Singleline));
            yield return Driver.OpenMenu();

            Assert.IsFalse(Driver.IsShown("resume-game-button"), "No Resume button for an unreadable version.");
            Assert.IsFalse(File.Exists(saveFile), "savegame.json is gone.");
            var bad = BadFiles();
            Assert.AreEqual(1, bad.Length, "One set-aside file: " + string.Join(", ", Driver.SaveFiles()));
            Assert.AreEqual(edited, File.ReadAllText(Driver.SavePath(bad[0])), "It holds the edited save.");

            // 2. A save file cut off half-way.
            Driver.WriteSave(board, Mode, spawnNextColour: true, moveCount: 1);
            string whole = File.ReadAllText(saveFile);
            string cut = whole.Substring(0, whole.Length / 2);
            File.WriteAllText(saveFile, cut);

            LogAssert.Expect(LogType.Warning, new Regex(@"Save rejected: reason=\w+ file=savegame\.json .*movedTo=savegame\.bad\.\d{8}T\d{9}Z.json", RegexOptions.Singleline));
            yield return Driver.OpenMenu();

            Assert.IsFalse(Driver.IsShown("resume-game-button"), "No Resume button for a cut-off file.");
            Assert.IsFalse(File.Exists(saveFile), "savegame.json is gone.");
            bad = BadFiles();
            Assert.AreEqual(2, bad.Length, "Both unusable files are kept: " + string.Join(", ", Driver.SaveFiles()));
            CollectionAssert.Contains(bad.Select(f => File.ReadAllText(Driver.SavePath(f))).ToList(), cut, "One holds the cut-off file.");
        }

        string[] BadFiles() => Driver.SaveFiles().Where(f => Regex.IsMatch(f, @"^savegame\.bad\.\d{8}T\d{9}Z\.json$")).ToArray();

        [UnityTest, Category("TP_SAVE_09")]
        public IEnumerator TP_SAVE_09_SavingLeavesNoTemporaryFile()
        {
            yield return Driver.StartFromMenu(Mode, 6, 6, true);
            for (int move = 1; move <= 4; move++)
                yield return Driver.Move(move % 2 == 0 ? Direction.Right : Direction.Left);

            var files = Driver.SaveFiles();
            CollectionAssert.Contains(files, "savegame.json");
            CollectionAssert.Contains(files, "savegame.1.json", "The move before the last is kept.");
            CollectionAssert.Contains(files, "savegame.2.json", "And the one before that.");
            CollectionAssert.DoesNotContain(files, "savegame.json.tmp", "No temporary file is left.");
            string board = Driver.Board();

            yield return Driver.ReturnToMenu();
            yield return ResumeFromMenu();

            Assert.AreEqual(board, Driver.Board(), "Resume restores the last move.");
            Assert.AreEqual(4, Driver.MoveCount);
        }

        [UnityTest, Category("TP_SAVE_10")]
        public IEnumerator TP_SAVE_10_ABrokenNewestSaveResumesOneMoveEarlier()
        {
            yield return Driver.StartFromMenu(Mode, 6, 6, true);
            yield return Driver.Move(Direction.Left);
            yield return Driver.Move(Direction.Up);
            string boardBeforeLastMove = Driver.Board();
            int scoreBeforeLastMove = Driver.Score;
            yield return Driver.Move(Direction.Right);
            Assert.AreEqual(3, Driver.MoveCount);

            yield return Driver.ReturnToMenu();
            File.WriteAllText(Driver.SavePath("savegame.json"), "broken");

            LogAssert.Expect(LogType.Warning, new Regex(@"Save rejected: reason=Unreadable file=savegame\.json detail=.* movedTo=savegame\.bad\.\d{8}T\d{9}Z.json", RegexOptions.Singleline));
            yield return Driver.OpenMenu(); // runs HasSaveGame again
            Assert.IsTrue(Driver.IsShown("resume-game-button"), "The earlier move is still offered.");
            yield return ResumeFromMenu();

            Assert.AreEqual(2, Driver.MoveCount, "Moves from before the last move.");
            Assert.AreEqual(scoreBeforeLastMove, Driver.Score);
            Assert.AreEqual(boardBeforeLastMove, Driver.Board(), "The board from before the last move.");
            var bad = Directory.GetFiles(Driver.SaveFolder).Select(Path.GetFileName).Where(f => f.StartsWith("savegame.bad.")).ToArray();
            Assert.AreEqual(1, bad.Length, "One set-aside file.");
            Assert.AreEqual("broken", File.ReadAllText(Driver.SavePath(bad[0])));
            CollectionAssert.Contains(Driver.SaveFiles(), "savegame.json", "savegame.json holds the earlier move.");
        }

        [UnityTest, Category("TP_SAVE_11")]
        public IEnumerator TP_SAVE_11_BackupsBelongToOneGame()
        {
            yield return Driver.StartFromMenu(Mode, 6, 6, true);
            for (int i = 0; i < 3; i++)
                yield return Driver.Move(Direction.Left);
            var files = Driver.SaveFiles();
            CollectionAssert.Contains(files, "savegame.1.json");
            CollectionAssert.Contains(files, "savegame.2.json");

            // A new game on a 4x4 board: its first move drops the old game's backups.
            yield return Driver.ReturnToMenu();
            yield return Driver.SetSlider("width-slider", 4);
            yield return Driver.SetSlider("height-slider", 4);
            yield return Driver.Click("start-button");
            yield return Driver.WaitForGameplay();
            yield return Driver.Move(Direction.Left);
            CollectionAssert.AreEqual(new[] { "savegame.json" }, Driver.SaveFiles(), "Only the new game's first move is saved.");

            // Play to game over (cycling directions): every save file goes.
            var directions = new[] { Direction.Up, Direction.Right, Direction.Down, Direction.Left };
            int moves = 1;
            while (!Driver.IsGameOver && moves < 2000)
                yield return Driver.Move(directions[moves++ % 4]);
            Assert.IsTrue(Driver.IsGameOver, "The 4x4 game reached game over within 2000 moves.");
            yield return GameDriver.Frames(3);

            Assert.IsEmpty(Driver.SaveFiles().Where(f => Regex.IsMatch(f, @"^savegame(\.\d)?\.json(\.tmp)?$")),
                "No save file remains: " + string.Join(", ", Driver.SaveFiles()));
            TestContext.WriteLine($"TP-SAVE-11: game over after {moves} moves");
            yield return Driver.Click(Driver.Find<VisualElement>("game-over-panel").Q<Button>("main-menu-button"));
            yield return Driver.WaitForMenu();
            Assert.IsFalse(Driver.IsShown("resume-game-button"), "The menu shows no Resume.");
        }
    }
}
