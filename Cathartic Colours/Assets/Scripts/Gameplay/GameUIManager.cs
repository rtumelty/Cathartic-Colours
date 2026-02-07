using System;
using UnityEngine;
using UnityEngine.UIElements;
using ECS.Components;
using Unity.Entities;

namespace Gameplay
{
    public class GameUIManager : MonoBehaviour
    {
        [Header("UI Configuration")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private Vector4 gridMarginInPixels = new(20, 20, 20, 20);

        // UI Elements
        private Label moveCountLabel;
        private Label scoreLabel;
        private Label statusLabel;
        private Label instructionLabel;
        private VisualElement gameOverPanel;
        private VisualElement levelCompletePanel;
        private Button restartButton;
        private Button nextLevelButton;
        private Button footerMainMenuButton;
        
        private LayoutObserver layoutObserver;
        private bool isSetup = false;
        
        // Events for GameRenderer to subscribe to
        public event Action OnRestartRequested;
        public event Action OnNextLevelRequested;
        public event Action<float, float> OnLayoutRecalculated;

        private void Awake()
        {
            GameManager.ActiveGameUIManager = this;
        }

        private void OnDestroy()
        {
            GameManager.ActiveGameUIManager = null;
            UnhookEvents();
            
            if (layoutObserver != null)
            {
                layoutObserver.OnLayoutRecalculated -= HandleLayoutRecalculated;
            }
        }

        private void OnEnable()
        {
            if (layoutObserver != null && isSetup)
            {
                layoutObserver.OnLayoutRecalculated += HandleLayoutRecalculated;
            }
        }

        private void OnDisable()
        {
            if (layoutObserver != null)
            {
                layoutObserver.OnLayoutRecalculated -= HandleLayoutRecalculated;
            }
        }

        private void HandleLayoutRecalculated(float headerHeight, float footerHeight)
        {
            OnLayoutRecalculated?.Invoke(headerHeight, footerHeight);
        }

        public void SetupUI()
        {
            if (isSetup)
            {
                Debug.LogWarning("UI already setup, skipping.");
                return;
            }

            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument == null)
            {
                Debug.LogError("UIDocument not found! Please assign it in the inspector.");
                return;
            }

            // Set visual tree from color profile
            uiDocument.visualTreeAsset = GameManager.ActiveColorProfile.UITreeAsset;
            var root = uiDocument.rootVisualElement;

            // Create layout observer after setting visual tree
            layoutObserver = new LayoutObserver(uiDocument);
            
            // Cache UI elements
            CacheUIElements(root);
            
            // Hook up events
            HookUpEvents();
            
            // Initialize UI state
            InitializeUIState();

            isSetup = true;

            // Subscribe to layout events now that everything is set up
            if (enabled) // Only if the component is currently enabled
            {
                layoutObserver.OnLayoutRecalculated += HandleLayoutRecalculated;
            }
        }

        // Rest of your existing methods remain the same...
        private void CacheUIElements(VisualElement root)
        {
            moveCountLabel = root.Q<Label>("move-count");
            scoreLabel = root.Q<Label>("score-label");
            statusLabel = root.Q<Label>("status-label");
            instructionLabel = root.Query<Label>(className: "instruction-text").First();
            gameOverPanel = root.Q<VisualElement>("game-over-panel");
            levelCompletePanel = root.Q<VisualElement>("level-complete-panel");
            restartButton = root.Q<Button>("restart-button");
            nextLevelButton = levelCompletePanel?.Q<Button>("next-level-button");
            footerMainMenuButton = root.Q<Button>("footer-main-menu-button");
        }

