// CC-046.3: the Gameplay HUD cases of docs/wiki/test-plan.md, TP-HUD-01 to TP-HUD-07 (except two that stay manual).
// The steps follow the test plan; the HUD is read from the scene's own labels.
//  - TP-HUD-02 (board framing) stays manual: it depends on the real screen size, which a batchmode editor doesn't have
//    (CC-047 will run it on a device).
//  - TP-HUD-06 (wobble, particles) stays manual: it is about animation timing and looks.
using System.Collections;
using NUnit.Framework;
using Simulation;
using TestSupport;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace PlayModeTests
{
    [Category("TP_HUD")]
    public class HudCases : PlaythroughTest
    {
        [UnityTest, Category("TP_HUD_01")]
        public IEnumerator TP_HUD_01_HeaderAndFooterShowTheirContentAtStart()
        {
            yield return Driver.StartFromMenu(GameMode.Standard, 6, 6, true);
            yield return Driver.Screenshot("TP-HUD-01");

            Assert.AreEqual("Cathartic Colours", Driver.Text("title"));
            Assert.AreEqual("Moves: 0", Driver.MovesText);
            Assert.AreEqual("Score: 0", Driver.ScoreText);
            Assert.AreEqual("Your turn...", Driver.StatusText);

            var instruction = Driver.Find<VisualElement>("instruction-text").Q<Label>();
            StringAssert.StartsWith("Merge matching tiles", instruction.text, "The footer's instruction line describes the mode.");
            Assert.IsTrue(Driver.IsShown("instruction-text"));

            bool hint = false;
            Driver.Find<VisualElement>("footer").Query<Label>().ForEach(l => hint |= l.text == "Swipe / Arrow Keys / WASD to Move");
            Assert.IsTrue(hint, "The footer shows 'Swipe / Arrow Keys / WASD to Move'.");
            Assert.IsTrue(Driver.IsShown("footer-main-menu-button"));
            Assert.AreEqual("Main Menu", Driver.Text("footer-main-menu-button"));
        }

        [UnityTest, Category("TP_HUD_03")]
        public IEnumerator TP_HUD_03_StatusLabelShowsYourTurnWhileWaitingAndGridFullAtGameOver()
        {
            yield return Driver.ResumeWith(CaseChecks.AlmostFullCheckerboard, GameMode.Standard, spawnNextColour: true);
            Assert.AreEqual("Your turn...", Driver.StatusText, "The game waits for input.");
            yield return Driver.Screenshot("TP-HUD-03-waiting");

            yield return Driver.Move(Direction.Right);
            yield return GameDriver.Frames(2);

            Assert.IsTrue(Driver.IsGameOver);
            Assert.AreEqual("Grid Full!", Driver.StatusText);
            yield return Driver.Screenshot("TP-HUD-03-game-over");
        }

        [UnityTest, Category("TP_HUD_04")]
        public IEnumerator TP_HUD_04_MoveCounterRisesByOnePerPressEvenWhenNothingMoves()
        {
            yield return Driver.StartFromMenu(GameMode.Standard, 4, 4, false);
            Assert.AreEqual("Moves: 0", Driver.MovesText);

            yield return Driver.Move(Direction.Left);
            Assert.AreEqual("Moves: 1", Driver.MovesText);

            // The resolved block walks to the left wall, then keeps being pressed against it.
            string expected = BoardNotation.Build(4, 4, (1, 2, "?"));
            yield return Driver.Move(Direction.Left);
            Driver.AssertBoard(expected);
            yield return Driver.Move(Direction.Left);
            Driver.AssertBoard(BoardNotation.Build(4, 4, (0, 2, "?")), "The block reached the wall");
            Assert.AreEqual("Moves: 3", Driver.MovesText);

            yield return Driver.Move(Direction.Left);
            Driver.AssertBoard(BoardNotation.Build(4, 4, (0, 2, "?")), "Nothing can move");
            Assert.AreEqual("Moves: 4", Driver.MovesText, "A press where nothing moves still counts.");
            yield return Driver.Move(Direction.Left);
            Assert.AreEqual("Moves: 5", Driver.MovesText);
            Assert.AreEqual(5, Driver.MoveCount);
            yield return Driver.Screenshot("TP-HUD-04");
        }

        [UnityTest, Category("TP_HUD_05")]
        public IEnumerator TP_HUD_05_ScoreLabelShowsTheRunningTotal()
        {
            // Move 1 right: the Red pair merges. Move 2 up: the two Green blocks (moved to column 1 by move 1) merge.
            string start = BoardNotation.Build(4, 4, (2, 0, "Rs"), (3, 0, "Rs"), (0, 2, "Gs"), (0, 3, "Gs"));
            yield return Driver.ResumeWith(start, GameMode.Standard);
            Assert.AreEqual("Score: 0", Driver.ScoreText);

            yield return Driver.Move(Direction.Right);
            Assert.AreEqual("Score: 10", Driver.ScoreText, "The first Small-pair merge adds 10.");
            Driver.AssertBoard(BoardNotation.Build(4, 4, (3, 0, "Rm"), (1, 2, "Gs"), (1, 3, "Gs")));

            yield return Driver.Move(Direction.Up);
            Assert.AreEqual("Score: 20", Driver.ScoreText, "The second merge shows the running total, not the last gain.");
            Driver.AssertBoard(BoardNotation.Build(4, 4, (3, 1, "Rm"), (1, 3, "Gm")));

            yield return Driver.Move(Direction.Down);
            Assert.AreEqual("Score: 20", Driver.ScoreText, "A move with no merge leaves the score alone.");
            yield return Driver.Screenshot("TP-HUD-05");
        }

        [UnityTest, Category("TP_HUD_07")]
        public IEnumerator TP_HUD_07_GridCellsUseTheProfilesGridColour()
        {
            yield return Driver.StartFromMenu(GameMode.Standard, 4, 4, true);
            yield return Driver.Screenshot("TP-HUD-07");

            Color expected = GameManager.ActiveColorProfile.GridBackgroundColor;
            Assert.AreEqual(0.66f, expected.r, 0.01f, "The default profile's grid colour is about 0.66 grey.");
            Assert.AreEqual(expected.r, expected.g, 0.001f);
            Assert.AreEqual(expected.r, expected.b, 0.001f);

            int cells = 0;
            var propertyBlock = new MaterialPropertyBlock();
            foreach (var cell in Object.FindObjectsByType<GameObject>())
            {
                if (!cell.name.StartsWith("Cell_"))
                    continue;
                cells++;
                Color actual;
                var mesh = cell.GetComponent<MeshRenderer>();
                var sprite = cell.GetComponent<SpriteRenderer>();
                if (mesh != null)
                {
                    mesh.GetPropertyBlock(propertyBlock);
                    actual = propertyBlock.GetColor("_BaseColor");
                }
                else if (sprite != null)
                    actual = sprite.color;
                else
                {
                    Assert.Fail($"{cell.name} has neither a MeshRenderer nor a SpriteRenderer.");
                    yield break;
                }
                Assert.AreEqual(expected.r, actual.r, 0.005f, $"{cell.name} red");
                Assert.AreEqual(expected.g, actual.g, 0.005f, $"{cell.name} green");
                Assert.AreEqual(expected.b, actual.b, 0.005f, $"{cell.name} blue");
            }
            Assert.AreEqual(16, cells, "Every cell of the 4x4 board was checked, and all look the same.");
        }
    }
}
