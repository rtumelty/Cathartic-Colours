// Shared setup for the CC-002.4 PlayMode smoke tests: the temp save folder (so the player's real save is never read or
// written and the game always starts fresh), a known GameManager configuration, and ECS query helpers.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Data;
using ECS.Components;
using SaveSystem;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PlayModeTests
{
    internal static class PlayModeTestSupport
    {
        public const string MainMenuScene = "MainMenu";
        public const string GameplayScene = "Gameplay scene";
        const int TimeoutFrames = 600;

        /// <summary>Points SaveGameManager at a fresh temp folder with no save in it and clears the resume flag.</summary>
        public static string RedirectSaves()
        {
            string folder = Path.Combine(Path.GetTempPath(), "cc-playmode-tests", Guid.NewGuid().ToString("N"));
            SaveGameManager.SaveFilePathOverride = Path.Combine(folder, "savegame.json");
            PlayerPrefs.DeleteKey("LoadFromSave"); // a stale resume flag would make GameRenderer look for a save
            return folder;
        }

        public static void RestoreSaves(string folder)
        {
            SaveGameManager.SaveFilePathOverride = null;
            if (folder != null && Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        /// <summary>Initialises GameManager with runtime copies of the Default configuration and colour profile assets.</summary>
        public static GameConfiguration InitializeGameManager()
        {
            var config = LoadAsset<GameConfiguration>("Assets/Data/Game Configs/Default.asset");
            var profile = LoadAsset<ColorProfile>("Assets/Data/Color Profiles/DefaultColorProfile.asset");
            GameManager.Initialize(config, profile);
            return GameManager.ActiveConfiguration;
        }

        static T LoadAsset<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException($"Test asset not found: {path}");
            return asset;
#else
            throw new NotSupportedException("The PlayMode smoke tests load their configuration assets in the editor only.");
#endif
        }

        /// <summary>Destroys the runtime ScriptableObject copies GameManager made, so GameManager reads as uninitialised.</summary>
        public static void DestroyRuntimeConfigs()
        {
#if UNITY_EDITOR
            foreach (var o in Resources.FindObjectsOfTypeAll<GameConfiguration>())
                if (!UnityEditor.EditorUtility.IsPersistent(o)) Object.DestroyImmediate(o);
            foreach (var o in Resources.FindObjectsOfTypeAll<ColorProfile>())
                if (!UnityEditor.EditorUtility.IsPersistent(o)) Object.DestroyImmediate(o);
#endif
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

        /// <summary>Waits until the game start singletons (GameRenderer.InitializeGame) exist, or fails after a timeout.</summary>
        public static IEnumerator WaitForGameState()
        {
            for (int i = 0; i < TimeoutFrames; i++)
            {
                if (HasSingleton<GameStateComponent>() && HasSingleton<GridConfigComponent>() &&
                    HasSingleton<ScoreComponent>() && HasSingleton<GameModeComponent>())
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
    }
}
