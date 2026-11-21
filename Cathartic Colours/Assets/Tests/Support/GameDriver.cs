// GameDriver (CC-046): plays the real game the way a tester does, for PlayMode tests and agents.
//
//  - Menus are driven through the scenes' own UI Toolkit panels with the UI Test Framework's RuntimePanelSimulator
//    (real pointer events on the named controls: start-button, gamemode-dropdown, width-slider, ...).
//  - Moves go through Input System test devices (InputTestFixture): a virtual keyboard, gamepad and touchscreen, so a
//    move takes the same path as a player's, through GameInputHandler and SwipeDetector.
//  - Known boards are written as a save file and resumed through the menu's real Resume button (ResumeWith).
//  - Readouts come from the ECS world (board in test-plan notation, score, move count, game over) and the HUD labels.
//
// Runtime APIs only, no UnityEditor calls, so the same tests can run in an Android player (CC-047). See
// docs/wiki/systems/test-automation.md and ADR-006.
//
// Tests derive from PlaythroughTest, which creates a driver before each test and ends it after:
//   public class MyCase : PlaythroughTest
//   {
//       [UnityTest] public IEnumerator Case()
//       {
//           yield return Driver.StartFromMenu(GameMode.Standard, 6, 6, true);
//           yield return Driver.Move(Direction.Up);
//           Assert.AreEqual(1, Driver.MoveCount);
//       }
//   }
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ECS.Components;
using NUnit.Framework;
using SaveSystem;
using Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;
using Object = UnityEngine.Object;

namespace TestSupport
{
    public enum Direction { Up, Down, Left, Right }

    public sealed class GameDriver
    {
        /// <summary>Frames a step may take before the driver gives up waiting for it.</summary>
        public const int TimeoutFrames = PlayModeTestSupport.TimeoutFrames;

        readonly InputTestFixture input = new InputTestFixture();
        readonly RuntimeUITestFixture ui;
        string saveFolder;
        UIDocument document;

