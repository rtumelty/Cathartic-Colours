using System;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using ECS.Components;

namespace CatharticColours
{
    public class GameRenderer : MonoBehaviour
    {
        // Structure to cache all visual components
        private struct BlockVisual
        {
            public GameObject GameObject;
            public Block Block;
            public SpriteRenderer Renderer;

            public BlockVisual(GameObject gameObject, Block block, SpriteRenderer renderer)
            {
                GameObject = gameObject;
                Block = block;
                Renderer = renderer;
            }
        }

        [SerializeField] private GameConfiguration activeGameConfiguration;

        [Header("Prefabs")]
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private GameObject blockPrefab;

        [Header("Colors")]
        [SerializeField] private Color redColor = Color.red;
        [SerializeField] private Color greenColor = Color.green;
        [SerializeField] private Color blueColor = Color.blue;
        [SerializeField] private Color whiteColor = Color.white;
        [SerializeField] private Color yellowColor = Color.yellow;
        [SerializeField] private Color cyanColor = Color.cyan;
        [SerializeField] private Color magentaColor = Color.magenta;
        [SerializeField] private Color backgroundColor = Color.gray;

        [Header("UI and presentation")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] public float cellSize = 1f;
        [SerializeField] public float cellSpacing = 0.1f;
        [SerializeField] private Vector4 gridMarginInPixels = new(20, 20, 20, 20);

        // UI Elements
        private Label moveCountLabel;
        private Label statusLabel;
        private VisualElement gameOverPanel;
        private VisualElement levelCompletePanel;
        private Button restartButton;
        private Button nextLevelButton;

        private Dictionary<Entity, BlockVisual> blockVisuals = new Dictionary<Entity, BlockVisual>();
        private int gridHeight;
        private int gridWidth;
        private GameObject[,] gridCells;
        private EntityManager entityManager;
        
        private Camera mainCamera;
        private LayoutObserver layoutObserver;

        private void Awake()
        {
            mainCamera = Camera.main;
            layoutObserver = new LayoutObserver(uiDocument);
            
            SetupUI();
            InitializeGame();
        }

        private void OnEnable()
        {
            layoutObserver.OnLayoutRecalculated += UpdateCamera;
        }

        private void OnDisable()
        {
            layoutObserver.OnLayoutRecalculated -= UpdateCamera;
        }

        private void SetupUI()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument == null)
            {
                Debug.LogError("UIDocument not found! Please assign it in the inspector.");
                return;
            }

            var root = uiDocument.rootVisualElement;

            // Query UI elements
            moveCountLabel = root.Q<Label>("move-count");
            statusLabel = root.Q<Label>("status-label");
            gameOverPanel = root.Q<VisualElement>("game-over-panel");
            levelCompletePanel = root.Q<VisualElement>("level-complete-panel");
            
            // Query buttons and hook up events
            restartButton = root.Q<Button>("restart-button");
            if (restartButton != null)
            {
                restartButton.clicked += RestartGame;
            }

            // Additional restart button in game over panel
            var gameOverRestartButton = gameOverPanel?.Q<Button>("restart-button");
            if (gameOverRestartButton != null)
            {
                gameOverRestartButton.clicked += RestartGame;
            }

            nextLevelButton = levelCompletePanel?.Q<Button>("next-level-button");
            if (nextLevelButton != null)
            {
                nextLevelButton.clicked += OnNextLevel;
            }

            // Hide panels initially
            if (gameOverPanel != null)
            {
                gameOverPanel.style.display = DisplayStyle.None;
            }
            
