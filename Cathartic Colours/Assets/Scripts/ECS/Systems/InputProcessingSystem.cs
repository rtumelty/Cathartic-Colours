using ECS.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(BlockMovementSystem))]
    public partial struct InputProcessingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameStateComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var gameStateEntity = SystemAPI.GetSingletonEntity<GameStateComponent>();
            var gameState = state.EntityManager.GetComponentData<GameStateComponent>(gameStateEntity);
        
            if (!gameState.WaitingForInput || gameState.GameOver)
                return;

            if (!SystemAPI.TryGetSingleton<MoveDirectionComponent>(out var moveDirection))
                return;

            if (moveDirection.Direction.x == 0 && moveDirection.Direction.y == 0)
                return;
            
            // Enable MovingBlockTag on all blocks
            foreach (var entity in SystemAPI.QueryBuilder()
                         .WithAll<BlockComponent>()
                         .WithDisabled<MovingBlockTag>()
                         .Build()
                         .ToEntityArray(Allocator.Temp))
            {
                state.EntityManager.SetComponentEnabled<MovingBlockTag>(entity, true);
            }
            GameManager.JellyManager.StartWobble(new Vector2(moveDirection.Direction.x, moveDirection.Direction.y));

            gameState.WaitingForInput = false;
            gameState.MoveCount++;
            state.EntityManager.SetComponentData(gameStateEntity, gameState);
        }
    }
}
