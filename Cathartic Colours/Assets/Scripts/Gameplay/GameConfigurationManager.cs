using Data;
using UnityEngine;

public static class GameConfigurationManager
{
    private static GameConfiguration _activeConfiguration;

    public static GameConfiguration ActiveConfiguration
    {
        get
        {
            if (_activeConfiguration == null)
            {
                Debug.LogWarning("GameConfigurationManager not initialized. Call Initialize() first.");
            }
            return _activeConfiguration;
        }
    }

    /// <summary>
    /// Initialize with a runtime copy of the default configuration
    /// </summary>
    public static void Initialize(GameConfiguration defaultConfig)
    {
        if (defaultConfig == null)
        {
            Debug.LogError("Cannot initialize GameConfigurationManager with null config!");
            return;
        }

        // Create a runtime copy that's not bound to an asset file
        _activeConfiguration = Object.Instantiate(defaultConfig);
    }

    /// <summary>
    /// Reset static fields on play mode entry (handles Fast Enter Play Mode)
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _activeConfiguration = null;
        Debug.Log("GameConfigurationManager reset (SubsystemRegistration)");
    }

    /// <summary>
    /// Check if the manager has been initialized
    /// </summary>
    public static bool IsInitialized => _activeConfiguration != null;
}