            if (levelCompletePanel != null)
            {
                levelCompletePanel.style.display = DisplayStyle.None;
            }
        }

        private void InitializeGame()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            entityManager = world.EntityManager;

            gridWidth = activeGameConfiguration.gridWidth;
            gridHeight = activeGameConfiguration.gridHeight;

            // Create grid config
            var gridConfigEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(gridConfigEntity, new GridConfigComponent
            {
                Width = gridWidth,
                Height = gridHeight
            });

            // Create game state
            var gameStateEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(gameStateEntity, new GameStateComponent
            {
                WaitingForInput = true,
                GameOver = false,
                LevelComplete = false,
                MoveCount = 0,
                Random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks)
            });

            // Set game mode
            switch (activeGameConfiguration.gameMode)
            {
                case GameConfiguration.GameMode.Standard:
                    entityManager.AddComponent<StandardMergeSystemTag>(gameStateEntity);
                    break;
                case GameConfiguration.GameMode.ColorMerge:
                    entityManager.AddComponent<ColorMergeSystemTag>(gameStateEntity);
                    break;
                case GameConfiguration.GameMode.AdvancedColorMerge:
                    entityManager.AddComponent<AdvancedColorMergeSystemTag>(gameStateEntity);
                    break;
                default:
                    entityManager.AddComponent<StandardMergeSystemTag>(gameStateEntity);
                    break;
            }

            // Enable colour spawning
            if (activeGameConfiguration.spawnNextColourSystem)
            {
                entityManager.AddComponent<SpawnColorSystemTag>(gameStateEntity);
            }

            // Create visual grid
            CreateVisualGrid();

            // Spawn initial white block
            SpawnInitialBlock();

            // Reset UI
            UpdateUI();
        }

        private void CreateVisualGrid()
        {
            gridCells = new GameObject[gridWidth, gridHeight];

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    Vector3 position = new Vector3(
                        x * (cellSize + cellSpacing),
                        y * (cellSize + cellSpacing),
                        0
                    );

                    GameObject cell = Instantiate(cellPrefab, position, Quaternion.identity, transform);
                    cell.name = $"Cell_{x}_{y}";
                    
                    var renderer = cell.GetComponent<SpriteRenderer>();
                    if (renderer != null)
                    {
                        renderer.color = backgroundColor;
                    }

                    gridCells[x, y] = cell;
                }
            }
        }

        private void SpawnInitialBlock()
        {
            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, new BlockComponent
            {
                Color = BlockColor.White,
                Size = BlockSize.Small,
                GridPosition = new int2(gridWidth / 2, gridHeight / 2),
                IsNextColorIndicator = true
            });
        }

        private void Update()
        {
            UpdateVisuals();
            UpdateUI();
        }

        private void UpdateVisuals()
        {
            var query = entityManager.CreateEntityQuery(typeof(BlockComponent));
            var entities = query.ToEntityArray(Allocator.Temp);
            var blocks = query.ToComponentDataArray<BlockComponent>(Allocator.Temp);

            HashSet<Entity> existingEntities = new HashSet<Entity>();

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                BlockComponent block = blocks[i];
                existingEntities.Add(entity);

                if (!blockVisuals.TryGetValue(entity, out BlockVisual visual))
                {
                    // Create new visual and cache all components
                    GameObject gameObject = Instantiate(blockPrefab, transform);
                    Block blockComponent = gameObject.GetComponent<Block>();
                    SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
                    
                    if (blockComponent == null)
                    {
                        Debug.LogError("Block component not found on blockPrefab!");
                        Destroy(gameObject);
                        continue;
                    }

                    if (renderer == null)
                    {
                        Debug.LogError("SpriteRenderer component not found on blockPrefab!");
                        Destroy(gameObject);
                        continue;
                    }
                    
                    visual = new BlockVisual(gameObject, blockComponent, renderer);
                    blockVisuals[entity] = visual;
                }

                Vector3 position = new Vector3(
                    block.GridPosition.x * (cellSize + cellSpacing),
                    block.GridPosition.y * (cellSize + cellSpacing),
                    -1
                );
                visual.GameObject.transform.position = position;

                UpdateBlockVisual(visual, block);
            }

            List<Entity> toRemove = new List<Entity>();
            foreach (var kvp in blockVisuals)
            {
                if (!existingEntities.Contains(kvp.Key))
                {
                    kvp.Value.Block.Disappear();

                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var entity in toRemove)
            {
                blockVisuals.Remove(entity);
            }

            entities.Dispose();
            blocks.Dispose();
        }

        private void UpdateBlockVisual(BlockVisual visual, BlockComponent blockComponent)
        {
            visual.Renderer.color = blockComponent.Color switch
            {
                BlockColor.Red => redColor,
                BlockColor.Green => greenColor,
                BlockColor.Blue => blueColor,
                BlockColor.Yellow => yellowColor,
                BlockColor.Cyan => cyanColor,
                BlockColor.Magenta => magentaColor,
                BlockColor.White => whiteColor,
                _ => Color.white
            };

            visual.Block.Size = (int)blockComponent.Size;
        }

        private void UpdateUI()
        {
            var query = entityManager.CreateEntityQuery(typeof(GameStateComponent));
            if (query.TryGetSingleton<GameStateComponent>(out var gameState))
            {
                // Update move count
                if (moveCountLabel != null)
                {
                    moveCountLabel.text = $"Moves: {gameState.MoveCount}";
                }

                // Update status
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

                // Show/hide panels
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
        }

        public void RestartGame()
        {
            // Clean up existing entities
            var query = entityManager.CreateEntityQuery(typeof(BlockComponent));
            entityManager.DestroyEntity(query);

            query = entityManager.CreateEntityQuery(typeof(GameStateComponent));
            entityManager.DestroyEntity(query);

            query = entityManager.CreateEntityQuery(typeof(GridConfigComponent));
            entityManager.DestroyEntity(query);

            // Clean up visuals
            foreach (var visual in blockVisuals.Values)
            {
                if (visual.GameObject != null)
                {
                    Destroy(visual.GameObject);
                }
            }
            blockVisuals.Clear();

            // Reinitialize
            InitializeGame();
        }

        private void OnNextLevel()
        {
            // Implement level progression logic
            RestartGame();
        }

        private void OnDestroy()
        {
            // Unhook events
            if (restartButton != null)
            {
                restartButton.clicked -= RestartGame;
            }

            if (nextLevelButton != null)
            {
                nextLevelButton.clicked -= OnNextLevel;
            }
        }

        private void UpdateCamera(float headerHeight, float footerHeight)
        {
            float actualGridWidth = (gridWidth - 1) * (cellSize + cellSpacing) + cellSize;
            float actualGridHeight = (gridHeight - 1) * (cellSize + cellSpacing) + cellSize;
            
            float effectiveScreenWidth = Screen.width - gridMarginInPixels.x - gridMarginInPixels.z;
            float effectiveScreenHeight = Screen.height - headerHeight - footerHeight 
                                          - gridMarginInPixels.y - gridMarginInPixels.w;
            
            float effectiveAspectRatio = effectiveScreenWidth / effectiveScreenHeight;
            float gridAspectRatio = actualGridWidth / actualGridHeight;
            bool verticallyConstrained = gridAspectRatio < effectiveAspectRatio;
            
            if (verticallyConstrained)
            {
                mainCamera.orthographicSize = (actualGridHeight * Screen.height) / (2 * effectiveScreenHeight);
            }
            else
            {
                mainCamera.orthographicSize = (actualGridWidth * Screen.height) / (2 * effectiveScreenWidth);
            }

            float worldUnitsPerPixel = (mainCamera.orthographicSize * 2) / Screen.height;
    
            // Calculate vertical offset to center grid in available space
            // The offset is the difference between space below and above the screen center
            float spaceBelow = footerHeight + gridMarginInPixels.w;
            float spaceAbove = headerHeight + gridMarginInPixels.y;
            float verticalShiftInPixels = (spaceBelow - spaceAbove) / 2f;
            float verticalShiftInWorldUnits = verticalShiftInPixels * worldUnitsPerPixel;
            
            // Center camera
            Vector3 gridCenter = new Vector3(
                (gridWidth - 1) * (cellSize + cellSpacing) / 2f,
                (gridHeight - 1) * (cellSize + cellSpacing) / 2f,
                -10
            );
            
            mainCamera.transform.position = gridCenter + new Vector3(0, -verticalShiftInWorldUnits, 0);
            
        }
    }
}