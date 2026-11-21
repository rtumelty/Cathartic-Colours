using Data;
using Simulation;
using Unity.Entities;

namespace ECS.Components
{
    /// <summary>
    /// The rules of the running game (ADR-005), built from <c>GameConfiguration</c> when a game starts or resumes
    /// (CC-008.3). Unmanaged, so <c>BoardStepSystem</c> can be Burst-compiled without reading <c>GameManager</c>.
    /// Never saved: a resume rebuilds it from the saved mode and spawn toggle, the same way a new game does.
    /// </summary>
    public struct GameRulesComponent : IComponentData
    {
        public GameRules Rules;

        /// <summary>
        /// The one builder for new and resumed games: tier points and the spawn toggle become <see cref="StepSettings"/>,
        /// and the mode picks the rule from each family through <see cref="GameRules.For"/>.
        /// </summary>
        public static GameRulesComponent From(GameConfiguration config)
        {
            var settings = new StepSettings
            {
                Points = new TierPoints
                {
                    Tier1 = config.tier1Points,
                    Tier2 = config.tier2Points,
                    Tier3 = config.tier3Points,
                    Tier4 = config.tier4Points
                },
                SpawnEnabled = config.spawnNextColourSystem
            };
            return new GameRulesComponent { Rules = GameRules.For(config.gameMode, settings) };
        }
    }
}
