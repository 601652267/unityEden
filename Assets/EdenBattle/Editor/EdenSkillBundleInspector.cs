using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Spine;
using Spine.Unity;

namespace EdenGallery.Editor
{
    public static class EdenSkillBundleInspector
    {
        [MenuItem("Eden Gallery/Inspect Skill 11301023 Bundles")]
        public static void Inspect11301023Bundles()
        {
            InspectBundles("11301023");
        }

        [MenuItem("Eden Gallery/Inspect Skill 11301006 Bundles")]
        public static void Inspect11301006Bundles()
        {
            InspectBundles("11301006");
        }

        // Batch-friendly entry point for future characters. Set the
        // EDEN_SKILL_CARD_ID environment variable before launching Unity.
        public static void InspectConfiguredCardBundles()
        {
            string cardId = Environment.GetEnvironmentVariable(
                "EDEN_SKILL_CARD_ID");
            long parsedCardId;
            if (string.IsNullOrEmpty(cardId) || cardId.Length != 8 ||
                !long.TryParse(cardId, out parsedCardId))
            {
                throw new InvalidOperationException(
                    "EDEN_SKILL_CARD_ID must be an eight-digit card ID.");
            }
            InspectBundles(cardId);
        }

        private static void InspectBundles(string cardId)
        {
            string directory = Path.Combine(
                Application.streamingAssetsPath,
                "Skill" + cardId + "Original");
            if (!Directory.Exists(directory))
                throw new InvalidOperationException(
                    "Skill bundle directory is missing: " + directory);

            string[] paths = Directory.GetFiles(directory, "*.aab");
            Array.Sort(paths, CompareBundlePaths);
            Dictionary<string, AssetBundle> bundles =
                new Dictionary<string, AssetBundle>(
                    StringComparer.OrdinalIgnoreCase);
            List<string> lines = new List<string>();
            SkillResourceRegistry.Clear();
            try
            {
                for (int pathIndex = 0;
                     pathIndex < paths.Length;
                     pathIndex++)
                {
                    string fileName = Path.GetFileName(paths[pathIndex]);
                    AssetBundle bundle =
                        AssetBundle.LoadFromFile(paths[pathIndex]);
                    if (bundle == null)
                    {
                        lines.Add("LOAD_FAILED " + fileName);
                        continue;
                    }
                    bundles[fileName] = bundle;
                    Texture2D[] textures = bundle.LoadAllAssets<Texture2D>();
                    for (int textureIndex = 0;
                         textureIndex < textures.Length;
                         textureIndex++)
                    {
                        SkillResourceRegistry.Register(textures[textureIndex]);
                    }
                    Mesh[] meshes = bundle.LoadAllAssets<Mesh>();
                    for (int meshIndex = 0;
                         meshIndex < meshes.Length;
                         meshIndex++)
                    {
                        SkillResourceRegistry.Register(meshes[meshIndex]);
                    }
                    SkillResourceRegistry.RestoreMaterials(
                        bundle.LoadAllAssets<Material>());
                }

                lines.Add(
                    "REGISTRY bundles=" + bundles.Count +
                    " textures=" + SkillResourceRegistry.TextureCount +
                    " meshes=" + SkillResourceRegistry.MeshCount);
                List<string> coreFiles = new List<string>();
                coreFiles.Add("m_cardspine_" + cardId + ".aab");
                foreach (string fileName in bundles.Keys)
                {
                    if (fileName.StartsWith(
                            "eft_fx_" + cardId + "_",
                            StringComparison.OrdinalIgnoreCase) ||
                        fileName.StartsWith(
                            "eft_fx_timeline_" + cardId + "_",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        coreFiles.Add(fileName);
                    }
                }
                coreFiles.Sort(StringComparer.OrdinalIgnoreCase);
                for (int fileIndex = 0;
                     fileIndex < coreFiles.Count;
                     fileIndex++)
                {
                    InspectCoreBundle(
                        coreFiles[fileIndex],
                        bundles,
                        lines);
                }
            }
            finally
            {
                foreach (AssetBundle bundle in bundles.Values)
                {
                    if (bundle != null)
                        bundle.Unload(true);
                }
                SkillResourceRegistry.Clear();
            }

            string reportPath =
                "/private/tmp/eden_skill_" + cardId + "_inspect.txt";
            File.WriteAllLines(reportPath, lines.ToArray());
            Debug.Log(
                "EDEN_SKILL_" + cardId +
                "_INSPECT_OK lines=" + lines.Count +
                " report=" + reportPath);
        }

        private static void InspectCoreBundle(
            string fileName,
            Dictionary<string, AssetBundle> bundles,
            List<string> lines)
        {
            AssetBundle bundle;
            if (!bundles.TryGetValue(fileName, out bundle) || bundle == null)
            {
                lines.Add("CORE_MISSING " + fileName);
                return;
            }

            string[] assetNames = bundle.GetAllAssetNames();
            lines.Add(
                "CORE " + fileName + " assets=" + assetNames.Length);
            for (int assetIndex = 0;
                 assetIndex < assetNames.Length;
                 assetIndex++)
            {
                lines.Add("  ASSET " + assetNames[assetIndex]);
                GameObject prefab =
                    bundle.LoadAsset<GameObject>(assetNames[assetIndex]);
                if (prefab == null)
                    continue;

                List<string> missing =
                    SkillResourceRegistry.FindMissingResources(prefab);
                int rebound = SkillResourceRegistry.RestorePrefab(prefab);
                lines.Add(
                    "  PREFAB name=" + prefab.name +
                    " position=" + prefab.transform.localPosition +
                    " scale=" + prefab.transform.localScale +
                    " particles=" +
                    prefab.GetComponentsInChildren<ParticleSystem>(true).Length +
                    " renderers=" +
                    prefab.GetComponentsInChildren<Renderer>(true).Length +
                    " directors=" +
                    prefab.GetComponentsInChildren<PlayableDirector>(true).Length +
                    " rebound=" + rebound +
                    " missing=" +
                    (missing.Count == 0
                        ? "none"
                        : string.Join(",", missing.ToArray())));

                if (fileName.StartsWith(
                        "m_cardspine_",
                        StringComparison.OrdinalIgnoreCase))
                {
                    InspectSpineWeaponAnchors(prefab, lines);
                }
                else if (fileName.StartsWith(
                    "eft_fx_timeline_",
                    StringComparison.OrdinalIgnoreCase))
                {
                    InspectTimelineWeaponPositions(prefab, lines);
                }

                PlayableDirector[] directors =
                    prefab.GetComponentsInChildren<PlayableDirector>(true);
                for (int directorIndex = 0;
                     directorIndex < directors.Length;
                     directorIndex++)
                {
                    InspectDirector(
                        directors[directorIndex],
                        lines,
                        "    ");
                }

                Renderer[] renderers =
                    prefab.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0;
                     rendererIndex < renderers.Length;
                     rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    ParticleSystem rendererParticle =
                        renderer.GetComponent<ParticleSystem>();
                    string particleText = string.Empty;
                    if (rendererParticle != null)
                    {
                        ParticleSystem.MainModule main =
                            rendererParticle.main;
                        ParticleSystem.ShapeModule shape =
                            rendererParticle.shape;
                        ParticleSystemRenderer particleRenderer =
                            renderer as ParticleSystemRenderer;
                        particleText =
                            " particleDelay=" +
                            main.startDelay.constant.ToString("0.000") +
                            " duration=" +
                            main.duration.ToString("0.000") +
                            " lifetime=" +
                            main.startLifetime.constant.ToString("0.000") +
                            " size=" +
                            main.startSize.constant.ToString("0.000") +
                            " rotation=" +
                            main.startRotation.constant.ToString("0.000") +
                            " simulation=" + main.simulationSpace +
                            " shapeEnabled=" + shape.enabled +
                            " shapePosition=" + shape.position +
                            (particleRenderer == null
                                ? string.Empty
                                : " rendererPivot=" +
                                  particleRenderer.pivot +
                                  " renderMode=" +
                                  particleRenderer.renderMode +
                                  " alignment=" +
                                  particleRenderer.alignment +
                                  (particleRenderer.mesh == null
                                      ? string.Empty
                                      : " mesh=" +
                                        particleRenderer.mesh.name +
                                        " meshBounds=" +
                                        particleRenderer.mesh.bounds));
                    }
                    string materialText = string.Empty;
                    Material[] materials = renderer.sharedMaterials;
                    for (int materialIndex = 0;
                         materialIndex < materials.Length;
                         materialIndex++)
                    {
                        Material material = materials[materialIndex];
                        if (materialText.Length != 0)
                            materialText += ";";
                        materialText += material == null
                            ? "null"
                            : material.name + "[" +
                              (material.shader == null
                                  ? "no-shader"
                                  : material.shader.name) + "," +
                              (material.mainTexture == null
                                  ? "no-texture"
                                  : material.mainTexture.name) + "]";
                    }
                    lines.Add(
                        "    RENDERER path=" +
                        GetPath(renderer.transform, prefab.transform) +
                        " type=" + renderer.GetType().Name +
                        " position=" + renderer.transform.position +
                        " localPosition=" +
                        renderer.transform.localPosition +
                        " active=" + renderer.gameObject.activeSelf +
                        " enabled=" + renderer.enabled +
                        " order=" + renderer.sortingOrder +
                        " materials=" + materialText +
                        particleText);
                }
            }
        }

