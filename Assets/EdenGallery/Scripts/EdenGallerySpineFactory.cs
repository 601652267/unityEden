using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace EdenGallery
{
    public static class EdenGallerySpineFactory
    {
        private static readonly string[] PreferredAnimations =
        {
            "idle", "renwu", "role", "r", "animation", "stand", "wait"
        };

        public static SkeletonAnimation Create(
            EdenGalleryLayer layer,
            Transform parent,
            int sortingOrder,
            List<UnityObject> ownedObjects)
        {
            TextAsset atlasText = Resources.Load<TextAsset>(layer.atlasPath);
            TextAsset skeletonBytes = Resources.Load<TextAsset>(layer.skeletonPath);
            if (atlasText == null)
                throw new InvalidOperationException("Missing atlas: " + layer.atlasPath);
            if (skeletonBytes == null)
                throw new InvalidOperationException("Missing skeleton: " + layer.skeletonPath);

            string[] configuredTexturePaths = layer.texturePaths ?? new string[0];
            Texture2D[] textures = new Texture2D[configuredTexturePaths.Length];
            for (int i = 0; i < configuredTexturePaths.Length; i++)
            {
                textures[i] = Resources.Load<Texture2D>(configuredTexturePaths[i]);
                if (textures[i] == null)
                    throw new InvalidOperationException("Missing texture: " + configuredTexturePaths[i]);
            }

            Shader shader = Shader.Find("Spine/Skeleton");
            if (shader == null)
                throw new InvalidOperationException("Spine/Skeleton shader was not found.");

            Material materialTemplate = new Material(shader);
            materialTemplate.name = layer.name + "_MaterialTemplate";
            materialTemplate.SetFloat("_StraightAlphaInput", 1f);
            materialTemplate.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
            ownedObjects.Add(materialTemplate);

            SpineAtlasAsset atlasAsset = SpineAtlasAsset.CreateRuntimeInstance(
                atlasText,
                textures,
                materialTemplate,
                true);
            atlasAsset.name = layer.name + "_RuntimeAtlas";
            ownedObjects.Add(atlasAsset);
            if (atlasAsset.materials != null)
            {
                for (int i = 0; i < atlasAsset.materials.Length; i++)
                {
                    if (atlasAsset.materials[i] != null)
                        ownedObjects.Add(atlasAsset.materials[i]);
                }
            }

            SkeletonDataAsset skeletonDataAsset = SkeletonDataAsset.CreateRuntimeInstance(
                skeletonBytes,
                atlasAsset,
                true,
                0.01f);
            skeletonDataAsset.name = layer.name + "_RuntimeSkeletonData";
            skeletonDataAsset.defaultMix = 0.12f;
            ownedObjects.Add(skeletonDataAsset);

            SkeletonAnimation animation = SkeletonAnimation.NewSkeletonAnimationGameObject(skeletonDataAsset);
            animation.name = layer.name;
            animation.transform.SetParent(parent, false);
            float displayScale = layer.displayScale > 0f ? layer.displayScale : 1f;
            float displayScaleX = Mathf.Abs(layer.displayScaleX) > 0.00001f
                ? layer.displayScaleX
                : displayScale;
            float displayScaleY = Mathf.Abs(layer.displayScaleY) > 0.00001f
                ? layer.displayScaleY
                : displayScale;
            animation.transform.localScale = new Vector3(displayScaleX, displayScaleY, displayScale);
            animation.transform.localPosition = new Vector3(layer.offsetX, layer.offsetY, layer.offsetZ);
            animation.transform.localEulerAngles = new Vector3(
                layer.rotationX,
                layer.rotationY,
                layer.rotationZ);
            animation.loop = true;

            if (!string.IsNullOrEmpty(layer.skinName) &&
                animation.Skeleton.Data.FindSkin(layer.skinName) != null)
            {
                animation.Skeleton.SetSkin(layer.skinName);
                animation.Skeleton.SetSlotsToSetupPose();
            }

            SkeletonData skeletonData = skeletonDataAsset.GetSkeletonData(false);
            string animationName = ResolveAnimationName(skeletonData, layer.animationName);
            if (!string.IsNullOrEmpty(animationName))
                animation.AnimationState.SetAnimation(0, animationName, true);

            ConfigureSlotFilter(animation, layer);

            MeshRenderer renderer = animation.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sortingOrder = layer.useCustomSortingOrder
                    ? layer.sortingOrder
                    : sortingOrder;
            return animation;
        }

        private static void ConfigureSlotFilter(
            SkeletonAnimation animation,
            EdenGalleryLayer layer)
        {
            List<string> included = new List<string>();
            if (!string.IsNullOrEmpty(layer.slotName))
                included.Add(layer.slotName);
            if (layer.slotNames != null)
            {
                for (int i = 0; i < layer.slotNames.Length; i++)
                {
                    if (!string.IsNullOrEmpty(layer.slotNames[i]))
                        included.Add(layer.slotNames[i]);
                }
            }
            List<string> excluded = new List<string>();
            if (layer.excludeSlotNames != null)
            {
                for (int i = 0; i < layer.excludeSlotNames.Length; i++)
                {
                    if (!string.IsNullOrEmpty(layer.excludeSlotNames[i]))
                        excluded.Add(layer.excludeSlotNames[i]);
                }
            }
            if (included.Count == 0 && excluded.Count == 0)
                return;

            UpdateBonesDelegate filter = delegate(ISkeletonAnimation animated)
            {
                Skeleton skeleton = animated.Skeleton;
                if (skeleton == null)
                    return;
                ExposedList<Slot> slots = skeleton.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    Slot slot = slots.Items[i];
                    string slotName = slot.Data.Name;
                    bool hidden = included.Count > 0 && !included.Contains(slotName);
                    if (!hidden && excluded.Count > 0 && excluded.Contains(slotName))
                        hidden = true;
                    if (hidden)
                        slot.Attachment = null;
                }
            };
            animation.UpdateComplete += filter;
            filter(animation);
        }

        private static string FindAnimationName(SkeletonData data)
        {
            if (data == null || data.Animations == null || data.Animations.Count == 0)
                return string.Empty;

            for (int preferredIndex = 0; preferredIndex < PreferredAnimations.Length; preferredIndex++)
            {
                string preferred = PreferredAnimations[preferredIndex];
                for (int animationIndex = 0; animationIndex < data.Animations.Count; animationIndex++)
                {
                    Spine.Animation animation = data.Animations.Items[animationIndex];
                    if (string.Equals(animation.Name, preferred, StringComparison.OrdinalIgnoreCase))
                        return animation.Name;
                }
            }

            return data.Animations.Items[0].Name;
        }

        private static string ResolveAnimationName(SkeletonData data, string configuredName)
        {
            if (data != null && !string.IsNullOrEmpty(configuredName))
            {
                Spine.Animation configuredAnimation = data.FindAnimation(configuredName);
                if (configuredAnimation != null)
                    return configuredAnimation.Name;
                Debug.LogWarning("Configured Spine animation was not found: " + configuredName);
            }
            return FindAnimationName(data);
        }
    }
}
