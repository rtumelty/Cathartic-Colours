// CC-046.3: the Main menu (MENU) cases of docs/wiki/test-plan.md, TP-MENU-01 to TP-MENU-08, one test each.
// The steps follow the test plan: the menu is driven through its own controls (dropdowns, sliders, toggle, Start and
// Resume buttons) and read back from the UI. Every test starts with no save file (the driver redirects saves).
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Simulation;
using TestSupport;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace PlayModeTests
{
    [Category("TP_MENU")]
    public class MenuCases : PlaythroughTest
    {
        static readonly string[] PresetNames = { "Custom", "AdvancedColorMerge_6x6", "ColorMerge_4x4", "Standard_6x6" };

        string ModeDropdown => Driver.Find<DropdownField>("gamemode-dropdown").value;
        string WidthLabel => Driver.Text("width-value");
        string HeightLabel => Driver.Text("height-value");

        [UnityTest, Category("TP_MENU_01")]
        public IEnumerator TP_MENU_01_MenuShowsItsControlsWithStandardAndSixBySixByDefault()
        {
            yield return Driver.OpenMenu();
            yield return Driver.Screenshot("TP-MENU-01");

            foreach (var name in new[] { "preset-dropdown", "gamemode-dropdown", "gamemode-description", "width-slider", "height-slider",
                         "width-value", "height-value", "start-button" })
                Assert.IsTrue(Driver.IsShown(name), $"The menu shows '{name}'.");

            Assert.AreEqual("Standard", ModeDropdown);
            Assert.AreEqual(6, Driver.Find<SliderInt>("width-slider").value);
            Assert.AreEqual(6, Driver.Find<SliderInt>("height-slider").value);
            Assert.AreEqual("6", WidthLabel);
            Assert.AreEqual("6", HeightLabel);
            Assert.IsNotEmpty(Driver.Text("gamemode-description"), "A description is shown for the mode.");
            Assert.IsFalse(Driver.IsShown("resume-game-button"), "No save file, so no Resume button.");
        }

        [UnityTest, Category("TP_MENU_02")]
        public IEnumerator TP_MENU_02_ModeDropdownListsThreeModesAndTheDescriptionFollowsTheMode()
        {
            yield return Driver.OpenMenu();
            var dropdown = Driver.Find<DropdownField>("gamemode-dropdown");
            CollectionAssert.AreEqual(new[] { "Standard", "Color Merge", "Advanced Color Merge" }, dropdown.choices);

            var descriptions = new List<string> { Driver.Text("gamemode-description") };
            foreach (var choice in new[] { "Color Merge", "Advanced Color Merge", "Standard" })
            {
                yield return Driver.SelectDropdown("gamemode-dropdown", choice);
                string text = Driver.Text("gamemode-description");
                Assert.IsNotEmpty(text, $"The description for {choice}");
                Assert.IsFalse(descriptions.Contains(text) && choice != "Standard", $"The description for {choice} differs from the ones before it: '{text}'");
                descriptions.Add(text);
                yield return Driver.Screenshot("TP-MENU-02-" + choice.Replace(' ', '-'));
            }
            Assert.AreEqual(descriptions[0], descriptions[3], "Back on Standard the Standard description returns.");
            Assert.AreEqual(3, new HashSet<string>(descriptions).Count, "Three modes have three different descriptions.");
        }

        [UnityTest, Category("TP_MENU_03")]
        public IEnumerator TP_MENU_03_SizeSlidersRangeFourToTenWithLabelsAndAreIndependent()
        {
            yield return Driver.OpenMenu();
            var width = Driver.Find<SliderInt>("width-slider");
            var height = Driver.Find<SliderInt>("height-slider");
            Assert.AreEqual(4, width.lowValue);
            Assert.AreEqual(10, width.highValue);
            Assert.AreEqual(4, height.lowValue);
            Assert.AreEqual(10, height.highValue);

            yield return Driver.SetSlider("width-slider", 4);
            Assert.AreEqual("4", WidthLabel);
            yield return Driver.SetSlider("width-slider", 10);
            Assert.AreEqual("10", WidthLabel);
            Assert.AreEqual(6, height.value, "Moving the width slider leaves the height alone.");
            Assert.AreEqual("6", HeightLabel);

            yield return Driver.SetSlider("height-slider", 4);
            Assert.AreEqual("4", HeightLabel);
            yield return Driver.SetSlider("height-slider", 10);
            Assert.AreEqual("10", HeightLabel);
            Assert.AreEqual(10, width.value, "Moving the height slider leaves the width alone.");

            yield return Driver.SetSlider("width-slider", 5);
            yield return Driver.SetSlider("height-slider", 7);
            Assert.AreEqual("5", WidthLabel);
            Assert.AreEqual("7", HeightLabel);
            yield return Driver.Screenshot("TP-MENU-03");
        }

        [UnityTest, Category("TP_MENU_04")]
        public IEnumerator TP_MENU_04_PresetsSetModeAndSizeAndTheControlsFollow()
        {
            yield return Driver.OpenMenu();
            CollectionAssert.AreEqual(PresetNames, Driver.Find<DropdownField>("preset-dropdown").choices);

            // The test plan asks the tester to record whether the dropdown and sliders visibly update: they do (LoadPreset
            // calls LoadActiveConfiguration), and this case holds the game to it.
            yield return Driver.SelectDropdown("preset-dropdown", "ColorMerge_4x4");
            yield return AssertMenuShows(GameMode.ColorMerge, "Color Merge", 4, 4);
            yield return Driver.Screenshot("TP-MENU-04-ColorMerge-4x4");

            yield return Driver.SelectDropdown("preset-dropdown", "AdvancedColorMerge_6x6");
            yield return AssertMenuShows(GameMode.AdvancedColorMerge, "Advanced Color Merge", 6, 6);
            yield return Driver.Screenshot("TP-MENU-04-AdvancedColorMerge-6x6");

            yield return Driver.SelectDropdown("preset-dropdown", "Standard_6x6");
            yield return AssertMenuShows(GameMode.Standard, "Standard", 6, 6);

            yield return Driver.SetSlider("width-slider", 5);
            yield return Driver.SetSlider("height-slider", 7);
            Assert.AreEqual(5, GameManager.MenuConfiguration.gridWidth);
            Assert.AreEqual(7, GameManager.MenuConfiguration.gridHeight);
            Assert.AreEqual("5", WidthLabel);
            Assert.AreEqual("7", HeightLabel);
        }

        IEnumerator AssertMenuShows(GameMode mode, string modeLabel, int width, int height)
        {
            yield return null;
            Assert.AreEqual(mode, GameManager.MenuConfiguration.gameMode, "The preset's mode is the menu's mode.");
            Assert.AreEqual(width, GameManager.MenuConfiguration.gridWidth);
            Assert.AreEqual(height, GameManager.MenuConfiguration.gridHeight);
            Assert.AreEqual(modeLabel, ModeDropdown, "The mode dropdown shows the preset's mode.");
            Assert.AreEqual(width, Driver.Find<SliderInt>("width-slider").value, "The width slider shows the preset's width.");
            Assert.AreEqual(height, Driver.Find<SliderInt>("height-slider").value, "The height slider shows the preset's height.");
            Assert.AreEqual(width.ToString(), WidthLabel);
            Assert.AreEqual(height.ToString(), HeightLabel);
        }

        [UnityTest, Category("TP_MENU_05")]
        public IEnumerator TP_MENU_05_StartLoadsABoardOfTheChosenSizeWithTheWhiteBlockInTheMiddle()
        {
            yield return Driver.StartFromMenu(GameMode.Standard, 5, 7, true);

            Assert.AreEqual(5, Driver.GridWidth);
            Assert.AreEqual(7, Driver.GridHeight);
            Assert.AreEqual("Score: 0", Driver.ScoreText);
            Assert.AreEqual("Moves: 0", Driver.MovesText);
            Assert.AreEqual(1, Driver.Blocks(), "Exactly one block is on the board.");
            Assert.AreEqual(BoardNotation.Build(5, 7, (2, 3, "i")), Driver.Board(), "The white block is at (2,3).");
            yield return Driver.Screenshot("TP-MENU-05");
        }

        [UnityTest, Category("TP_MENU_06")]
        public IEnumerator TP_MENU_06_FooterMainMenuKeepsTheSettingsAndOffersNoResumeWithoutAMove()
        {
            yield return Driver.StartFromMenu(GameMode.Standard, 5, 7, true);

            yield return Driver.ReturnToMenu();

            Assert.AreEqual("Standard", ModeDropdown);
            Assert.AreEqual(5, Driver.Find<SliderInt>("width-slider").value);
            Assert.AreEqual(7, Driver.Find<SliderInt>("height-slider").value);
            Assert.AreEqual("5", WidthLabel);
            Assert.AreEqual("7", HeightLabel);
            Assert.IsFalse(Driver.IsShown("resume-game-button"), "No move was made, so no save and no Resume.");
            yield return Driver.Screenshot("TP-MENU-06");
        }

        [UnityTest, Category("TP_MENU_07")]
        public IEnumerator TP_MENU_07_NextColourIndicatorToggleOffMeansNoNewWhiteBlock()
        {
            yield return Driver.OpenMenu();
            yield return Driver.SelectDropdown("preset-dropdown", "Standard_6x6");
            Assert.IsTrue(Driver.Find<Toggle>("spawn-toggle").value, "The Standard 6x6 preset ticks the toggle.");

            yield return Driver.SetToggle("spawn-toggle", false);
            yield return Driver.Click("start-button");
            yield return Driver.WaitForGameplay();
            Assert.AreEqual(1, BoardNotation.Count(Driver.Board(), "i"), "The starting white block is there.");

            for (int move = 1; move <= 3; move++)
            {
                yield return Driver.Move(Direction.Left);
                Assert.AreEqual(0, BoardNotation.Count(Driver.Board(), "i"),
                    $"After move {move} there is no white block: the first move resolved it and none is spawned. Board: {Driver.Board()}");
            }
            Assert.AreEqual(1, Driver.Blocks(), "Only the resolved block is on the board.");

            yield return Driver.ReturnToMenu();
            Assert.IsFalse(Driver.Find<Toggle>("spawn-toggle").value, "The toggle is unticked on return.");
            yield return Driver.SetToggle("spawn-toggle", true);
            Assert.IsTrue(Driver.Find<Toggle>("spawn-toggle").value, "Ticking it sticks.");
            yield return Driver.Screenshot("TP-MENU-07");
        }

        [UnityTest, Category("TP_MENU_08")]
        public IEnumerator TP_MENU_08_ResumingAGameDoesNotChangeTheMenuSettings()
        {
            // A saved Color Merge 4x4 game with one move made.
            Driver.WriteSave(BoardNotation.Build(4, 4, (2, 2, "i"), (0, 0, "Rs")), GameMode.ColorMerge, spawnNextColour: true, moveCount: 1);
            yield return Driver.OpenMenu();
            Assert.IsTrue(Driver.IsShown("resume-game-button"));

            yield return Driver.SelectDropdown("gamemode-dropdown", "Standard");
            yield return Driver.SetSlider("width-slider", 5);
            yield return Driver.SetSlider("height-slider", 7);
            yield return Driver.Click("resume-game-button");
            yield return Driver.WaitForGameplay();
            Assert.AreEqual(GameMode.ColorMerge, Driver.Mode, "The resumed game is Color Merge.");
            Assert.AreEqual(4, Driver.GridWidth);
            Assert.AreEqual(4, Driver.GridHeight);

            yield return Driver.ReturnToMenu();
            Assert.AreEqual("Standard", ModeDropdown, "The menu keeps its own settings, not the resumed game's.");
            Assert.AreEqual(5, Driver.Find<SliderInt>("width-slider").value);
            Assert.AreEqual(7, Driver.Find<SliderInt>("height-slider").value);
            Assert.AreEqual("5", WidthLabel);
            Assert.AreEqual("7", HeightLabel);

            yield return Driver.Click("start-button");
            yield return Driver.WaitForGameplay();
            Assert.AreEqual(GameMode.Standard, Driver.Mode);
            Assert.AreEqual(5, Driver.GridWidth);
            Assert.AreEqual(7, Driver.GridHeight);
            Assert.AreEqual(0, Driver.MoveCount, "It is a new game.");
        }
    }
}
