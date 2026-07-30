using UnityEngine;

namespace EdenGallery
{
    public sealed class EdenGalleryTextureScroller : MonoBehaviour
    {
        private Renderer targetRenderer;
        private Material[] runtimeMaterials;
        private Vector2[] initialOffsets;
        private Vector2 scrollSpeed;
        private float startTime;

        public void Initialize(Renderer renderer, Vector2 speed)
        {
            targetRenderer = renderer;
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
                if (material != null && material.HasProperty("_MainTex"))
                    initialOffsets[i] = material.GetTextureOffset("_MainTex");
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
                if (material == null || !material.HasProperty("_MainTex"))
                    continue;
                Vector2 offset = initialOffsets[i] + movement;
                offset.x = Mathf.Repeat(offset.x, 1f);
                offset.y = Mathf.Repeat(offset.y, 1f);
                material.SetTextureOffset("_MainTex", offset);
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
