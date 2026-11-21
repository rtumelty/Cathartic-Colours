// CC-002.3: EditMode tests for the save path: SaveGameSystem writes and deletes the save file, SaveGameManager reads it
// back (LoadSaveGame, InitializeFromSave, RestoreWorldState). A game is built in a BoardStepTestWorld, played with real
// moves, saved by SaveGameSystem and restored into a second, fresh world that holds only the singletons
// GameRenderer.InitializeGame creates (no blocks), as a resume does.
//
// Every test saves to its own temp folder through SaveGameManager.SaveFilePathOverride, so the real
// persistentDataPath/savegame.json is never read or written. Tests that pin a known bug are written for CURRENT
// behaviour and name the issue: when the bug is fixed, the test is expected to change with it.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Data;
using ECS.Components;
using NUnit.Framework;
using SaveSystem;
using Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ECS.Tests
{
    public class SaveRestoreTests
    {
        // The first move merges the two reds (Standard mode, 10 points) and spawns an indicator; the 4x4 board leaves room for spawns.
        const string RoundTripBoard = ". . Rs Rs|. . . .|. Gs . .|. . . .";

        bool previousSynchronousBurst;
        string folder;
        string savePath;
        World previousDefaultWorld;
        Action<SaveRejection> rejectionHandler;
        readonly List<SaveRejection> rejections = new List<SaveRejection>();
        readonly List<BoardStepTestWorld> worlds = new List<BoardStepTestWorld>();

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            previousSynchronousBurst = BurstCompiler.Options.EnableBurstCompileSynchronously;
            BurstCompiler.Options.EnableBurstCompileSynchronously = true;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            BurstCompiler.Options.EnableBurstCompileSynchronously = previousSynchronousBurst;
        }

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "cc-save-tests", Guid.NewGuid().ToString("N"));
            savePath = Path.Combine(folder, "savegame.json");
            SaveGameManager.SaveFilePathOverride = savePath; // the folder is created by SaveGameSystem, as on a first save
            previousDefaultWorld = World.DefaultGameObjectInjectionWorld;
        }

        [TearDown]
        public void TearDown()
        {
            SaveGameManager.SaveFilePathOverride = null;
            SaveGameManager.SimulateNoFileReplace = false;
            StopListeningForRejections(); // SaveRejected is static
            World.DefaultGameObjectInjectionWorld = previousDefaultWorld;

            foreach (var world in worlds)
                world.Dispose();
            worlds.Clear();

            // The ScriptableObjects the tests (and GameManager / InitializeFromSave) instantiated: not assets.
            foreach (var o in Resources.FindObjectsOfTypeAll<GameConfiguration>())
                if (!UnityEditor.EditorUtility.IsPersistent(o)) Object.DestroyImmediate(o);
            foreach (var o in Resources.FindObjectsOfTypeAll<ColorProfile>())
                if (!UnityEditor.EditorUtility.IsPersistent(o)) Object.DestroyImmediate(o);

            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        // ---- Helpers -----------------------------------------------------------------------------------------

        BoardStepTestWorld CreateWorld(GameMode mode, string board, bool spawnEnabled = true, uint seed = 777u)
        {
            var world = new BoardStepTestWorld(mode, board, spawnEnabled, seed);
            world.AddSaveGameSystem();
            worlds.Add(world);
            return world;
        }

        static GameConfiguration NewConfig(GameMode mode, int width, int height, bool spawn, int tier1Points = 10)
        {
            var config = ScriptableObject.CreateInstance<GameConfiguration>();
            config.gameMode = mode;
            config.gridWidth = width;
            config.gridHeight = height;
            config.spawnNextColourSystem = spawn;
            config.tier1Points = tier1Points;
            return config;
        }

        static ColorProfile NewProfile(string name)
        {
            var profile = ScriptableObject.CreateInstance<ColorProfile>();
            profile.name = name;
            return profile;
        }

        /// <summary>Sets the static GameManager configuration SaveGameSystem reads when it saves.</summary>
        static void InitializeGameManager(GameMode mode, int width, int height, bool spawn, string profileName = "ProfileA")
        {
            GameManager.Initialize(NewConfig(mode, width, height, spawn), NewProfile(profileName));
        }

        /// <summary>A world as GameRenderer.InitializeGame leaves it before RestoreWorldState: singletons, no blocks.</summary>
        BoardStepTestWorld CreateFreshWorldForResume(GameMode mode = GameMode.Standard)
        {
            var world = CreateWorld(mode, ". . . .|. . . .|. . . .|. . . .", true, 1u);
            World.DefaultGameObjectInjectionWorld = world.World;
            return world;
        }

        /// <summary>Plays two real moves (a merge, then a slide), each followed by the save frame.</summary>
        BoardStepTestWorld PlayTwoMovesAndSave()
        {
            InitializeGameManager(GameMode.Standard, 4, 4, true);
            var game = CreateWorld(GameMode.Standard, RoundTripBoard);
            game.MoveAndSave(BoardStepTestWorld.Right);
            game.MoveAndSave(BoardStepTestWorld.Down);
            Assert.AreEqual(2, game.GameState.MoveCount, "precondition: two moves were accepted");
            Assert.IsTrue(SaveGameManager.HasSaveGame(), "precondition: SaveGameSystem wrote the save");
            return game;
        }

        static List<string> Snapshot(BoardStepTestWorld world)
        {
            var list = new List<string>();
            foreach (var (_, b) in world.Blocks())
                list.Add($"{b.GridPosition.x},{b.GridPosition.y}:{b.Color}/{b.Size}/{(b.IsNextColorIndicator ? "ind" : "blk")}");
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        /// <summary>Resume as GameRenderer does: load the save, initialise from it, restore into the fresh world.</summary>
        BoardStepTestWorld Resume(GameMode freshMode = GameMode.Standard)
        {
            var saveData = SaveGameManager.LoadSaveGame();
            Assert.IsNotNull(saveData, "precondition: the save loads");
            SaveGameManager.InitializeFromSave(saveData, NewConfig(GameMode.Standard, 6, 6, false), NewProfile("ProfileA"));
            var resumed = CreateFreshWorldForResume(freshMode);
            SaveGameManager.RestoreWorldState(saveData);
            return resumed;
        }

        // ---- Round trip --------------------------------------------------------------------------------------

        [Test]
        public void RoundTrip_RestoresBoardScoreMoveCountRandomStateAndConfiguration()
        {
            var game = PlayTwoMovesAndSave();
            Assert.Greater(game.Score.TotalScore, 0, "precondition: the moves scored");
            var originalBoard = Snapshot(game);
            var originalState = game.GameState;
            var originalScore = game.Score;
            Assert.IsTrue(originalBoard.Exists(s => s.EndsWith("ind")), "precondition: an indicator is on the board");

            var resumed = Resume();

            CollectionAssert.AreEqual(originalBoard, Snapshot(resumed), "same blocks, positions and indicator");
            Assert.AreEqual(originalScore.TotalScore, resumed.Score.TotalScore);
            Assert.AreEqual(originalScore.CurrentFrameScore, resumed.Score.CurrentFrameScore);
            Assert.AreEqual(2, resumed.GameState.MoveCount);
            Assert.AreEqual(originalState.Random.state, resumed.GameState.Random.state);
            Assert.IsFalse(resumed.GameState.GameOver);
            Assert.IsFalse(resumed.GameState.LevelComplete);
            Assert.IsTrue(resumed.GameState.WaitingForInput);

            var config = GameManager.ActiveConfiguration;
            Assert.AreEqual(GameMode.Standard, config.gameMode);
            Assert.AreEqual(4, config.gridWidth);
            Assert.AreEqual(4, config.gridHeight);
            Assert.IsTrue(config.spawnNextColourSystem);
        }

        [TestCase(GameMode.Standard, 4, 5, true)]
        [TestCase(GameMode.ColorMerge, 6, 3, false)]
        [TestCase(GameMode.AdvancedColorMerge, 5, 5, true)]
        public void RoundTrip_RestoresTheConfigurationOfEachMode(GameMode mode, int width, int height, bool spawn)
        {
            InitializeGameManager(mode, width, height, spawn);
            var game = CreateWorld(mode, "Rs . . .|. . . .|. . . .|. . . .");
            game.MoveAndSave(BoardStepTestWorld.Right);

            // The template is deliberately different, so the values can only have come from the save.
            var saveData = SaveGameManager.LoadSaveGame();
            SaveGameManager.InitializeFromSave(saveData, NewConfig(GameMode.Standard, 9, 9, !spawn), NewProfile("ProfileA"));

            var config = GameManager.ActiveConfiguration;
            Assert.AreEqual(mode, config.gameMode);
            Assert.AreEqual(width, config.gridWidth);
            Assert.AreEqual(height, config.gridHeight);
            Assert.AreEqual(spawn, config.spawnNextColourSystem);
        }

        [Test]
        public void ResumedGame_ContinuesTheSavedRandomSequence()
        {
            // CC-044 (#77): the generator state is restored directly, so the resumed game plays on exactly as the original.
            var game = PlayTwoMovesAndSave();
            var savedState = game.GameState.Random.state;
            var resumed = Resume();

            Assert.AreEqual(savedState, resumed.GameState.Random.state);

            game.MoveAndSave(BoardStepTestWorld.Left);
            resumed.MoveAndSave(BoardStepTestWorld.Left);

            CollectionAssert.AreEqual(Snapshot(game), Snapshot(resumed));
            Assert.AreEqual(game.Score.TotalScore, resumed.Score.TotalScore);
            Assert.AreEqual(game.GameState.Random.state, resumed.GameState.Random.state);
        }

        [Test]
        public void RestoreWorldState_WithAZeroRandomState_UsesAValidGenerator()
        {
            // State 0 can only come from a hand-edited or broken file; Random rejects it, so a fixed seed is used.
            var saveData = new SaveGameData { randomState = 0u, blocks = new SavedBlock[0] };
            var resumed = CreateFreshWorldForResume();

            SaveGameManager.RestoreWorldState(saveData);

            var random = resumed.GameState.Random;
            Assert.AreNotEqual(0u, random.state);
            Assert.DoesNotThrow(() => random.NextUInt());
        }

        [Test]
        public void ResumedGame_PlaysOnTheSameEveryTimeItIsResumedFromTheSameSave()
        {
            // What does hold: the restore is deterministic, so two resumes of one save play the same next move the same way.
            PlayTwoMovesAndSave();
            var first = Resume();
            var second = Resume();

            first.MoveAndSave(BoardStepTestWorld.Left);
            second.MoveAndSave(BoardStepTestWorld.Left);

            CollectionAssert.AreEqual(Snapshot(first), Snapshot(second));
            Assert.AreEqual(3, first.GameState.MoveCount);
            Assert.AreEqual(first.Score.TotalScore, second.Score.TotalScore);
            Assert.AreEqual(first.GameState.Random.state, second.GameState.Random.state);
        }

        // ---- Resume changes nothing until the first move (CC-022) ---------------------------------------------

        [Test]
        public void Resume_ChangesNothingAndWritesNothingUntilTheFirstMove()
        {
            PlayTwoMovesAndSave();
            // Make a rewrite visible even if it produced identical content: age the file.
            var oldTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(savePath, oldTime);
            var bytesBefore = File.ReadAllBytes(savePath);

            var resumed = Resume();
            var boardBefore = Snapshot(resumed);
            var stateBefore = resumed.GameState;
            var scoreBefore = resumed.Score;

            for (int i = 0; i < 3; i++)
            {
                resumed.Update();             // BoardStepSystem with no move command
                resumed.UpdatePresentation(); // SaveGameSystem while the game waits for input
                resumed.EndFrame();
            }

            CollectionAssert.AreEqual(boardBefore, Snapshot(resumed));
            Assert.AreEqual(stateBefore.MoveCount, resumed.GameState.MoveCount, "no move was counted");
            Assert.AreEqual(stateBefore.Random.state, resumed.GameState.Random.state);
            Assert.AreEqual(scoreBefore.TotalScore, resumed.Score.TotalScore);
            Assert.AreEqual(oldTime, File.GetLastWriteTimeUtc(savePath), "the save was not rewritten");
            CollectionAssert.AreEqual(bytesBefore, File.ReadAllBytes(savePath));
        }

        [Test]
        public void Resume_ThenAMoveCountsOnAndSavesAgain()
        {
            PlayTwoMovesAndSave();
            var resumed = Resume();
            File.Delete(savePath);

            resumed.MoveAndSave(BoardStepTestWorld.Left);

            Assert.AreEqual(3, resumed.GameState.MoveCount);
            Assert.IsTrue(SaveGameManager.HasSaveGame(), "the first move after a resume writes the save again");
            Assert.AreEqual(3, SaveGameManager.LoadSaveGame().moveCount);
        }

        [Test]
        public void RestoredGameOverFlagsAreWrittenBack()
        {
            // A save made in a finished game can't come from the game today (the file is deleted), but RestoreWorldState
            // writes the flags back, so the restore path is pinned here.
            var saveData = new SaveGameData { gameOver = true, levelComplete = true, moveCount = 7, randomState = 99u, totalScore = 123 };
            var resumed = CreateFreshWorldForResume();
            SaveGameManager.RestoreWorldState(saveData);

            Assert.IsTrue(resumed.GameState.GameOver);
            Assert.IsTrue(resumed.GameState.LevelComplete);
            Assert.AreEqual(7, resumed.GameState.MoveCount);
            Assert.AreEqual(99u, resumed.GameState.Random.state);
            Assert.AreEqual(123, resumed.Score.TotalScore);
            Assert.AreEqual(0, resumed.BlockCount, "a null blocks array restores no blocks");
        }

        [Test]
        public void RestoreWorldState_AlwaysResumesWaitingForInput_EvenFromAnOldSaveThatStoredFalse()
        {
            var saveData = new SaveGameData { waitingForInput = false, randomState = 5u, blocks = new SavedBlock[0] };
            var resumed = CreateFreshWorldForResume();
            var state = resumed.GameState;
            state.WaitingForInput = false;
            resumed.GameState = state;

            SaveGameManager.RestoreWorldState(saveData);

            Assert.IsTrue(resumed.GameState.WaitingForInput);
        }

        [Test]
        public void RestoreWorldState_WithNullSaveData_DoesNothing()
        {
            var resumed = CreateFreshWorldForResume();
            Assert.DoesNotThrow(() => SaveGameManager.RestoreWorldState(null));
            Assert.AreEqual(0, resumed.GameState.MoveCount);
        }

        // ---- When the file is written and deleted ------------------------------------------------------------

        [Test]
        public void IdleFrames_DoNotWriteTheSave()
        {
            InitializeGameManager(GameMode.Standard, 4, 4, true);
            var game = CreateWorld(GameMode.Standard, RoundTripBoard);

            game.Update();
            game.UpdatePresentation();

            Assert.IsFalse(File.Exists(savePath), "nothing is saved until a move is accepted");
        }

        [Test]
        public void AMoveThatChangesNothing_IsStillAcceptedCountedAndSaved()
        {
            // Current behaviour: BoardStepSystem accepts any move while the game waits for input, even one where no block
            // can move, so it counts and SaveGameSystem saves it.
            InitializeGameManager(GameMode.Standard, 4, 4, true);
            var game = CreateWorld(GameMode.Standard, "Rs . . .|. . . .|. . . .|. . . .", spawnEnabled: false);

            game.MoveAndSave(BoardStepTestWorld.Left);

            Assert.AreEqual(1, game.GameState.MoveCount);
            Assert.IsTrue(File.Exists(savePath));
        }

        [Test]
        public void AMoveAfterGameOver_IsRejectedAndWritesNothing()
        {
            InitializeGameManager(GameMode.Standard, 4, 4, true);
            var game = CreateWorld(GameMode.Standard, RoundTripBoard);
            var state = game.GameState;
            state.GameOver = true;
            game.GameState = state;

            game.MoveAndSave(BoardStepTestWorld.Right);

            Assert.AreEqual(0, game.GameState.MoveCount);
            Assert.IsFalse(File.Exists(savePath));
        }

        [Test]
        public void GameOver_DeletesTheSaveOnce()
        {
            PlayTwoMovesAndSave();
            var game = worlds[0];
            var state = game.GameState;
            state.GameOver = true;
            game.GameState = state;

            game.UpdatePresentation();
            Assert.IsFalse(SaveGameManager.HasSaveGame(), "the game-over frame deletes the save");

            // The delete runs once per game over (endedLastFrame): a file that appears later is left alone.
            File.WriteAllText(savePath, "{}");
            game.UpdatePresentation();
            game.UpdatePresentation();
            Assert.IsTrue(File.Exists(savePath), "later frames of the same game over do not delete again");
        }

        [Test]
        public void GameOver_AfterAnAcceptedMoveFrame_DeletesInsteadOfSaving()
        {
            PlayTwoMovesAndSave();
            var game = worlds[0];
            // BoardStepSystem sets GameOver and returns to waiting for input in the move that ends the game.
            var state = game.GameState;
            state.GameOver = true;
            state.WaitingForInput = false; // even if the flag were still false, ended wins over a save
            game.GameState = state;

            game.UpdatePresentation();

            Assert.IsFalse(File.Exists(savePath));
        }

        [Test]
        public void LevelComplete_AlsoDeletesTheSave()
        {
            // Nothing sets LevelComplete in the game today (CC-019, #31), but the delete branch exists and is pinned.
            PlayTwoMovesAndSave();
            var game = worlds[0];
            var state = game.GameState;
            state.LevelComplete = true;
            game.GameState = state;

            game.UpdatePresentation();

            Assert.IsFalse(SaveGameManager.HasSaveGame());
        }

        [Test]
        public void GameOver_WithNoSaveFile_DoesNotThrowOrLog()
        {
            InitializeGameManager(GameMode.Standard, 4, 4, true);
            var game = CreateWorld(GameMode.Standard, RoundTripBoard);
            var state = game.GameState;
            state.GameOver = true;
            game.GameState = state;

            Assert.DoesNotThrow(() => game.UpdatePresentation());
            LogAssert.NoUnexpectedReceived();
        }

        // ---- HasSaveGame / LoadSaveGame / DeleteSaveGame follow the file --------------------------------------

        [Test]
        public void HasSaveGame_FollowsTheFile()
        {
            Assert.IsFalse(SaveGameManager.HasSaveGame(), "no file");

            PlayTwoMovesAndSave();
            Assert.IsTrue(SaveGameManager.HasSaveGame(), "after a save");

            SaveGameManager.DeleteSaveGame();
            Assert.IsFalse(SaveGameManager.HasSaveGame(), "after DeleteSaveGame");
            Assert.IsFalse(File.Exists(savePath));
        }

        [Test]
        public void HasSaveGame_IsFalseAfterGameOverDeletesTheSave()
        {
            PlayTwoMovesAndSave();
            var game = worlds[0];
            var state = game.GameState;
            state.GameOver = true;
            game.GameState = state;
            game.UpdatePresentation();

            Assert.IsFalse(SaveGameManager.HasSaveGame());
        }

        [Test]
        public void DeleteSaveGame_WithNoFile_DoesNothing()
        {
            Assert.DoesNotThrow(() => SaveGameManager.DeleteSaveGame());
            Assert.IsFalse(SaveGameManager.HasSaveGame());
        }

        [Test]
        public void LoadSaveGame_WithNoFile_ReturnsNullAndWarns()
        {
            LogAssert.Expect(LogType.Warning, "No save game file found");

            Assert.IsNull(SaveGameManager.LoadSaveGame());
        }

        [Test]
        public void LoadSaveGame_ReadsWhatSaveGameSystemWrote()
        {
            PlayTwoMovesAndSave();

            var saveData = SaveGameManager.LoadSaveGame();

            Assert.AreEqual(2, saveData.moveCount);
            Assert.AreEqual(GameMode.Standard, saveData.gameMode);
            Assert.AreEqual(4, saveData.gridWidth);
            Assert.IsTrue(saveData.waitingForInput, "a save always describes the idle state after the move");
            Assert.AreEqual(worlds[0].BlockCount, saveData.blocks.Length);
        }

        [Test]
        public void InitializeFromSave_WithNullSaveData_LogsAnErrorAndLeavesTheConfigurationAlone()
        {
            InitializeGameManager(GameMode.ColorMerge, 3, 3, false);
            var before = GameManager.ActiveConfiguration;
            LogAssert.Expect(LogType.Error, "Cannot initialize from null save data");

            SaveGameManager.InitializeFromSave(null, NewConfig(GameMode.Standard, 6, 6, true), NewProfile("ProfileA"));

            Assert.AreSame(before, GameManager.ActiveConfiguration);
        }

        // ---- Save file robustness (CC-031) --------------------------------------------------------------------

        /// <summary>The timestamped bad files in the folder (savegame.bad.&lt;timestamp&gt;.json), oldest first. A legacy
        /// savegame.bad.json is not included.</summary>
        string[] BadFilesOnDisk()
        {
            if (!Directory.Exists(folder)) return new string[0];
            var files = Array.FindAll(Directory.GetFiles(folder), f => BadFileName.IsMatch(Path.GetFileName(f)));
            Array.Sort(files, StringComparer.Ordinal);
            return files;
        }

        /// <summary>The content of the only bad file, failing if there is not exactly one.</summary>
        string OnlyBadFileContent()
        {
            var files = BadFilesOnDisk();
            Assert.AreEqual(1, files.Length, "exactly one bad file");
            return File.ReadAllText(files[0]);
        }

        [Test]
        public void ColorProfile_SavedByAssetName_AndRestoredWhenKnown()
        {
            InitializeGameManager(GameMode.Standard, 4, 4, true, "ProfileA");
            var game = CreateWorld(GameMode.Standard, RoundTripBoard);
            game.MoveAndSave(BoardStepTestWorld.Right);
            var saveData = SaveGameManager.LoadSaveGame();
            Assert.AreEqual("ProfileA", saveData.colorProfileName, "the asset name, without the runtime copy's \"(Clone)\"");

            var profileA = NewProfile("ProfileA");
            var profileB = NewProfile("ProfileB");
            SaveGameManager.InitializeFromSave(saveData, NewConfig(GameMode.Standard, 6, 6, false), profileB, new[] { profileB, profileA });

            StringAssert.StartsWith("ProfileA", GameManager.ActiveColorProfile.name, "the saved profile is used");
        }

        [Test]
        public void ColorProfile_FromAnOlderSaveWithTheCloneSuffix_IsStillFound()
        {
            var saveData = new SaveGameData { gridWidth = 4, gridHeight = 4, colorProfileName = "ProfileA(Clone)" };

            SaveGameManager.InitializeFromSave(saveData, NewConfig(GameMode.Standard, 4, 4, false), NewProfile("ProfileB"),
                new[] { NewProfile("ProfileA") });

            StringAssert.StartsWith("ProfileA", GameManager.ActiveColorProfile.name);
        }

        [TestCase("Missing")]
        [TestCase(null)]
        public void ColorProfile_NotKnown_FallsBackToTheTemplate(string savedName)
        {
            var saveData = new SaveGameData { gridWidth = 4, gridHeight = 4, colorProfileName = savedName };

            SaveGameManager.InitializeFromSave(saveData, NewConfig(GameMode.Standard, 4, 4, false), NewProfile("ProfileB"),
                new[] { NewProfile("ProfileA") });

            StringAssert.StartsWith("ProfileB", GameManager.ActiveColorProfile.name);
        }

        [Test]
        public void UnknownVersion_IsSetAsideAndNotOffered()
        {
            // One move, so there is no backup to fall back to (CC-042).
            InitializeGameManager(GameMode.Standard, 4, 4, true);
            CreateWorld(GameMode.Standard, RoundTripBoard).MoveAndSave(BoardStepTestWorld.Right);
            var json = File.ReadAllText(savePath).Replace("\"version\": \"1.0\"", "\"version\": \"99.0\"");
            Assert.That(json, Does.Contain("99.0"), "precondition: the version was rewritten");
            File.WriteAllText(savePath, json);

            LogAssert.Expect(LogType.Warning, new Regex("^Save rejected: reason=UnknownVersion file=savegame\\.json detail=99\\.0 "));
            Assert.IsFalse(SaveGameManager.HasSaveGame(), "the menu would not offer Resume");

            Assert.IsFalse(File.Exists(savePath));
            Assert.AreEqual(json, OnlyBadFileContent(), "the file is kept, unchanged, as a savegame.bad.<timestamp>.json");
            LogAssert.Expect(LogType.Warning, "No save game file found");
            Assert.IsNull(SaveGameManager.LoadSaveGame());
        }

        [TestCase("{ \"version\": \"1.0\", \"gameMode\": ", TestName = "UnreadableSave_Truncated_IsSetAside")]
        [TestCase("", TestName = "UnreadableSave_Empty_IsSetAside")]
        [TestCase("{}", TestName = "UnreadableSave_NoGrid_IsSetAside")]
        public void UnreadableSave_IsSetAsideAndLoadsAsNull(string content)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(savePath, content);

            LogAssert.Expect(LogType.Warning, Unusable);
            Assert.IsNull(SaveGameManager.LoadSaveGame());

            Assert.IsFalse(SaveGameManager.HasSaveGame());
            Assert.IsFalse(File.Exists(savePath));
            Assert.AreEqual(content, OnlyBadFileContent());
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ASecondUnusableSave_IsKeptBesideTheEarlierBadFile()
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(savePath, "older bad save");
            LogAssert.Expect(LogType.Warning, Unusable);
            Assert.IsFalse(SaveGameManager.HasSaveGame());

            File.WriteAllText(savePath, "newer bad save");
            LogAssert.Expect(LogType.Warning, Unusable);
            Assert.IsFalse(SaveGameManager.HasSaveGame());

            var bad = BadFilesOnDisk();
            Assert.AreEqual(2, bad.Length, "both are kept");
            Assert.AreEqual("older bad save", File.ReadAllText(bad[0]), "names sort in time order");
            Assert.AreEqual("newer bad save", File.ReadAllText(bad[1]));
        }

        [Test]
        public void Save_IsWrittenThroughATempFile_ThatIsGoneAfterwards()
        {
            PlayTwoMovesAndSave(); // two saves: a first write, then a replace of an existing file

            Assert.IsFalse(File.Exists(savePath + ".tmp"), "the temp file was moved into place");
            Assert.AreEqual(2, SaveGameManager.LoadSaveGame().moveCount);
            Assert.AreEqual(SaveGameManager.CurrentVersion, SaveGameManager.LoadSaveGame().version);
        }

        [Test]
        public void Save_OverALeftoverTempFile_FromACrashedWrite_Succeeds()
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(savePath + ".tmp", "{ \"version\": \"1.0\", \"gam"); // a write that never finished

            PlayTwoMovesAndSave();

            Assert.IsFalse(File.Exists(savePath + ".tmp"));
            Assert.AreEqual(2, SaveGameManager.LoadSaveGame().moveCount);
        }

        [Test]
        public void DeleteSaveGame_AlsoRemovesALeftoverTempFile()
        {
            PlayTwoMovesAndSave();
            File.WriteAllText(savePath + ".tmp", "partial");

            SaveGameManager.DeleteSaveGame();

            Assert.IsFalse(File.Exists(savePath));
            Assert.IsFalse(File.Exists(savePath + ".tmp"));
        }

        [Test]
        public void Resume_LeavesTheMenuConfigurationUnchanged()
        {
            // CC-027 (#39): a resume starts the game with its own configuration; the menu's copy is not replaced or edited.
            InitializeGameManager(GameMode.ColorMerge, 3, 5, false);
            var game = CreateWorld(GameMode.ColorMerge, "Rs . . .|. . . .|. . . .|. . . .");
            game.MoveAndSave(BoardStepTestWorld.Right);
            var saveData = SaveGameManager.LoadSaveGame();
            var menuBefore = GameManager.MenuConfiguration;

            SaveGameManager.InitializeFromSave(saveData, NewConfig(GameMode.Standard, 6, 6, true, tier1Points: 4242), NewProfile("ProfileA"));

            Assert.AreSame(menuBefore, GameManager.MenuConfiguration, "the menu configuration instance is kept");
            Assert.AreEqual(GameMode.ColorMerge, GameManager.MenuConfiguration.gameMode);
            Assert.AreEqual(3, GameManager.MenuConfiguration.gridWidth);
            Assert.AreEqual(5, GameManager.MenuConfiguration.gridHeight);
            Assert.AreNotSame(menuBefore, GameManager.ActiveConfiguration, "the resumed game has its own configuration");
            Assert.AreEqual(GameMode.ColorMerge, GameManager.ActiveConfiguration.gameMode);
            Assert.AreEqual(3, GameManager.ActiveConfiguration.gridWidth);
            Assert.AreEqual(5, GameManager.ActiveConfiguration.gridHeight);
        }

        // ---- Rotating saves and fallback on load (CC-042) ---------------------------------------------------

        static readonly Regex Unusable = new Regex("^Save rejected: ");
        static readonly Regex BadFileName = new Regex(@"^savegame\.bad\.\d{8}T\d{9}Z\.json$");

        string SlotPath(int slot) => SaveGameManager.SaveSlotPath(slot);

        /// <summary>Writes a minimal valid save for the given move, as SaveGameSystem would after that move.</summary>
        static void WriteMove(int move)
        {
            SaveGameManager.WriteSave(new SaveGameData { gridWidth = 4, gridHeight = 4, moveCount = move, randomState = 7u, blocks = new SavedBlock[0] });
        }

        int MoveIn(int slot) => JsonUtility.FromJson<SaveGameData>(File.ReadAllText(SlotPath(slot))).moveCount;

        void Corrupt(int slot, string content) => File.WriteAllText(SlotPath(slot), content);

        /// <summary>The save-slot files in the folder (not the temp or bad files), so a stray name shows up.</summary>
        string[] SlotFilesOnDisk() => Directory.Exists(folder)
            ? Array.FindAll(Directory.GetFiles(folder, "savegame*.json"), f => !Path.GetFileName(f).StartsWith("savegame.bad", StringComparison.Ordinal))
            : new string[0];

        [Test]
        public void SlotPaths_AreTheBackupsBesideTheSave()
        {
            Assert.AreEqual(savePath, SlotPath(0));
            Assert.AreEqual(Path.Combine(folder, "savegame.1.json"), SlotPath(1));
            Assert.AreEqual(Path.Combine(folder, "savegame.2.json"), SlotPath(2));
        }

        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(4, false)]
        [TestCase(4, true, TestName = "Rotation_After4Saves_WithoutFileReplace")]
        public void Rotation_NewestIsSaveJson_AndOnlyTwoBackupsExist(int saves, bool noFileReplace)
        {
            SaveGameManager.SimulateNoFileReplace = noFileReplace;

            for (int move = 1; move <= saves; move++)
                WriteMove(move);

            Assert.AreEqual(saves, MoveIn(0), "the newest save is savegame.json");
            if (saves >= 2) Assert.AreEqual(saves - 1, MoveIn(1), "savegame.1.json is the move before");
            if (saves >= 3) Assert.AreEqual(saves - 2, MoveIn(2), "savegame.2.json is the move before that");
            Assert.AreEqual(Math.Min(saves, 3), SlotFilesOnDisk().Length, "never more than the newest save and two backups");
            Assert.IsFalse(File.Exists(SaveGameManager.TempFilePath));
        }

        [Test]
        public void Rotation_ThroughSaveGameSystem_KeepsTheLastThreeMoves()
        {
            var game = PlayTwoMovesAndSave();
            game.MoveAndSave(BoardStepTestWorld.Left);
            game.MoveAndSave(BoardStepTestWorld.Up);

            Assert.AreEqual(4, MoveIn(0));
            Assert.AreEqual(3, MoveIn(1));
            Assert.AreEqual(2, MoveIn(2));
            Assert.AreEqual(3, SlotFilesOnDisk().Length);
        }

        [TestCase("{ \"version\": \"1.0\", \"gridWi", TestName = "Fallback_TruncatedNewest_LoadsTheMoveBefore")]
        [TestCase("not json at all", TestName = "Fallback_CorruptNewest_LoadsTheMoveBefore")]
        [TestCase("{ \"version\": \"99.0\", \"gridWidth\": 4, \"gridHeight\": 4, \"moveCount\": 3 }", TestName = "Fallback_UnknownVersionNewest_LoadsTheMoveBefore")]
        public void Fallback_UnusableNewest_LoadsTheMoveBefore(string content)
        {
            for (int move = 1; move <= 3; move++)
                WriteMove(move);
            Corrupt(0, content);

            LogAssert.Expect(LogType.Warning, Unusable);
            var saveData = SaveGameManager.LoadSaveGame();

            Assert.IsNotNull(saveData);
            Assert.AreEqual(2, saveData.moveCount);
            Assert.AreEqual(content, OnlyBadFileContent(), "the unusable file is set aside, unchanged");
            // Renumbered, so the next save rotates normally.
            Assert.AreEqual(2, MoveIn(0));
            Assert.AreEqual(1, MoveIn(1));
            Assert.IsFalse(File.Exists(SlotPath(2)));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Fallback_NewestAndFirstBackupUnusable_LoadsTheSecondBackup()
        {
            for (int move = 1; move <= 3; move++)
                WriteMove(move);
            Corrupt(0, "");
            Corrupt(1, "{}");

            LogAssert.Expect(LogType.Warning, Unusable);
            LogAssert.Expect(LogType.Warning, Unusable);
            Assert.IsTrue(SaveGameManager.HasSaveGame());

            Assert.AreEqual(1, MoveIn(0), "the second backup is now savegame.json");
            Assert.AreEqual(1, SlotFilesOnDisk().Length);
            Assert.AreEqual(1, SaveGameManager.LoadSaveGame().moveCount);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Fallback_NoUsableFile_HasSaveGameIsFalse()
        {
            for (int move = 1; move <= 3; move++)
                WriteMove(move);
            Corrupt(0, "");
            Corrupt(1, "x");
            Corrupt(2, "{}");

            for (int i = 0; i < 3; i++)
                LogAssert.Expect(LogType.Warning, Unusable);
            Assert.IsFalse(SaveGameManager.HasSaveGame(), "the menu would not offer Resume");

            Assert.AreEqual(0, SlotFilesOnDisk().Length, "every unusable file was moved aside");
            LogAssert.Expect(LogType.Warning, "No save game file found");
            Assert.IsNull(SaveGameManager.LoadSaveGame());
        }

        [Test]
        public void Fallback_OnlyBackupsLeft_LoadsTheNewestBackup()
        {
            // As after a crash between the two moves of the no-File.Replace path: savegame.json is gone, .1 is complete.
            for (int move = 1; move <= 3; move++)
                WriteMove(move);
            File.Delete(savePath);

            Assert.IsTrue(SaveGameManager.HasSaveGame());
            Assert.AreEqual(2, SaveGameManager.LoadSaveGame().moveCount);
            Assert.AreEqual(1, MoveIn(1));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Fallback_ThenTheNextSaveRotatesNormally()
        {
            for (int move = 1; move <= 3; move++)
                WriteMove(move);
            Corrupt(0, "broken");
            LogAssert.Expect(LogType.Warning, Unusable);
            Assert.AreEqual(2, SaveGameManager.LoadSaveGame().moveCount, "precondition: resumed one move earlier");

            WriteMove(3); // the resumed game's next move
            WriteMove(4);

            Assert.AreEqual(4, MoveIn(0));
            Assert.AreEqual(3, MoveIn(1));
            Assert.AreEqual(2, MoveIn(2));
        }

        [Test]
        public void Fallback_ThroughAResume_RestoresTheBoardOneMoveEarlier()
        {
            PlayTwoMovesAndSave();
            var afterFirstMove = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(SlotPath(1)));
            Corrupt(0, "{ \"version\": \"1.0\", \"gam");

            LogAssert.Expect(LogType.Warning, Unusable);
            var resumed = Resume();

            Assert.AreEqual(1, resumed.GameState.MoveCount);
            Assert.AreEqual(afterFirstMove.totalScore, resumed.Score.TotalScore);
            Assert.AreEqual(afterFirstMove.blocks.Length, resumed.BlockCount);
        }

        [Test]
        public void NewGame_FirstSave_ClearsTheEarlierGamesFiles()
        {
            for (int move = 1; move <= 5; move++)
                WriteMove(move);

            WriteMove(1); // a new game's first move

            Assert.AreEqual(1, MoveIn(0));
            Assert.AreEqual(1, SlotFilesOnDisk().Length, "no backup from the earlier game is kept");

            WriteMove(2);
            Assert.AreEqual(1, MoveIn(1), "the new game's own saves are kept as backups");
        }

        [Test]
        public void NewGame_FirstSave_WithoutFileReplace_AlsoClearsTheEarlierGamesFiles()
        {
            SaveGameManager.SimulateNoFileReplace = true;
            for (int move = 1; move <= 5; move++)
                WriteMove(move);

            WriteMove(1);

            Assert.AreEqual(1, MoveIn(0));
            Assert.AreEqual(1, SlotFilesOnDisk().Length);
        }

        [Test]
        public void NewGame_ThroughSaveGameSystem_LeavesNoFallbackToTheOldGame()
        {
            var oldGame = PlayTwoMovesAndSave();
            oldGame.MoveAndSave(BoardStepTestWorld.Left);

            InitializeGameManager(GameMode.ColorMerge, 4, 4, false);
            CreateWorld(GameMode.ColorMerge, "Rs . . .|. . . .|. . . .|. . . .", spawnEnabled: false).MoveAndSave(BoardStepTestWorld.Right);
            Corrupt(0, "");

            LogAssert.Expect(LogType.Warning, Unusable);
            Assert.IsFalse(SaveGameManager.HasSaveGame(), "the old game's saves were cleared, so nothing is resumed");
        }

        [Test]
        public void GameOver_DeletesTheSaveAllBackupsAndTheTempFile()
        {
            var game = PlayTwoMovesAndSave();
            game.MoveAndSave(BoardStepTestWorld.Left);
            Assert.AreEqual(3, SlotFilesOnDisk().Length, "precondition: the save and two backups");
            File.WriteAllText(SaveGameManager.TempFilePath, "partial");

            var state = game.GameState;
            state.GameOver = true;
            game.GameState = state;
            game.UpdatePresentation();

            Assert.AreEqual(0, SlotFilesOnDisk().Length);
            Assert.IsFalse(File.Exists(SaveGameManager.TempFilePath));
            Assert.IsFalse(SaveGameManager.HasSaveGame());
        }

        [Test]
        public void GameOver_KeepsSetAsideBadFiles()
        {
            for (int move = 1; move <= 2; move++)
                WriteMove(move);
            Corrupt(0, "bad");
            LogAssert.Expect(LogType.Warning, Unusable);
            Assert.IsTrue(SaveGameManager.HasSaveGame());

            SaveGameManager.DeleteSaveGame();

            Assert.AreEqual("bad", OnlyBadFileContent(), "bad files are kept for inspection");
        }

        [Test]
        public void Compatibility_ASingleSaveFromAnEarlierBuild_LoadsAndBecomesTheFirstBackup()
        {
            // The layout before CC-042: one savegame.json and nothing else.
            Directory.CreateDirectory(folder);
            File.WriteAllText(savePath,
                "{\n    \"version\": \"1.0\",\n    \"saveTimestamp\": \"-8585000000000000000\",\n    \"gameMode\": 0,\n" +
                "    \"gridWidth\": 4,\n    \"gridHeight\": 4,\n    \"spawnNextColourSystem\": true,\n    \"colorProfileName\": \"ProfileA\",\n" +
                "    \"waitingForInput\": true,\n    \"gameOver\": false,\n    \"levelComplete\": false,\n    \"moveCount\": 6,\n" +
                "    \"randomState\": 12345,\n    \"totalScore\": 40,\n    \"currentFrameScore\": 0,\n    \"blocks\": [\n" +
                "        {\n            \"color\": 1,\n            \"size\": 0,\n            \"gridX\": 2,\n            \"gridY\": 3,\n" +
                "            \"isNextColorIndicator\": false\n        }\n    ]\n}");

            Assert.IsTrue(SaveGameManager.HasSaveGame());
            var saveData = SaveGameManager.LoadSaveGame();
            Assert.AreEqual(6, saveData.moveCount);
            Assert.AreEqual(40, saveData.totalScore);
            Assert.AreEqual(1, saveData.blocks.Length);

            WriteMove(7);
            Assert.AreEqual(7, MoveIn(0));
            Assert.AreEqual(6, MoveIn(1), "the old file is now the first backup");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void LeftoverTempFile_WithBackupsPresent_DoesNotBlockTheNextSave()
        {
            for (int move = 1; move <= 3; move++)
                WriteMove(move);
            File.WriteAllText(SaveGameManager.TempFilePath, "{ \"version\": \"1.0\", \"gam"); // a write that never finished

            WriteMove(4);

            Assert.AreEqual(4, MoveIn(0));
            Assert.AreEqual(3, MoveIn(1));
            Assert.AreEqual(2, MoveIn(2));
            Assert.IsFalse(File.Exists(SaveGameManager.TempFilePath));
        }

        [Test]
        public void TheBackupsAlsoLiveInTheTemporaryFolder_NotPersistentDataPath()
        {
            for (int slot = 0; slot <= SaveGameManager.BackupCount; slot++)
                Assert.IsFalse(SlotPath(slot).StartsWith(Application.persistentDataPath, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(folder.StartsWith(Application.persistentDataPath, StringComparison.OrdinalIgnoreCase), "so bad files are too");
        }

        // ---- Rejection reasons and reporting (CC-042.2) -------------------------------------------------------

        /// <summary>Starts recording every <see cref="SaveGameManager.SaveRejected"/> into <see cref="rejections"/>.</summary>
        void ListenForRejections()
        {
            rejections.Clear();
            rejectionHandler = r => rejections.Add(r);
            SaveGameManager.SaveRejected += rejectionHandler;
        }

        void StopListeningForRejections()
        {
            if (rejectionHandler != null)
                SaveGameManager.SaveRejected -= rejectionHandler;
            rejectionHandler = null;
        }

        [TestCase("not json", SaveRejectReason.Unreadable, null, TestName = "Rejection_NotJson_IsUnreadable")]
        [TestCase("", SaveRejectReason.Empty, "", TestName = "Rejection_EmptyFile_IsEmpty")]
        [TestCase("{ \"version\": \"99.0\", \"gridWidth\": 4, \"gridHeight\": 4 }", SaveRejectReason.UnknownVersion, "99.0", TestName = "Rejection_VersionNotReadable_IsUnknownVersion")]
        [TestCase("{ \"version\": \"1.0\" }", SaveRejectReason.InvalidGrid, "0x0", TestName = "Rejection_NoGridSize_IsInvalidGrid")]
        [TestCase("{ \"version\": \"1.0\", \"gridWidth\": 0, \"gridHeight\": 4 }", SaveRejectReason.InvalidGrid, "0x4", TestName = "Rejection_ZeroWidth_IsInvalidGrid")]
        public void SaveRejected_FiresOnceWithTheReasonForTheRejectedFile(string content, SaveRejectReason reason, string detail)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(savePath, content);
            ListenForRejections();

            LogAssert.Expect(LogType.Warning, Unusable);
            Assert.IsFalse(SaveGameManager.HasSaveGame());

            Assert.AreEqual(1, rejections.Count, "once per rejected file");
            Assert.AreEqual(reason, rejections[0].Reason);
            Assert.AreEqual("savegame.json", rejections[0].FileName);
            if (detail != null)
                Assert.AreEqual(detail, rejections[0].Detail);
            else
                Assert.IsNotEmpty(rejections[0].Detail, "the exception message");
        }

        [Test]
        public void SaveRejected_FiresForEachRejectedFile_NewestFirst()
        {
            for (int move = 1; move <= 3; move++)
                WriteMove(move);
            Corrupt(0, "first bad");
            Corrupt(1, "{}");
            ListenForRejections();

            LogAssert.Expect(LogType.Warning, Unusable);
            LogAssert.Expect(LogType.Warning, Unusable);
            var saveData = SaveGameManager.LoadSaveGame();

            Assert.AreEqual(1, saveData.moveCount, "the second backup is used");
            Assert.AreEqual(2, rejections.Count);
            Assert.AreEqual("savegame.json", rejections[0].FileName);
            Assert.AreEqual("savegame.1.json", rejections[1].FileName);
            Assert.AreEqual(SaveRejectReason.Unreadable, rejections[0].Reason);
            Assert.AreEqual(SaveRejectReason.InvalidGrid, rejections[1].Reason);

            var bad = BadFilesOnDisk();
            Assert.AreEqual(2, bad.Length, "two files set aside, with different names");
            Assert.AreEqual(rejections[0].SetAsidePath, bad[0], "names sort in time order");
            Assert.AreEqual(rejections[1].SetAsidePath, bad[1]);
        }

        [Test]
        public void SaveRejected_DoesNotFireForAValidSave()
        {
            WriteMove(1);
            ListenForRejections();

            Assert.IsTrue(SaveGameManager.HasSaveGame());
            Assert.IsNotNull(SaveGameManager.LoadSaveGame());

            Assert.AreEqual(0, rejections.Count);
            Assert.AreEqual(0, BadFilesOnDisk().Length);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SaveRejected_DoesNotFireWhenThereAreNoFiles()
        {
            ListenForRejections();

            Assert.IsFalse(SaveGameManager.HasSaveGame());

            Assert.AreEqual(0, rejections.Count);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SaveRejection_SetAsidePath_IsTheTimestampedFileHoldingTheRejectedContent()
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(savePath, "rejected content");
            ListenForRejections();
            LogAssert.Expect(LogType.Warning, Unusable);

            Assert.IsFalse(SaveGameManager.HasSaveGame());

            string moved = rejections[0].SetAsidePath;
            Assert.IsTrue(File.Exists(moved));
            Assert.AreEqual("rejected content", File.ReadAllText(moved));
            Assert.AreEqual(folder, Path.GetDirectoryName(moved));
            StringAssert.IsMatch(BadFileName.ToString(), Path.GetFileName(moved));
        }

        [Test]
        public void BadFiles_OnlyTheNewestThreeAreKept()
        {
            Directory.CreateDirectory(folder);
            string[] old =
            {
                Path.Combine(folder, "savegame.bad.20200101T000000000Z.json"),
                Path.Combine(folder, "savegame.bad.20200102T000000000Z.json"),
                Path.Combine(folder, "savegame.bad.20200103T000000000Z.json")
            };
            foreach (string file in old)
                File.WriteAllText(file, "old");
            File.WriteAllText(savePath, "fourth bad save");
            ListenForRejections();
            LogAssert.Expect(LogType.Warning, Unusable);

            Assert.IsFalse(SaveGameManager.HasSaveGame());

            var bad = BadFilesOnDisk();
            Assert.AreEqual(SaveGameManager.KeptBadFiles, bad.Length);
            Assert.IsFalse(File.Exists(old[0]), "the oldest was deleted");
            Assert.IsTrue(File.Exists(old[1]));
            Assert.IsTrue(File.Exists(old[2]));
            Assert.AreEqual(rejections[0].SetAsidePath, bad[2], "the new one is the newest");
            Assert.AreEqual("fourth bad save", File.ReadAllText(bad[2]));
        }

        [Test]
        public void BadFiles_ALegacyBadFileIsLeftAloneAndNotCounted()
        {
            Directory.CreateDirectory(folder);
            string legacy = Path.Combine(folder, "savegame.bad.json");
            File.WriteAllText(legacy, "from an older build");
            for (int day = 1; day <= 3; day++)
                File.WriteAllText(Path.Combine(folder, $"savegame.bad.2020010{day}T000000000Z.json"), "old");
            File.WriteAllText(savePath, "bad");
            LogAssert.Expect(LogType.Warning, Unusable);

            Assert.IsFalse(SaveGameManager.HasSaveGame());

            Assert.AreEqual("from an older build", File.ReadAllText(legacy), "not deleted or replaced");
            Assert.AreEqual(SaveGameManager.KeptBadFiles, BadFilesOnDisk().Length, "three timestamped files besides the legacy one");
            Assert.IsFalse(File.Exists(Path.Combine(folder, "savegame.bad.20200101T000000000Z.json")));
        }

        [Test]
        public void RejectionLog_IsOneWarningInTheFixedFormat()
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(savePath, "{ \"version\": \"99.0\", \"gridWidth\": 4, \"gridHeight\": 4 }");
            LogAssert.Expect(LogType.Warning, new Regex(
                @"^Save rejected: reason=UnknownVersion file=savegame\.json detail=99\.0 movedTo=savegame\.bad\.\d{8}T\d{9}Z\.json$"));

            Assert.IsFalse(SaveGameManager.HasSaveGame());

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void FormatRejection_NamesTheFileAndShowsNoneWhenTheMoveFailed()
        {
            var moved = new SaveRejection(SaveRejectReason.InvalidGrid, "savegame.1.json", "0x4", Path.Combine(folder, "savegame.bad.20200101T000000000Z.json"));
            var notMoved = new SaveRejection(SaveRejectReason.Empty, "savegame.2.json", "", null);

            Assert.AreEqual("Save rejected: reason=InvalidGrid file=savegame.1.json detail=0x4 movedTo=savegame.bad.20200101T000000000Z.json",
                SaveGameManager.FormatRejection(moved));
            Assert.AreEqual("Save rejected: reason=Empty file=savegame.2.json detail= movedTo=none", SaveGameManager.FormatRejection(notMoved));
        }

        [Test]
        public void ASubscriberThatThrows_DoesNotStopTheFallbackLoad()
        {
            for (int move = 1; move <= 3; move++)
                WriteMove(move);
            Corrupt(0, "broken");
            Action<SaveRejection> thrower = _ => throw new InvalidOperationException("boom");
            SaveGameManager.SaveRejected += thrower;
            ListenForRejections(); // after the thrower, so it shows the later subscribers still run
            try
            {
                LogAssert.Expect(LogType.Warning, Unusable);
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: boom"));

                var saveData = SaveGameManager.LoadSaveGame();

                Assert.IsNotNull(saveData);
                Assert.AreEqual(2, saveData.moveCount, "the load fell back as usual");
                Assert.AreEqual(1, rejections.Count, "a subscriber after the broken one still ran");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                SaveGameManager.SaveRejected -= thrower;
            }
        }

        // ---- Known gaps: current behaviour, named issues -----------------------------------------------------

        [Test]
        public void Resume_TierPointsComeFromTheTemplateBecauseTheyAreNotSaved()
        {
            // Current behaviour (not part of CC-027): only gameMode, grid size and spawnNextColourSystem are saved, so
            // the tier points are whatever the template has.
            InitializeGameManager(GameMode.ColorMerge, 3, 5, false);
            var game = CreateWorld(GameMode.ColorMerge, "Rs . . .|. . . .|. . . .|. . . .");
            game.MoveAndSave(BoardStepTestWorld.Right);
            var saveData = SaveGameManager.LoadSaveGame();

            SaveGameManager.InitializeFromSave(saveData, NewConfig(GameMode.Standard, 6, 6, true, tier1Points: 4242), NewProfile("ProfileA"));

            Assert.AreEqual(4242, GameManager.ActiveConfiguration.tier1Points, "tier points are not saved: the template's value is used");
        }

        [Test]
        public void KnownGap_RestoreAcceptsBlocksOutsideTheSavedGridWithoutValidation()
        {
            // No validation of restored data (docs/wiki/systems/save-system.md, "SaveGameManager"): positions outside the
            // grid are restored as-is. Since CC-031 (#43) a load rejects unknown versions and a missing grid size, but block
            // positions are not checked. Not tied to an issue yet.
            var saveData = new SaveGameData
            {
                randomState = 5u,
                gridWidth = 4,
                gridHeight = 4,
                blocks = new[]
                {
                    new SavedBlock { color = BlockColor.Red, size = BlockSize.Small, gridX = 40, gridY = -3 }
                }
            };
            var resumed = CreateFreshWorldForResume();

            SaveGameManager.RestoreWorldState(saveData);

            Assert.AreEqual(1, resumed.BlockCount);
            var b = resumed.Blocks()[0].Block;
            Assert.AreEqual(40, b.GridPosition.x);
            Assert.AreEqual(-3, b.GridPosition.y);
        }

        // ---- The tests never touch the real save ------------------------------------------------------------

        [Test]
        public void TheSavePathIsTheTemporaryOverride_NotPersistentDataPath()
        {
            Assert.AreEqual(savePath, SaveGameManager.SaveFilePath);
            Assert.IsFalse(savePath.StartsWith(Application.persistentDataPath, StringComparison.OrdinalIgnoreCase));

            SaveGameManager.SaveFilePathOverride = null;
            Assert.AreEqual(Path.Combine(Application.persistentDataPath, "savegame.json"), SaveGameManager.SaveFilePath,
                "with no override the default path is unchanged");
            SaveGameManager.SaveFilePathOverride = savePath;
        }
    }
}
