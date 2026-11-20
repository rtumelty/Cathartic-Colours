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
            // KNOWN BUG (CC-044, #77; see ResumedGame_RandomStateIsReSeeded_NotContinued): the restored state is not the saved one.
            Assert.AreEqual(new Unity.Mathematics.Random(originalState.Random.state).state, resumed.GameState.Random.state);
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
        public void ResumedGame_RandomStateIsReSeeded_NotContinued()
        {
            // KNOWN BUG, current behaviour (CC-044, #77, found by CC-002.3): RestoreWorldState does
            // `new Random(saveData.randomState)`, but Random(uint) takes a SEED and hashes it, while randomState holds the
            // generator STATE (Random.state). So a resumed game does not continue the saved random sequence: the next
            // spawn can differ from the one the original game would have made. Fix: `new Random { state = saved }`.
            var game = PlayTwoMovesAndSave();
            var savedState = game.GameState.Random.state;
            var resumed = Resume();

            Assert.AreNotEqual(savedState, resumed.GameState.Random.state);
            Assert.AreEqual(new Unity.Mathematics.Random(savedState).state, resumed.GameState.Random.state);
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
            Assert.AreEqual(new Unity.Mathematics.Random(99u).state, resumed.GameState.Random.state);
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

        string BadPath => Path.Combine(folder, "savegame.bad.json");

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
            PlayTwoMovesAndSave();
            var json = File.ReadAllText(savePath).Replace("\"version\": \"1.0\"", "\"version\": \"99.0\"");
            Assert.That(json, Does.Contain("99.0"), "precondition: the version was rewritten");
            File.WriteAllText(savePath, json);

            LogAssert.Expect(LogType.Warning, new Regex("unknown version \"99.0\""));
            Assert.IsFalse(SaveGameManager.HasSaveGame(), "the menu would not offer Resume");

            Assert.IsFalse(File.Exists(savePath));
            Assert.AreEqual(json, File.ReadAllText(BadPath), "the file is kept, unchanged, as savegame.bad.json");
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

            LogAssert.Expect(LogType.Warning, new Regex("Save game is unusable"));
            Assert.IsNull(SaveGameManager.LoadSaveGame());

            Assert.IsFalse(SaveGameManager.HasSaveGame());
            Assert.IsFalse(File.Exists(savePath));
            Assert.AreEqual(content, File.ReadAllText(BadPath));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ASecondUnusableSave_ReplacesTheEarlierBadFile()
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(BadPath, "older bad save");
            File.WriteAllText(savePath, "newer bad save");

            LogAssert.Expect(LogType.Warning, new Regex("Save game is unusable"));
            Assert.IsFalse(SaveGameManager.HasSaveGame());

            Assert.AreEqual("newer bad save", File.ReadAllText(BadPath));
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

        // ---- Known gaps: current behaviour, named issues -----------------------------------------------------

        [Test]
        public void KnownGap_ConfigurationOutsideTheFourSavedFieldsComesFromTheTemplate_CC027()
        {
            // CC-027 (#39): a resume replaces the shared runtime configuration (the menu then shows the resumed game's
            // settings) and only gameMode, grid size and spawnNextColourSystem are saved: the tier points are
            // whatever the template has.
            InitializeGameManager(GameMode.ColorMerge, 3, 5, false);
            var game = CreateWorld(GameMode.ColorMerge, "Rs . . .|. . . .|. . . .|. . . .");
            game.MoveAndSave(BoardStepTestWorld.Right);
            var saveData = SaveGameManager.LoadSaveGame();
            var configBefore = GameManager.ActiveConfiguration;

            SaveGameManager.InitializeFromSave(saveData, NewConfig(GameMode.Standard, 6, 6, true, tier1Points: 4242), NewProfile("ProfileA"));

            Assert.AreNotSame(configBefore, GameManager.ActiveConfiguration, "the shared runtime configuration was replaced");
            Assert.AreEqual(GameMode.ColorMerge, GameManager.ActiveConfiguration.gameMode);
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
