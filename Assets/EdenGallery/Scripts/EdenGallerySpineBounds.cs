using Spine;
using Spine.Unity;
using UnityEngine;

namespace EdenGallery
{
    /// <summary>
    /// Calculates a stable native Spine bound by sampling the animation itself.
    /// No browser coordinates or per-character placement data are involved.
    /// </summary>
    public static class EdenGallerySpineBounds
    {
        private const float TargetSamplesPerSecond = 20f;
        private const int MaximumSamples = 61;

        public static bool TryCalculateAnimationBounds(Transform root, out Bounds result)
        {
            result = new Bounds();
            if (root == null)
                return false;

            SkeletonAnimation[] renderers = root.GetComponentsInChildren<SkeletonAnimation>(false);
            bool found = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                SkeletonAnimation renderer = renderers[i];
                Bounds rendererBounds;
                if (!TryCalculateRendererBounds(renderer, out rendererBounds))
                    continue;

                if (!found)
                {
                    result = rendererBounds;
                    found = true;
                }
                else
                {
                    result.Encapsulate(rendererBounds);
                }
            }
            return found;
        }

        private static bool TryCalculateRendererBounds(
            SkeletonAnimation renderer,
            out Bounds result)
        {
            result = new Bounds();
            if (renderer == null || renderer.Skeleton == null || !renderer.valid)
                return false;

            Skeleton source = renderer.Skeleton;
            Skeleton sample = new Skeleton(source.Data);
            if (source.Skin != null)
                sample.SetSkin(source.Skin);
            sample.ScaleX = source.ScaleX;
            sample.ScaleY = source.ScaleY;

            TrackEntry track = renderer.AnimationState == null
                ? null
                : renderer.AnimationState.GetCurrent(0);
            Spine.Animation animation = track == null ? null : track.Animation;
            float duration = animation == null ? 0f : Mathf.Max(animation.Duration, 0f);
            int sampleCount = animation == null
                ? 1
                : Mathf.Clamp(
                    Mathf.CeilToInt(duration * TargetSamplesPerSecond) + 1,
                    2,
                    MaximumSamples);

            bool found = false;
            float[] vertexBuffer = null;
            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                sample.SetToSetupPose();
                if (animation != null)
                {
                    float time = sampleCount <= 1
                        ? 0f
                        : duration * sampleIndex / (sampleCount - 1f);
                    animation.PoseSkeleton(sample, time, false);
                }
                sample.UpdateWorldTransform();

                float x;
                float y;
                float width;
                float height;
                sample.GetBounds(out x, out y, out width, out height, ref vertexBuffer);
                if (!IsUsableBounds(x, y, width, height))
                    continue;

                Bounds worldBounds = TransformBounds(
                    renderer.transform,
                    x,
                    y,
                    width,
                    height);
                if (!found)
                {
                    result = worldBounds;
                    found = true;
                }
                else
                {
                    result.Encapsulate(worldBounds);
                }
            }
            return found;
        }

        private static Bounds TransformBounds(
            Transform transform,
            float x,
            float y,
            float width,
            float height)
        {
            Vector3 bottomLeft = transform.TransformPoint(x, y, 0f);
            Bounds result = new Bounds(bottomLeft, Vector3.zero);
            result.Encapsulate(transform.TransformPoint(x + width, y, 0f));
            result.Encapsulate(transform.TransformPoint(x, y + height, 0f));
            result.Encapsulate(transform.TransformPoint(x + width, y + height, 0f));
            return result;
        }

        private static bool IsUsableBounds(float x, float y, float width, float height)
        {
            return IsFinite(x) && IsFinite(y) && IsFinite(width) && IsFinite(height) &&
                   width > 0.0001f && height > 0.0001f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
