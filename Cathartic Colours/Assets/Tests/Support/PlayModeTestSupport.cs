// Shared setup for PlayMode tests (CC-002.4, moved into the test-support assembly by CC-046.1): the temp save folder
// (so the player's real save is never read or written), resetting GameManager to a first launch, scene unloading and
// ECS query helpers. Runtime APIs only, no UnityEditor calls, so the same code can run in an Android player (CC-047).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ECS.Components;
using SaveSystem;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TestSupport
{
    public static class PlayModeTestSupport
    {
        public const string MainMenuScene = "MainMenu";
        public const string GameplayScene = "Gameplay scene";
        public const int TimeoutFrames = 600;

        /// <summary>Points SaveGameManager at a fresh temp folder with no save in it and clears the resume flag.</summary>
        public static string RedirectSaves()
        {
            string folder = Path.Combine(Application.temporaryCachePath, "cc-playmode-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            SaveGameManager.SaveFilePathOverride = Path.Combine(folder, "savegame.json");
            PlayerPrefs.DeleteKey("LoadFromSave"); // a stale resume flag would make GameRenderer look for a save
            return folder;
        }

        /// <summary>The save file path while saves are redirected.</summary>
        public static string SaveFilePath => SaveGameManager.SaveFilePath;

        public static void RestoreSaves(string folder)
        {
            SaveGameManager.SaveFilePathOverride = null;
            PlayerPrefs.DeleteKey("LoadFromSave");
            if (folder != null && Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        /// <summary>
        /// Destroys the runtime configuration and colour profile copies GameManager holds, so GameManager reads as
        /// uninitialised and the main menu initialises it from its own default configuration, as on a first launch.
        /// </summary>
        public static void DestroyRuntimeConfigs()
        {
            if (!GameManager.IsInitialized)
                return;
            var menu = GameManager.MenuConfiguration;
            var active = GameManager.ActiveConfiguration;
            var profile = GameManager.ActiveColorProfile;
            if (active != null && active != menu) Object.DestroyImmediate(active);
            if (menu != null) Object.DestroyImmediate(menu);
            if (profile != null) Object.DestroyImmediate(profile);
        }

        /// <summary>Makes an empty scene the active one and unloads the menu and gameplay scenes (their OnDestroy cleans up the ECS entities).</summary>
        public static IEnumerator UnloadGameScenes()
        {
            var empty = SceneManager.CreateScene("cc-empty-" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            foreach (var name in new[] { MainMenuScene, GameplayScene })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
            yield return null;
        }

        public static EntityManager EntityManager
        {
            get
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                    throw new InvalidOperationException("The default ECS world does not exist.");
                return world.EntityManager;
            }
        }

        public static bool HasGameState()
        {
            return HasSingleton<GameStateComponent>() && HasSingleton<GridConfigComponent>() &&
                   HasSingleton<ScoreComponent>() && HasSingleton<GameModeComponent>();
        }

        /// <summary>Waits until the game start singletons (GameRenderer.InitializeGame) exist, or fails after a timeout.</summary>
        public static IEnumerator WaitForGameState()
        {
            for (int i = 0; i < TimeoutFrames; i++)
            {
                if (HasGameState())
                    yield break;
                yield return null;
            }
            throw new TimeoutException("The ECS game state was not created within " + TimeoutFrames + " frames.");
        }

        public static bool HasSingleton<T>() where T : unmanaged, IComponentData
        {
            return Count<T>() == 1;
        }

        public static T GetSingleton<T>() where T : unmanaged, IComponentData
        {
            var em = EntityManager;
            using var query = em.CreateEntityQuery(typeof(T));
            return query.GetSingleton<T>();
        }

        public static int Count<T>()
        {
            var em = EntityManager;
            using var query = em.CreateEntityQuery(typeof(T));
            return query.CalculateEntityCount();
        }

        /// <summary>The block entities and their grid positions, indicators included.</summary>
        public static Dictionary<Entity, int2> BlockPositions()
        {
            var em = EntityManager;
            using var query = em.CreateEntityQuery(typeof(BlockComponent));
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var blocks = query.ToComponentDataArray<BlockComponent>(Allocator.Temp);
            var result = new Dictionary<Entity, int2>();
            for (int i = 0; i < entities.Length; i++)
                result[entities[i]] = blocks[i].GridPosition;
            return result;
        }

        /// <summary>The block components on the board, indicators included.</summary>
        public static BlockComponent[] Blocks()
        {
            var em = EntityManager;
            using var query = em.CreateEntityQuery(typeof(BlockComponent));
            using var blocks = query.ToComponentDataArray<BlockComponent>(Allocator.Temp);
            return blocks.ToArray();
        }
    }
}