        /// <summary>A temp folder per run, used when no report folder is given.</summary>
        public static readonly string DefaultReportFolder =
            Path.Combine(Application.temporaryCachePath, "cc-playthrough", DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss"));

        /// <summary>Where screenshots and logs go. Given to <see cref="Begin"/>, or a temp folder.</summary>
        public string ReportFolder { get; }

        /// <summary>The redirected save folder: the player's real save is never read or written.</summary>
        public string SaveFolder => saveFolder;

        public Keyboard Keyboard { get; private set; }
        public Gamepad Gamepad { get; private set; }
        public Touchscreen Touchscreen { get; private set; }

        /// <summary>The Input System test fixture, for steps the driver has no helper for.</summary>
        public InputTestFixture Input => input;

        /// <summary>The UI Test Framework simulator, attached to the current scene's panel.</summary>
        public PanelSimulator Simulate => ui.simulate;

        GameDriver(RuntimeUITestFixture ui, string reportFolder)
        {
            this.ui = ui;
            ReportFolder = string.IsNullOrEmpty(reportFolder) ? DefaultReportFolder : reportFolder;
        }

        /// <summary>
        /// Prepares a test: redirects saves to an empty temp folder, resets GameManager to a first launch, replaces the
        /// input devices with a virtual keyboard, gamepad and touchscreen, and starts recording audio events.
        /// <paramref name="ui"/> is the test's UI Test Framework fixture (see <see cref="PlaythroughTest"/>), which the driver
        /// attaches to each scene's UIDocument. Call from [UnitySetUp], with no game scene loaded.
        /// </summary>
        public static GameDriver Begin(RuntimeUITestFixture ui, string reportFolder = null)
        {
            var driver = new GameDriver(ui, reportFolder);
            Directory.CreateDirectory(driver.ReportFolder);
            driver.saveFolder = PlayModeTestSupport.RedirectSaves();
            PlayModeTestSupport.DestroyRuntimeConfigs();

            driver.input.Setup();
            driver.Keyboard = InputSystem.AddDevice<Keyboard>();
            driver.Gamepad = InputSystem.AddDevice<Gamepad>();
            driver.Touchscreen = InputSystem.AddDevice<Touchscreen>();

            PanelClock.Ensure();
            AudioEventRecorderSystem.EnsureInDefaultWorld();
            AudioEventRecorderSystem.Clear();
            return driver;
        }

        /// <summary>Ends a test: unloads the game scenes, restores the input system and the save path, and resets GameManager.</summary>
        public IEnumerator End()
        {
            document = null;
            yield return PlayModeTestSupport.UnloadGameScenes();
            RestorePanelTarget();
            input.TearDown();
            PlayModeTestSupport.RestoreSaves(saveFolder);
            PlayModeTestSupport.DestroyRuntimeConfigs();
            AudioEventRecorderSystem.Clear();
        }

        // ---------------------------------------------------------------- scenes and menus

        /// <summary>The active scene's name.</summary>
        public string SceneName => SceneManager.GetActiveScene().name;

        /// <summary>Loads the main menu, as on launch, and attaches to its panel.</summary>
        public IEnumerator OpenMenu()
        {
            yield return SceneManager.LoadSceneAsync(PlayModeTestSupport.MainMenuScene);
            yield return AttachToScene(PlayModeTestSupport.MainMenuScene);
        }

        /// <summary>
        /// Starts a new game from the main menu with the given settings, through the real controls: picks the mode in
        /// gamemode-dropdown, drags width-slider and height-slider, sets spawn-toggle and presses start-button. Opens the
        /// menu first if it isn't showing. Returns once the gameplay scene waits for the first move.
        /// </summary>
        public IEnumerator StartFromMenu(GameMode mode, int width, int height, bool spawnNextColour = true)
        {
            if (SceneName != PlayModeTestSupport.MainMenuScene)
                yield return OpenMenu();

            yield return SelectDropdown("gamemode-dropdown", ModeLabel(mode));
            yield return SetSlider("width-slider", width);
            yield return SetSlider("height-slider", height);
            yield return SetToggle("spawn-toggle", spawnNextColour);
            yield return Click("start-button");
            yield return WaitForGameplay();
        }

        /// <summary>Waits until the gameplay scene is active, its game state exists and it waits for input, then attaches to its panel.</summary>
        public IEnumerator WaitForGameplay()
        {
            yield return WaitUntil(() => SceneName == PlayModeTestSupport.GameplayScene, "the gameplay scene to load");
            yield return PlayModeTestSupport.WaitForGameState();
            yield return AttachToScene(PlayModeTestSupport.GameplayScene);
            yield return null; // GameRenderer's first Update fills the HUD
        }

        /// <summary>The menu label of a game mode in gamemode-dropdown.</summary>
        public static string ModeLabel(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.ColorMerge: return "Color Merge";
                case GameMode.AdvancedColorMerge: return "Advanced Color Merge";
                default: return "Standard";
            }
        }

        /// <summary>Waits until the main menu is the active scene (after a button loaded it), then attaches to its panel.</summary>
        public IEnumerator WaitForMenu()
        {
            yield return WaitUntil(() => SceneName == PlayModeTestSupport.MainMenuScene, "the main menu to load");
            yield return AttachToScene(PlayModeTestSupport.MainMenuScene);
        }

        /// <summary>Taps the footer Main Menu button of the gameplay scene and waits for the menu.</summary>
        public IEnumerator ReturnToMenu()
        {
            yield return Click("footer-main-menu-button");
            yield return WaitForMenu();
        }

        /// <summary>The file names in the redirected save folder (savegame.json, savegame.1.json, savegame.bad.*.json, ...), sorted.</summary>
        public string[] SaveFiles()
        {
            var names = new List<string>();
            foreach (var path in Directory.GetFiles(saveFolder))
                names.Add(Path.GetFileName(path));
            names.Sort(string.CompareOrdinal);
            return names.ToArray();
        }

