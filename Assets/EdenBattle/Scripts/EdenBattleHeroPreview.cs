using System;
using System.Collections;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace EdenGallery
{
    /// <summary>
    /// Loads a character's recovered battle Spine from Resources and exposes
    /// only the actions that really exist in that skeleton.
    /// </summary>
    public sealed class EdenBattleHeroPreview : MonoBehaviour
    {
        private static readonly string[] IdleAnimations =
        {
            "idle", "wait", "stand", "float_up", "walk"
        };

        private static readonly string[] NormalAnimations =
        {
            "attack", "attack_1", "attack1", "normalattack",
            "commonattack", "atk", "atk_1", "atk1"
        };

        private static readonly string[] BurstAnimations =
        {
            "skill", "skill_1", "skill1", "specialskill",
            "special_skill", "burst"
        };

        private static readonly string[] UltimateAnimations =
        {
            "uniqueskill", "unique_skill", "ultimate", "ultimate_skill",
            "xp", "special"
        };

        private readonly List<UnityObject> ownedObjects =
            new List<UnityObject>();

        private string cardId = string.Empty;
        private Transform heroRoot;
        private SkeletonAnimation hero;
        private Coroutine actionRoutine;
        private string idleAnimation = string.Empty;
        private string normalAnimation = string.Empty;
        private string burstAnimation = string.Empty;
        private string ultimateAnimation = string.Empty;
        private bool loading;
        private bool busy;
        private string loadingStatus = "正在载入战斗角色…";

        public bool IsReady
        {
            get { return !loading && hero != null; }
        }

        public bool IsBusy
        {
            get { return busy; }
        }

        public bool HasNormalAttack
        {
            get { return !string.IsNullOrEmpty(normalAnimation); }
        }

        public bool HasBurst
        {
            get { return !string.IsNullOrEmpty(burstAnimation); }
        }

        public bool HasUltimate
        {
            get { return !string.IsNullOrEmpty(ultimateAnimation); }
        }

        public string LoadingStatus
        {
            get { return loadingStatus; }
        }

        public void Configure(string selectedCardId)
        {
            cardId = selectedCardId ?? string.Empty;
        }

        private IEnumerator Start()
        {
            loading = true;
            yield return null;

            if (string.IsNullOrEmpty(cardId))
            {
                loadingStatus = "未指定战斗角色";
                loading = false;
                yield break;
            }

            string resourceRoot = "EdenBattle/Heroes/" + cardId + "/";
            EdenGalleryLayer layer = new EdenGalleryLayer();
            layer.name = "CardSpine_" + cardId;
            layer.atlasPath = resourceRoot + "CardSpine_" + cardId + ".atlas";
            layer.skeletonPath = resourceRoot + "CardSpine_" + cardId + ".skel";
            layer.texturePaths = new[]
            {
                resourceRoot + "CardSpine_" + cardId
            };
            layer.animationName = "idle";
            layer.displayScale = 1f;
            layer.roleLayer = true;
            layer.useCustomSortingOrder = true;
            layer.sortingOrder = 40;

            heroRoot = new GameObject("Hero_" + cardId).transform;
            heroRoot.SetParent(transform, false);
            heroRoot.position = new Vector3(-18f, -4f, 0f);
            heroRoot.localScale = Vector3.one * 1.2f;

            try
            {
                hero = EdenGallerySpineFactory.Create(
                    layer,
                    heroRoot,
                    40,
                    ownedObjects);
                CacheAvailableAnimations();
                PlayIdle();
                loadingStatus = "准备完成";
                Debug.Log(
                    "EDEN_BATTLE_HERO_READY id=" + cardId +
                    " normal=" + HasNormalAttack +
                    " burst=" + HasBurst +
                    " ultimate=" + HasUltimate);
            }
            catch (Exception exception)
            {
                loadingStatus = cardId + " 没有可用的战斗 Spine";
                Debug.LogWarning(
                    "EDEN_BATTLE_HERO_MISSING id=" + cardId +
                    " reason=" + exception.Message);
                if (heroRoot != null)
                    heroRoot.gameObject.SetActive(false);
            }
            loading = false;
        }

        public bool BeginNormalAttack()
        {
            return BeginAction(normalAnimation);
        }

        public bool BeginBurst()
        {
            return BeginAction(burstAnimation);
        }

        public bool BeginUltimate()
        {
            return BeginAction(ultimateAnimation);
        }

        private bool BeginAction(string animationName)
        {
            if (!IsReady || busy || string.IsNullOrEmpty(animationName))
                return false;

            if (actionRoutine != null)
                StopCoroutine(actionRoutine);
            actionRoutine = StartCoroutine(PlayAction(animationName));
            return true;
        }

        private IEnumerator PlayAction(string animationName)
        {
            busy = true;
            Spine.Animation animation = FindAnimation(animationName);
            if (animation == null || hero.AnimationState == null)
            {
                busy = false;
                actionRoutine = null;
                yield break;
            }

            hero.AnimationState.SetAnimation(0, animation.Name, false);
            yield return new WaitForSeconds(
                Mathf.Max(0.05f, animation.Duration));
            PlayIdle();
            busy = false;
            actionRoutine = null;
        }

        private void CacheAvailableAnimations()
        {
            SkeletonData data = GetSkeletonData();
            idleAnimation = FindAnimationName(data, IdleAnimations);
            normalAnimation = FindAnimationName(data, NormalAnimations);
            burstAnimation = FindAnimationName(data, BurstAnimations);
            ultimateAnimation = FindAnimationName(data, UltimateAnimations);
        }

        private void PlayIdle()
        {
            if (hero == null || hero.AnimationState == null ||
                string.IsNullOrEmpty(idleAnimation))
            {
                return;
            }
            hero.AnimationState.SetAnimation(0, idleAnimation, true);
        }

        private Spine.Animation FindAnimation(string animationName)
        {
            SkeletonData data = GetSkeletonData();
            return data == null || string.IsNullOrEmpty(animationName)
                ? null
                : data.FindAnimation(animationName);
        }

        private SkeletonData GetSkeletonData()
        {
            return hero == null || hero.Skeleton == null
                ? null
                : hero.Skeleton.Data;
        }

        private static string FindAnimationName(
            SkeletonData data,
            string[] candidates)
        {
            if (data == null || data.Animations == null ||
                candidates == null)
            {
                return string.Empty;
            }

            for (int candidateIndex = 0;
                 candidateIndex < candidates.Length;
                 candidateIndex++)
            {
                string candidate = candidates[candidateIndex];
                for (int animationIndex = 0;
                     animationIndex < data.Animations.Count;
                     animationIndex++)
                {
                    Spine.Animation animation =
                        data.Animations.Items[animationIndex];
                    if (animation != null && string.Equals(
                            animation.Name,
                            candidate,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return animation.Name;
                    }
                }
            }
            return string.Empty;
        }

        private void OnDestroy()
        {
            if (actionRoutine != null)
                StopCoroutine(actionRoutine);
            for (int index = ownedObjects.Count - 1; index >= 0; index--)
            {
                if (ownedObjects[index] != null)
                    Destroy(ownedObjects[index]);
            }
            ownedObjects.Clear();
        }
    }
}
