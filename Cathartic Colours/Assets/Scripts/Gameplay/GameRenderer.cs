using System.Collections.Generic;
using Data;
using ECS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Gameplay
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
        
        [Header("Configuration")]
        [Tooltip("Fallback configurations - used if GameConfigurationManager is not initialized")]
        [SerializeField] private GameConfiguration fallbackConfiguration;
        [SerializeField] private ColorProfile fallbackColorProfile;

        [Header("Prefabs")]
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private GameObject blockPrefab;

        [Header("Grid Settings")]
        [SerializeField] public float cellSize = 1f;
        [SerializeField] public float cellSpacing = 0.1f;

        private Dictionary<Entity, BlockVisual> blockVisuals = new Dictionary<Entity, BlockVisual>();
        private int gridHeight;
        private int gridWidth;
        private GameObject[,] gridCells;
        private EntityManager entityManager;
        
        private Entity gameStateEntity;
        private Entity scoreEntity;
        
        private Camera mainCamera;

        private void Awake()
        {
            GameManager.ActiveGameRenderer = this;
            mainCamera = Camera.main;

            LoadConfigs();
        }

        private void Start()
        {
            SetupUI(); 
            InitializeGame();
        }

        private void OnEnable()
        {
            StartCoroutine(SubscribeToUIEventsWhenReady());
        }
        
        private System.Collections.IEnumerator SubscribeToUIEventsWhenReady()
        {
            // Wait until UI manager is available and set up
            while (GameManager.ActiveGameUIManager == null)
            {
                yield return null;
            }

            // Subscribe to UI manager events
            GameManager.ActiveGameUIManager.OnRestartRequested += RestartGame;
            GameManager.ActiveGameUIManager.OnNextLevelRequested += OnNextLevel;
            GameManager.ActiveGameUIManager.OnLayoutRecalculated += UpdateCamera;

            Debug.Log("GameRenderer subscribed to UI events");
        }

        private void OnDisable()
        {
            // Unsubscribe from UI manager events
            if (GameManager.ActiveGameUIManager != null)
            {
                GameManager.ActiveGameUIManager.OnRestartRequested -= RestartGame;
                GameManager.ActiveGameUIManager.OnNextLevelRequested -= OnNextLevel;
                GameManager.ActiveGameUIManager.OnLayoutRecalculated -= UpdateCamera;
            }
        }

        private void OnDestroy()
        {
            GameManager.ActiveGameRenderer = null;
            CleanUp();
        }

        void LoadConfigs()
        {
            // Get configuration from static manager
            GameConfiguration config = GameManager.ActiveConfiguration;
            
            // Fallback to serialized config if manager isn't initialized
            if (config == null && fallbackConfiguration != null)
            {
                Debug.LogWarning("GameConfigurationManager not initialized, using fallback configuration");
                GameManager.Initialize(fallbackConfiguration, fallbackColorProfile);
                config = GameManager.ActiveConfiguration;
            }
        }

        private void SetupUI()
        {
            // Set camera background color
            mainCamera.backgroundColor = GameManager.ActiveColorProfile.BackgroundColor;
            
            // Delegate UI setup to UI manager
            if (GameManager.ActiveGameUIManager != null)
            {
                GameManager.ActiveGameUIManager.SetupUI();
            }
            else
            {
                Debug.LogError("GameUIManager not found! Make sure it's attached to the scene.");
            }
        }

        private void InitializeGame()
        {
            var config = GameManager.ActiveConfiguration;
            if (config == null)
            {
                Debug.LogError("No configuration available!");
                return;
            }
            
            var world = World.DefaultGameObjectInjectionWorld;
            entityManager = world.EntityManager;

            gridWidth = config.gridWidth;
            gridHeight = config.gridHeight;

            // Create grid config
            var gridConfigEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(gridConfigEntity, new GridConfigComponent
            {
                Width = gridWidth,
                Height = gridHeight
            });

            // Create game state
            gameStateEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(gameStateEntity, new GameStateComponent
            {
                WaitingForInput = true,
                GameOver = false,
                LevelComplete = false,
                MoveCount = 0,
                Random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks)
            });

            // Create score entity and cache reference
            scoreEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(scoreEntity, new ScoreComponent
            {
                TotalScore = 0,
                CurrentFrameScore = 0
            });

            var gameModeEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(gameModeEntity, new ECS.Components.GameModeComponent
            {
                Mode = config.gameMode
            });

            // Set game mode tags
            switch (config.gameMode)
            {
                case ECS.Components.GameMode.Standard:
                    entityManager.AddComponent<StandardMergeSystemTag>(gameStateEntity);
                    break;
                case ECS.Components.GameMode.ColorMerge:
                    entityManager.AddComponent<ColorMergeSystemTag>(gameStateEntity);
                    break;
                case ECS.Components.GameMode.AdvancedColorMerge:
                    entityManager.AddComponent<AdvancedColorMergeSystemTag>(gameStateEntity);
                    break;
            }

            // Enable colour spawning
            if (config.spawnNextColourSystem)
            {
                entityManager.AddComponent<SpawnColorSystemTag>(gameStateEntity);
            }

            // Update instruction text via UI manager
            if (GameManager.ActiveGameUIManager != null)
            {
                GameManager.ActiveGameUIManager.UpdateInstructionText(config.gameMode);
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
                        renderer.color = GameManager.ActiveColorProfile.GridBackgroundColor;
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
            if (GameManager.ActiveColorProfile != null)
            {
                visual.Renderer.color = GameManager.ActiveColorProfile.GetColor(blockComponent.Color);
            }
            else
            {
                // Fallback colors if configuration is missing
                visual.Renderer.color = blockComponent.Color switch
                {
                    BlockColor.Red => Color.red,
                    BlockColor.Green => Color.green,
                    BlockColor.Blue => Color.blue,
                    BlockColor.Yellow => Color.yellow,
                    BlockColor.Cyan => Color.cyan,
                    BlockColor.Magenta => Color.magenta,
                    BlockColor.White => Color.white,
                    _ => Color.white
                };
            }

            visual.Block.Size = (int)blockComponent.Size;
        }

        private void UpdateUI()
        {
            // Check if entities are valid before accessing them
            if (gameStateEntity == Entity.Null || !entityManager.Exists(gameStateEntity) ||
                scoreEntity == Entity.Null || !entityManager.Exists(scoreEntity))
                return;

            var gameState = entityManager.GetComponentData<GameStateComponent>(gameStateEntity);
            var score = entityManager.GetComponentData<ScoreComponent>(scoreEntity);

            // Delegate UI updates to UI manager
            if (GameManager.ActiveGameUIManager != null)
            {
                GameManager.ActiveGameUIManager.UpdateGameState(gameState, score);
            }
        }

        private void CleanUp()
        {
            // Clean up existing entities
            var query = entityManager.CreateEntityQuery(typeof(BlockComponent));
            entityManager.DestroyEntity(query);

            // Clean up cached entities if they exist
            if (gameStateEntity != Entity.Null && entityManager.Exists(gameStateEntity))
            {
                entityManager.DestroyEntity(gameStateEntity);
                gameStateEntity = Entity.Null;
            }

            if (scoreEntity != Entity.Null && entityManager.Exists(scoreEntity))
            {
                entityManager.DestroyEntity(scoreEntity);
                scoreEntity = Entity.Null;
            }

            query = entityManager.CreateEntityQuery(typeof(GridConfigComponent));
            entityManager.DestroyEntity(query);

            query = entityManager.CreateEntityQuery(typeof(ECS.Components.GameModeComponent));
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
        }
        
        public void RestartGame()
        {
            CleanUp();
            InitializeGame();
        }

        private void OnNextLevel()
        {
            // TODO: Implement level progression logic
            RestartGame();
        }

        private void UpdateCamera(float headerHeight, float footerHeight)
        {
            if (GameManager.ActiveGameUIManager == null) return;
            
            var gridMarginInPixels = GameManager.ActiveGameUIManager.GridMarginInPixels;
            
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
    
            float spaceBelow = footerHeight + gridMarginInPixels.w;
            float spaceAbove = headerHeight + gridMarginInPixels.y;
            float verticalShiftInPixels = (spaceBelow - spaceAbove) / 2f;
            float verticalShiftInWorldUnits = verticalShiftInPixels * worldUnitsPerPixel;
            
            Vector3 gridCenter = new Vector3(
                (gridWidth - 1) * (cellSize + cellSpacing) / 2f,
                (gridHeight - 1) * (cellSize + cellSpacing) / 2f,
                -10
            );
            
            mainCamera.transform.position = gridCenter + new Vector3(0, -verticalShiftInWorldUnits, 0);
        }
    }
}