        /// <summary>The full path of a file in the redirected save folder.</summary>
        public string SavePath(string fileName) => Path.Combine(saveFolder, fileName);

        IEnumerator AttachToScene(string sceneName)
        {
            UIDocument found = null;
            yield return WaitUntil(() =>
            {
                found = FindDocument(sceneName);
                return found != null && found.rootVisualElement != null && found.rootVisualElement.panel != null;
            }, "a UIDocument in " + sceneName);
            Attach(found);
            yield return null; // first layout pass
        }

        static UIDocument FindDocument(string sceneName)
        {
            foreach (var doc in Object.FindObjectsByType<UIDocument>())
                if (doc.isActiveAndEnabled && doc.gameObject.scene.name == sceneName && doc.panelSettings != null)
                    return doc;
            return null;
        }

        /// <summary>
        /// The size the scene UI is laid out at in the editor: a portrait phone screen. A batchmode editor's screen is
        /// 640x480, where the menu's controls overlap and clicks land on the wrong element, so in the editor the
        /// driver points the panel at a render texture of this size for the test (see <see cref="UseEditorPanelTarget"/>).
        /// In a player the real screen is used.
        /// </summary>
        public static readonly Vector2Int EditorPanelSize = new Vector2Int(1080, 1920);

        static RenderTexture editorPanelTarget;
        PanelSettings retargetedPanel;
        RenderTexture originalPanelTarget;
        bool originalClearColor;
        Color originalClearValue;

        /// <summary>The render texture the UI draws into in the editor, or null in a player.</summary>
        public RenderTexture PanelTarget => retargetedPanel != null ? editorPanelTarget : null;

        void Attach(UIDocument doc)
        {
            document = doc;
            if (Application.isEditor)
                UseEditorPanelTarget(doc.panelSettings);
            ui.SetUIContent(doc);
            PanelClock.Sync();
        }

        void UseEditorPanelTarget(PanelSettings settings)
        {
            if (retargetedPanel == settings)
                return;
            RestorePanelTarget();
            if (editorPanelTarget == null)
            {
                editorPanelTarget = new RenderTexture(EditorPanelSize.x, EditorPanelSize.y, 24, RenderTextureFormat.ARGB32)
                    { name = "CC-046 editor panel target", hideFlags = HideFlags.HideAndDontSave };
                editorPanelTarget.Create();
            }
            retargetedPanel = settings;
            originalPanelTarget = settings.targetTexture;
            originalClearColor = settings.clearColor;
            originalClearValue = settings.colorClearValue;
            settings.targetTexture = editorPanelTarget;
            settings.clearColor = true; // a render texture keeps last frame's pixels unless the panel clears it
            settings.colorClearValue = Color.clear;
        }

        void RestorePanelTarget()
        {
            if (retargetedPanel != null)
            {
                retargetedPanel.targetTexture = originalPanelTarget;
                retargetedPanel.clearColor = originalClearColor;
                retargetedPanel.colorClearValue = originalClearValue;
            }
            retargetedPanel = null;
            originalPanelTarget = null;
        }

        // ---------------------------------------------------------------- UI helpers

        /// <summary>The current scene's UI root (the UIDocument's root element).</summary>
        public VisualElement Root
        {
            get
            {
                if (document == null)
                    throw new InvalidOperationException("No scene UI is attached: open the menu or start a game first.");
                return document.rootVisualElement;
            }
        }

        /// <summary>The named element in the current scene's UI, or a failure naming it.</summary>
        public T Find<T>(string name) where T : VisualElement
        {
            var element = Root.Q<T>(name);
            if (element == null)
                throw new InvalidOperationException($"No {typeof(T).Name} named '{name}' in the {SceneName} UI.");
            return element;
        }

        /// <summary>True when the named element exists and is displayed (it and its ancestors not display: none).</summary>
        public bool IsShown(string name)
        {
            var element = Root.Q<VisualElement>(name);
            for (var e = element; e != null; e = e.parent)
                if (e.resolvedStyle.display == DisplayStyle.None)
                    return false;
            return element != null;
        }

