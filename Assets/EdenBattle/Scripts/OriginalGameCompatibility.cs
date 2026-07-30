using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Timeline;

// Compatibility types reconstructed from the Unity 2018 SerializedFile type
// trees. Their names, assembly (Assembly-CSharp) and serialized field layouts
// match the scripts referenced by the original AssetBundles.

[Serializable]
public sealed class MaterialTextureInfo
{
    public string attribute;
    public string tex2dName;
}

[Serializable]
public sealed class MaterialAllTextureInfo
{
    public int matIndex;
    public List<MaterialTextureInfo> matTexInfos = new List<MaterialTextureInfo>();
}

[Serializable]
public sealed class RenderMatTexPair
{
    public Renderer renderObj;
    public List<MaterialAllTextureInfo> matAllInfos = new List<MaterialAllTextureInfo>();
}

[Serializable]
public sealed class RenderMeshPair
{
    public MeshFilter meshFilterObj;
    public string meshName;
}

public sealed class PrefabRenderHolder : MonoBehaviour
{
    public List<RenderMatTexPair> rendersMatTexInfo = new List<RenderMatTexPair>();
    public List<RenderMeshPair> rendersMeshInfo = new List<RenderMeshPair>();

    private void Awake()
    {
        RestoreResources();
    }

    public int RestoreResources()
    {
        int restored = 0;
        if (rendersMatTexInfo != null)
        {
            for (int i = 0; i < rendersMatTexInfo.Count; i++)
            {
                RenderMatTexPair pair = rendersMatTexInfo[i];
                if (pair == null || pair.renderObj == null || pair.matAllInfos == null)
                    continue;

                Material[] materials = pair.renderObj.sharedMaterials;
                for (int m = 0; m < pair.matAllInfos.Count; m++)
                {
                    MaterialAllTextureInfo materialInfo = pair.matAllInfos[m];
                    if (materialInfo == null || materialInfo.matIndex < 0 || materialInfo.matIndex >= materials.Length)
                        continue;

                    Material material = materials[materialInfo.matIndex];
                    if (material == null || materialInfo.matTexInfos == null)
                        continue;

                    for (int t = 0; t < materialInfo.matTexInfos.Count; t++)
                    {
                        MaterialTextureInfo textureInfo = materialInfo.matTexInfos[t];
                        Texture2D texture;
                        if (textureInfo == null || !SkillResourceRegistry.TryGetTexture(textureInfo.tex2dName, out texture))
                            continue;

                        string property = string.IsNullOrEmpty(textureInfo.attribute) ? "_MainTex" : textureInfo.attribute;
                        material.SetTexture(property, texture);
                        restored++;
                    }
                }
                // Some Timeline clips instantiate dependency prefabs lazily.
                // Their Awake path only reaches this holder, so replace the
                // Android-only material shader here as well.
                restored += SkillResourceRegistry.RestoreMaterials(materials);
            }
        }

        if (rendersMeshInfo != null)
        {
            for (int i = 0; i < rendersMeshInfo.Count; i++)
            {
                RenderMeshPair pair = rendersMeshInfo[i];
                Mesh mesh;
                if (pair == null || pair.meshFilterObj == null || !SkillResourceRegistry.TryGetMesh(pair.meshName, out mesh))
                    continue;
                pair.meshFilterObj.sharedMesh = mesh;
                restored++;
            }
        }
        return restored;
    }
}

public sealed class SkillEffectsHelper : MonoBehaviour
{
    public bool flipX;
    public bool flipXZ;
    public bool rotationY;
    public bool flipSimpleX;
    public string sortingLayerName;
    public int sortingOrder;

    private void Awake()
    {
        Vector3 scale = transform.localScale;
        if (flipX || flipSimpleX)
            scale.x = -Mathf.Abs(scale.x);
        if (flipXZ)
        {
            scale.x = -Mathf.Abs(scale.x);
            scale.z = -Mathf.Abs(scale.z);
        }
        transform.localScale = scale;
        if (rotationY)
            transform.Rotate(0f, 180f, 0f, Space.Self);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!string.IsNullOrEmpty(sortingLayerName))
                renderers[i].sortingLayerName = sortingLayerName;
            renderers[i].sortingOrder += sortingOrder;
        }
    }
}

// The labi XP Timeline contains a custom, clip-less camera track from the
// original Assembly-CSharp.  Recreating the type lets Unity deserialize the
// TimelineAsset.  The preview deliberately keeps its own wide, static camera,
// so the track does not need the original battle-camera implementation.
[Serializable]
public sealed class BattleCameraTrack : TrackAsset
{
}

public static class SkillResourceRegistry
{
    private static readonly Dictionary<string, Texture2D> Textures =
        new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Mesh> Meshes =
        new Dictionary<string, Mesh>(StringComparer.OrdinalIgnoreCase);

    public static int TextureCount { get { return Textures.Count; } }
    public static int MeshCount { get { return Meshes.Count; } }

    public static void Clear()
    {
        Textures.Clear();
        Meshes.Clear();
    }

    public static void Register(Texture2D texture)
    {
        if (texture == null)
            return;
        AddAliases(Textures, texture.name, texture);
    }

    public static void Register(Mesh mesh)
    {
        if (mesh == null)
            return;
        AddAliases(Meshes, mesh.name, mesh);
    }

    public static bool TryGetTexture(string name, out Texture2D texture)
    {
        return TryGet(Textures, name, out texture);
    }

    public static bool TryGetMesh(string name, out Mesh mesh)
    {
        return TryGet(Meshes, name, out mesh);
    }

