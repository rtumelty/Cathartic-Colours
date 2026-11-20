// CC-002.4: PlayMode smoke tests. They run the real scenes with the real systems, which the EditMode tests don't:
//  - GameplaySceneSmokeTests: load the gameplay scene with a known configuration, send one move the way
//    GameInputHandler does, and check that the move count, score and block positions react.
//  - MainMenuSmokeTests: load the main menu, press the Start button and check the gameplay scene starts a fresh game.
// Both redirect saves to a temp folder (SaveGameManager.SaveFilePathOverride), so the player's real save is never
// touched and the game never resumes. Any error logged during a test fails it (Unity's LogAssert default).
using System.Collections;
using System.IO;
using System.Linq;
using ECS.Components;
using NUnit.Framework;
using SaveSystem;
using Simulation;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace PlayModeTests
{
    public class GameplaySceneSmokeTests
    {
        string saveFolder;

        [SetUp]
        public void SetUp()
        {
            saveFolder = PlayModeTestSupport.RedirectSaves();
            Assert.IsFalse(SaveGameManager.HasSaveGame(), "The temp save folder must start without a save, so the game starts fresh.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Unload the game scenes before the configuration is destroyed: their objects still read it every frame.
            yield return PlayModeTestSupport.UnloadGameScenes();
            PlayModeTestSupport.RestoreSaves(saveFolder);
            PlayModeTestSupport.DestroyRuntimeConfigs();
        }

        [UnityTest]
        public IEnumerator OneMove_UpdatesMoveCountScoreAndBlockPositions()
        {
            var config = PlayModeTestSupport.InitializeGameManager();

            yield return SceneManager.LoadSceneAsync(PlayModeTestSupport.GameplayScene);
            yield return PlayModeTestSupport.WaitForGameState();
            yield return null; // let GameRenderer finish its first frame

            var before = PlayModeTestSupport.GetSingleton<GameStateComponent>();
            var scoreBefore = PlayModeTestSupport.GetSingleton<ScoreComponent>();
            var grid = PlayModeTestSupport.GetSingleton<GridConfigComponent>();
            Assert.AreEqual(config.gameMode, PlayModeTestSupport.GetSingleton<GameModeComponent>().Mode);
            Assert.AreEqual(config.gridWidth, grid.Width);
            Assert.AreEqual(config.gridHeight, grid.Height);
            Assert.IsTrue(before.WaitingForInput, "A new game waits for input.");
            Assert.IsFalse(before.GameOver);
            Assert.AreEqual(0, before.MoveCount);
            Assert.AreEqual(0, scoreBefore.TotalScore);
            var blocksBefore = PlayModeTestSupport.BlockPositions();
            Assert.Greater(blocksBefore.Count, 0, "A new game starts with the first indicator block.");

            // Send moves as GameInputHandler.SendMoveCommand does, until one is accepted (a direction can be rejected
            // when nothing can slide that way).
            var directions = new[] { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) };
            GameStateComponent after = before;
            foreach (var direction in directions)
            {
                var em = PlayModeTestSupport.EntityManager;
                var moveEntity = em.CreateEntity();
                em.AddComponentData(moveEntity, new MoveDirectionComponent { Direction = direction });

                // ClearMoveDirectionSystem destroys the command in the frame it is processed.
                for (int frame = 0; frame < 30 && PlayModeTestSupport.Count<MoveDirectionComponent>() > 0; frame++)
                    yield return null;
                Assert.AreEqual(0, PlayModeTestSupport.Count<MoveDirectionComponent>(), "The move command was never consumed.");
                yield return null; // the frames after: EndFrameSystem has put WaitingForInput back
                yield return null;

                after = PlayModeTestSupport.GetSingleton<GameStateComponent>();
                if (after.MoveCount > before.MoveCount)
                    break;
            }

            Assert.AreEqual(before.MoveCount + 1, after.MoveCount, "Exactly one move was accepted.");
            Assert.IsTrue(after.WaitingForInput, "The game waits for the next move.");

            var scoreAfter = PlayModeTestSupport.GetSingleton<ScoreComponent>();
            // The first move only slides the white indicator and resolves it: nothing merges, so no points yet.
            Assert.AreEqual(scoreBefore.TotalScore, scoreAfter.TotalScore);
            Assert.AreEqual(0, scoreAfter.CurrentFrameScore);

            var blocksAfter = PlayModeTestSupport.BlockPositions();
            bool anyChanged = blocksAfter.Count != blocksBefore.Count ||
                              blocksAfter.Any(kv => !blocksBefore.TryGetValue(kv.Key, out var oldPosition) || !oldPosition.Equals(kv.Value));
            Assert.IsTrue(anyChanged, "At least one block changed grid position (or was added) by the move.");

            // SaveGameSystem saved after the move, to the temp folder rather than persistentDataPath.
            Assert.IsTrue(File.Exists(Path.Combine(saveFolder, "savegame.json")), "The move should have been saved to the temp folder.");
        }
    }

    public class MainMenuSmokeTests
    {
        string saveFolder;

        [SetUp]
        public void SetUp()
        {
            saveFolder = PlayModeTestSupport.RedirectSaves();
            Assert.IsFalse(SaveGameManager.HasSaveGame(), "The temp save folder must start without a save, so there is no Resume path.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Unload the game scenes before the configuration is destroyed: their objects still read it every frame.
            yield return PlayModeTestSupport.UnloadGameScenes();
            PlayModeTestSupport.RestoreSaves(saveFolder);
            PlayModeTestSupport.DestroyRuntimeConfigs();
        }

        [UnityTest]
        public IEnumerator StartButton_LoadsGameplaySceneWithANewGame()
        {
            // The menu keeps an already initialised GameManager (it only initialises when it isn't), so the settings the
            // game must start with are known: a non-default mode and size, set on the runtime copy.
            var config = PlayModeTestSupport.InitializeGameManager();
            config.gameMode = GameMode.ColorMerge;
            config.gridWidth = 5;
            config.gridHeight = 5;

            yield return SceneManager.LoadSceneAsync(PlayModeTestSupport.MainMenuScene);
            yield return null; // UIDocument builds its visual tree

            var menu = Object.FindAnyObjectByType<CatharticColours.UI.MainMenuController>();
            Assert.IsNotNull(menu, "The main menu scene has a MainMenuController.");
            var root = menu.GetComponent<UIDocument>().rootVisualElement;
            var startButton = root.Q<Button>("start-button");
            Assert.IsNotNull(startButton, "The menu has the start-button.");
            var resumeButton = root.Q<Button>("resume-game-button");
            if (resumeButton != null)
                Assert.AreEqual(DisplayStyle.None, resumeButton.resolvedStyle.display, "No save, so no Resume button.");

            // A real submit event on the button (what a keyboard or gamepad activation sends), handled by the same
            // Clickable that raises Button.clicked and calls StartGame. A synthetic ClickEvent isn't handled by it.
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = startButton;
                startButton.SendEvent(submit);
            }

            for (int i = 0; i < 600 && SceneManager.GetActiveScene().name != PlayModeTestSupport.GameplayScene; i++)
                yield return null;
            Assert.AreEqual(PlayModeTestSupport.GameplayScene, SceneManager.GetActiveScene().name, "Start loads the gameplay scene.");

            yield return PlayModeTestSupport.WaitForGameState();
            yield return null;

            var gameState = PlayModeTestSupport.GetSingleton<GameStateComponent>();
            Assert.IsTrue(gameState.WaitingForInput);
            Assert.IsFalse(gameState.GameOver);
            Assert.AreEqual(0, gameState.MoveCount, "A new game, not a resumed one.");
            Assert.AreEqual(GameMode.ColorMerge, PlayModeTestSupport.GetSingleton<GameModeComponent>().Mode);
            var grid = PlayModeTestSupport.GetSingleton<GridConfigComponent>();
            Assert.AreEqual(5, grid.Width);
            Assert.AreEqual(5, grid.Height);
            Assert.AreEqual(0, PlayModeTestSupport.GetSingleton<ScoreComponent>().TotalScore);
        }
    }
}