        private static void InspectSpineWeaponAnchors(
            GameObject prefab,
            List<string> lines)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                SkeletonAnimation animation =
                    instance.GetComponentInChildren<SkeletonAnimation>(true);
                if (animation == null)
                {
                    lines.Add("  SPINE_ANCHORS unavailable");
                    return;
                }
                animation.Initialize(true);
                string[] animationNames =
                {
                    "idle", "attack", "skill", "uniqueskill"
                };
                float[] sampleTimes =
                {
                    0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.8f, 1.2f,
                    1.7f, 1.75f, 1.85f, 2f,
                    4.4f, 4.5f, 4.6f, 4.8f, 5f, 5.2f, 5.4f
                };
                for (int animationIndex = 0;
                     animationIndex < animationNames.Length;
                     animationIndex++)
                {
                    string animationName = animationNames[animationIndex];
                    if (animation.Skeleton.Data.FindAnimation(
                            animationName) == null)
                    {
                        continue;
                    }
                    for (int sampleIndex = 0;
                         sampleIndex < sampleTimes.Length;
                         sampleIndex++)
                    {
                        animation.Skeleton.SetToSetupPose();
                        animation.AnimationState.ClearTracks();
                        animation.AnimationState.SetAnimation(
                            0,
                            animationName,
                            false);
                        animation.Update(sampleTimes[sampleIndex]);
                        WriteWeaponAttachments(
                            animation.Skeleton,
                            animationName,
                            sampleTimes[sampleIndex],
                            lines);
                    }
                }
            }
            catch (Exception exception)
            {
                lines.Add(
                    "  SPINE_ANCHORS_FAILED " + exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void WriteWeaponAttachments(
            Skeleton skeleton,
            string animationName,
            float sampleTime,
            List<string> lines)
        {
            for (int slotIndex = 0;
                 slotIndex < skeleton.Slots.Count;
                 slotIndex++)
            {
                Slot slot = skeleton.Slots.Items[slotIndex];
                Attachment attachment = slot.Attachment;
                string slotName = slot.Data.Name ?? string.Empty;
                string attachmentName = attachment == null
                    ? string.Empty
                    : attachment.Name;
                string searchable =
                    (slotName + "/" + attachmentName).ToLowerInvariant();
                if (searchable.IndexOf("wuqi") < 0 &&
                    searchable.IndexOf("weapon") < 0)
                {
                    continue;
                }

                float[] vertices;
                RegionAttachment region =
                    attachment as RegionAttachment;
                VertexAttachment vertex =
                    attachment as VertexAttachment;
                if (region != null)
                {
                    vertices = new float[8];
                    region.ComputeWorldVertices(
                        slot.Bone,
                        vertices,
                        0);
                }
                else if (vertex != null)
                {
                    vertices = new float[vertex.WorldVerticesLength];
                    vertex.ComputeWorldVertices(slot, vertices);
                }
                else
                {
                    lines.Add(
                        "  SPINE_WEAPON animation=" + animationName +
                        " time=" + sampleTime.ToString("0.000") +
                        " slot=" + slotName +
                        " attachment=" + attachmentName +
                        " bone=" + slot.Bone.Data.Name +
                        " boneWorld=(" +
                        slot.Bone.WorldX.ToString("0.000") + "," +
                        slot.Bone.WorldY.ToString("0.000") + ")");
                    continue;
                }

                float minX = float.MaxValue;
                float maxX = float.MinValue;
                float minY = float.MaxValue;
                float maxY = float.MinValue;
                for (int vertexIndex = 0;
                     vertexIndex + 1 < vertices.Length;
                     vertexIndex += 2)
                {
                    minX = Mathf.Min(minX, vertices[vertexIndex]);
                    maxX = Mathf.Max(maxX, vertices[vertexIndex]);
                    minY = Mathf.Min(minY, vertices[vertexIndex + 1]);
                    maxY = Mathf.Max(maxY, vertices[vertexIndex + 1]);
                }
                lines.Add(
                    "  SPINE_WEAPON animation=" + animationName +
                    " time=" + sampleTime.ToString("0.000") +
                    " slot=" + slotName +
                    " attachment=" + attachmentName +
                    " bone=" + slot.Bone.Data.Name +
                    " boneWorld=(" +
                    slot.Bone.WorldX.ToString("0.000") + "," +
                    slot.Bone.WorldY.ToString("0.000") + ")" +
                    " bounds=(" + minX.ToString("0.000") + "," +
                    minY.ToString("0.000") + ")-(" +
                    maxX.ToString("0.000") + "," +
                    maxY.ToString("0.000") + ")" +
                    " vertices=" + FormatVertices(vertices));
            }
        }

        private static string FormatVertices(float[] vertices)
        {
            string text = string.Empty;
            for (int index = 0; index + 1 < vertices.Length; index += 2)
            {
                if (text.Length != 0)
                    text += ";";
                text += "(" + vertices[index].ToString("0.000") + "," +
                    vertices[index + 1].ToString("0.000") + ")";
            }
            return text;
        }

        private static void InspectTimelineWeaponPositions(
            GameObject prefab,
            List<string> lines)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.SetActive(true);
                PlayableDirector director =
                    instance.GetComponentInChildren<PlayableDirector>(true);
                if (director == null)
                    return;
                director.RebuildGraph();
                float[] sampleTimes =
                {
                    0f, 0.5f, 1f, 4.4f, 4.5f, 4.6f,
                    4.8f, 5f, 5.2f, 5.4f, 5.6f
                };
                for (int sampleIndex = 0;
                     sampleIndex < sampleTimes.Length;
                     sampleIndex++)
                {
                    director.time = sampleTimes[sampleIndex];
                    director.Evaluate();
                    WriteTimelineTransform(
                        instance.transform,
                        "FX_zidan01",
                        sampleTimes[sampleIndex],
                        lines);
                    WriteTimelineTransform(
                        instance.transform,
                        "Shoot-baofa",
                        sampleTimes[sampleIndex],
                        lines);
                }
            }
            catch (Exception exception)
            {
                lines.Add(
                    "  TIMELINE_WEAPON_FAILED " + exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void WriteTimelineTransform(
            Transform root,
            string objectName,
            float sampleTime,
            List<string> lines)
        {
            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform current = transforms[index];
                if (!string.Equals(
                    current.name,
                    objectName,
                    StringComparison.Ordinal))
                {
                    continue;
                }
                lines.Add(
                    "  TIMELINE_WEAPON time=" +
                    sampleTime.ToString("0.000") +
                    " name=" + objectName +
                    " active=" + current.gameObject.activeInHierarchy +
                    " position=" + current.position +
                    " localPosition=" + current.localPosition);
            }
        }

        private static void InspectDirector(
            PlayableDirector director,
            List<string> lines,
            string indent)
        {
            lines.Add(
                indent + "DIRECTOR path=" + GetPath(
                    director.transform,
                    director.transform.root) +
                " duration=" + director.duration.ToString("0.000") +
                " asset=" +
                (director.playableAsset == null
                    ? "null"
                    : director.playableAsset.name));
            TimelineAsset timeline = director.playableAsset as TimelineAsset;
            if (timeline == null)
                return;
            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                lines.Add(
                    indent + "  TRACK name=" + track.name +
                    " type=" + track.GetType().FullName);
                foreach (TimelineClip clip in track.GetClips())
                {
                    lines.Add(
                        indent + "    CLIP name=" + clip.displayName +
                        " start=" + clip.start.ToString("0.000") +
                        " duration=" + clip.duration.ToString("0.000") +
                        " asset=" +
                        (clip.asset == null
                            ? "null"
                            : clip.asset.GetType().FullName));
                }
            }
        }

        private static int CompareBundlePaths(string left, string right)
        {
            string leftName = Path.GetFileName(left);
            string rightName = Path.GetFileName(right);
            int priority = BundlePriority(leftName).CompareTo(
                BundlePriority(rightName));
            return priority != 0
                ? priority
                : string.CompareOrdinal(leftName, rightName);
        }

        private static int BundlePriority(string fileName)
        {
            if (string.Equals(
                fileName,
                "common.aab",
                StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
            if (fileName.StartsWith(
                    "st_",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    fileName,
                    "m_soulgames.aab",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
            if (fileName.StartsWith(
                    "m_cardspine_",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }
            return 3;
        }

        private static string GetPath(Transform item, Transform root)
        {
            if (item == null)
                return string.Empty;
            string result = item.name;
            Transform current = item.parent;
            while (current != null && current != root)
            {
                result = current.name + "/" + result;
                current = current.parent;
            }
            return result;
        }
    }
}