    public static int RestorePrefab(GameObject prefab)
    {
        if (prefab == null)
            return 0;

        int restored = 0;
        PrefabRenderHolder[] holders = prefab.GetComponentsInChildren<PrefabRenderHolder>(true);
        for (int i = 0; i < holders.Length; i++)
            restored += holders[i].RestoreResources();

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            restored += RestoreMaterials(renderers[i].sharedMaterials);
        return restored;
    }

    public static List<string> FindMissingResources(GameObject prefab)
    {
        List<string> missing = new List<string>();
        if (prefab == null)
            return missing;

        PrefabRenderHolder[] holders = prefab.GetComponentsInChildren<PrefabRenderHolder>(true);
        for (int h = 0; h < holders.Length; h++)
        {
            PrefabRenderHolder holder = holders[h];
            if (holder.rendersMatTexInfo != null)
            {
                for (int r = 0; r < holder.rendersMatTexInfo.Count; r++)
                {
                    RenderMatTexPair renderInfo = holder.rendersMatTexInfo[r];
                    if (renderInfo == null || renderInfo.matAllInfos == null)
                        continue;
                    for (int m = 0; m < renderInfo.matAllInfos.Count; m++)
                    {
                        MaterialAllTextureInfo materialInfo = renderInfo.matAllInfos[m];
                        if (materialInfo == null || materialInfo.matTexInfos == null)
                            continue;
                        for (int t = 0; t < materialInfo.matTexInfos.Count; t++)
                        {
                            MaterialTextureInfo textureInfo = materialInfo.matTexInfos[t];
                            Texture2D texture;
                            if (textureInfo != null && !TryGetTexture(textureInfo.tex2dName, out texture))
                                AddUnique(missing, "texture:" + textureInfo.tex2dName);
                        }
                    }
                }
            }

            if (holder.rendersMeshInfo == null)
                continue;
            for (int m = 0; m < holder.rendersMeshInfo.Count; m++)
            {
                RenderMeshPair meshInfo = holder.rendersMeshInfo[m];
                Mesh mesh;
                if (meshInfo != null && !TryGetMesh(meshInfo.meshName, out mesh))
                    AddUnique(missing, "mesh:" + meshInfo.meshName);
            }
        }
        return missing;
    }

    public static int RestoreMaterials(Material[] materials)
    {
        if (materials == null)
            return 0;

        int restored = 0;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
                continue;

            if (ReplaceUnsupportedShader(material))
                restored++;
            if (material.mainTexture != null)
                continue;

            Texture2D texture;
            string materialName = material.name;
            if (TryGetTexture(materialName, out texture) ||
                TryGetTexture(RemoveSuffix(materialName, "_Material"), out texture) ||
                TryGetTexture(RemoveSuffix(materialName, " Material"), out texture))
            {
                material.mainTexture = texture;
                restored++;
            }
        }
        return restored;
    }

    private static bool ReplaceUnsupportedShader(Material material)
    {
        string shaderName = material.shader == null ? string.Empty : material.shader.name;
        string fallbackName = null;
        if (shaderName == "Spine/Skeleton")
            fallbackName = "SkillRestore/Spine Skeleton";
        else if (shaderName.IndexOf("Mask Additive", StringComparison.OrdinalIgnoreCase) >= 0)
            fallbackName = "SkillRestore/Particle Mask Additive";
        else if (shaderName.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0)
            fallbackName = "SkillRestore/Particle Additive";
        else if (shaderName.IndexOf("SoulGames/Effects", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 shaderName == "Sprites/Default")
            fallbackName = "SkillRestore/Particle AlphaBlend";

        if (fallbackName == null)
            return false;

#if !UNITY_EDITOR
        if (material.shader != null && material.shader.isSupported)
            return false;
#endif
        Shader fallback = Shader.Find(fallbackName);
        if (fallback == null || material.shader == fallback)
            return false;
        material.shader = fallback;
        return true;
    }

    private static void AddAliases<T>(Dictionary<string, T> target, string name, T value)
    {
        if (string.IsNullOrEmpty(name))
            return;
        target[name] = value;
        target[Path.GetFileName(name)] = value;
        target[Path.GetFileNameWithoutExtension(name)] = value;
    }

    private static bool TryGet<T>(Dictionary<string, T> source, string name, out T value)
    {
        value = default(T);
        if (string.IsNullOrEmpty(name))
            return false;
        return source.TryGetValue(name, out value) ||
               source.TryGetValue(Path.GetFileName(name), out value) ||
               source.TryGetValue(Path.GetFileNameWithoutExtension(name), out value);
    }

    private static string RemoveSuffix(string value, string suffix)
    {
        if (string.IsNullOrEmpty(value) || !value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return value;
        return value.Substring(0, value.Length - suffix.Length);
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (!values.Contains(value))
            values.Add(value);
    }
}

namespace FairyGUI
{
    // The card prefab contains a disabled UIPanel used by the original game's
    // UI layer. A data-compatible placeholder preserves the prefab while the
    // Spine renderer is previewed without importing the entire FairyGUI SDK.
    public sealed class UIPanel : MonoBehaviour
    {
        public string packageName;
        public string componentName;
        public int fitScreen;
        public int sortingOrder;
        public string packagePath;
        public int renderMode;
        public Camera renderCamera;
        public Vector3 position;
        public Vector3 scale;
        public Vector3 rotation;
        public bool fairyBatching;
        public bool touchDisabled;
        public Vector2 cachedUISize;
        public int hitTestMode;
        public bool setNativeChildrenOrder;
    }
}
