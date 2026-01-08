using Unity.Entities;

namespace ECS.Components
{
    public struct GameStateComponent : IComponentData
    {
        public bool WaitingForInput;
        public bool GameOver;
        public bool LevelComplete;
        public int MoveCount;
        public Unity.Mathematics.Random Random;
    }
}
