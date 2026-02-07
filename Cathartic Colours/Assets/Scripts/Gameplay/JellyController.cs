using UnityEngine;

namespace Gameplay
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class JellyController : MonoBehaviour
    {
        [Header("Animation")]
        public bool animateWobble = true;
        public Vector2 wobbleStrengthRange = new Vector2(0.01f, 0.05f);
        public float wobbleAnimationSpeed = 1.0f;
    
        [Header("Interaction")]
        public bool respondToScale = true;
        public float scaleInfluence = 1.0f;
    
        private Material material;
        private SpriteRenderer spriteRenderer;
        private Vector3 originalScale;
    
        void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            material = spriteRenderer.material;
            originalScale = transform.localScale;
        }
    
        void Update()
        {
            if (material == null) return;
        
            // Animate wobble strength
            if (animateWobble)
            {
                float wobbleStrength = Mathf.Lerp(wobbleStrengthRange.x, wobbleStrengthRange.y, 
                    (Mathf.Sin(Time.time * wobbleAnimationSpeed) + 1.0f) * 0.5f);
                material.SetFloat("_WobbleStrength", wobbleStrength);
            }
        
            // Respond to scale changes
            if (respondToScale)
            {
                float scaleRatio = transform.localScale.magnitude / originalScale.magnitude;
                float distortionBoost = (scaleRatio - 1.0f) * scaleInfluence;
                material.SetFloat("_DistortionStrength", 0.02f + distortionBoost * 0.02f);
            }
        }
    
        // Call this method to make the sprite "jiggle"
        public void Jiggle(float intensity = 1.0f)
        {
            if (material != null)
            {
                StartCoroutine(JiggleCoroutine(intensity));
            }
        }
    
        private System.Collections.IEnumerator JiggleCoroutine(float intensity)
        {
            float originalWobble = material.GetFloat("_WobbleStrength");
            float targetWobble = originalWobble * (1.0f + intensity);
        
            float timer = 0f;
            float duration = 0.3f;
        
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float progress = timer / duration;
                float wobble = Mathf.Lerp(targetWobble, originalWobble, progress);
                material.SetFloat("_WobbleStrength", wobble);
                yield return null;
            }
        
            material.SetFloat("_WobbleStrength", originalWobble);
        }
    }
}
