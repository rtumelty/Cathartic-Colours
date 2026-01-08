using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay
{
    namespace CatharticColours
    {
        // Simple swipe detection for mobile
        public class SwipeDetector : MonoBehaviour
        {
            [SerializeField] private float swipeThreshold = 50f;
            [SerializeField] private GameInputHandler inputHandler;

            private Vector2 touchStartPos;
            private bool isSwiping;

            private void Update()
            {
                if (Touchscreen.current != null)
                {
                    var touch = Touchscreen.current.primaryTouch;
                
                    if (touch.press.wasPressedThisFrame)
                    {
                        touchStartPos = touch.position.ReadValue();
                        isSwiping = true;
                    }
                    else if (touch.press.wasReleasedThisFrame && isSwiping)
                    {
                        Vector2 touchEndPos = touch.position.ReadValue();
                        Vector2 swipe = touchEndPos - touchStartPos;

                        if (swipe.magnitude >= swipeThreshold)
                        {
                            inputHandler.OnSwipe(swipe.normalized);
                        }
                    
                        isSwiping = false;
                    }
                }
            }
        }
    }
}