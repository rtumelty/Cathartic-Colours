using Gameplay;
using UnityEngine;

/// <summary>
/// The presentation objects in the loaded scene, registered by each one in its OnEnable/Awake and
/// cleared on disable. Split from <see cref="GameManager"/> (CC-002) so the Core assembly, which holds
/// GameManager, never references presentation types.
/// </summary>
public static class PresentationRegistry
{
    public static GameRenderer ActiveGameRenderer { get; set; }
    public static GameUIManager ActiveGameUIManager { get; set; }
    public static PointsParticleSpawner ActivePointsParticleSpawner { get; set; }
    public static JellyManager JellyManager { get; set; }

    /// <summary>
    /// Reset static fields on play mode entry (handles Fast Enter Play Mode)
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ActiveGameRenderer = null;
        ActiveGameUIManager = null;
        ActivePointsParticleSpawner = null;
        JellyManager = null;
    }
}
