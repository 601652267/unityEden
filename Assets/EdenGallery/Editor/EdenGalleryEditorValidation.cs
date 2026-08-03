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
        private static readonly string[] MissingBattleHeroIds =
        {
            "11202003", "11300056", "11300057"
        };

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
                Validate11301023RecoveredSkill();
                Validate11301006RecoveredSkill();
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
                "EDEN_BATTLE_VALIDATION_OK heroes=11300018,11301023,11301006 " +
                "enemy=12010002 mode=apk-only " +
                "actions=attack,skill,uniqueskill apkHits=9,8,16 " +
                "voices11301023=10 voices11301006=9 " +
                "repeatedHideRestore=ok");
        }

        [MenuItem("Eden Gallery/Validate All Battle Heroes")]
        public static void ValidateBattleHeroes()
        {
            TextAsset manifestAsset =
                Resources.Load<TextAsset>("EdenGallery/gallery");
            if (manifestAsset == null)
                throw new InvalidOperationException(
                    "Gallery manifest is missing.");

            EdenGalleryManifest manifest =
                JsonUtility.FromJson<EdenGalleryManifest>(
                    manifestAsset.text);
            if (manifest == null || manifest.characters == null)
                throw new InvalidOperationException(
                    "Gallery character list is missing.");

            int imported = 0;
            int missing = 0;
            int normalCount = 0;
            int burstCount = 0;
            int ultimateCount = 0;
            for (int characterIndex = 0;
                 characterIndex < manifest.characters.Length;
                 characterIndex++)
            {
                EdenGalleryCharacter character =
                    manifest.characters[characterIndex];
                if (character == null ||
                    string.IsNullOrEmpty(character.cardId))
                {
                    continue;
                }

                string id = character.cardId;
                string root = "EdenBattle/Heroes/" + id + "/";
                TextAsset atlas = Resources.Load<TextAsset>(
                    root + "CardSpine_" + id + ".atlas");
                TextAsset skeleton = Resources.Load<TextAsset>(
                    root + "CardSpine_" + id + ".skel");
                Texture2D texture = Resources.Load<Texture2D>(
                    root + "CardSpine_" + id);
                bool complete = atlas != null && skeleton != null &&
                    texture != null;
                if (!complete)
                {
                    if (!Contains(MissingBattleHeroIds, id))
                        throw new InvalidOperationException(
                            "Unexpected missing battle Spine: " + id);
                    missing++;
                    continue;
                }

                if (Contains(MissingBattleHeroIds, id))
                    throw new InvalidOperationException(
                        "Expected missing battle Spine was imported: " + id);

                List<UnityObject> ownedObjects =
                    new List<UnityObject>();
                GameObject validationRoot =
                    new GameObject("BattleHeroValidation_" + id);
                try
                {
                    EdenGalleryLayer layer = new EdenGalleryLayer();
                    layer.name = "CardSpine_" + id;
                    layer.atlasPath =
                        root + "CardSpine_" + id + ".atlas";
                    layer.skeletonPath =
                        root + "CardSpine_" + id + ".skel";
                    layer.texturePaths = new[]
                    {
                        root + "CardSpine_" + id
                    };
                    layer.animationName = "idle";
                    SkeletonAnimation animation =
                        EdenGallerySpineFactory.Create(
                            layer,
                            validationRoot.transform,
                            0,
                            ownedObjects);
                    if (animation == null || animation.Skeleton == null ||
                        animation.Skeleton.Data == null)
                    {
                        throw new InvalidOperationException(
                            "Battle Spine could not be instantiated: " + id);
                    }

                    Spine.SkeletonData data = animation.Skeleton.Data;
                    if (HasAnyAnimation(data, new[]
                        { "attack", "attack_1", "attack1", "atk" }))
                    {
                        normalCount++;
                    }
                    if (HasAnyAnimation(data, new[]
                        { "skill", "skill_1", "skill1", "burst" }))
                    {
                        burstCount++;
                    }
                    if (HasAnyAnimation(data, new[]
                        { "uniqueskill", "unique_skill", "ultimate", "xp" }))
                    {
                        ultimateCount++;
                    }
                    imported++;
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        "Battle Spine validation failed for " + id +
                        ": " + exception.Message,
                        exception);
                }
                finally
                {
                    UnityObject.DestroyImmediate(validationRoot);
                    for (int ownedIndex = ownedObjects.Count - 1;
                         ownedIndex >= 0;
                         ownedIndex--)
                    {
                        if (ownedObjects[ownedIndex] != null)
                        {
                            UnityObject.DestroyImmediate(
                                ownedObjects[ownedIndex]);
                        }
                    }
                }
            }

            if (imported != 133 || missing != 3)
                throw new InvalidOperationException(
                    "Unexpected battle hero totals imported=" + imported +
                    " missing=" + missing);

            Debug.Log(
                "EDEN_BATTLE_HEROES_VALIDATION_OK imported=" + imported +
                " missing=" + missing +
                " normal=" + normalCount +
                " burst=" + burstCount +
                " ultimate=" + ultimateCount);
        }

        private static bool HasAnyAnimation(
            Spine.SkeletonData data,
            string[] names)
        {
            if (data == null || data.Animations == null || names == null)
                return false;
            for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
            {
                for (int animationIndex = 0;
                     animationIndex < data.Animations.Count;
                     animationIndex++)
                {
                    Spine.Animation animation =
                        data.Animations.Items[animationIndex];
                    if (animation != null && string.Equals(
                            animation.Name,
                            names[nameIndex],
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool Contains(string[] values, string expected)
        {
            if (values == null)
                return false;
            for (int index = 0; index < values.Length; index++)
            {
                if (string.Equals(
                        values[index],
                        expected,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
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
                Mathf.Abs(
                    Skill11300018ApkBattleLogic.UltimateVideoStartTime -
                    1.300000f) > 0.0001f ||
                Mathf.Abs(
                    Skill11300018ApkBattleLogic.UltimateVideoEndTime -
                    4.333333f) > 0.0001f ||
                Mathf.Abs(
                    Skill11300018ApkBattleLogic
                        .UltimateAttackerInvisibleTime -
                    5.6994f) > 0.0001f ||
                Skill11300018ApkBattleLogic.UltimateVideoEndTime >=
                    Skill11300018ApkBattleLogic
                        .UltimateAttackerInvisibleTime ||
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

        private static void Validate11301023RecoveredSkill()
        {
            EdenRecoveredSkillConfiguration config =
                EdenRecoveredSkillConfiguration.ForCard("11301023");
            if (!string.Equals(
                    config.timelineName,
                    "FX_timeline_11301023_xp",
                    StringComparison.Ordinal) ||
                config.ultimateHits == null ||
                config.ultimateHits.Length != 8 ||
                config.ultimateTotalHitCount != 8 ||
                Mathf.Abs(config.ultimateVideoStartTime - 1.5f) > 0.0001f ||
                Mathf.Abs(config.ultimateVideoEndTime - 6f) > 0.0001f ||
                Mathf.Abs(
                    config.ultimateDefendersVisibleTime -
                    7.19928f) > 0.0001f ||
                Mathf.Abs(config.ultimateReturnTime - 9.999f) > 0.0001f)
            {
                throw new InvalidOperationException(
                    "11301023 recovered Timeline configuration is invalid.");
            }

            float[] expectedHitTimes =
            {
                8.765f, 8.915f, 9.065f, 9.215f,
                9.365f, 9.515f, 9.665f, 9.999f
            };
            for (int index = 0; index < config.ultimateHits.Length; index++)
            {
                EdenRecoveredSkillHit hit = config.ultimateHits[index];
                string expectedState =
                    index % 2 == 0 ? "hit_2" : "hit_1";
                bool expectedFinal = index == expectedHitTimes.Length - 1;
                if (Mathf.Abs(
                        hit.timeSeconds -
                        expectedHitTimes[index]) > 0.0001f ||
                    !string.Equals(
                        hit.defenderState,
                        expectedState,
                        StringComparison.Ordinal) ||
                    hit.isFinal != expectedFinal)
                {
                    throw new InvalidOperationException(
                        "Invalid 11301023 APK ultimate cue #" +
                        (index + 1));
                }
            }

            string[] requiredBundles =
            {
                config.CharacterBundleName,
                "eft_fx_11301023_attack.aab",
                "eft_fx_11301023_attack_2.aab",
                "eft_fx_11301023_skill.aab",
                "eft_fx_11301023_skill_2.aab",
                config.TimelineBundleName,
                "manifest.json"
            };
            string bundleRoot = System.IO.Path.Combine(
                Application.streamingAssetsPath,
                config.BundleDirectoryName);
            for (int index = 0; index < requiredBundles.Length; index++)
            {
                string path = System.IO.Path.Combine(
                    bundleRoot,
                    requiredBundles[index]);
                if (!System.IO.File.Exists(path))
                {
                    throw new InvalidOperationException(
                        "Missing 11301023 battle resource: " + path);
                }
            }

            string videoPath = System.IO.Path.Combine(
                Application.streamingAssetsPath,
                config.VideoDirectoryName +
                "/" + config.videoFileName);
            if (!System.IO.File.Exists(videoPath))
            {
                throw new InvalidOperationException(
                    "Missing 11301023 ultimate video: " + videoPath);
            }

            int voiceCount = 0;
            voiceCount += ValidateVoiceResources(
                config.normalVoiceResources);
            voiceCount += ValidateVoiceResources(
                config.burstVoiceResources);
            voiceCount += ValidateVoiceResources(
                config.ultimateVoiceResources);
            if (voiceCount != 10)
            {
                throw new InvalidOperationException(
                    "11301023 battle voice count is invalid: " +
                    voiceCount);
            }

            ValidateRepeatedHideRestoresCharacter();
        }

        private static int ValidateVoiceResources(string[] paths)
        {
            if (paths == null)
                return 0;
            for (int index = 0; index < paths.Length; index++)
            {
                AudioClip clip = Resources.Load<AudioClip>(paths[index]);
                if (clip == null ||
                    (clip.channels != 1 && clip.channels != 2) ||
                    clip.frequency != 44100)
                {
                    throw new InvalidOperationException(
                        "Invalid recovered battle voice: " + paths[index]);
                }
            }
            return paths.Length;
        }

        private static void Validate11301006RecoveredSkill()
        {
            EdenRecoveredSkillConfiguration config =
                EdenRecoveredSkillConfiguration.ForCard("11301006");
            if (!EdenRecoveredSkillConfiguration.Supports("11301006") ||
                !string.Equals(
                    config.timelineName,
                    "Fx_timeline_11301006_xp",
                    StringComparison.Ordinal) ||
                config.ultimateHits == null ||
                config.ultimateHits.Length != 16 ||
                config.ultimateTotalHitCount != 16 ||
                config.normalHitTimes == null ||
                config.normalHitTimes.Length != 3 ||
                config.burstHitTimes == null ||
                config.burstHitTimes.Length != 3 ||
                config.normalMovesToTarget ||
                config.burstMovesToTarget ||
                !config.normalPrimaryEffectAtCaster ||
                !config.burstPrimaryEffectAtCaster ||
                Mathf.Abs(config.normalCleanupTime - 0.85f) > 0.0001f ||
                Mathf.Abs(config.burstCleanupTime - 4.10f) > 0.0001f ||
                Mathf.Abs(config.ultimateVideoStartTime - 1.5f) > 0.0001f ||
                Mathf.Abs(config.ultimateVideoEndTime - 4.5f) > 0.0001f ||
                Mathf.Abs(
                    config.ultimateAttackerInvisibleTime -
                    1.499f) > 0.0001f ||
                Mathf.Abs(
                    config.ultimateAttackerReappearTime -
                    4.499f) > 0.0001f ||
                Mathf.Abs(
                    config.ultimateAttackerSecondInvisibleTime -
                    5.799f) > 0.0001f ||
                Mathf.Abs(
                    config.ultimateDefendersVisibleTime -
                    5.999f) > 0.0001f ||
                Mathf.Abs(config.ultimateReturnTime - 8.665f) > 0.0001f ||
                Mathf.Abs(config.ultimateIdleTime - 10f) > 0.0001f ||
                Mathf.Abs(
                    config.ultimatePresentationEndTime -
                    10.665f) > 0.0001f ||
                Mathf.Abs(
                    config.timelineContainerYOffset + 4f) > 0.0001f)
            {
                throw new InvalidOperationException(
                    "11301006 recovered Timeline configuration is invalid.");
            }

            float[] expectedNormalHitTimes =
            {
                0.33f, 0.46f, 0.59f
            };
            float[] expectedBurstHitTimes =
            {
                3.00f, 3.30f, 3.40f
            };
            for (int index = 0;
                 index < expectedNormalHitTimes.Length;
                 index++)
            {
                if (Mathf.Abs(
                        config.normalHitTimes[index] -
                        expectedNormalHitTimes[index]) > 0.0001f ||
                    Mathf.Abs(
                        config.burstHitTimes[index] -
                        expectedBurstHitTimes[index]) > 0.0001f)
                {
                    throw new InvalidOperationException(
                        "11301006 attack/burst hit timing is invalid.");
                }
            }

            string[] expectedNormalEffects =
            {
                "eft_fx_11301006_attack.aab",
                "eft_fx_11301006_attack_2.aab"
            };
            string[] expectedBurstEffects =
            {
                "eft_fx_11301006_skill.aab",
                "eft_fx_11301006_skill_2.aab"
            };
            if (!SequenceEquals(
                    config.normalEffectBundles,
                    expectedNormalEffects) ||
                !SequenceEquals(
                    config.burstEffectBundles,
                    expectedBurstEffects))
            {
                throw new InvalidOperationException(
                    "11301006 attack/burst resource mapping is invalid.");
            }

            float[] expectedHitTimes =
            {
                7.432f, 7.599f, 7.765f, 7.932f,
                8.065f, 8.199f, 8.365f, 8.565f,
                8.732f, 8.965f, 9.165f, 9.332f,
                9.499f, 9.665f, 9.832f, 9.999f
            };
            for (int index = 0; index < config.ultimateHits.Length; index++)
            {
                EdenRecoveredSkillHit hit = config.ultimateHits[index];
                string expectedState =
                    index % 2 == 0 ? "hit_1" : "hit_2";
                bool expectedFinal = index == expectedHitTimes.Length - 1;
                if (Mathf.Abs(
                        hit.timeSeconds -
                        expectedHitTimes[index]) > 0.0001f ||
                    !string.Equals(
                        hit.defenderState,
                        expectedState,
                        StringComparison.Ordinal) ||
                    hit.isFinal != expectedFinal)
                {
                    throw new InvalidOperationException(
                        "Invalid 11301006 APK ultimate cue #" +
                        (index + 1));
                }
            }
            if (!(config.ultimateHits[7].timeSeconds <
                    config.ultimateReturnTime &&
                config.ultimateReturnTime <
                    config.ultimateHits[8].timeSeconds))
            {
                throw new InvalidOperationException(
                    "11301006 must return between ultimate hits 8 and 9.");
            }

            string[] requiredBundles =
            {
                config.CharacterBundleName,
                "eft_fx_11301006_attack.aab",
                "eft_fx_11301006_attack_2.aab",
                "eft_fx_11301006_attack_hit.aab",
                "eft_fx_11301006_skill.aab",
                "eft_fx_11301006_skill2.aab",
                "eft_fx_11301006_skill_2.aab",
                "eft_fx_11301006_skill_hit.aab",
                config.TimelineBundleName,
                "manifest.json"
            };
            string bundleRoot = System.IO.Path.Combine(
                Application.streamingAssetsPath,
                config.BundleDirectoryName);
            for (int index = 0; index < requiredBundles.Length; index++)
            {
                string path = System.IO.Path.Combine(
                    bundleRoot,
                    requiredBundles[index]);
                if (!System.IO.File.Exists(path))
                {
                    throw new InvalidOperationException(
                        "Missing 11301006 battle resource: " + path);
                }
            }

            string videoPath = System.IO.Path.Combine(
                Application.streamingAssetsPath,
                config.VideoDirectoryName +
                "/" + config.videoFileName);
            if (!System.IO.File.Exists(videoPath))
            {
                throw new InvalidOperationException(
                    "Missing 11301006 ultimate video: " + videoPath);
            }

            int voiceCount = 0;
            voiceCount += ValidateVoiceResources(
                config.normalVoiceResources);
            voiceCount += ValidateVoiceResources(
                config.burstVoiceResources);
            voiceCount += ValidateVoiceResources(
                config.ultimateVoiceResources);
            if (voiceCount != 9)
            {
                throw new InvalidOperationException(
                    "11301006 battle voice count is invalid: " +
                    voiceCount);
            }
            ValidateVoiceChannels(config.normalVoiceResources, 1);
            ValidateVoiceChannels(config.burstVoiceResources, 1);
            ValidateVoiceChannels(config.ultimateVoiceResources, 2);
        }

        private static void ValidateVoiceChannels(
            string[] paths,
            int expectedChannels)
        {
            for (int index = 0; index < paths.Length; index++)
            {
                AudioClip clip = Resources.Load<AudioClip>(paths[index]);
                if (clip == null || clip.channels != expectedChannels)
                {
                    throw new InvalidOperationException(
                        "Unexpected channel count for recovered voice: " +
                        paths[index]);
                }
            }
        }

        private static void ValidateRepeatedHideRestoresCharacter()
        {
            GameObject host = new GameObject(
                "RecoveredVisibilityValidation");
            try
            {
                EdenRecoveredBattlePreview preview =
                    host.AddComponent<EdenRecoveredBattlePreview>();
                GameObject character = GameObject.CreatePrimitive(
                    PrimitiveType.Quad);
                character.transform.SetParent(host.transform, false);
                Renderer renderer = character.GetComponent<Renderer>();
                System.Reflection.FieldInfo characterField =
                    typeof(EdenRecoveredBattlePreview).GetField(
                        "character",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);
                System.Reflection.MethodInfo visibilityMethod =
                    typeof(EdenRecoveredBattlePreview).GetMethod(
                        "SetCharacterVisible",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);
                if (characterField == null || visibilityMethod == null)
                {
                    throw new InvalidOperationException(
                        "Recovered visibility API is unavailable.");
                }

                characterField.SetValue(preview, character);
                visibilityMethod.Invoke(preview, new object[] { false });
                visibilityMethod.Invoke(preview, new object[] { false });
                visibilityMethod.Invoke(preview, new object[] { true });
                if (renderer == null || !renderer.enabled)
                {
                    throw new InvalidOperationException(
                        "Repeated ultimate hide did not restore character.");
                }
            }
            finally
            {
                UnityObject.DestroyImmediate(host);
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