        /// <summary>The text of the named label, button or text element.</summary>
        public string Text(string name) => Find<TextElement>(name).text;

        IEnumerator WaitForLayout(VisualElement element)
        {
            yield return WaitUntil(() =>
            {
                var r = element.worldBound;
                return !float.IsNaN(r.width) && r.width > 0 && r.height > 0;
            }, $"'{element.name}' to be laid out");
        }

        /// <summary>Clicks the named element with a simulated mouse click at its centre.</summary>
        public IEnumerator Click(string name)
        {
            yield return Click(Find<VisualElement>(name));
        }

        /// <summary>Clicks an element with a simulated mouse click at its centre.</summary>
        public IEnumerator Click(VisualElement element)
        {
            yield return WaitForLayout(element);
            PanelClock.Sync();
            ui.simulate.Click(element);
            yield return null;
        }

        /// <summary>Opens the named dropdown and clicks the entry with the given text.</summary>
        public IEnumerator SelectDropdown(string name, string choice)
        {
            var dropdown = Find<DropdownField>(name);
            if (dropdown.value == choice)
                yield break;
            yield return Click(dropdown.Q(className: "unity-base-popup-field__input") ?? dropdown);

            VisualElement item = null;
            for (int i = 0; i < 30 && (item = FindMenuItem(choice)) == null; i++)
                yield return null;
            if (item == null)
                throw new InvalidOperationException($"The '{choice}' entry of '{name}' did not appear. Panel:\n{DescribeTree(Root.panel.visualTree, 0, 3)}");
            yield return Click(item);
            yield return WaitUntil(() => dropdown.value == choice, $"'{name}' to read '{choice}'");
        }

        VisualElement FindMenuItem(string text)
        {
            // The open menu (GenericDropdownMenu) is added to the panel's top level, outside the document's root.
            VisualElement match = null;
            Root.panel.visualTree.Query<VisualElement>(className: "unity-base-dropdown__item").ForEach(item =>
            {
                if (match == null && item.Q<Label>()?.text == text)
                    match = item;
            });
            return match;
        }

        /// <summary>The element tree under <paramref name="element"/>, one line per element with its type, name, classes and text.</summary>
        public static string DescribeTree(VisualElement element, int depth, int maxDepth = 12)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(' ', depth * 2).Append(element.GetType().Name);
            if (!string.IsNullOrEmpty(element.name)) sb.Append(" #").Append(element.name);
            foreach (var c in element.GetClasses()) sb.Append(" .").Append(c);
            if (element is TextElement t && !string.IsNullOrEmpty(t.text)) sb.Append(" \"").Append(t.text).Append('"');
            sb.Append(' ').Append(element.worldBound).Append('\n');
            if (depth < maxDepth)
                foreach (var child in element.Children())
                    sb.Append(DescribeTree(child, depth + 1, maxDepth));
            return sb.ToString();
        }

        /// <summary>Drags the named SliderInt's handle to the given value.</summary>
        public IEnumerator SetSlider(string name, int value)
        {
            var slider = Find<SliderInt>(name);
            if (value < slider.lowValue || value > slider.highValue)
                throw new ArgumentOutOfRangeException(nameof(value), $"'{name}' ranges {slider.lowValue}..{slider.highValue}, not {value}.");
            if (slider.value == value)
                yield break;

            var dragger = slider.Q(className: BaseSlider<int>.draggerUssClassName);
            var tracker = slider.Q(className: BaseSlider<int>.trackerUssClassName);
            yield return WaitForLayout(dragger);
            yield return WaitForLayout(tracker);

            // The dragger's centre travels between the tracker's ends minus half its own width at each end.
            Rect track = tracker.worldBound, handle = dragger.worldBound;
            float t = (value - slider.lowValue) / (float)(slider.highValue - slider.lowValue);
            float x = track.xMin + handle.width / 2f + t * (track.width - handle.width);
            var from = handle.center;
            var to = new Vector2(x, from.y);

            PanelClock.Sync();
            ui.simulate.DragAndDrop(from, to);
            yield return null;
            if (slider.value != value)
                throw new InvalidOperationException($"Dragging '{name}' to {value} left it at {slider.value}.");
        }

