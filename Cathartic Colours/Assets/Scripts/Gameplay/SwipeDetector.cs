using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay
{
    namespace CatharticColours
    {
        // Swipe detection: touchscreen, or a left-button mouse drag (so it can be tested in the editor)
        public class SwipeDetector : MonoBehaviour
        {
            [SerializeField] private float swipeThreshold = 50f;
            [SerializeField] private GameInputHandler inputHandler;

            private Vector2 touchStartPos;
            private bool isSwiping;

            private void Update()
            {
                if (inputHandler == null) return;

                if (!TryGetPointer(out bool pressedThisFrame, out bool releasedThisFrame, out Vector2 position))
                {
                    return;
                }

                if (pressedThisFrame)
                {
                    touchStartPos = position;
                    isSwiping = true;
                }
                else if (releasedThisFrame && isSwiping)
                {
                    Vector2 swipe = position - touchStartPos;

                    if (swipe.magnitude >= swipeThreshold)
                    {
                        inputHandler.OnSwipe(swipe.normalized);
                    }

                    isSwiping = false;
                }
            }

            // Touchscreen when present and in use this frame, otherwise the mouse.
            private static bool TryGetPointer(out bool pressedThisFrame, out bool releasedThisFrame, out Vector2 position)
            {
                var touchscreen = Touchscreen.current;
                if (touchscreen != null)
                {
                    var touch = touchscreen.primaryTouch;
                    if (touch.press.isPressed || touch.press.wasPressedThisFrame || touch.press.wasReleasedThisFrame)
                    {
                        pressedThisFrame = touch.press.wasPressedThisFrame;
                        releasedThisFrame = touch.press.wasReleasedThisFrame;
                        position = touch.position.ReadValue();
                        return true;
                    }
                }

                var mouse = Mouse.current;
                if (mouse != null)
                {
                    pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
                    releasedThisFrame = mouse.leftButton.wasReleasedThisFrame;
                    position = mouse.position.ReadValue();
                    return true;
                }

                pressedThisFrame = releasedThisFrame = false;
                position = default;
                return false;
            }
        }
    }
}
