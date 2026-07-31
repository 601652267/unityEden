using UnityEngine;

namespace EdenGallery
{
    public sealed class EdenGalleryTextureScroller : MonoBehaviour
    {
        private Renderer targetRenderer;
        private Material[] runtimeMaterials;
        private Vector2[] initialOffsets;
        private string textureProperty;
        private Vector2 scrollSpeed;
        private float startTime;

        public void Initialize(Renderer renderer, Vector2 speed)
        {
            Initialize(renderer, "_MainTex", speed);
        }

        public void Initialize(
            Renderer renderer,
            string property,
            Vector2 speed)
        {
            targetRenderer = renderer;
            textureProperty = string.IsNullOrEmpty(property)
                ? "_MainTex"
                : property;
            scrollSpeed = speed;
            startTime = Time.unscaledTime;
            runtimeMaterials = targetRenderer == null
                ? null
                : targetRenderer.materials;
            initialOffsets = new Vector2[
                runtimeMaterials == null ? 0 : runtimeMaterials.Length];
            for (int i = 0; i < initialOffsets.Length; i++)
            {
                Material material = runtimeMaterials[i];
                if (material != null &&
                    material.HasProperty(textureProperty))
                {
                    initialOffsets[i] = material.GetTextureOffset(
                        textureProperty);
                }
            }
        }

        private void Update()
        {
            if (runtimeMaterials == null)
                return;

            float elapsed = Time.unscaledTime - startTime;
            Vector2 movement = scrollSpeed * elapsed;
            for (int i = 0; i < runtimeMaterials.Length; i++)
            {
                Material material = runtimeMaterials[i];
                if (material == null ||
                    !material.HasProperty(textureProperty))
                    continue;
                Vector2 offset = initialOffsets[i] + movement;
                offset.x = Mathf.Repeat(offset.x, 1f);
                offset.y = Mathf.Repeat(offset.y, 1f);
                material.SetTextureOffset(textureProperty, offset);
            }
        }

        private void OnDestroy()
        {
            if (runtimeMaterials == null)
                return;
            for (int i = 0; i < runtimeMaterials.Length; i++)
                if (runtimeMaterials[i] != null)
                    Destroy(runtimeMaterials[i]);
        }
    }
}