        private void HookUpEvents()
        {
            if (restartButton != null)
            {
                restartButton.clicked += () => OnRestartRequested?.Invoke();
            }

            var gameOverRestartButton = gameOverPanel?.Q<Button>("restart-button");
            if (gameOverRestartButton != null)
            {
                gameOverRestartButton.clicked += () => OnRestartRequested?.Invoke();
            }

            if (nextLevelButton != null)
            {
                nextLevelButton.clicked += () => OnNextLevelRequested?.Invoke();
            }

            var gameOverMainMenuButton = gameOverPanel?.Q<Button>("main-menu-button");
            if (gameOverMainMenuButton != null)
            {
                gameOverMainMenuButton.clicked += ReturnToMainMenu;
            }

            var levelCompleteMainMenuButton = levelCompletePanel?.Q<Button>("main-menu-button");
            if (levelCompleteMainMenuButton != null)
            {
                levelCompleteMainMenuButton.clicked += ReturnToMainMenu;
            }

            if (footerMainMenuButton != null)
            {
                footerMainMenuButton.clicked += ReturnToMainMenu;
            }
        }

        private void UnhookEvents()
        {
            if (restartButton != null)
            {
                restartButton.clicked -= () => OnRestartRequested?.Invoke();
            }

            var gameOverRestartButton = gameOverPanel?.Q<Button>("restart-button");
            if (gameOverRestartButton != null)
            {
                gameOverRestartButton.clicked -= () => OnRestartRequested?.Invoke();
            }

            if (nextLevelButton != null)
            {
                nextLevelButton.clicked -= () => OnNextLevelRequested?.Invoke();
            }

            var gameOverMainMenuButton = gameOverPanel?.Q<Button>("main-menu-button");
            if (gameOverMainMenuButton != null)
            {
                gameOverMainMenuButton.clicked -= ReturnToMainMenu;
            }

            var levelCompleteMainMenuButton = levelCompletePanel?.Q<Button>("main-menu-button");
            if (levelCompleteMainMenuButton != null)
            {
                levelCompleteMainMenuButton.clicked -= ReturnToMainMenu;
            }

            if (footerMainMenuButton != null)
            {
                footerMainMenuButton.clicked -= ReturnToMainMenu;
            }
        }

        private void InitializeUIState()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.style.display = DisplayStyle.None;
            }

            if (levelCompletePanel != null)
            {
                levelCompletePanel.style.display = DisplayStyle.None;
            }
        }

        public void UpdateInstructionText(GameMode mode)
        {
            if (instructionLabel == null) return;

            string instructionText = mode switch
            {
                GameMode.Standard =>
                    "Merge matching tiles\n" +
                    "Small → Medium → Large → Clear",

                GameMode.ColorMerge =>
                    "Combine primary colors\n" +
                    "(R+G=Yellow, R+B=Magenta, G+B=Cyan) \n" +
                    "Secondary + missing primary = White (clears)",

                GameMode.AdvancedColorMerge =>
                    "Small same color → Medium\n" +
                    "Medium primary colors → Large secondary \n" +
                    "Large secondary + missing medium primary -> White\n" +
                    "White + White → Clear",

                _ => "Merge matching tiles"
            };

            instructionLabel.text = instructionText;
        }

        public void UpdateGameState(GameStateComponent gameState, ScoreComponent score)
        {
            if (moveCountLabel != null)
            {
                moveCountLabel.text = $"Moves: {gameState.MoveCount}";
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = $"Score: {score.TotalScore}";
            }

            if (statusLabel != null)
            {
                if (gameState.GameOver)
                {
                    statusLabel.text = "Grid Full!";
                }
                else if (gameState.LevelComplete)
                {
                    statusLabel.text = "Level Complete!";
                }
                else if (gameState.WaitingForInput)
                {
                    statusLabel.text = "Your turn...";
                }
                else
                {
                    statusLabel.text = "Processing...";
                }
            }

            if (gameState.GameOver && gameOverPanel != null)
            {
                gameOverPanel.style.display = DisplayStyle.Flex;
            }
            else if (gameOverPanel != null)
            {
                gameOverPanel.style.display = DisplayStyle.None;
            }

            if (gameState.LevelComplete && levelCompletePanel != null)
            {
                levelCompletePanel.style.display = DisplayStyle.Flex;
            }
            else if (levelCompletePanel != null)
            {
                levelCompletePanel.style.display = DisplayStyle.None;
            }
        }

        public Vector4 GridMarginInPixels => gridMarginInPixels;

        private void ReturnToMainMenu()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}