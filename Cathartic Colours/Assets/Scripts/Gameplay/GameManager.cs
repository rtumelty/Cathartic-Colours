using Data;
using Gameplay;
using UnityEngine;

public static class GameManager
{
    private static GameConfiguration _activeConfiguration;
    private static ColorProfile _activeColorProfile;

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

    public static ColorProfile ActiveColorProfile
    {
        get
        {
            if (_activeColorProfile == null)
            {
                Debug.LogWarning("GameConfigurationManager not initialized. Call Initialize() first.");
            }
            return _activeColorProfile;
        }
    }
    
    public static GameRenderer ActiveGameRenderer { get; set; }
    public static PointsParticleSpawner ActivePointsParticleSpawner { get; set; }

    /// <summary>
    /// Initialize with a runtime copy of the configurations
    /// </summary>
    public static void Initialize(GameConfiguration defaultConfig, ColorProfile defaultColorProfile)
    {
        if (defaultConfig == null)
        {
            Debug.LogError("Cannot initialize GameConfigurationManager with null config!");
            return;
        }

        // Create a runtime copy that's not bound to an asset file
        _activeConfiguration = Object.Instantiate(defaultConfig);
        _activeColorProfile = Object.Instantiate(defaultColorProfile);
    }

    /// <summary>
    /// Reset static fields on play mode entry (handles Fast Enter Play Mode)
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _activeConfiguration = null;
        _activeColorProfile = null;
    }

    /// <summary>
    /// Check if the manager has been initialized
    /// </summary>
    public static bool IsInitialized => _activeConfiguration != null;
}