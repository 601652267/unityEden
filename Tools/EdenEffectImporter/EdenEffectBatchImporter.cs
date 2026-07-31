using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class EdenEffectBatchImporter
{
    private const string BundleRoot = "/Users/zhuhaiming/Desktop/edenAssetsObb/eft_fx_mainback";
    private const string FallbackBundleRoot = "/Users/zhuhaiming/Desktop/edenAssetsObb/jp.co.yoozoo.projectyellow/files/Config/res";
    private const string ResourceBundleRoot = "/Users/zhuhaiming/Desktop/edenAssetsObb/main.19.jp.co.yoozoo.projectyellow/assets/ResEx";
    private const string ExtractedTextureRoot = "/Users/zhuhaiming/Desktop/edenAssets/edenAssets/edenAssets2/Texture2D";
    private const string ApkBundleRoot = "/private/tmp/eden_apk_newchars/bundles";
    private const string ApkExtractedTextureRoot = "/private/tmp/eden_apk_newchars/export";
    private const string EffectRoot = "Assets/EdenGallery/Resources/EdenGallery/Effects";
    private const string TextureRoot = EffectRoot + "/Common/Textures";

    private static readonly string[] Ids =
    {
        "11300041",
        "11301003",
        "11301004",
        "11301005",
        "11301006"
    };

    private static readonly string[] SupplementIds =
    {
        "11202020",
        "11300045",
        "11300046",
        "11300055",
        "11301023"
    };

    private static readonly Dictionary<string, int> BaseOrders = new Dictionary<string, int>
    {
        { "11300029", 20 },
        { "11300030", 27 },
        { "11300031", 2 },
        { "11300033", 5 },
        { "11300034", 13 },
        { "11300035", 13 },
        { "11300037", 0 },
        { "11300038", 101 },
        { "11300039", 51 },
        { "11300040", 21 },
        { "11300056", 1 },
        { "11300057", 1 },
        { "11300043", 0 },
        { "11300048", 0 },
        { "11300049", 0 },
        { "11301001", 0 },
        { "11301002", 0 },
        { "11300041", 0 },
        { "11301003", 0 },
        { "11301004", 0 },
        { "11301005", 0 },
        { "11301006", 0 },
        { "11202020", 0 },
        { "11300045", 0 },
        { "11300046", 0 },
        { "11300055", 0 },
        { "11301023", 0 }
    };

    private static readonly Dictionary<string, Texture2D> Textures =
        new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> ExtractedTextures =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> Report = new List<string>();
    private static int materialIndex;
    private static int meshIndex;
    private static int failures;
    private static string currentEffectId;

    public static void Inspect()
    {
        List<string> lines = new List<string>();
        foreach (string id in new[]
        {
            "11202014",
            "11202016",
            "11300032",
            "11300036"
        })
        {
            List<AssetBundle> dependencies = new List<AssetBundle>();
            foreach (string dependencyPath in Directory.GetFiles(
                ResourceBundleRoot,
                "st_cardshowspine_" + id + "*bg*.aab",
                SearchOption.TopDirectoryOnly))
            {
                AssetBundle dependency =
                    AssetBundle.LoadFromFile(dependencyPath);
                if (dependency != null)
                    dependencies.Add(dependency);
            }
            string path = Path.Combine(BundleRoot, "eft_fx_mainback_" + id + ".aab");
            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            GameObject prefab = FindPrefab(bundle);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            lines.Add(
                id + " ROOT=" + instance.name +
                " particles=" +
                instance.GetComponentsInChildren<ParticleSystem>(true).Length);
            foreach (Renderer renderer in
                instance.GetComponentsInChildren<Renderer>(true))
            {
                string materialNames = string.Empty;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (materialNames.Length != 0)
                        materialNames += ",";
                    materialNames += material == null
                        ? "<null>"
                        : material.name + "(" +
                          (material.mainTexture == null
                              ? "<no-texture>"
                              : material.mainTexture.name) + ")";
                }
                SpriteRenderer spriteRenderer = renderer as SpriteRenderer;
                Sprite sprite = spriteRenderer == null
                    ? null
                    : spriteRenderer.sprite;
                lines.Add(
                    "  RENDERER " +
                    ParticlePath(renderer.transform, instance.transform) +
                    " type=" + renderer.GetType().Name +
                    " active=" + renderer.gameObject.activeInHierarchy +
                    " enabled=" + renderer.enabled +
                    " order=" + renderer.sortingOrder +
                    " localPosition=" + renderer.transform.localPosition +
                    " localScale=" + renderer.transform.localScale +
                    " worldPosition=" + renderer.transform.position +
                    " worldScale=" + renderer.transform.lossyScale +
                    " sprite=" + (sprite == null ? "<none>" : sprite.name) +
                    " texture=" +
                    (sprite == null || sprite.texture == null
                        ? "<none>"
                        : sprite.texture.name) +
                    " rect=" +
                    (sprite == null ? Rect.zero : sprite.rect) +
                    " ppu=" +
                    (sprite == null ? 0f : sprite.pixelsPerUnit) +
                    " materials=" + materialNames);
            }
            foreach (Transform item in instance.GetComponentsInChildren<Transform>(true))
            {
                int depth = 0;
                Transform cursor = item;
                while (cursor != instance.transform) { depth++; cursor = cursor.parent; }
                string types = string.Empty;
                foreach (Component component in item.GetComponents<Component>())
                    types += component == null ? " Missing" : " " + component.GetType().Name;
                lines.Add(new string(' ', depth * 2) + item.name + " [" + types.Trim() + "]");
            }
            UnityEngine.Object.DestroyImmediate(instance);
            bundle.Unload(true);
            foreach (AssetBundle dependency in dependencies)
                dependency.Unload(true);
        }
        File.WriteAllLines(
            "/private/tmp/eden_effect_nonparticle_hierarchy.txt",
            lines.ToArray());
        Debug.Log("EDEN_EFFECT_HIERARCHY_OK");
    }

    public static void Inspect11202009Particles()
    {
        string bundlePath = Path.Combine(BundleRoot, "eft_fx_mainback_11202009.aab");
        AssetBundle bundle = LoadBundle(bundlePath);
        GameObject instance = UnityEngine.Object.Instantiate(FindPrefab(bundle));
        List<string> lines = new List<string>();
        foreach (ParticleSystem particle in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particle.main;
            ParticleSystem.EmissionModule emission = particle.emission;
            ParticleSystem.ShapeModule shape = particle.shape;
            ParticleSystemRenderer renderer = particle.GetComponent<ParticleSystemRenderer>();
            string materials = string.Empty;
            if (renderer != null)
                foreach (Material material in renderer.sharedMaterials)
                    materials += (materials.Length == 0 ? string.Empty : ",") +
                        (material == null ? "null" : material.name + "[" +
                         (material.shader == null ? "no-shader" : material.shader.name) + "]");
            lines.Add(
                ParticlePath(particle.transform, instance.transform) +
                " active=" + particle.gameObject.activeInHierarchy +
                " renderer=" + (renderer != null && renderer.enabled) +
                " position=" + particle.transform.localPosition +
                " scale=" + particle.transform.localScale +
                " lifetime=" + CurveText(main.startLifetime) +
                " speed=" + CurveText(main.startSpeed) +
                " size=" + CurveText(main.startSize) +
                " rate=" + CurveText(emission.rateOverTime) +
                " max=" + main.maxParticles +
                " shape=" + (shape.enabled ? shape.shapeType.ToString() : "off") +
                " shapeScale=" + shape.scale +
                " radius=" + shape.radius +
                " renderMode=" + (renderer == null ? "none" : renderer.renderMode.ToString()) +
                " materials=" + materials);
        }
        File.WriteAllLines("/private/tmp/eden_11202009_particles.txt", lines.ToArray());
        UnityEngine.Object.DestroyImmediate(instance);
        bundle.Unload(true);
        Debug.Log("EDEN_11202009_PARTICLES_OK count=" + lines.Count);
    }

    public static void InspectNextFive()
    {
        string[] ids =
        {
            "11300043", "11300048", "11300049", "11301001", "11301002"
        };
        List<string> lines = new List<string>();
        foreach (string id in ids)
        {
            string bundlePath = ResolveBundlePath(id);
            AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
            GameObject prefab = bundle == null ? null : FindPrefab(bundle);
            if (prefab == null)
            {
                lines.Add(id + " MISSING_PREFAB");
                if (bundle != null)
                    bundle.Unload(true);
                continue;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            ParticleSystem[] particles =
                instance.GetComponentsInChildren<ParticleSystem>(true);
            Renderer[] renderers =
                instance.GetComponentsInChildren<Renderer>(true);
            lines.Add(id + " ROOT=" + instance.name +
                " particles=" + particles.Length +
                " renderers=" + renderers.Length +
                " rootPosition=" + instance.transform.localPosition +
                " rootScale=" + instance.transform.localScale);
            foreach (Renderer renderer in renderers)
            {
                string materials = string.Empty;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (materials.Length != 0)
                        materials += ",";
                    materials += material == null
                        ? "null"
                        : material.name + "[" +
                          (material.shader == null
                              ? "no-shader"
                              : material.shader.name) + "]";
                }
                lines.Add("  " + ParticlePath(
                    renderer.transform,
                    instance.transform) +
                    " type=" + renderer.GetType().Name +
                    " active=" + renderer.gameObject.activeInHierarchy +
                    " enabled=" + renderer.enabled +
                    " order=" + renderer.sortingOrder +
                    " position=" + renderer.transform.localPosition +
                    " scale=" + renderer.transform.localScale +
                    " worldPosition=" + renderer.transform.position +
                    " worldScale=" + renderer.transform.lossyScale +
                    " materials=" + materials);
            }

            UnityEngine.Object.DestroyImmediate(instance);
            bundle.Unload(true);
        }
        File.WriteAllLines(
            "/private/tmp/eden_effect_11300043_11301002_inspect.txt",
            lines.ToArray());
        Debug.Log("EDEN_EFFECT_NEXT_FIVE_INSPECT_OK");
    }

    public static void InspectProblemEffects()
    {
        string[] ids =
        {
            "11300041", "11301003", "11301004", "11301005", "11301006"
        };
        List<string> lines = new List<string>();
        foreach (string id in ids)
        {
            AssetBundle dependencyBundle = null;
            if (id == "11300034")
            {
                dependencyBundle = AssetBundle.LoadFromFile(Path.Combine(
                    ResourceBundleRoot,
                    "st_1005_sky.aab"));
                lines.Add(
                    id + " DEPENDENCY=" +
                    (dependencyBundle == null
                        ? "<missing>"
                        : string.Join(
                            ",",
                            dependencyBundle.GetAllAssetNames())));
            }
            else if (id == "11301005")
            {
                dependencyBundle = AssetBundle.LoadFromFile(Path.Combine(
                    ResourceBundleRoot,
                    "common.aab"));
                lines.Add(
                    id + " COMMON_SHADER_DEPENDENCY=" +
                    (dependencyBundle == null
                        ? "<missing>"
                        : "loaded"));
            }
            string bundlePath = Path.Combine(
                BundleRoot,
                "eft_fx_mainback_" + id + ".aab");
            AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
            GameObject prefab = bundle == null ? null : FindPrefab(bundle);
            if (prefab == null)
            {
                lines.Add(id + " MISSING_PREFAB");
                if (bundle != null)
                    bundle.Unload(true);
                if (dependencyBundle != null)
                    dependencyBundle.Unload(true);
                continue;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            lines.Add(id + " ROOT=" + instance.name);
            foreach (Transform item in
                instance.GetComponentsInChildren<Transform>(true))
            {
                string components = string.Empty;
                foreach (Component component in item.GetComponents<Component>())
                {
                    if (components.Length != 0)
                        components += ",";
                    components += component == null
                        ? "<missing>"
                        : component.GetType().FullName;
                }
                lines.Add(
                    "  OBJECT " + ParticlePath(item, instance.transform) +
                    " components=" + components);
            }
            foreach (Animator animator in
                instance.GetComponentsInChildren<Animator>(true))
            {
                RuntimeAnimatorController controller =
                    animator.runtimeAnimatorController;
                lines.Add(
                    "  ANIMATOR " + ParticlePath(
                        animator.transform,
                        instance.transform) +
                    " controller=" + (controller == null
                        ? "<none>"
                        : controller.name + "[" +
                          controller.GetType().FullName + "]"));
                if (controller == null)
                    continue;
                foreach (AnimationClip clip in controller.animationClips)
                {
                    lines.Add(
                        "    CLIP " + clip.name +
                        " length=" + clip.length +
                        " frameRate=" + clip.frameRate +
                        " legacy=" + clip.legacy +
                        " wrapMode=" + clip.wrapMode);
                    foreach (EditorCurveBinding binding in
                        AnimationUtility.GetCurveBindings(clip))
                    {
                        AnimationCurve curve =
                            AnimationUtility.GetEditorCurve(clip, binding);
                        string keys = string.Empty;
                        foreach (Keyframe key in curve.keys)
                        {
                            if (keys.Length != 0)
                                keys += ",";
                            keys += key.time + ":" + key.value;
                        }
                        lines.Add(
                            "      CURVE path=" + binding.path +
                            " type=" + binding.type.FullName +
                            " property=" + binding.propertyName +
                            " keys=" + keys);
                    }
                    foreach (EditorCurveBinding binding in
                        AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    {
                        string keys = string.Empty;
                        foreach (ObjectReferenceKeyframe key in
                            AnimationUtility.GetObjectReferenceCurve(
                                clip,
                                binding))
                        {
                            if (keys.Length != 0)
                                keys += ",";
                            keys += key.time + ":" +
                                (key.value == null
                                    ? "<null>"
                                    : key.value.name);
                        }
                        lines.Add(
                            "      OBJECT_CURVE path=" + binding.path +
                            " type=" + binding.type.FullName +
                            " property=" + binding.propertyName +
                            " keys=" + keys);
                    }
                    if (id == "11301005")
                        Inspect11301005AnimationSamples(
                            lines,
                            instance,
                            clip);
                }
            }
            HashSet<Material> inspectedMaterials = new HashSet<Material>();
            foreach (ParticleSystem particle in
                instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particle.main;
                ParticleSystem.EmissionModule emission = particle.emission;
                ParticleSystem.ShapeModule shape = particle.shape;
                ParticleSystem.VelocityOverLifetimeModule velocity =
                    particle.velocityOverLifetime;
                ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
                    particle.sizeOverLifetime;
                ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                    particle.colorOverLifetime;
                ParticleSystemRenderer renderer =
                    particle.GetComponent<ParticleSystemRenderer>();
                string materials = string.Empty;
                if (renderer != null)
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (materials.Length != 0)
                            materials += ",";
                        materials += MaterialText(material);
                        if (material != null)
                            inspectedMaterials.Add(material);
                    }
                lines.Add(
                    "  PARTICLE " + ParticlePath(
                        particle.transform,
                        instance.transform) +
                    " activeSelf=" + particle.gameObject.activeSelf +
                    " active=" + particle.gameObject.activeInHierarchy +
                    " position=" + particle.transform.localPosition +
                    " rotation=" + particle.transform.localEulerAngles +
                    " scale=" + particle.transform.localScale +
                    " loop=" + main.loop +
                    " lifetime=" + CurveText(main.startLifetime) +
                    " speed=" + CurveText(main.startSpeed) +
                    " size=" + CurveText(main.startSize) +
                    " color=" + GradientText(main.startColor) +
                    " rate=" + CurveText(emission.rateOverTime) +
                    " max=" + main.maxParticles +
                    " shape=" + (shape.enabled
                        ? shape.shapeType.ToString()
                        : "off") +
                    " shapeScale=" + shape.scale +
                    " radius=" + shape.radius +
                    " velocity=" + (velocity.enabled
                        ? "x=" + CurveText(velocity.x) +
                          " y=" + CurveText(velocity.y) +
                          " z=" + CurveText(velocity.z)
                        : "off") +
                    " sizeOverLifetime=" + sizeOverLifetime.enabled +
                    " colorOverLifetime=" + colorOverLifetime.enabled +
                    " renderMode=" + (renderer == null
                        ? "none"
                        : renderer.renderMode.ToString()) +
                    " order=" + (renderer == null
                        ? 0
                        : renderer.sortingOrder) +
                    " rendererEnabled=" + (renderer != null &&
                        renderer.enabled) +
                    " mesh=" + (renderer != null && renderer.mesh != null
                        ? renderer.mesh.name
                        : "<none>") +
                    " materials=" + materials);
            }

            foreach (MeshRenderer renderer in
                instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                string materials = string.Empty;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (materials.Length != 0)
                        materials += ",";
                    materials += MaterialText(material);
                    if (material != null)
                        inspectedMaterials.Add(material);
                }
                lines.Add(
                    "  MESH " + ParticlePath(
                        renderer.transform,
                        instance.transform) +
                    " activeSelf=" + renderer.gameObject.activeSelf +
                    " active=" + renderer.gameObject.activeInHierarchy +
                    " enabled=" + renderer.enabled +
                    " position=" + renderer.transform.localPosition +
                    " rotation=" + renderer.transform.localEulerAngles +
                    " scale=" + renderer.transform.localScale +
                    " order=" + renderer.sortingOrder +
                    " materials=" + materials);
            }
            foreach (SpriteRenderer renderer in
                instance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Sprite sprite = renderer.sprite;
                lines.Add(
                    "  SPRITE " + ParticlePath(
                        renderer.transform,
                        instance.transform) +
                    " activeSelf=" + renderer.gameObject.activeSelf +
                    " enabled=" + renderer.enabled +
                    " position=" + renderer.transform.localPosition +
                    " scale=" + renderer.transform.localScale +
                    " order=" + renderer.sortingOrder +
                    " sprite=" + (sprite == null
                        ? "<none>"
                        : sprite.name) +
                    " texture=" + (sprite == null ||
                        sprite.texture == null
                        ? "<none>"
                        : sprite.texture.name));
            }
            foreach (Material material in inspectedMaterials)
                lines.Add("  MATERIAL_PROPERTIES " +
                    MaterialPropertiesText(material));

            UnityEngine.Object.DestroyImmediate(instance);
            bundle.Unload(true);
            if (dependencyBundle != null)
                dependencyBundle.Unload(true);
        }
        File.WriteAllLines(
            "/private/tmp/eden_effect_trial_five_inspect.txt",
            lines.ToArray());
        Debug.Log("EDEN_EFFECT_TRIAL_FIVE_INSPECT_OK");
    }

    private static void Inspect11301005AnimationSamples(
        List<string> lines,
        GameObject instance,
        AnimationClip clip)
    {
        float[] sampleTimes =
        {
            0f,
            0.25f,
            0.5f,
            1f,
            2f,
            3f,
            4f,
            4.999f
        };
        Transform mesh = FindChildByName(
            instance.transform,
            "Mesh_mainback_xiao");
        if (mesh == null)
        {
            lines.Add("      SAMPLE missing Mesh_mainback_xiao");
            return;
        }
        MeshRenderer renderer = mesh.GetComponent<MeshRenderer>();
        foreach (float time in sampleTimes)
        {
            clip.SampleAnimation(instance, time);
            Material material = renderer == null
                ? null
                : renderer.material;
            MaterialPropertyBlock propertyBlock =
                new MaterialPropertyBlock();
            if (renderer != null)
                renderer.GetPropertyBlock(propertyBlock);
            lines.Add(
                "      SAMPLE t=" + time +
                " active=" + mesh.gameObject.activeSelf +
                " enabled=" + (renderer != null && renderer.enabled) +
                " position=" + mesh.localPosition +
                " rotation=" + mesh.localEulerAngles +
                " scale=" + mesh.localScale +
                " mainOffset=" + ReadMaterialOffset(
                    material,
                    "_MainTex") +
                " maskOffset=" + ReadMaterialOffset(
                    material,
                    "_MaskTex") +
                " mainScale=" + ReadMaterialScale(
                    material,
                    "_MainTex") +
                " maskScale=" + ReadMaterialScale(
                    material,
                    "_MaskTex") +
                " blockMainST=" + propertyBlock.GetVector(
                    "_MainTex_ST") +
                " blockMaskST=" + propertyBlock.GetVector(
                    "_MaskTex_ST") +
                " dissolve=" + ReadMaterialFloat(
                    material,
                    "_DissolveVal"));
            foreach (Transform item in
                instance.GetComponentsInChildren<Transform>(true))
            {
                Renderer itemRenderer = item.GetComponent<Renderer>();
                lines.Add(
                    "        SAMPLE_OBJECT " +
                    ParticlePath(item, instance.transform) +
                    " active=" + item.gameObject.activeSelf +
                    " position=" + item.localPosition +
                    " rotation=" + item.localEulerAngles +
                    " scale=" + item.localScale +
                    " renderer=" +
                    (itemRenderer == null
                        ? "<none>"
                        : itemRenderer.enabled.ToString()));
            }
        }
    }

    private static Transform FindChildByName(
        Transform root,
        string name)
    {
        foreach (Transform item in
            root.GetComponentsInChildren<Transform>(true))
            if (string.Equals(
                item.name,
                name,
                StringComparison.Ordinal))
                return item;
        return null;
    }

    private static Vector2 ReadMaterialOffset(
        Material material,
        string property)
    {
        return material != null && material.HasProperty(property)
            ? material.GetTextureOffset(property)
            : Vector2.zero;
    }

    private static Vector2 ReadMaterialScale(
        Material material,
        string property)
    {
        return material != null && material.HasProperty(property)
            ? material.GetTextureScale(property)
            : Vector2.one;
    }

    private static float ReadMaterialFloat(
        Material material,
        string property)
    {
        return material != null && material.HasProperty(property)
            ? material.GetFloat(property)
            : float.NaN;
    }

    private static string MaterialText(Material material)
    {
        if (material == null)
            return "null";
        string result = material.name + "[" +
            (material.shader == null
                ? "no-shader"
                : material.shader.name) + "]";
        SavedTexture main = ReadTexture(material, "_MainTex", false);
        SavedTexture mask = ReadTexture(material, "_MaskTex", true);
        result += " main=" +
            (main.Texture == null ? "<none>" : main.Texture.name);
        if (mask.Texture != null)
            result += " mask=" + mask.Texture.name;
        if (ReadColor(material, "_TintColor", out Color tint) ||
            ReadColor(material, "_Color", out tint))
            result += " tint=" + tint;
        return result;
    }

    private static string MaterialPropertiesText(Material material)
    {
        string result = material.name + " shader=" +
            (material.shader == null ? "<none>" : material.shader.name);
        SerializedObject serialized = new SerializedObject(material);
        SerializedProperty floats = serialized.FindProperty(
            "m_SavedProperties.m_Floats");
        if (floats != null && floats.isArray)
            for (int i = 0; i < floats.arraySize; i++)
            {
                SerializedProperty entry = floats.GetArrayElementAtIndex(i);
                SerializedProperty key = entry.FindPropertyRelative("first");
                SerializedProperty value = entry.FindPropertyRelative("second");
                if (key != null && value != null)
                    result += " float:" + key.stringValue + "=" +
                        value.floatValue;
            }
        SerializedProperty colors = serialized.FindProperty(
            "m_SavedProperties.m_Colors");
        if (colors != null && colors.isArray)
            for (int i = 0; i < colors.arraySize; i++)
            {
                SerializedProperty entry = colors.GetArrayElementAtIndex(i);
                SerializedProperty key = entry.FindPropertyRelative("first");
                SerializedProperty value = entry.FindPropertyRelative("second");
                if (key != null && value != null)
                    result += " color:" + key.stringValue + "=" +
                        value.colorValue;
            }
        SerializedProperty textures = serialized.FindProperty(
            "m_SavedProperties.m_TexEnvs");
        if (textures != null && textures.isArray)
            for (int i = 0; i < textures.arraySize; i++)
            {
                SerializedProperty entry = textures.GetArrayElementAtIndex(i);
                SerializedProperty key = entry.FindPropertyRelative("first");
                SerializedProperty value = entry.FindPropertyRelative("second");
                SerializedProperty texture = value == null
                    ? null
                    : value.FindPropertyRelative("m_Texture");
                SerializedProperty scale = value == null
                    ? null
                    : value.FindPropertyRelative("m_Scale");
                SerializedProperty offset = value == null
                    ? null
                    : value.FindPropertyRelative("m_Offset");
                if (key != null)
                    result += " tex:" + key.stringValue + "=" +
                        (texture == null || texture.objectReferenceValue == null
                            ? "<none>"
                            : texture.objectReferenceValue.name) +
                        " scale=" + (scale == null
                            ? Vector2.one
                            : scale.vector2Value) +
                        " offset=" + (offset == null
                            ? Vector2.zero
                            : offset.vector2Value);
            }
        return result;
    }

    private static string GradientText(ParticleSystem.MinMaxGradient gradient)
    {
        return gradient.mode + "(" +
            gradient.colorMin + "," + gradient.colorMax + ")";
    }

    private static string ParticlePath(Transform item, Transform root)
    {
        string path = item.name;
        while (item.parent != null && item.parent != root)
        {
            item = item.parent;
            path = item.name + "/" + path;
        }
        return path;
    }

    private static string CurveText(ParticleSystem.MinMaxCurve curve)
    {
        return curve.mode + "(" + curve.constantMin + "," + curve.constantMax + ")";
    }

    public static void Run()
    {
        RunExport(
            Ids,
            "Eden effect trial import 11300041, 11301003 through 11301006",
            "EDEN_EFFECT_BATCH_IMPORT_OK");
    }

    public static void RunVisualFixes()
    {
        RunExport(
            new[] { "11301003", "11301005" },
            "Eden effect visual fixes for 11301003 and 11301005",
            "EDEN_EFFECT_VISUAL_FIX_OK");
    }

    public static void RunSupplements()
    {
        RunExport(
            SupplementIds,
            "Eden effect supplements for four missing roles and 11301023",
            "EDEN_EFFECT_SUPPLEMENTS_OK");
    }

    private static void RunExport(
        string[] exportIds,
        string reportTitle,
        string successMarker)
    {
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CacheTextures();
            Report.Clear();
            failures = 0;
            Report.Add(reportTitle);
            foreach (string id in exportIds)
                Export(id);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate(exportIds);
            WriteReport();
            if (failures != 0)
                throw new InvalidOperationException("Effect import failures=" + failures);
            Debug.Log(successMarker);
        }
        catch (Exception exception)
        {
            Report.Add("FATAL " + exception);
            WriteReport();
            Debug.LogException(exception);
            throw;
        }
    }

    private static void Export(string id)
    {
        currentEffectId = id;
        string destination = EffectRoot + "/" + id;
        if (AssetDatabase.IsValidFolder(destination))
            AssetDatabase.DeleteAsset(destination);
        EnsureFolder(destination + "/Materials");
        EnsureFolder(destination + "/Meshes");
        EnsureFolder(destination + "/Sprites");
        materialIndex = 0;
        meshIndex = 0;

        string bundlePath = ResolveBundlePath(id);
        AssetBundle shaderDependency = null;
        if (id == "11301005")
            shaderDependency = AssetBundle.LoadFromFile(Path.Combine(
                ResourceBundleRoot,
                "common.aab"));
        AssetBundle bundle = LoadBundle(bundlePath);
        if (bundle == null)
        {
            if (shaderDependency != null)
                shaderDependency.Unload(true);
            throw new FileNotFoundException("Cannot load effect bundle", bundlePath);
        }

        GameObject sourceInstance = null;
        GameObject cleanRoot = null;
        AssetBundle spriteDependency = null;
        try
        {
            if (id == "11202017")
            {
                string dependencyPath = Path.Combine(
                    ResourceBundleRoot,
                    "st_cardshowspine_11202017_bg.aab");
                spriteDependency = AssetBundle.LoadFromFile(dependencyPath);
            }
            GameObject sourcePrefab = FindPrefab(bundle);
            if (sourcePrefab == null)
                throw new InvalidOperationException("No prefab in " + bundlePath);
            sourceInstance = UnityEngine.Object.Instantiate(sourcePrefab);
            cleanRoot = new GameObject("FX_MainBack_" + id + "_Effect");
            CopyRootComponents(sourceInstance, cleanRoot);

            List<Transform> children = new List<Transform>();
            for (int i = 0; i < sourceInstance.transform.childCount; i++)
                children.Add(sourceInstance.transform.GetChild(i));
            foreach (Transform child in children)
                child.SetParent(cleanRoot.transform, false);

            PruneGalleryObjects(cleanRoot.transform);
            DisableUnsupportedEffectRenderers(id, cleanRoot);
            RestoreSourceDepthOrders(id, cleanRoot);
            RemoveBehaviours(cleanRoot);
            NormalizeWideGlow(cleanRoot.transform);
            PersistMeshes(cleanRoot, destination + "/Meshes");
            PersistMaterials(cleanRoot, destination + "/Materials", BaseOrders[id]);
            PersistSprites(cleanRoot, destination + "/Sprites");
            cleanRoot.transform.localPosition = Vector3.zero;
            cleanRoot.transform.localRotation = Quaternion.identity;
            cleanRoot.transform.localScale = Vector3.one * 0.01f;

            string prefabPath = destination + "/" + cleanRoot.name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(cleanRoot, prefabPath);
            Report.Add(id + " EXPORT particles=" +
                cleanRoot.GetComponentsInChildren<ParticleSystem>(true).Length +
                " renderers=" + cleanRoot.GetComponentsInChildren<Renderer>(true).Length +
                " materials=" + materialIndex + " meshes=" + meshIndex);
        }
        finally
        {
            if (cleanRoot != null)
                UnityEngine.Object.DestroyImmediate(cleanRoot);
            if (sourceInstance != null)
                UnityEngine.Object.DestroyImmediate(sourceInstance);
            bundle.Unload(true);
            if (spriteDependency != null)
                spriteDependency.Unload(true);
            if (shaderDependency != null)
                shaderDependency.Unload(true);
        }
    }

    private static void CopyRootComponents(GameObject source, GameObject target)
    {
        foreach (Component sourceComponent in source.GetComponents<Component>())
        {
            if (sourceComponent == null || sourceComponent is Transform ||
                sourceComponent is MonoBehaviour || sourceComponent is Animator)
                continue;
            Type type = sourceComponent.GetType();
            Component targetComponent = target.GetComponent(type);
            if (targetComponent == null)
                targetComponent = target.AddComponent(type);
            EditorUtility.CopySerialized(sourceComponent, targetComponent);
        }
    }

    private static GameObject FindPrefab(AssetBundle bundle)
    {
        GameObject[] objects = bundle.LoadAllAssets<GameObject>();
        foreach (GameObject item in objects)
        {
            if (item.name.IndexOf("FX_MainBack", StringComparison.OrdinalIgnoreCase) >= 0)
                return item;
        }
        return objects.Length == 0 ? null : objects[0];
    }

    private static void PruneGalleryObjects(Transform root)
    {
        List<GameObject> remove = new List<GameObject>();
        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
        {
            if (item == root)
                continue;
            string name = item.name.Trim().ToLowerInvariant();
            if (name.StartsWith("cardshowspine_") || name == "cankao" ||
                name == "role" || name == "role (1)" || name == "bg" ||
                name == "bg1" || name == "bg2" || name == "bg3" ||
                name == "11300046_bg" ||
                name == "background" || name == "reference" ||
                name == "eventsystem")
                remove.Add(item.gameObject);
        }
        for (int i = remove.Count - 1; i >= 0; i--)
            if (remove[i] != null)
                UnityEngine.Object.DestroyImmediate(remove[i]);

        // The source prefabs also contain inactive helper sprites used only by
        // the original game's controller. Gallery playback never activates
        // SpriteRenderers, so retaining them only creates unresolved
        // Sprites-Default dependencies in the exported prefab.
        foreach (SpriteRenderer renderer in
            root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.gameObject.activeSelf)
                continue;
            Report.Add(currentEffectId + " REMOVED_INACTIVE_HELPER_SPRITE object=" +
                renderer.gameObject.name);
            UnityEngine.Object.DestroyImmediate(renderer);
        }
    }

    private static void RemoveBehaviours(GameObject root)
    {
        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
        {
            SerializedObject serialized =
                new SerializedObject(item.gameObject);
            SerializedProperty components =
                serialized.FindProperty("m_Component");
            int removed = 0;
            if (components != null && components.isArray)
                for (int i = components.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty component =
                        components.GetArrayElementAtIndex(i)
                            .FindPropertyRelative("component");
                    if (component != null &&
                        component.objectReferenceValue == null)
                    {
                        components.DeleteArrayElementAtIndex(i);
                        removed++;
                    }
                }
            if (removed > 0)
                serialized.ApplyModifiedPropertiesWithoutUndo();
            if (removed > 0)
                Report.Add(currentEffectId +
                    " REMOVED_MISSING_BEHAVIOUR object=" +
                    item.gameObject.name + " count=" + removed);
        }
        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            UnityEngine.Object.DestroyImmediate(animator);
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != null)
                UnityEngine.Object.DestroyImmediate(behaviour);
    }

    private static void DisableUnsupportedEffectRenderers(string id, GameObject root)
    {
        if (id == "11300048")
        {
            Transform dustSpine = root.transform.Find(
                "FX_MainBack_11300048_grp/huichen");
            if (dustSpine != null)
            {
                Report.Add(
                    id + " REMOVED_UNRESOLVED_DUST_SPINE object=" +
                    dustSpine.name);
                UnityEngine.Object.DestroyImmediate(dustSpine.gameObject);
            }
        }

        List<GameObject> unsupportedCasterMasks = new List<GameObject>();
        foreach (MeshRenderer renderer in
            root.GetComponentsInChildren<MeshRenderer>(true))
        {
            bool unsupported =
                id == "11202020" &&
                renderer.gameObject.name.IndexOf(
                    "Mesh_low_plane",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            foreach (Material material in renderer.sharedMaterials)
                if (material != null &&
                    ((material.shader != null &&
                      material.shader.name.IndexOf(
                          "HDCaster_Mask",
                          StringComparison.OrdinalIgnoreCase) >= 0) ||
                     (id == "11202020" &&
                      material.name.IndexOf(
                          "FX_MainBack_11202020_dis",
                          StringComparison.OrdinalIgnoreCase) >= 0)))
                        unsupported = true;
            if (!unsupported)
                continue;
            unsupportedCasterMasks.Add(renderer.gameObject);
            Report.Add(id + " REMOVED_UNSUPPORTED_CASTER_MASK object=" +
                renderer.gameObject.name);
        }
        for (int i = unsupportedCasterMasks.Count - 1; i >= 0; i--)
            if (unsupportedCasterMasks[i] != null)
                UnityEngine.Object.DestroyImmediate(unsupportedCasterMasks[i]);

        if (id == "11300002")
        {
            const int backgroundEffectOrder = 127;
            const int foregroundEffectOrder = 145;
            const int fistEffectOrder = 160;
            List<Renderer> behindCharacter = new List<Renderer>();
            float maximumDepth = 1f;
            foreach (Renderer renderer in
                root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject.name == "Mesh_mainback_bulijite")
                {
                    // The golden fist is the only effect that crosses in front
                    // of the character (gallery character order is 147).
                    renderer.sortingOrder = fistEffectOrder;
                    Report.Add(
                        id + " FIST_FOREGROUND object=" +
                        renderer.gameObject.name +
                        " order=" + renderer.sortingOrder);
                    continue;
                }
                behindCharacter.Add(renderer);
                maximumDepth = Mathf.Max(
                    maximumDepth,
                    Mathf.Max(0f, -renderer.transform.localPosition.z));
            }
            foreach (Renderer renderer in behindCharacter)
            {
                float depth = Mathf.Max(
                    0f,
                    -renderer.transform.localPosition.z);
                renderer.sortingOrder = Mathf.RoundToInt(Mathf.Lerp(
                    backgroundEffectOrder,
                    foregroundEffectOrder,
                    depth / maximumDepth));
                Report.Add(
                    id + " BEHIND_CHARACTER object=" +
                    renderer.gameObject.name +
                    " order=" + renderer.sortingOrder);
            }
        }

        if (id == "11300037")
        {
            foreach (Renderer renderer in
                root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sortingOrder = Mathf.Max(
                    renderer.sortingOrder,
                    Mathf.RoundToInt(Mathf.Max(
                        0f,
                        -renderer.transform.localPosition.z)));
                Report.Add(
                    id + " RESTORED_DEPTH_ORDER object=" +
                    renderer.gameObject.name +
                    " order=" + renderer.sortingOrder);
            }
        }

        if (id == "11202009")
        {
            foreach (ParticleSystemRenderer renderer in
                root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                bool smokeLayer = renderer.gameObject.name == "Particle System (2)";
                bool glowingMotes = renderer.gameObject.name == "Particle System (7)";
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null &&
                        material.name.IndexOf("sfx_tx_smoke_005", StringComparison.OrdinalIgnoreCase) >= 0)
                        smokeLayer = true;

                if (glowingMotes)
                {
                    ParticleSystem particle = renderer.GetComponent<ParticleSystem>();
                    ParticleSystem.MainModule main = particle.main;
                    ParticleSystem.EmissionModule emission = particle.emission;
                    ParticleSystem.VelocityOverLifetimeModule velocity =
                        particle.velocityOverLifetime;
                    main.maxParticles = 12;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.012f);
                    emission.rateOverTime = 2.4f;
                    velocity.enabled = true;
                    velocity.x = new ParticleSystem.MinMaxCurve(-0.003f, 0.003f);
                    velocity.y = new ParticleSystem.MinMaxCurve(0.002f, 0.006f);
                    renderer.sortingOrder = 2;
                    Report.Add(id + " SPARSE_FLOATING_MOTES object=" + renderer.gameObject.name);
                }

                if (!smokeLayer)
                    continue;

                // The reference keeps only a very thin morning mist near the
                // lower forest. The source values are tuned for the original
                // screen composition and become dense dust clouds in the gallery.
                renderer.gameObject.SetActive(true);
                ParticleSystem mist = renderer.GetComponent<ParticleSystem>();
                ParticleSystem.MainModule mistMain = mist.main;
                ParticleSystem.EmissionModule mistEmission = mist.emission;
                mistMain.maxParticles = 3;
                mistEmission.rateOverTime = 0.65f;
                Report.Add(id + " REDUCED_MORNING_MIST object=" + renderer.gameObject.name);
            }
        }

        if (id == "11300008")
        {
            List<GameObject> helperMasks = new List<GameObject>();
            foreach (MeshRenderer renderer in
                root.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool helperMask =
                    renderer.gameObject.name.IndexOf(
                        "Mesh_low_plane",
                        StringComparison.OrdinalIgnoreCase) >= 0;
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null &&
                        (material.name.IndexOf(
                            "FX_MainBack_11300008_dis",
                            StringComparison.OrdinalIgnoreCase) >= 0 ||
                         (material.shader != null &&
                          material.shader.name.IndexOf(
                              "HDCaster_Mask",
                              StringComparison.OrdinalIgnoreCase) >= 0)))
                        helperMask = true;
                if (!helperMask)
                    continue;
                helperMasks.Add(renderer.gameObject);
                Report.Add(id + " REMOVED_HELPER_MASK object=" +
                    renderer.gameObject.name);
            }
            for (int i = helperMasks.Count - 1; i >= 0; i--)
                if (helperMasks[i] != null)
                    UnityEngine.Object.DestroyImmediate(helperMasks[i]);

            foreach (ParticleSystemRenderer renderer in
                root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                bool sequenceSparkles = false;
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null &&
                        material.name.IndexOf(
                            "sequence_12_w",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                        sequenceSparkles = true;
                if (!sequenceSparkles)
                    continue;
                ParticleSystem.MainModule main =
                    renderer.GetComponent<ParticleSystem>().main;
                ParticleSystem.MinMaxCurve sourceSize = main.startSize;
                if (sourceSize.mode == ParticleSystemCurveMode.TwoConstants)
                    main.startSize = new ParticleSystem.MinMaxCurve(
                        sourceSize.constantMin * 0.65f,
                        sourceSize.constantMax * 0.65f);
                else
                    main.startSizeMultiplier *= 0.65f;
                Report.Add(id + " REDUCED_SEQUENCE_SPARKLES object=" +
                    renderer.gameObject.name +
                    " min=" + main.startSize.constantMin +
                    " max=" + main.startSize.constantMax);
            }
        }

        if (id == "11300009")
        {
            List<GameObject> uiHelpers = new List<GameObject>();
            foreach (ParticleSystemRenderer renderer in
                root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                bool uiHelper = false;
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null &&
                        material.name.IndexOf(
                            "sfx_ui_getmessage",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                        uiHelper = true;
                if (!uiHelper)
                    continue;
                uiHelpers.Add(renderer.gameObject);
                Report.Add(id + " REMOVED_UI_HELPER_PARTICLE object=" +
                    renderer.gameObject.name);
            }
            for (int i = uiHelpers.Count - 1; i >= 0; i--)
                if (uiHelpers[i] != null)
                    UnityEngine.Object.DestroyImmediate(uiHelpers[i]);
        }

        if (id == "11300022")
        {
            List<GameObject> unsupportedDistortionMasks = new List<GameObject>();
            foreach (MeshRenderer renderer in
                root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.gameObject.name.IndexOf(
                        "Mesh_low_plane",
                        StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                unsupportedDistortionMasks.Add(renderer.gameObject);
                Report.Add(id + " REMOVED_UNSUPPORTED_DISTORTION_MASK object=" +
                    renderer.gameObject.name);
            }
            for (int i = unsupportedDistortionMasks.Count - 1; i >= 0; i--)
                if (unsupportedDistortionMasks[i] != null)
                    UnityEngine.Object.DestroyImmediate(
                        unsupportedDistortionMasks[i]);
        }

        if (id != "11202017")
            return;

        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null ||
                    material.name.IndexOf("sfx_ronghou_mask", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                // This texture is a helper mask consumed by the original game's
                // custom shader. Rendering it directly with the fallback additive
                // shader creates an opaque white, full-screen plane.
                renderer.enabled = false;
                Report.Add(id + " DISABLED_HELPER_MASK object=" + renderer.gameObject.name);
                break;
            }
        }
    }

    private static void RestoreSourceDepthOrders(string id, GameObject root)
    {
        if (id != "11300043" && id != "11300048" &&
            id != "11300049" && id != "11301001" &&
            id != "11301002" && id != "11300041" &&
            id != "11301003" && id != "11301004" &&
            id != "11301005" && id != "11301006" &&
            id != "11202020" && id != "11300045" &&
            id != "11300046" && id != "11300055" &&
            id != "11301023")
            return;

        foreach (Renderer renderer in
            root.GetComponentsInChildren<Renderer>(true))
        {
            int sourceOrder = renderer.sortingOrder;
            int depthOrder = Mathf.RoundToInt(
                Mathf.Max(0f, -renderer.transform.position.z));
            renderer.sortingOrder = depthOrder + sourceOrder;
            Report.Add(
                id + " RESTORED_SOURCE_DEPTH object=" +
                ParticlePath(renderer.transform, root.transform) +
                " depth=" + depthOrder +
                " sourceOrder=" + sourceOrder +
                " order=" + renderer.sortingOrder);
        }
    }

    private static void NormalizeWideGlow(Transform root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            bool wideGlow = false;
            foreach (Material material in renderer.sharedMaterials)
                if (material != null && material.name.ToLowerInvariant().Contains("sfx_tx_371"))
                    wideGlow = true;
            Vector3 position = renderer.transform.localPosition;
            if (wideGlow && position.z <= -1000f)
            {
                position.z = -166f;
                renderer.transform.localPosition = position;
            }
        }
    }

    private static void PersistMeshes(GameObject root, string folder)
    {
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null)
                filter.sharedMesh = SaveMesh(filter.sharedMesh, folder, filter.gameObject.name);
        foreach (ParticleSystemRenderer renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh != null)
                renderer.mesh = SaveMesh(renderer.mesh, folder, renderer.gameObject.name + "_particle");
            else if (renderer.renderMode != ParticleSystemRenderMode.Mesh && renderer.mesh != null)
            {
                // AssetBundle renderers can keep an embedded mesh PPtr even
                // when they render billboards. Once saved as a standalone
                // prefab that PPtr becomes a zero-GUID warning and is unused.
                renderer.mesh = null;
                EditorUtility.SetDirty(renderer);
            }
        }
    }

    private static Mesh SaveMesh(Mesh source, string folder, string owner)
    {
        Mesh copy = UnityEngine.Object.Instantiate(source);
        copy.name = source.name;
        if (currentEffectId == "11301005" &&
            owner.IndexOf(
                "Mesh_mainback_xiao",
                StringComparison.OrdinalIgnoreCase) >= 0 &&
            copy.uv2.Length == 0 &&
            copy.uv.Length == copy.vertexCount)
        {
            // The APK Mask Additive shader reads TEXCOORD1, while this source
            // mesh only stores TEXCOORD0. The original render-holder binds a
            // scrolling beam mask, so duplicate the ribbon UVs for that mask.
            copy.uv2 = copy.uv;
            Report.Add(
                currentEffectId +
                " DUPLICATED_MASK_UV object=" + owner +
                " vertices=" + copy.vertexCount);
        }
        AssetDatabase.CreateAsset(copy, folder + "/" + Clean(owner + "_" + source.name + "_" + (++meshIndex)) + ".asset");
        return copy;
    }

    private static void PersistSprites(GameObject root, string folder)
    {
        int spriteIndex = 0;
        foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Sprite source = renderer.sprite;
            if (source == null)
            {
                // The source bundle can retain an unresolved external sprite PPtr.
                // Gallery backgrounds are already rendered by gallery.json, so an
                // unresolved duplicate background sprite must be cleared explicitly.
                renderer.sprite = null;
                EditorUtility.SetDirty(renderer);
                continue;
            }
            Texture2D texture = null;
            if (renderer.sharedMaterial != null)
                texture = renderer.sharedMaterial.mainTexture as Texture2D;
            if (texture == null && source.texture != null)
                texture = ResolveTexture(source.texture, source.name, false);
            if (texture == null)
            {
                failures++;
                Report.Add("MISSING_SPRITE_TEXTURE object=" + renderer.gameObject.name +
                    " sprite=" + source.name);
                renderer.sprite = null;
                continue;
            }
            Rect rect = source.rect;
            rect.x = Mathf.Clamp(rect.x, 0f, texture.width - 1f);
            rect.y = Mathf.Clamp(rect.y, 0f, texture.height - 1f);
            rect.width = Mathf.Clamp(rect.width, 1f, texture.width - rect.x);
            rect.height = Mathf.Clamp(rect.height, 1f, texture.height - rect.y);
            Vector2 pivot = new Vector2(
                source.pivot.x / Mathf.Max(1f, source.rect.width),
                source.pivot.y / Mathf.Max(1f, source.rect.height));
            Sprite copy = Sprite.Create(texture, rect, pivot, source.pixelsPerUnit);
            copy.name = source.name;
            string path = folder + "/" + Clean(renderer.gameObject.name + "_" +
                source.name + "_" + (++spriteIndex)) + ".asset";
            AssetDatabase.CreateAsset(copy, path);
            renderer.sprite = copy;
        }
    }

    private static void PersistMaterials(GameObject root, string folder, int baseOrder)
    {
        Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder += baseOrder;
            Material[] source = renderer.sharedMaterials;
            Material[] target = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null)
                    continue;
                if (!copies.TryGetValue(source[i], out target[i]))
                {
                    target[i] = ExportMaterial(source[i], folder);
                    copies.Add(source[i], target[i]);
                }
            }
            renderer.sharedMaterials = target;
            ParticleSystemRenderer particle = renderer as ParticleSystemRenderer;
            if (particle != null && particle.trailMaterial != null)
            {
                Material trail = particle.trailMaterial;
                if (!copies.TryGetValue(trail, out Material trailCopy))
                {
                    trailCopy = ExportMaterial(trail, folder);
                    copies.Add(trail, trailCopy);
                }
                particle.trailMaterial = trailCopy;
            }
        }
    }

    private static Material ExportMaterial(Material source, string folder)
    {
        SavedTexture main = ReadTexture(source, "_MainTex", false);
        SavedTexture mask = ReadTexture(source, "_MaskTex", true);
        string description = (source.name + " " +
            (source.shader != null ? source.shader.name : string.Empty)).ToLowerInvariant();
        bool masked = mask.Texture != null || description.Contains("mask additive") ||
            description.Contains("maskadditive");
        bool xiaoBladeFlow = currentEffectId == "11301005" &&
            source.name.IndexOf(
                "FX_tex_xiao_liuguang_mainback",
                StringComparison.OrdinalIgnoreCase) >= 0;
        if (xiaoBladeFlow)
            masked = true;
        if (currentEffectId == "11300041" &&
            source.name.IndexOf(
                "sfx_11300041_loft",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            masked = true;
        }
        bool additive = description.Contains("additive");
        bool auraScroll = currentEffectId == "11300002" &&
            source.name.IndexOf(
                "sfx_tx_157_mainback_gold1",
                StringComparison.OrdinalIgnoreCase) >= 0;
        bool fistFlow = currentEffectId == "11300002" &&
            source.name.IndexOf(
                "sfx_tx_bulijitenengliang",
                StringComparison.OrdinalIgnoreCase) >= 0;
        bool naturalRock = currentEffectId == "11300002" &&
            source.name.IndexOf(
                "CardShowSpine_11300002_3_shitou",
                StringComparison.OrdinalIgnoreCase) >= 0;
        if (naturalRock)
            additive = false;
        if (currentEffectId == "11300009" &&
            source.name.IndexOf(
                "FX_MainBack_11300009_water",
                StringComparison.OrdinalIgnoreCase) >= 0)
            additive = true;
        bool alpha = naturalRock || (!additive &&
            (description.Contains("alpha") || description.Contains("smoke") ||
             description.Contains("sequence")));
        string shaderName = auraScroll
            ? "EdenGallery/Particles/AuraScrollAdditive"
            : fistFlow
                ? "EdenGallery/Particles/FistFlowAdditive"
            : masked ? "EdenGallery/Particles/MaskAdditive" :
            alpha ? "EdenGallery/Effects/SoulGamesAlphaBlend" :
            "EdenGallery/Particles/Additive";
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
            throw new InvalidOperationException("Missing shader " + shaderName);
        Material target = new Material(shader) { name = source.name };

        Texture2D mainTexture = ResolveTexture(main.Texture, source.name, false);
        if (currentEffectId == "11202009" &&
            source.name.IndexOf("sfx_tx_371", StringComparison.OrdinalIgnoreCase) >= 0 &&
            Textures.TryGetValue("FX_glow_022_sh", out Texture2D highlightTexture))
            mainTexture = highlightTexture;
        if (mainTexture != null)
        {
            target.SetTexture("_MainTex", mainTexture);
            target.SetTextureScale("_MainTex", main.Scale);
            target.SetTextureOffset("_MainTex", main.Offset);
        }
        else
        {
            failures++;
            Report.Add("MISSING_TEXTURE material=" + source.name + " property=_MainTex");
        }
        if (masked)
        {
            Texture2D maskTexture = ResolveTexture(
                mask.Texture,
                source.name,
                true);
            if (maskTexture != null)
            {
                target.SetTexture("_MaskTex", maskTexture);
                target.SetTextureScale("_MaskTex", mask.Scale);
                target.SetTextureOffset("_MaskTex", mask.Offset);
            }
            else
            {
                failures++;
                Report.Add("MISSING_TEXTURE material=" + source.name + " property=_MaskTex");
            }
        }

        Color tint;
        if (!ReadColor(source, "_TintColor", out tint) && !ReadColor(source, "_Color", out tint))
            tint = alpha || masked ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.5f);
        if (currentEffectId == "11202009" &&
            source.name.IndexOf("sfx_tx_371", StringComparison.OrdinalIgnoreCase) >= 0)
            tint = new Color(0.58f, 0.48f, 0.32f, 0.5f);
        if (currentEffectId == "11300009" &&
            source.name.IndexOf(
                "FX_MainBack_11300009_water",
                StringComparison.OrdinalIgnoreCase) >= 0)
            tint = new Color(0.32f, 0.32f, 0.32f, 0.65f);
        if (auraScroll)
            tint = new Color(1f, 0.76f, 0.30f, 0.24f);
        if (fistFlow)
            tint = new Color(1f, 0.78f, 0.30f, 0.46f);
        if (currentEffectId == "11300011" &&
            source.name.IndexOf(
                "sfx_tx_suidian_001",
                StringComparison.OrdinalIgnoreCase) >= 0)
            tint = new Color(1f, 0.18f, 0.34f, 0.78f);
        if (target.HasProperty("_TintColor"))
            target.SetColor("_TintColor", tint);
        if (target.HasProperty("_DeadStrength"))
        {
            float deadStrength;
            if (!ReadFloat(source, "_DeadStrength", out deadStrength))
                deadStrength = 0.01f;
            target.SetFloat("_DeadStrength", deadStrength);
        }
        if (auraScroll)
        {
            target.SetVector("_ScrollSpeed", new Vector4(0f, -0.10f, 0f, 0f));
            target.SetFloat("_PulseStrength", 0.04f);
            target.SetFloat("_PulseSpeed", 1.56f);
            target.SetFloat("_ExpandStrength", 0.16f);
            target.SetFloat("_WaveSpeed", 0.25f);
            target.SetFloat("_WaveOpacity", 0.55f);
        }
        if (fistFlow)
        {
            target.SetVector("_ScrollSpeed", new Vector4(0.25f, 0f, 0f, 0f));
            target.SetFloat("_PulseStrength", 0.10f);
            target.SetFloat("_PulseSpeed", 1.56f);
        }
        if (target.HasProperty("_EdenOpacity"))
        {
            float opacity = 1f;
            if (currentEffectId == "11202009" &&
                source.name.IndexOf("sfx_tx_smoke_005", StringComparison.OrdinalIgnoreCase) >= 0)
                opacity = 0.16f;
            target.SetFloat("_EdenOpacity", opacity);
        }

        string path = folder + "/" + Clean(source.name + "_" + (++materialIndex)) + ".mat";
        AssetDatabase.CreateAsset(target, path);
        Report.Add("  MATERIAL " + source.name + " shader=" + shaderName +
            " main=" + (mainTexture != null ? mainTexture.name : "<none>"));
        return target;
    }

    private static Texture2D ResolveTexture(Texture2D source, string materialName, bool mask)
    {
        List<string> candidates = new List<string>();
        if (source != null)
            candidates.Add(source.name);
        string lower = materialName.ToLowerInvariant();
        if (lower.Contains("sfx_mask_penquan"))
            candidates.Add(mask ? "sfx_mask_penquan" : "Beam_08_kf");
        if (lower.Contains("sfx_11300041_loft"))
            candidates.Add(mask
                ? "sfx_11300041_loft_mask"
                : "sfx_11300041_loft");
        if (lower.Contains("fx_tex_xiao_liuguang_mainback"))
            candidates.Add(mask
                ? "beam_mask004"
                : "sfx_tx_light_xiao_mainback");
        if (!mask)
        {
            candidates.Add(materialName);
            candidates.Add(StripMaterialSuffix(materialName));
            if (lower.Contains("fx_glow_001_a_sh")) candidates.Add("FX_glow_001_sh");
            if (lower.Contains("story_snow")) candidates.Add("sfx_tx_snow");
            if (lower.Contains("bloodup")) candidates.Add("glow_white005");
            if (lower.Contains("sfx_tx_157_mainback_gold1")) candidates.Add("sfx_tx_157");
            if (lower.Contains("sfx_tx_suidian_001"))
                candidates.Add("stf_tx_suidian_001");
            if (lower.Contains("fx_mainback_11300009_water"))
                candidates.Add("sfx_mainback_11300009");
            if (lower.Contains("sfx_tx_097"))
                candidates.Add("sfx_tx_097_1");
            if (lower.Contains("11301002_3_yumao"))
                candidates.Add("yumao");
            if (lower.Contains("sfx_inspace_hexin"))
                candidates.Add("flare_red_mask01");
            if (lower.Contains("sfx_tx_ui_xiao_li"))
                candidates.Add("sfx_tx_ui_xiaolizi");
        }

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrEmpty(candidate))
                continue;
            if (Textures.TryGetValue(candidate, out Texture2D existing))
                return existing;
            if (ExtractedTextures.TryGetValue(candidate, out string external))
                return ImportTexture(external);
        }

        string wanted = Normalize(StripMaterialSuffix(materialName));
        string fuzzyPath = null;
        foreach (KeyValuePair<string, string> pair in ExtractedTextures)
        {
            string available = Normalize(pair.Key);
            if (wanted.Length >= 6 && (wanted == available ||
                (available.Contains(wanted) && available.Length - wanted.Length <= 5) ||
                (wanted.Contains(available) && wanted.Length - available.Length <= 5)))
            {
                fuzzyPath = pair.Value;
                break;
            }
        }
        return fuzzyPath == null ? null : ImportTexture(fuzzyPath);
    }

    private static Texture2D ImportTexture(string sourcePath)
    {
        string fileName = Path.GetFileName(sourcePath);
        string assetPath = TextureRoot + "/" + fileName;
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string physicalPath = Path.Combine(projectRoot, assetPath);
        if (!File.Exists(physicalPath))
        {
            File.Copy(sourcePath, physicalPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            Report.Add("  IMPORT_TEXTURE " + fileName);
        }
        ConfigureTextureImporter(assetPath, Path.GetFileNameWithoutExtension(fileName));
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (texture != null)
            Textures[texture.name] = texture;
        return texture;
    }

    private static void ConfigureTextureImporter(
        string assetPath,
        string textureName)
    {
        if (!string.Equals(
            textureName,
            "sfx_main_fish",
            StringComparison.OrdinalIgnoreCase))
            return;

        TextureImporter importer =
            AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null || importer.wrapMode == TextureWrapMode.Repeat)
            return;

        importer.wrapMode = TextureWrapMode.Repeat;
        importer.SaveAndReimport();
        Report.Add("  TEXTURE_REPEAT " + textureName);
    }

    private static SavedTexture ReadTexture(Material material, string property, bool mask)
    {
        SavedTexture fallback = new SavedTexture { Scale = Vector2.one, Offset = Vector2.zero };
        if (material.HasProperty(property))
        {
            fallback.Texture = material.GetTexture(property) as Texture2D;
            fallback.Scale = material.GetTextureScale(property);
            fallback.Offset = material.GetTextureOffset(property);
            if (fallback.Texture != null)
                return fallback;
        }
        SerializedProperty list = new SerializedObject(material).FindProperty("m_SavedProperties.m_TexEnvs");
        if (list == null || !list.isArray)
            return fallback;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            SerializedProperty key = entry.FindPropertyRelative("first");
            SerializedProperty value = entry.FindPropertyRelative("second");
            if (key == null || value == null)
                continue;
            bool isMask = key.stringValue.ToLowerInvariant().Contains("mask");
            if (!string.Equals(key.stringValue, property, StringComparison.OrdinalIgnoreCase) && isMask != mask)
                continue;
            SerializedProperty texture = value.FindPropertyRelative("m_Texture");
            SerializedProperty scale = value.FindPropertyRelative("m_Scale");
            SerializedProperty offset = value.FindPropertyRelative("m_Offset");
            fallback.Texture = texture != null ? texture.objectReferenceValue as Texture2D : null;
            fallback.Scale = scale != null ? scale.vector2Value : Vector2.one;
            fallback.Offset = offset != null ? offset.vector2Value : Vector2.zero;
            if (string.Equals(key.stringValue, property, StringComparison.OrdinalIgnoreCase))
                break;
        }
        return fallback;
    }

    private static bool ReadColor(Material material, string property, out Color color)
    {
        if (material.HasProperty(property))
        {
            color = material.GetColor(property);
            return true;
        }
        SerializedProperty list = new SerializedObject(material).FindProperty("m_SavedProperties.m_Colors");
        if (list != null && list.isArray)
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                SerializedProperty key = entry.FindPropertyRelative("first");
                SerializedProperty value = entry.FindPropertyRelative("second");
                if (key != null && value != null && string.Equals(key.stringValue, property, StringComparison.OrdinalIgnoreCase))
                {
                    color = value.colorValue;
                    return true;
                }
            }
        color = Color.white;
        return false;
    }

    private static bool ReadFloat(
        Material material,
        string property,
        out float value)
    {
        if (material.HasProperty(property))
        {
            value = material.GetFloat(property);
            return true;
        }
        SerializedProperty list = new SerializedObject(material).FindProperty(
            "m_SavedProperties.m_Floats");
        if (list != null && list.isArray)
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                SerializedProperty key = entry.FindPropertyRelative("first");
                SerializedProperty savedValue =
                    entry.FindPropertyRelative("second");
                if (key != null && savedValue != null &&
                    string.Equals(
                        key.stringValue,
                        property,
                        StringComparison.OrdinalIgnoreCase))
                {
                    value = savedValue.floatValue;
                    return true;
                }
            }
        value = 0f;
        return false;
    }

    private static void CacheTextures()
    {
        Textures.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureRoot }))
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
            if (texture != null && !Textures.ContainsKey(texture.name))
                Textures.Add(texture.name, texture);
        }
        ExtractedTextures.Clear();
        string[] files = Directory.GetFiles(ExtractedTextureRoot, "*.png", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (!ExtractedTextures.ContainsKey(name) ||
                (ExtractedTextures[name].Contains(" #") && !file.Contains(" #")))
                ExtractedTextures[name] = file;
        }
        if (Directory.Exists(ApkExtractedTextureRoot))
        {
            files = Directory.GetFiles(
                ApkExtractedTextureRoot,
                "*.png",
                SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (!ExtractedTextures.ContainsKey(name) ||
                    (ExtractedTextures[name].Contains("_10000") &&
                     !file.Contains("_10000")))
                    ExtractedTextures[name] = file;
            }
        }
    }

    private static void Validate(string[] validationIds)
    {
        foreach (string id in validationIds)
        {
            string path = EffectRoot + "/" + id + "/FX_MainBack_" + id + "_Effect.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                failures++;
                Report.Add(id + " FAIL missing prefab");
                continue;
            }
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            int badComponents = 0, badMaterials = 0, badMeshes = 0;
            foreach (Transform item in instance.GetComponentsInChildren<Transform>(true))
                foreach (Component component in item.GetComponents<Component>())
                    if (component == null) badComponents++;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null && (material.shader == null ||
                        material.shader.name == "Hidden/InternalErrorShader" || material.mainTexture == null))
                        badMaterials++;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh == null) badMeshes++;
                ParticleSystemRenderer particle = renderer as ParticleSystemRenderer;
                if (particle != null && particle.renderMode == ParticleSystemRenderMode.Mesh && particle.mesh == null)
                    badMeshes++;
            }
            int particles = instance.GetComponentsInChildren<ParticleSystem>(true).Length;
            bool valid = particles > 0 && badComponents == 0 && badMaterials == 0 && badMeshes == 0;
            if (!valid) failures++;
            Report.Add(id + (valid ? " OK" : " FAIL") + " particles=" + particles +
                " badComponents=" + badComponents + " badMaterials=" + badMaterials + " badMeshes=" + badMeshes);
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static string StripMaterialSuffix(string value)
    {
        value = value.Replace(" (Instance)", string.Empty);
        while (true)
        {
            int underscore = value.LastIndexOf('_');
            if (underscore < 0) break;
            if (!int.TryParse(value.Substring(underscore + 1), out int ignored)) break;
            value = value.Substring(0, underscore);
        }
        if (value.EndsWith("_add", StringComparison.OrdinalIgnoreCase)) value = value.Substring(0, value.Length - 4);
        return value;
    }

    private static string Normalize(string value)
    {
        value = value.ToLowerInvariant().Replace("material", string.Empty).Replace("_add", string.Empty).Replace("_alpha", string.Empty);
        char[] result = new char[value.Length];
        int length = 0;
        foreach (char character in value)
            if (char.IsLetterOrDigit(character)) result[length++] = character;
        return new string(result, 0, length);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static string Clean(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
        return value.Replace('/', '_').Replace('\\', '_');
    }

    private static void WriteReport()
    {
        File.WriteAllLines(
            "/private/tmp/eden_effect_trial_five.txt",
            Report.ToArray());
    }

    private static string ResolveBundlePath(string id)
    {
        string fileName = "eft_fx_mainback_" + id + ".aab";
        string apk = Path.Combine(ApkBundleRoot, fileName);
        if (File.Exists(apk))
            return apk;

        string primary = Path.Combine(BundleRoot, fileName);
        if (File.Exists(primary))
            return primary;

        string fallback = Path.Combine(FallbackBundleRoot, fileName);
        if (File.Exists(fallback))
            return fallback;

        throw new FileNotFoundException(
            "Cannot find effect bundle in primary or fallback roots",
            primary);
    }

    private static AssetBundle LoadBundle(string bundlePath)
    {
        byte[] source = File.ReadAllBytes(bundlePath);
        byte[] signature =
        {
            (byte)'U', (byte)'n', (byte)'i', (byte)'t',
            (byte)'y', (byte)'F', (byte)'S'
        };
        int offset = -1;
        for (int i = 0; i <= source.Length - signature.Length; i++)
        {
            bool matches = true;
            for (int j = 0; j < signature.Length; j++)
                if (source[i + j] != signature[j])
                {
                    matches = false;
                    break;
                }
            if (!matches)
                continue;
            offset = i;
            break;
        }
        if (offset < 0)
            return null;
        if (offset == 0)
            return AssetBundle.LoadFromMemory(source);
        byte[] bundle = new byte[source.Length - offset];
        Buffer.BlockCopy(source, offset, bundle, 0, bundle.Length);
        return AssetBundle.LoadFromMemory(bundle);
    }

    private struct SavedTexture
    {
        public Texture2D Texture;
        public Vector2 Scale;
        public Vector2 Offset;
    }
}
