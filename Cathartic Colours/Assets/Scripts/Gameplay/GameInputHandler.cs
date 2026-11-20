using ECS.Components;
using ECS.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using Unity.Mathematics;

namespace Gameplay
{
    public class GameInputHandler : MonoBehaviour
    {
        // Assigned to Assets/Input/GameControls.inputactions on the GameManager prefab.
        [SerializeField] private InputActionAsset actions;

        private InputAction moveAction;

        // One tilt = one move: fires when the input passes the dead zone, re-arms once it returns near centre.
        private bool armed = true;

        private void OnEnable()
        {
            if (actions == null)
            {
                Debug.LogError("GameInputHandler: no InputActionAsset assigned; keyboard and gamepad input is disabled.", this);
                return;
            }

            moveAction = actions.FindAction("Gameplay/Move", throwIfNotFound: false);
            if (moveAction == null)
            {
                Debug.LogError("GameInputHandler: action 'Gameplay/Move' not found in the assigned asset; keyboard and gamepad input is disabled.", this);
                return;
            }

            armed = true;
            moveAction.performed += OnMove;
            moveAction.canceled += OnMoveCanceled;
            moveAction.Enable();
        }

        private void OnDisable()
        {
            if (moveAction == null) return;

            moveAction.performed -= OnMove;
            moveAction.canceled -= OnMoveCanceled;
            moveAction.Disable();
            moveAction = null;
        }

        private void OnMove(InputAction.CallbackContext context)
        {
            Vector2 input = context.ReadValue<Vector2>();

            if (!armed)
            {
                // Re-arm only when the stick/keys are back near centre. While disarmed, a second key
                // pressed while the first is held does not fire; release everything, then press again.
                if (input.sqrMagnitude < InputDirection.ReleaseZone * InputDirection.ReleaseZone)
                    armed = true;
                return;
            }

            int2 direction = InputDirection.Resolve(input);
            if (direction.x != 0 || direction.y != 0)
            {
                armed = false;
                SendMoveCommand(direction);
            }
        }

        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            armed = true;
        }

        private void SendMoveCommand(int2 direction)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var entityManager = world.EntityManager;

            // Check if a MoveDirectionComponent entity already exists
            bool movePending;
            using (var query = entityManager.CreateEntityQuery(typeof(MoveDirectionComponent)))
            {
                movePending = !query.IsEmpty;
            }

            if (movePending)
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

        // For touch/swipe/mouse-drag input. The vector is normalised, so it always passes the dead zone.
        public void OnSwipe(Vector2 swipeDirection)
        {
            int2 direction = InputDirection.Resolve(swipeDirection);
            if (direction.x != 0 || direction.y != 0)
            {
                SendMoveCommand(direction);
            }
        }
    }
}
