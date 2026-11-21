// CC-046.1: the spike that proved Unity's official test tools on our scenes, kept as tests. Each test answers one
// question recorded in ADR-006 (docs/wiki/decisions/ADR-006-official-test-tools.md):
//  1. The UI Test Framework's RuntimePanelSimulator drives the scenes' own UIDocument panels (dropdown, slider, button).
//  2. Input System test devices (InputTestFixture) make moves through GameInputHandler (keyboard) and SwipeDetector (touch).
//  3. A screenshot taken in a batchmode PlayMode run holds the rendered frame.
//  4. Audio events can be read without FMOD playing sound (AudioEventRecorderSystem).
using System.Collections;
using System.IO;
using NUnit.Framework;
using Simulation;
using TestSupport;
using UnityEngine;
using UnityEngine.TestTools;

namespace PlayModeTests
{
    [Category("CC046")]
    public class ToolSpikeTests : PlaythroughTest
    {

        [UnityTest]
        public IEnumerator Q1_PanelSimulator_DrivesTheMenuScenesOwnPanel()
        {
            yield return Driver.OpenMenu();

            yield return Driver.SelectDropdown("gamemode-dropdown", "Color Merge");
            StringAssert.StartsWith("Combine primary colors", Driver.Text("gamemode-description"));
            Assert.AreEqual(GameMode.ColorMerge, GameManager.MenuConfiguration.gameMode, "The dropdown's change callback ran.");

            yield return Driver.SetSlider("width-slider", 8);
            Assert.AreEqual("8", Driver.Text("width-value"));
            Assert.AreEqual(8, GameManager.MenuConfiguration.gridWidth);

            yield return Driver.SetSlider("height-slider", 4);
            Assert.AreEqual("4", Driver.Text("height-value"));

            yield return Driver.Click("start-button");
            yield return Driver.WaitForGameplay();
            Assert.AreEqual(GameMode.ColorMerge, Driver.Mode);
            Assert.AreEqual(8, Driver.GridWidth);
            Assert.AreEqual(4, Driver.GridHeight);
        }

        [UnityTest]
        public IEnumerator Q2_KeyboardMove_GoesThroughGameInputHandler()
        {
            yield return Driver.StartFromMenu(GameMode.Standard, 6, 6, true);
            Assert.AreEqual(". . . . . .|. . . . . .|. . . i . .|. . . . . .|. . . . . .|. . . . . .", Driver.Board(),
                "A new 6x6 game has the white indicator at (3,3).");

            yield return Driver.Move(Direction.Up);

            Assert.AreEqual(1, Driver.MoveCount, "The arrow key made one move.");
            var tokens = BoardNotation.Tokens(Driver.Board());
            Assert.IsTrue(BoardNotation.IsResolvedPrimary(BoardNotation.ParseCell(tokens[3, 3])),
                "The indicator resolved in place to a Small primary: " + Driver.Board());
        }

        [UnityTest]
        public IEnumerator Q2_TouchSwipe_GoesThroughSwipeDetector()
        {
            yield return Driver.StartFromMenu(GameMode.Standard, 6, 6, true);

            // queueEventOnly: the events are processed by the player loop's input update, as on a device.
            var start = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Driver.Input.BeginTouch(1, start, queueEventOnly: true, screen: Driver.Touchscreen);
            yield return null;
            yield return null;
            Driver.Input.EndTouch(1, start + new Vector2(-200f, 0f), queueEventOnly: true, screen: Driver.Touchscreen);
            yield return GameDriver.Frames(4);

            Assert.AreEqual(1, Driver.MoveCount, "The left swipe made one move.");
        }

        [UnityTest]
        public IEnumerator Q3_Screenshot_HoldsTheRenderedBoardAndUi()
        {
            yield return Driver.StartFromMenu(GameMode.Standard, 6, 6, true);
            yield return Driver.Move(Direction.Up);

            yield return Driver.Screenshot("CC046-Q3");

            Assert.IsTrue(File.Exists(Driver.LastScreenshotPath), "The screenshot was saved to the report folder.");
            Assert.Greater(new FileInfo(Driver.LastScreenshotPath).Length, 10000,
                "A rendered board and HUD compress to more than a blank image would.");
        }

        [UnityTest]
        public IEnumerator Q4_AudioEvents_AreRecordedBeforeAudioSystemPlaysThem()
        {
            yield return Driver.StartFromMenu(GameMode.Standard, 6, 6, true);
            Driver.AudioEvents(); // nothing before the first move

            yield return Driver.Move(Direction.Up);

            CollectionAssert.Contains(Driver.AudioEvents(), "event:/Block_Move");
            Assert.AreEqual(0, PlayModeTestSupport.Count<ECS.Components.AudioEventComponent>(), "AudioSystem still consumed the event.");
        }
    }
}
