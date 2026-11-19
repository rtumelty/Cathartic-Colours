using Unity.Entities;
using Unity.Mathematics;

namespace ECS.Components
{
    public struct GameStateComponent : IComponentData
    {
        public bool WaitingForInput;
        public bool GameOver;
        public bool LevelComplete;
        public int MoveCount;
        public Unity.Mathematics.Random Random;

        // Direction of the last accepted move, for presentation (jelly wobble). Not saved.
        public int2 LastMoveDirection;
    }
}
