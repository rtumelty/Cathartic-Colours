using ECS.Components;
using Unity.Burst;
using Unity.Entities;

[UpdateInGroup(typeof(PresentationSystemGroup))]
partial struct EndFrameSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameStateComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var gameState = SystemAPI.GetSingletonRW<GameStateComponent>();
        gameState.ValueRW.WaitingForInput = true;
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
