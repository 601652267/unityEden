using System;
using System.Collections.Generic;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace EdenGallery.Editor
{
    public static class EdenGalleryEditorValidation
    {
        [MenuItem("Eden Gallery/Validate All Portraits")]
        public static void ValidateAllPortraits()
        {
            TextAsset manifestAsset = Resources.Load<TextAsset>("EdenGallery/gallery");
            if (manifestAsset == null)
                throw new InvalidOperationException("EdenGallery/gallery.json was not imported.");

            EdenGalleryManifest manifest = JsonUtility.FromJson<EdenGalleryManifest>(manifestAsset.text);
            if (manifest == null || manifest.characters == null || manifest.characters.Length == 0)
                throw new InvalidOperationException("The Eden gallery manifest is empty.");

            TextAsset voiceCatalogAsset = Resources.Load<TextAsset>("EdenGallery/voice_catalog");
            if (voiceCatalogAsset == null)
                throw new InvalidOperationException("EdenGallery/voice_catalog.json was not imported.");
            EdenGalleryVoiceCatalog voiceCatalog =
                JsonUtility.FromJson<EdenGalleryVoiceCatalog>(voiceCatalogAsset.text);
            if (voiceCatalog == null || voiceCatalog.entries == null)
                throw new InvalidOperationException("The Eden gallery voice catalog is empty.");
            Dictionary<string, EdenGalleryVoiceCatalogEntry> voiceEntries =
                new Dictionary<string, EdenGalleryVoiceCatalogEntry>(StringComparer.Ordinal);
            int voiceLineCount = 0;
            for (int entryIndex = 0; entryIndex < voiceCatalog.entries.Length; entryIndex++)
            {
                EdenGalleryVoiceCatalogEntry entry = voiceCatalog.entries[entryIndex];
                if (entry == null || string.IsNullOrEmpty(entry.folder))
                    throw new InvalidOperationException("Voice catalog has an invalid entry.");
                if (voiceEntries.ContainsKey(entry.folder))
                    throw new InvalidOperationException("Duplicate voice catalog folder: " + entry.folder);
                if (entry.lines == null || entry.lines.Length == 0)
                    throw new InvalidOperationException("Voice catalog entry has no lines: " + entry.folder);
                for (int lineIndex = 0; lineIndex < entry.lines.Length; lineIndex++)
                {
                    EdenGalleryVoiceLine line = entry.lines[lineIndex];
                    if (line == null || string.IsNullOrEmpty(line.voicePath) ||
                        string.IsNullOrEmpty(line.audioFile) ||
                        string.IsNullOrEmpty(line.text) || string.IsNullOrEmpty(line.textCn))
                    {
                        throw new InvalidOperationException(
                            "Voice catalog line is incomplete: " + entry.folder + " #" + lineIndex);
                    }
                    voiceLineCount += 1;
                }
                voiceEntries.Add(entry.folder, entry);
            }

            int stageCount = 0;
            int spineLayerCount = 0;
            int stableBoundsCount = 0;
            int imageCount = 0;
            List<UnityObject> ownedObjects = new List<UnityObject>();
            GameObject validationRoot = new GameObject("EdenGalleryValidation");

            try
            {
                for (int characterIndex = 0; characterIndex < manifest.characters.Length; characterIndex++)
                {
                    EdenGalleryCharacter character = manifest.characters[characterIndex];
                    if (Resources.Load<Texture2D>(character.portraitPath) == null)
                        throw new InvalidOperationException("Missing portrait: " + character.portraitPath);
                    imageCount += 1;

                    EdenGalleryStage[] stages = character.stages ?? new EdenGalleryStage[0];
                    for (int stageIndex = 0; stageIndex < stages.Length; stageIndex++)
                    {
                        EdenGalleryStage stage = stages[stageIndex];
                        EdenGalleryVoiceCatalogEntry voiceEntry;
                        if (!voiceEntries.TryGetValue(stage.folder, out voiceEntry))
                        {
                            throw new InvalidOperationException(
                                "Missing voice catalog entry: " + stage.folder);
                        }
                        if (!string.Equals(
                            voiceEntry.cardId,
                            character.cardId,
                            StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "Voice catalog card mismatch: " + stage.folder);
                        }
                        GameObject stageValidationRoot = new GameObject("Stage_" + stage.folder);
                        stageValidationRoot.transform.SetParent(validationRoot.transform, false);
                        bool hasSpineLayer = false;
                        stageCount += 1;
                        if (!string.IsNullOrEmpty(stage.backgroundPath))
                        {
                            if (Resources.Load<Texture2D>(stage.backgroundPath) == null)
                                throw new InvalidOperationException("Missing background: " + stage.backgroundPath);
                            imageCount += 1;
                        }

                        EdenGalleryLayer[] layers = stage.layers ?? new EdenGalleryLayer[0];
                        for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
                        {
                            EdenGalleryLayer layer = layers[layerIndex];
                            if (string.Equals(layer.type, "image", StringComparison.OrdinalIgnoreCase))
                            {
                                if (Resources.Load<Texture2D>(layer.imagePath) == null)
                                    throw new InvalidOperationException("Missing static image: " + layer.imagePath);
                                imageCount += 1;
                                continue;
                            }

                            SkeletonAnimation animation = EdenGallerySpineFactory.Create(
                                layer,
                                stageValidationRoot.transform,
                                layerIndex,
                                ownedObjects);
                            if (animation == null || animation.Skeleton == null || animation.AnimationState == null)
                                throw new InvalidOperationException("Spine layer failed to initialize: " + layer.name);
                            hasSpineLayer = true;
                            spineLayerCount += 1;
                        }

                        Bounds stableBounds;
                        if (hasSpineLayer &&
                            !EdenGallerySpineBounds.TryCalculateAnimationBounds(
                                stageValidationRoot.transform,
                                out stableBounds))
                        {
                            throw new InvalidOperationException(
                                "Stable Spine bounds failed: " + stage.folder);
                        }
                        if (hasSpineLayer)
                            stableBoundsCount += 1;
                        UnityObject.DestroyImmediate(stageValidationRoot);
                    }
                }
            }
            finally
            {
                UnityObject.DestroyImmediate(validationRoot);
                for (int i = ownedObjects.Count - 1; i >= 0; i--)
                {
                    if (ownedObjects[i] != null)
                        UnityObject.DestroyImmediate(ownedObjects[i]);
                }
            }

            Debug.Log(
                "EDEN_GALLERY_VALIDATION_OK characters=" + manifest.characters.Length +
                " stages=" + stageCount +
                " spineLayers=" + spineLayerCount +
                " stableBounds=" + stableBoundsCount +
                " images=" + imageCount +
                " voiceEntries=" + voiceEntries.Count +
                " voiceLines=" + voiceLineCount);
        }
    }
}
