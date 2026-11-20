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
        private Material runtimeMaterial = null;
        
        private Vector2 currentWobbleDirection = Vector2.zero;
        private Vector2 targetWobbleDirection = Vector2.zero;
        
        /// <summary>
        /// Per-run copy of <c>jellyMaterial</c> that the wobble is written to, so the saved
        /// <c>BlockMaterial.mat</c> asset is never modified. <see cref="GameRenderer"/> assigns it to each block
        /// renderer; its per-block <c>_MainColor</c> MaterialPropertyBlock still applies on top. Falls back to the
        /// shared material if none is assigned.
        /// </summary>
        public Material RuntimeMaterial => runtimeMaterial;

        void Awake()
        {
            if (jellyMaterial != null)
                runtimeMaterial = new Material(jellyMaterial) { name = jellyMaterial.name + " (Runtime)" };
        }

        void OnDestroy()
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }

        void OnEnable()
        {
            PresentationRegistry.JellyManager = this;
        }
        
        void OnDisable()
        {
            PresentationRegistry.JellyManager = null;
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
            timeSinceEvent = 0;

            while (timeSinceEvent < wobblePeriod)
            {
                // Exponential smoothing: directionSmoothing is the fraction closed per 1/60 s frame, so the feel is
                // identical at 60 fps and independent of the actual frame rate.
                float smoothing = 1f - Mathf.Pow(1f - directionSmoothing, Time.deltaTime * 60f);
                currentWobbleDirection =
                    Vector2.Lerp(currentWobbleDirection, targetWobbleDirection, smoothing);
                var material = runtimeMaterial != null ? runtimeMaterial : jellyMaterial;
                material.SetVector(MoveDirection, currentWobbleDirection);

                float t = timeSinceEvent / wobblePeriod;
                
                material.SetFloat(WobbleStrength, wobbleStrengthCurve.Evaluate(t) * wobbleScale);
                material.SetFloat(DirectionInfluence, directionInfluenceCurve.Evaluate(t) * directionInfluenceScale);
                material.SetFloat(DirectionWobbleFrequency, directionWobbleFrequencyCurve.Evaluate(t) * directionFrequencyScale);

                timeSinceEvent += Time.deltaTime;
                yield return 0;
            }
            
            wobbleCoroutine = null;
        }
    }
}