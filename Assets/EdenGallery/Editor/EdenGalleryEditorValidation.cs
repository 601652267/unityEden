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
        [MenuItem("Eden Gallery/Validate Battle Scene")]
        public static void ValidateBattleScene()
        {
            const string battleScenePath =
                "Assets/Scenes/CharacterBattleScene.unity";
            bool battleSceneEnabled = false;
            EditorBuildSettingsScene[] scenes =
                EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled &&
                    string.Equals(
                        scenes[i].path,
                        battleScenePath,
                        StringComparison.Ordinal))
                {
                    battleSceneEnabled = true;
                    break;
                }
            }
            if (!battleSceneEnabled)
                throw new InvalidOperationException(
                    "CharacterBattleScene is not enabled in Build Settings.");

            string root = "EdenBattle/Enemies/12010002/";
            if (Resources.Load<TextAsset>(
                    root + "CardSpine_12010002.atlas") == null ||
                Resources.Load<TextAsset>(
                    root + "CardSpine_12010002.skel") == null ||
                Resources.Load<Texture2D>(
                    root + "CardSpine_12010002") == null)
            {
                throw new InvalidOperationException(
                    "Enemy 12010002 Spine resources are incomplete.");
            }
            if (Resources.Load<Shader>(
                    "SkillRestoreShaders/BattleSceneUnlit") == null ||
                Resources.Load<Shader>(
                    "Skill11300018/VideoUnlit") == null)
            {
                throw new InvalidOperationException(
                    "11300018 video fallback shaders are unavailable.");
            }

            EdenGalleryLayer layer = new EdenGalleryLayer();
            layer.name = "BattleValidation_12010002";
            layer.atlasPath =
                root + "CardSpine_12010002.atlas";
            layer.skeletonPath =
                root + "CardSpine_12010002.skel";
            layer.texturePaths = new[]
            {
                root + "CardSpine_12010002"
            };
            layer.animationName = "idle";

            List<UnityObject> ownedObjects = new List<UnityObject>();
            GameObject validationRoot =
                new GameObject("EdenBattleValidation");
            try
            {
                SkeletonAnimation enemy =
                    EdenGallerySpineFactory.Create(
                        layer,
                        validationRoot.transform,
                        0,
                        ownedObjects);
                if (enemy == null ||
                    enemy.Skeleton == null ||
                    enemy.Skeleton.Data.FindAnimation("idle") == null ||
                    enemy.Skeleton.Data.FindAnimation("hit_1") == null ||
                    enemy.Skeleton.Data.FindAnimation("hit_2") == null)
                {
                    throw new InvalidOperationException(
                        "Enemy 12010002 idle/hit_1/hit_2 animation is unavailable.");
                }

                string streamingRoot = System.IO.Path.Combine(
                    Application.streamingAssetsPath,
                    "Skill11300018Original");
                string[] requiredBundles =
                {
                    "m_cardspine_11300018.aab",
                    "eft_fx_11300018_attack.aab",
                    "eft_fx_11300018_attack_2.aab",
                    "eft_fx_11300018_attack_air.aab",
                    "eft_fx_11300018_attack_air_hit.aab",
                    "eft_fx_11300018_skill.aab",
                    "eft_fx_11300018_skill_2.aab",
                    "eft_fx_timeline_11300018_xp.aab",
                    "eft_labi_shouji.aab",
                    "manifest.json"
                };
                for (int bundleIndex = 0;
                     bundleIndex < requiredBundles.Length;
                     bundleIndex++)
                {
                    string path = System.IO.Path.Combine(
                        streamingRoot,
                        requiredBundles[bundleIndex]);
                    if (!System.IO.File.Exists(path))
                        throw new InvalidOperationException(
                            "Missing 11300018 battle resource: " + path);
                }

                string videoPath = System.IO.Path.Combine(
                    Application.streamingAssetsPath,
                    "Skill11300018/FX_timeline_11300018_xp.m4v");
                if (!System.IO.File.Exists(videoPath))
                    throw new InvalidOperationException(
                        "Missing 11300018 ultimate video: " + videoPath);

                Validate11300018ApkLogic();
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
                "EDEN_BATTLE_VALIDATION_OK hero=11300018 enemy=12010002 " +
                "mode=apk-only " +
                "actions=attack,skill,uniqueskill apkHits=9");
        }

        private static void Validate11300018ApkLogic()
        {
            if (!string.Equals(
                    Skill11300018ApkBattleLogic.TimelineName,
                    "Fx_timeline_11300018_xp",
                    StringComparison.Ordinal) ||
                Skill11300018ApkBattleLogic.UltimateHits.Length !=
                    Skill11300018ApkBattleLogic.UltimateTotalHitCount ||
                Mathf.Abs(
                    Skill11300018ApkBattleLogic.UltimateSwordRevealTime -
                    6.700f) > 0.0001f ||
                Skill11300018ApkBattleLogic.UltimateSwordRevealTime <=
                    Skill11300018ApkBattleLogic.UltimateDefendersVisibleTime ||
                Skill11300018ApkBattleLogic.UltimateSwordRevealTime >=
                    Skill11300018ApkBattleLogic.UltimateHits[0].timeSeconds)
            {
                throw new InvalidOperationException(
                    "11300018 recovered Timeline or hit count is invalid.");
            }

            float previousTime = 0f;
            float[] expectedTimes =
            {
                6.865f,
                7.232f,
                7.599f,
                8.665f,
                8.815f,
                8.965f,
                9.115f,
                9.265f,
                9.415f
            };
            for (int i = 0;
                 i < Skill11300018ApkBattleLogic.UltimateHits.Length;
                 i++)
            {
                Skill11300018UltimateHit hit =
                    Skill11300018ApkBattleLogic.UltimateHits[i];
                string expectedState =
                    i % 2 == 0 ? "hit_2" : "hit_1";
                bool expectedFinal =
                    i ==
                    Skill11300018ApkBattleLogic.UltimateHits.Length - 1;
                if (hit.timeSeconds <= previousTime ||
                    Mathf.Abs(
                        hit.timeSeconds -
                        expectedTimes[i]) > 0.0001f ||
                    !string.Equals(
                        hit.defenderState,
                        expectedState,
                        StringComparison.Ordinal) ||
                    hit.isFinal != expectedFinal)
                {
                    throw new InvalidOperationException(
                        "Invalid 11300018 APK ultimate cue #" + (i + 1));
                }
                previousTime = hit.timeSeconds;
            }

            string[] expectedNormal =
            {
                "eft_fx_11300018_attack_2.aab"
            };
            string[] expectedBurst =
            {
                "eft_fx_11300018_attack.aab",
                "eft_fx_11300018_skill.aab",
                "eft_fx_11300018_skill_2.aab"
            };
            if (!SequenceEquals(
                    Skill11300018ApkBattleLogic.NormalEffectBundles,
                    expectedNormal) ||
                !SequenceEquals(
                    Skill11300018ApkBattleLogic.BurstEffectBundles,
                    expectedBurst))
            {
                throw new InvalidOperationException(
                    "11300018 APK attack/burst resource mapping changed.");
            }
        }

        private static bool SequenceEquals(
            string[] left,
            string[] right)
        {
            if (left == null || right == null ||
                left.Length != right.Length)
            {
                return false;
            }
            for (int i = 0; i < left.Length; i++)
            {
                if (!string.Equals(
                        left[i],
                        right[i],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        [MenuItem("Eden Gallery/Validate Character Details")]
        public static void ValidateCharacterDetails()
        {
            TextAsset manifestAsset = Resources.Load<TextAsset>("EdenGallery/gallery");
            TextAsset detailsAsset = Resources.Load<TextAsset>(
                "EdenGallery/character_details");
            if (manifestAsset == null || detailsAsset == null)
                throw new InvalidOperationException(
                    "Gallery manifest or character details catalog is missing.");

            EdenGalleryManifest manifest =
                JsonUtility.FromJson<EdenGalleryManifest>(manifestAsset.text);
            EdenGalleryCharacterDetailsCatalog catalog =
                JsonUtility.FromJson<EdenGalleryCharacterDetailsCatalog>(
                    detailsAsset.text);
            if (manifest == null || manifest.characters == null ||
                catalog == null || catalog.characters == null)
            {
                throw new InvalidOperationException(
                    "Gallery manifest or character details catalog is empty.");
            }

            Dictionary<string, EdenGalleryCharacterDetails> detailsByCardId =
                new Dictionary<string, EdenGalleryCharacterDetails>(
                    StringComparer.Ordinal);
            int voiceCount = 0;
            for (int i = 0; i < catalog.characters.Length; i++)
            {
                EdenGalleryCharacterDetails details = catalog.characters[i];
                if (details == null || string.IsNullOrEmpty(details.cardId))
                    throw new InvalidOperationException(
                        "Character details catalog has an invalid entry.");
                if (detailsByCardId.ContainsKey(details.cardId))
                    throw new InvalidOperationException(
                        "Duplicate character details: " + details.cardId);
                detailsByCardId.Add(details.cardId, details);
                EdenGalleryVoiceLine[] voices =
                    details.voices ?? new EdenGalleryVoiceLine[0];
                for (int voiceIndex = 0; voiceIndex < voices.Length; voiceIndex++)
                {
                    EdenGalleryVoiceLine voice = voices[voiceIndex];
                    if (voice == null || string.IsNullOrEmpty(voice.voicePath) ||
                        string.IsNullOrEmpty(voice.audioFile) ||
                        (string.IsNullOrEmpty(voice.text) &&
                         string.IsNullOrEmpty(voice.textCn)))
                    {
                        throw new InvalidOperationException(
                            "Invalid details voice: " + details.cardId +
                            " #" + voiceIndex);
                    }
                    voiceCount++;
                }
            }

            for (int i = 0; i < manifest.characters.Length; i++)
            {
                EdenGalleryCharacter character = manifest.characters[i];
                EdenGalleryCharacterDetails details;
                if (character == null ||
                    !detailsByCardId.TryGetValue(character.cardId, out details))
                {
                    throw new InvalidOperationException(
                        "Missing character details: " +
                        (character == null ? "(null)" : character.cardId));
                }
                if (details.voices == null || details.voices.Length == 0)
                    throw new InvalidOperationException(
                        "Character details have no voices: " + character.cardId);
            }

            Debug.Log(
                "EDEN_GALLERY_DETAILS_VALIDATION_OK characters=" +
                manifest.characters.Length + " voices=" + voiceCount);
        }

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
                        (string.IsNullOrEmpty(line.text) &&
                         string.IsNullOrEmpty(line.textCn)))
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
                        if (!voiceEntries.TryGetValue(stage.folder, out voiceEntry) &&
                            !voiceEntries.TryGetValue(character.cardId, out voiceEntry))
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
                        ValidateOriginalEffect(stage);

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

        private static void ValidateOriginalEffect(EdenGalleryStage stage)
        {
            if (stage == null || string.IsNullOrEmpty(stage.originalEffectPrefabPath))
                return;
            GameObject effectPrefab = Resources.Load<GameObject>(
                stage.originalEffectPrefabPath);
            if (effectPrefab == null ||
                effectPrefab.GetComponentsInChildren<ParticleSystem>(true).Length == 0)
            {
                throw new InvalidOperationException(
                    "Original effect prefab is missing or empty: " +
                    stage.originalEffectPrefabPath);
            }
        }
    }
}