        /// <summary>Clicks the named Toggle if it doesn't already have the given value.</summary>
        public IEnumerator SetToggle(string name, bool value)
        {
            var toggle = Find<Toggle>(name);
            if (toggle.value == value)
                yield break;
            yield return Click(toggle.Q(className: Toggle.inputUssClassName) ?? toggle);
            yield return WaitUntil(() => toggle.value == value, $"'{name}' to read {value}");
        }

        // ---------------------------------------------------------------- known boards

        /// <summary>
        /// Starts a game from a known board through the real resume path: writes a save file for the board (see
        /// <see cref="WriteSave"/>), opens the main menu, clicks resume-game-button and waits for the gameplay scene.
        /// The board's size comes from the board text. The game then waits for input with the given move count and score.
        /// <paramref name="seed"/> sets the random generator, so spawns are repeatable; with
        /// <paramref name="spawnNextColour"/> off, moves don't depend on it.
        /// </summary>
        public IEnumerator ResumeWith(string board, GameMode mode, uint seed = 1, bool spawnNextColour = false, int moveCount = 0, int score = 0)
        {
            WriteSave(board, mode, seed, spawnNextColour, moveCount, score);
            yield return OpenMenu();
            yield return WaitUntil(() => IsShown("resume-game-button"), "the Resume button to show");
            yield return Click("resume-game-button");
            yield return WaitForGameplay();
        }

        /// <summary>
        /// Writes a save file for a known board to the redirected save path, without opening any scene. Use it for cases
        /// that start from the main menu themselves. "?" tokens are for expected boards only and throw here.
        /// </summary>
        public void WriteSave(string board, GameMode mode, uint seed = 1, bool spawnNextColour = false, int moveCount = 0, int score = 0)
        {
            var tokens = BoardNotation.Tokens(board);
            int width = tokens.GetLength(0), height = tokens.GetLength(1);
            var blocks = new List<SavedBlock>();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (tokens[x, y] == "?")
                        throw new ArgumentException($"'?' at ({x},{y}) only belongs in an expected board, not in a board to start from.", nameof(board));
                    var cell = BoardNotation.ParseCell(tokens[x, y]);
                    if (cell.IsEmpty)
                        continue;
                    blocks.Add(new SavedBlock
                    {
                        color = cell.Color,
                        size = cell.Size,
                        gridX = x,
                        gridY = y,
                        isNextColorIndicator = cell.IsIndicator
                    });
                }

