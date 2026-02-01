using System.Collections;
using UnityEngine;

namespace Gameplay
{
    public class JellyManager : MonoBehaviour
    {
        private static readonly int WobbleStrength = Shader.PropertyToID("_WobbleStrength");
        private static readonly int DirectionInfluence = Shader.PropertyToID("_DirectionInfluence");
        private static readonly int DirectionWobbleFrequency = Shader.PropertyToID("_DirectionWobbleFrequency");
        private static readonly int MoveDirection = Shader.PropertyToID("_MoveDirection");

        [SerializeField] private Material jellyMaterial = null;
        [SerializeField] private float wobblePeriod = 1;
        [SerializeField] private float directionSmoothing = 0.7f;
        
        [SerializeField] private AnimationCurve wobbleStrengthCurve;
        [SerializeField] private float wobbleScale = 0.3f;
        [SerializeField] private AnimationCurve directionInfluenceCurve;
        [SerializeField] private float directionInfluenceScale = 0.3f;
        [SerializeField] private AnimationCurve directionWobbleFrequencyCurve;
        [SerializeField] private float directionFrequencyScale = 0.3f;
        
        private Coroutine wobbleCoroutine = null;
        private float timeSinceEvent = 0;
        private bool wobbleActive = false;
        
        private Vector2 currentWobbleDirection = Vector2.zero;
        private Vector2 targetWobbleDirection = Vector2.zero;
        
        void OnEnable()
        {
            GameManager.JellyManager = this;
        }
        
        void OnDisable()
        {
            GameManager.JellyManager = null;
        }

        public void StartWobble(Vector2 targetWobbleDirection)
        {
            this.targetWobbleDirection = targetWobbleDirection;
            timeSinceEvent = 0;

            if (wobbleCoroutine == null)
            {
                wobbleCoroutine = StartCoroutine(WobbleCoroutine());
            }
        }
        
        IEnumerator WobbleCoroutine() {
            wobbleActive = true;
            timeSinceEvent = 0;

            while (timeSinceEvent < wobblePeriod)
            {
                currentWobbleDirection =
                    Vector2.Lerp(currentWobbleDirection, targetWobbleDirection, directionSmoothing);
                jellyMaterial.SetVector(MoveDirection, currentWobbleDirection);

                float t = timeSinceEvent / wobblePeriod;
                
                jellyMaterial.SetFloat(WobbleStrength, wobbleStrengthCurve.Evaluate(t) * wobbleScale);
                jellyMaterial.SetFloat(DirectionInfluence, directionInfluenceCurve.Evaluate(t) * directionInfluenceScale);
                jellyMaterial.SetFloat(DirectionWobbleFrequency, directionWobbleFrequencyCurve.Evaluate(t) * directionFrequencyScale);

                timeSinceEvent += Time.deltaTime;
                yield return 0;
            }
            
            wobbleActive = false;
            wobbleCoroutine = null;
        }
    }
}