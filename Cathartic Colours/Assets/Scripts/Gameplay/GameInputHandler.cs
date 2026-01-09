using ECS.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using Unity.Mathematics;

namespace Gameplay
{
    public class GameInputHandler : MonoBehaviour
    {
        private PlayerInput playerInput;
        private InputAction moveAction;

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            if (playerInput == null)
            {
                playerInput = gameObject.AddComponent<PlayerInput>();
            }
        }

        private void OnEnable()
        {
            // Setup input actions
            if (playerInput.actions == null)
            {
                playerInput.actions = ScriptableObject.CreateInstance<InputActionAsset>();
            }

            var gameplayMap = playerInput.actions.FindActionMap("Gameplay");
            if (gameplayMap == null)
            {
                gameplayMap = playerInput.actions.AddActionMap("Gameplay");
            }

            moveAction = gameplayMap.FindAction("Move");
            if (moveAction == null)
            {
                moveAction = gameplayMap.AddAction("Move", InputActionType.Value, "<Keyboard>/upArrow");
                moveAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/upArrow")
                    .With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow")
                    .With("Right", "<Keyboard>/rightArrow")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");
            }

            moveAction.Enable();
            moveAction.performed += OnMove;
        }

        private void OnDisable()
        {
            if (moveAction != null)
            {
                moveAction.performed -= OnMove;
                moveAction.Disable();
            }
        }

        private void OnMove(InputAction.CallbackContext context)
        {
            Vector2 input = context.ReadValue<Vector2>();
            
            // Convert to discrete direction
            int2 direction = new int2(0, 0);
            
            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            {
                direction.x = input.x > 0 ? 1 : -1;
            }
            else if (Mathf.Abs(input.y) > 0.1f)
            {
                direction.y = input.y > 0 ? 1 : -1;
            }

            if (direction.x != 0 || direction.y != 0)
            {
                SendMoveCommand(direction);
            }
        }

        private void SendMoveCommand(int2 direction)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var entityManager = world.EntityManager;
            
            // Check if a MoveDirectionComponent entity already exists
            var query = entityManager.CreateEntityQuery(typeof(MoveDirectionComponent));
            if (!query.IsEmpty)
            {
                return; // Do not create a new entity if a move is already pending
            }
            
            // Create move direction entity
            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, new MoveDirectionComponent
            {
                Direction = direction
            });
        }

        // For touch/swipe input
        public void OnSwipe(Vector2 swipeDirection)
        {
            int2 direction = new int2(0, 0);
            
            if (Mathf.Abs(swipeDirection.x) > Mathf.Abs(swipeDirection.y))
            {
                direction.x = swipeDirection.x > 0 ? 1 : -1;
            }
            else
            {
                direction.y = swipeDirection.y > 0 ? 1 : -1;
            }

            SendMoveCommand(direction);
        }
    }
}