            SaveGameManager.WriteSave(new SaveGameData
            {
                gameMode = mode,
                gridWidth = width,
                gridHeight = height,
                spawnNextColourSystem = spawnNextColour,
                colorProfileName = null,
                waitingForInput = true,
                gameOver = false,
                moveCount = moveCount,
                randomState = new Unity.Mathematics.Random(seed).state,
                totalScore = score,
                blocks = blocks.ToArray()
            });
        }

        // ---------------------------------------------------------------- moves

        /// <summary>
        /// One move with the virtual keyboard's arrow key: press, wait until the board has handled it (accepted or
        /// rejected), release. Check <see cref="MoveCount"/> to see whether it was accepted.
        /// </summary>
        public IEnumerator Move(Direction direction)
        {
            yield return Press(ArrowKey(direction));
        }

        /// <summary>One move with a WASD key instead of the arrows.</summary>
        public IEnumerator MoveWasd(Direction direction)
        {
            yield return Press(WasdKey(direction));
        }

        ButtonControl ArrowKey(Direction d)
        {
            switch (d)
            {
                case Direction.Up: return Keyboard.upArrowKey;
                case Direction.Down: return Keyboard.downArrowKey;
                case Direction.Left: return Keyboard.leftArrowKey;
                default: return Keyboard.rightArrowKey;
            }
        }

        ButtonControl WasdKey(Direction d)
        {
            switch (d)
            {
                case Direction.Up: return Keyboard.wKey;
                case Direction.Down: return Keyboard.sKey;
                case Direction.Left: return Keyboard.aKey;
                default: return Keyboard.dKey;
            }
        }

        /// <summary>
        /// One move with a touch swipe on the virtual touchscreen, through SwipeDetector: touch down at the screen
        /// centre, two frames, lift <paramref name="distance"/> pixels away. The game ignores swipes under 50 pixels, so a
        /// short distance is a swipe that makes no move.
        /// </summary>
        public IEnumerator Swipe(Direction direction, float distance = 200f)
        {
            // queueEventOnly: the events are processed by the player loop's input update, as on a device.
            var start = new Vector2(Screen.width / 2f, Screen.height / 2f);
            input.BeginTouch(1, start, queueEventOnly: true, screen: Touchscreen);
            yield return null;
            yield return null;
            input.EndTouch(1, start + distance * Vector(direction), queueEventOnly: true, screen: Touchscreen);
            yield return WaitForStep();
            yield return null;
            yield return null;
        }

        /// <summary>One move with a gamepad d-pad button: press, wait for the board to handle it, release.</summary>
        public IEnumerator GamepadDpad(Direction direction)
        {
            yield return Press(DpadButton(direction));
        }

        /// <summary>
        /// One move with the gamepad's left stick: push it fully in the direction, wait for the board to handle it, keep
        /// it there for <paramref name="holdSeconds"/> (a held stick must still make only one move), then let go.
        /// </summary>
        public IEnumerator GamepadStick(Direction direction, float holdSeconds = 0)
        {
            yield return Hold(
                () => input.Set(Gamepad.leftStick, Vector(direction), queueEventOnly: true),
                () => input.Set(Gamepad.leftStick, Vector2.zero, queueEventOnly: true),
                holdSeconds);
        }

        /// <summary>The unit vector of a direction in screen and stick space, where up is +y.</summary>
        static Vector2 Vector(Direction d)
        {
            switch (d)
            {
                case Direction.Up: return Vector2.up;
                case Direction.Down: return Vector2.down;
                case Direction.Left: return Vector2.left;
                default: return Vector2.right;
            }
        }

        ButtonControl DpadButton(Direction d)
        {
            switch (d)
            {
                case Direction.Up: return Gamepad.dpad.up;
                case Direction.Down: return Gamepad.dpad.down;
                case Direction.Left: return Gamepad.dpad.left;
                default: return Gamepad.dpad.right;
            }
        }

        IEnumerator Press(ButtonControl button)
        {
            yield return Hold(() => input.Press(button), () => input.Release(button), 0);
        }

        /// <summary>The shared move step: apply the input, wait until the board has handled it, hold, release, settle.</summary>
        IEnumerator Hold(Action apply, Action release, float holdSeconds)
        {
            apply();
            yield return WaitForStep();
            if (holdSeconds > 0)
                yield return new WaitForSeconds(holdSeconds);
            release();
            yield return null;
            yield return null;
        }

        /// <summary>Waits two frames: the input update that raises the move, then BoardStepSystem, ClearMoveDirectionSystem and EndFrameSystem.</summary>
        IEnumerator WaitForStep()
        {
            yield return null;
            yield return null;
            yield return WaitUntil(() => PlayModeTestSupport.Count<MoveDirectionComponent>() == 0, "the move command to be consumed");
            yield return null;
        }

        // ---------------------------------------------------------------- readouts

        /// <summary>The board in test-plan notation, top row first (see <see cref="BoardNotation"/>).</summary>
        public string Board()
        {
            var grid = PlayModeTestSupport.GetSingleton<GridConfigComponent>();
            var cells = new NotationCell[grid.Width, grid.Height];
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    cells[x, y] = NotationCell.Empty;
            foreach (var b in PlayModeTestSupport.Blocks())
            {
                var p = b.GridPosition;
                if (p.x < 0 || p.y < 0 || p.x >= grid.Width || p.y >= grid.Height)
                    throw new InvalidOperationException($"A block is outside the {grid.Width}x{grid.Height} grid at {p}.");
                if (!cells[p.x, p.y].IsEmpty)
                    throw new InvalidOperationException($"Two blocks share cell ({p.x},{p.y}).");
                cells[p.x, p.y] = b.IsNextColorIndicator ? NotationCell.Indicator : NotationCell.Of(b.Color, b.Size);
            }
            return BoardNotation.Format(cells);
        }

        /// <summary>
        /// Fails the test when the board differs from <paramref name="expected"/> (test-plan notation, "?" allowed),
        /// naming the first differing cell and both boards.
        /// </summary>
        public void AssertBoard(string expected, string message = null)
        {
            string difference = BoardNotation.Compare(expected, Board());
            if (difference != null)
                Assert.Fail((string.IsNullOrEmpty(message) ? "" : message + ": ") + "Board differs: " + difference);
        }

        /// <summary>The move counter label, for example "Moves: 5".</summary>
        public string MovesText => Text("move-count").Trim();

        /// <summary>The score label, for example "Score: 30".</summary>
        public string ScoreText => Text("score-label").Trim();

        /// <summary>The status label (the game's one-line message under the header).</summary>
        public string StatusText => Text("status-label").Trim();

        /// <summary>True while the game over panel is displayed.</summary>
        public bool IsGameOverPanelShown => IsShown("game-over-panel");

        public int MoveCount => PlayModeTestSupport.GetSingleton<GameStateComponent>().MoveCount;
        public int Score => PlayModeTestSupport.GetSingleton<ScoreComponent>().TotalScore;
        public bool IsGameOver => PlayModeTestSupport.GetSingleton<GameStateComponent>().GameOver;
        public bool WaitingForInput => PlayModeTestSupport.GetSingleton<GameStateComponent>().WaitingForInput;
        public GameMode Mode => PlayModeTestSupport.GetSingleton<GameModeComponent>().Mode;
        public int GridWidth => PlayModeTestSupport.GetSingleton<GridConfigComponent>().Width;
        public int GridHeight => PlayModeTestSupport.GetSingleton<GridConfigComponent>().Height;

        /// <summary>The path of the last screenshot taken.</summary>
        public string LastScreenshotPath { get; private set; }

        /// <summary>
        /// Saves a PNG of the game as shown, named <paramref name="name"/>.png in <see cref="ReportFolder"/>. In a player it
        /// is the screen at the end of the frame. In the editor (a batchmode editor never reaches the end of a frame)
        /// it is the main camera rendered at <see cref="EditorPanelSize"/> with the UI's render texture drawn over it.
        /// First waits <paramref name="settleSeconds"/> of game time, so block grow-in and wobble animations finish
        /// (batchmode frames are so short that a few frames show blocks still at size zero).
        /// </summary>
        public IEnumerator Screenshot(string name, float settleSeconds = 0.7f)
        {
            if (settleSeconds > 0)
                yield return new WaitForSeconds(settleSeconds);
            Directory.CreateDirectory(ReportFolder);
            string path = Path.Combine(ReportFolder, name + ".png");
            Texture2D image;
            if (Application.isEditor)
            {
                yield return null; // let the UI draw this frame's state into its render texture
                image = EditorComposite();
            }
            else
            {
                yield return new WaitForEndOfFrame();
                image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                image.Apply();
            }
            try
            {
                PngWriter.Write(path, image.GetPixels32(), image.width, image.height);
                LastScreenshotPath = path;
            }
            finally
            {
                Object.Destroy(image);
            }
        }

        Texture2D EditorComposite()
        {
            int width = EditorPanelSize.x, height = EditorPanelSize.y;
            var cameraTarget = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var camera = Camera.main;
            Color32[] board = null;
            if (camera != null)
            {
                var savedTarget = camera.targetTexture;
                var savedSize = camera.orthographicSize;
                var savedPosition = camera.transform.position;
                camera.targetTexture = cameraTarget;
                FrameBoardForEditorShot(camera, width, height);
                camera.Render();
                camera.targetTexture = savedTarget;
                camera.orthographicSize = savedSize;
                camera.transform.position = savedPosition;
                board = Read(cameraTarget, width, height);
            }
            RenderTexture.ReleaseTemporary(cameraTarget);

            var ui = PanelTarget != null ? Read(PanelTarget, width, height) : null;
            var pixels = board ?? new Color32[width * height];
            if (ui != null)
                for (int i = 0; i < pixels.Length; i++)
                {
                    var over = ui[i];
                    if (over.a == 0) continue;
                    var under = pixels[i];
                    float a = over.a / 255f;
                    // UI Toolkit writes premultiplied alpha.
                    pixels[i] = new Color32((byte)Mathf.Min(255, over.r + under.r * (1 - a)), (byte)Mathf.Min(255, over.g + under.g * (1 - a)),
                        (byte)Mathf.Min(255, over.b + under.b * (1 - a)), 255);
                }
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.SetPixels32(pixels);
            image.Apply();
            return image;
        }

        static Color32[] Read(RenderTexture source, int width, int height)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = source;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            var pixels = texture.GetPixels32();
            Object.Destroy(texture);
            return pixels;
        }

        /// <summary>
        /// GameRenderer frames the board from Screen's size, which in a batchmode editor is 640x480, not the phone-sized
        /// UI. For an editor screenshot the camera is framed the same way for <see cref="EditorPanelSize"/>: the board
        /// centred between the header and the footer with the UI manager's margin (editor only, restored after).
        /// </summary>
        void FrameBoardForEditorShot(Camera camera, int screenWidth, int screenHeight)
        {
            var renderer = PresentationRegistry.ActiveGameRenderer;
            var uiManager = PresentationRegistry.ActiveGameUIManager;
            if (renderer == null || uiManager == null || document == null || !PlayModeTestSupport.HasGameState())
                return;
            var header = Root.Q<VisualElement>("header");
            var footer = Root.Q<VisualElement>("footer");
            float headerHeight = header != null ? header.worldBound.yMax : 0;
            float footerHeight = footer != null ? screenHeight - footer.worldBound.yMin : 0;
            var margin = uiManager.GridMarginInPixels;
            float step = renderer.cellSize + renderer.cellSpacing;
            float gridWidth = (GridWidth - 1) * step + renderer.cellSize;
            float gridHeight = (GridHeight - 1) * step + renderer.cellSize;
            float freeWidth = screenWidth - margin.x - margin.z;
            float freeHeight = screenHeight - headerHeight - footerHeight - margin.y - margin.w;
            camera.orthographicSize = gridWidth / gridHeight < freeWidth / freeHeight
                ? gridHeight * screenHeight / (2 * freeHeight)
                : gridWidth * screenHeight / (2 * freeWidth);
            float worldPerPixel = camera.orthographicSize * 2 / screenHeight;
            float shift = ((footerHeight + margin.w) - (headerHeight + margin.y)) / 2f * worldPerPixel;
            camera.transform.position = new Vector3((GridWidth - 1) * step / 2f, (GridHeight - 1) * step / 2f - shift, -10);
        }

        /// <summary>FMOD event paths raised since the last call (recorded before AudioSystem plays them).</summary>
        public List<string> AudioEvents() => AudioEventRecorderSystem.Take();

        // ---------------------------------------------------------------- waiting

        /// <summary>Waits until the condition holds, or throws after <paramref name="timeoutFrames"/> frames naming what it waited for.</summary>
        public static IEnumerator WaitUntil(Func<bool> condition, string what, int timeoutFrames = TimeoutFrames)
        {
            for (int i = 0; i < timeoutFrames; i++)
            {
                if (condition())
                    yield break;
                yield return null;
            }
            throw new TimeoutException($"Timed out after {timeoutFrames} frames waiting for {what}.");
        }

        /// <summary>Waits the given number of frames.</summary>
        public static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }
    }
}
