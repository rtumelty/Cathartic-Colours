using Data;
using UnityEngine;

/// <summary>
/// The runtime copies of the active configuration and colour profile. The presentation objects
/// live in PresentationRegistry (Presentation assembly) since CC-002.
/// </summary>
public static class GameManager
{
    private static GameConfiguration _menuConfiguration;
    private static GameConfiguration _activeConfiguration;
    private static ColorProfile _activeColorProfile;

    /// <summary>
    /// The main menu's runtime copy: the menu reads and edits only this. A resumed game never replaces it (CC-027).
    /// </summary>
    public static GameConfiguration MenuConfiguration
    {
        get
        {
            if (_menuConfiguration == null)
            {
                Debug.LogWarning("GameConfigurationManager not initialized. Call Initialize() first.");
            }
            return _menuConfiguration;
        }
    }

    /// <summary>The configuration of the game being played (the menu's copy for a new game).</summary>
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

        if (defaultColorProfile == null)
        {
            Debug.LogError("Cannot initialize GameConfigurationManager with null color profile!");
            return;
        }

        // Create a runtime copy that's not bound to an asset file
        // A new game is played with the menu's settings, so both slots share one instance.
        _menuConfiguration = Object.Instantiate(defaultConfig);
        _activeConfiguration = _menuConfiguration;
        _activeColorProfile = Object.Instantiate(defaultColorProfile);
    }

    /// <summary>
    /// Starts a resumed game: sets the active configuration and colour profile to runtime copies of the given ones
    /// without touching <see cref="MenuConfiguration"/>.
    /// </summary>
    public static void StartResumedGame(GameConfiguration resumedConfig, ColorProfile profile)
    {
        if (resumedConfig == null)
        {
            Debug.LogError("Cannot start a resumed game with a null config!");
            return;
        }

        if (profile == null)
        {
            Debug.LogError("Cannot start a resumed game with a null color profile!");
            return;
        }

        _activeConfiguration = Object.Instantiate(resumedConfig);
        _activeColorProfile = Object.Instantiate(profile);

        // Not expected (the menu initialises first), but never leave the menu slot null.
        if (_menuConfiguration == null)
        {
            _menuConfiguration = _activeConfiguration;
        }
    }

    /// <summary>Makes the menu's configuration the active one: called when a new game starts from the menu.</summary>
    public static void UseMenuConfiguration()
    {
        if (_menuConfiguration == null)
        {
            Debug.LogWarning("GameConfigurationManager not initialized. Call Initialize() first.");
            return;
        }

        _activeConfiguration = _menuConfiguration;
    }

    /// <summary>
    /// Reset static fields on play mode entry (handles Fast Enter Play Mode)
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _menuConfiguration = null;
        _activeConfiguration = null;
        _activeColorProfile = null;
    }

    /// <summary>
    /// Check if the manager has been initialized (the menu configuration exists)
    /// </summary>
    public static bool IsInitialized => _menuConfiguration != null;
}
