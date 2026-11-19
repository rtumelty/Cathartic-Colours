using Simulation;
using Unity.Entities;

namespace ECS.Components
{
    /// <summary>
    /// Per-game settings the move step needs, copied from <c>GameConfiguration</c> when a game starts or resumes.
    /// Unmanaged, so <c>BoardStepSystem</c> can be Burst-compiled without reading <c>GameManager</c>.
    /// </summary>
    public struct ScoringConfigComponent : IComponentData
    {
        public StepSettings Settings;
    }
}
