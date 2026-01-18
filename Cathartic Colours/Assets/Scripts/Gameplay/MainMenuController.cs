using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using Data;
using ECS.Components;

namespace CatharticColours.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GameConfiguration defaultConfiguration;
        [SerializeField] private GameConfiguration[] presetConfigurations;
        [SerializeField] private string gameSceneName = "GameScene";
        
        [Header("UI")]
        [SerializeField] private UIDocument uiDocument;

        // UI Elements
        private DropdownField presetDropdown;
        private DropdownField gameModeDropdown;
        private Label gameModeDescription;
        private SliderInt widthSlider;
        private SliderInt heightSlider;
        private Label widthValue;
        private Label heightValue;
        private Button startButton;

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            // Initialize the static configuration manager with a runtime copy
            if (!GameConfigurationManager.IsInitialized)
            {
                GameConfigurationManager.Initialize(defaultConfiguration);
            }

            SetupUI();
        }

        private void SetupUI()
        {
            var root = uiDocument.rootVisualElement;

            // Query UI elements
            presetDropdown = root.Q<DropdownField>("preset-dropdown");
            gameModeDropdown = root.Q<DropdownField>("gamemode-dropdown");
            gameModeDescription = root.Q<Label>("gamemode-description");
            widthSlider = root.Q<SliderInt>("width-slider");
            heightSlider = root.Q<SliderInt>("height-slider");
            widthValue = root.Q<Label>("width-value");
            heightValue = root.Q<Label>("height-value");
            startButton = root.Q<Button>("start-button");

            // Setup preset dropdown
            SetupPresetDropdown();

            // Setup game mode dropdown
            SetupGameModeDropdown();

            // Setup sliders
            SetupSliders();

            // Setup button
            if (startButton != null)
            {
                startButton.clicked += StartGame;
            }

            // Load current configuration values
            LoadActiveConfiguration();
        }

        private void SetupPresetDropdown()
        {
            if (presetDropdown == null || presetConfigurations == null) return;

            List<string> presetNames = new List<string> { "Custom" };
            presetNames.AddRange(presetConfigurations.Select(p => p.name));

            presetDropdown.choices = presetNames;
            presetDropdown.value = presetNames[0];

            presetDropdown.RegisterValueChangedCallback(evt =>
            {
                int index = presetDropdown.index - 1;
                if (index >= 0 && index < presetConfigurations.Length)
                {
                    LoadPreset(presetConfigurations[index]);
                }
            });
        }

        private void SetupGameModeDropdown()
        {
            if (gameModeDropdown == null) return;

            gameModeDropdown.choices = new List<string>
            {
                "Standard",
                "Color Merge",
                "Advanced Color Merge"
            };

            gameModeDropdown.RegisterValueChangedCallback(evt =>
            {
                var config = GameConfigurationManager.ActiveConfiguration;
                if (config == null) return;

                config.gameMode = evt.newValue switch
                {
                    "Color Merge" => GameMode.ColorMerge,
                    "Advanced Color Merge" => GameMode.AdvancedColorMerge,
                    _ => GameMode.Standard
                };

                UpdateGameModeDescription(config.gameMode);
            });
        }

        private void SetupSliders()
        {
            if (widthSlider != null)
            {
                widthSlider.RegisterValueChangedCallback(evt =>
                {
                    var config = GameConfigurationManager.ActiveConfiguration;
                    if (config == null) return;

                    widthValue.text = evt.newValue.ToString();
                    config.gridWidth = evt.newValue;
                });
            }

            if (heightSlider != null)
            {
                heightSlider.RegisterValueChangedCallback(evt =>
                {
                    var config = GameConfigurationManager.ActiveConfiguration;
                    if (config == null) return;

                    heightValue.text = evt.newValue.ToString();
                    config.gridHeight = evt.newValue;
                });
            }
        }

        private void LoadActiveConfiguration()
        {
            var config = GameConfigurationManager.ActiveConfiguration;
            if (config == null) return;

            // Set game mode
            gameModeDropdown.value = config.gameMode switch
            {
                GameMode.ColorMerge => "Color Merge",
                GameMode.AdvancedColorMerge => "Advanced Color Merge",
                _ => "Standard"
            };

            UpdateGameModeDescription(config.gameMode);

            // Set sliders
            widthSlider.value = config.gridWidth;
            heightSlider.value = config.gridHeight;
            widthValue.text = config.gridWidth.ToString();
            heightValue.text = config.gridHeight.ToString();
        }

        private void LoadPreset(GameConfiguration preset)
        {
            if (preset == null) return;
            
            var config = GameConfigurationManager.ActiveConfiguration;
            if (config == null) return;

            // Copy preset values to active configuration (runtime instance)
            config.gameMode = preset.gameMode;
            config.gridWidth = preset.gridWidth;
            config.gridHeight = preset.gridHeight;
            config.spawnNextColourSystem = preset.spawnNextColourSystem;

            // Update UI to reflect preset values
            LoadActiveConfiguration();

            Debug.Log($"Loaded preset: {preset.name}");
        }

        private void UpdateGameModeDescription(GameMode mode)
        {
            if (gameModeDescription == null) return;

            gameModeDescription.text = mode switch
            {
                GameMode.Standard => 
                    "Match color and size to merge.\nSmall → Medium → Large → Clear",
                
                GameMode.ColorMerge => 
                    "Combine primary colors to make secondary.\nSecondary + Primary = White (clears)",
                
                GameMode.AdvancedColorMerge => 
                    "Merge by size and color.\nBuild up to White blocks, then clear",
                
                _ => "Standard merge rules"
            };
        }

        private void StartGame()
        {
            SceneManager.LoadScene(gameSceneName);
        }

        private void OnDestroy()
        {
            if (startButton != null)
            {
                startButton.clicked -= StartGame;
            }
        }
    }
}
