// CC-002.4 PlayMode smoke tests, moved onto the GameDriver by CC-046.1. They run the real scenes with the real systems:
//  - GameplaySceneSmokeTests: start a game from the menu, make one move with the virtual keyboard (through
//    GameInputHandler, as a player does) and check that the move count, score and block positions react.
//  - MainMenuSmokeTests: set the menu's controls, press Start and check the gameplay scene starts a fresh game with them.
// The driver redirects saves to a temp folder (SaveGameManager.SaveFilePathOverride), so the player's real save is never
// touched. Any error logged during a test fails it (Unity's LogAssert default).
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SaveSystem;
using Simulation;
using TestSupport;
using UnityEngine.TestTools;

namespace PlayModeTests
{
    public class GameplaySceneSmokeTests : PlaythroughTest
    {

        [UnityTest]
        public IEnumerator OneMove_UpdatesMoveCountScoreAndBlockPositions()
        {
            Assert.IsFalse(SaveGameManager.HasSaveGame(), "The temp save folder must start without a save, so the game starts fresh.");
            yield return Driver.StartFromMenu(GameMode.Standard, 6, 6, spawnNextColour: true);

            Assert.AreEqual(GameMode.Standard, Driver.Mode);
            Assert.AreEqual(6, Driver.GridWidth);
            Assert.AreEqual(6, Driver.GridHeight);
            Assert.IsTrue(Driver.WaitingForInput, "A new game waits for input.");
            Assert.IsFalse(Driver.IsGameOver);
            Assert.AreEqual(0, Driver.MoveCount);
            Assert.AreEqual(0, Driver.Score);
            var blocksBefore = PlayModeTestSupport.BlockPositions();
            Assert.Greater(blocksBefore.Count, 0, "A new game starts with the first indicator block.");

            // Arrow keys until one move is accepted (a direction can be rejected when nothing can slide that way).
            foreach (var direction in new[] { Direction.Right, Direction.Left, Direction.Up, Direction.Down })
            {
                yield return Driver.Move(direction);
                if (Driver.MoveCount > 0)
                    break;
            }

            Assert.AreEqual(1, Driver.MoveCount, "Exactly one move was accepted.");
            Assert.IsTrue(Driver.WaitingForInput, "The game waits for the next move.");
            // The first move only slides the white indicator and resolves it: nothing merges, so no points yet.
            Assert.AreEqual(0, Driver.Score);

            var blocksAfter = PlayModeTestSupport.BlockPositions();
            bool anyChanged = blocksAfter.Count != blocksBefore.Count ||
                              blocksAfter.Any(kv => !blocksBefore.TryGetValue(kv.Key, out var oldPosition) || !oldPosition.Equals(kv.Value));
            Assert.IsTrue(anyChanged, "At least one block changed grid position (or was added) by the move.");

            // SaveGameSystem saved after the move, to the temp folder rather than persistentDataPath.
            Assert.IsTrue(File.Exists(Path.Combine(Driver.SaveFolder, "savegame.json")), "The move should have been saved to the temp folder.");
        }
    }

    public class MainMenuSmokeTests : PlaythroughTest
    {

        [UnityTest]
        public IEnumerator StartButton_LoadsGameplaySceneWithANewGame()
        {
            Assert.IsFalse(SaveGameManager.HasSaveGame(), "The temp save folder must start without a save, so there is no Resume path.");
            yield return Driver.OpenMenu();
            Assert.IsFalse(Driver.IsShown("resume-game-button"), "No save, so no Resume button.");

            // A non-default mode and size, set through the menu's own controls.
            yield return Driver.StartFromMenu(GameMode.ColorMerge, 5, 5, spawnNextColour: true);

            Assert.AreEqual(PlayModeTestSupport.GameplayScene, Driver.SceneName, "Start loads the gameplay scene.");
            Assert.IsTrue(Driver.WaitingForInput);
            Assert.IsFalse(Driver.IsGameOver);
            Assert.AreEqual(0, Driver.MoveCount, "A new game, not a resumed one.");
            Assert.AreEqual(GameMode.ColorMerge, Driver.Mode);
            Assert.AreEqual(5, Driver.GridWidth);
            Assert.AreEqual(5, Driver.GridHeight);
            Assert.AreEqual(0, Driver.Score);
        }
    }
}
