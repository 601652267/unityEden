using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Semantic Stage-space anchors used by recovered battle effects. Character
/// scripts select an anchor and an APK-prefab-space correction; the shared
/// preview owns all conversion to the actual world transform.
/// </summary>
public enum EdenRecoveredEffectAnchor
{
    /// <summary>The attacker's current Stage-local position.</summary>
    CurrentCaster = 0,

    /// <summary>The fixed position reached by a melee approach.</summary>
    AttackApproach = 1,

    /// <summary>The centre of the current defender.</summary>
    EnemyFocus = 2,

    /// <summary>The attacker's home position.</summary>
    CharacterStart = 3,

    /// <summary>The recovered ultimate casting position.</summary>
    UltimateCast = 4
}

/// <summary>
/// Selects one semantic end of a Spine RegionAttachment and applies the
/// character/action-specific correction after converting it to world space.
/// </summary>
public struct EdenRecoveredWeaponAnchor
{
    public readonly string attachmentNameFragment;
    public readonly bool useFirstEdge;
    public readonly Vector3 worldOffset;

    public EdenRecoveredWeaponAnchor(
        string attachmentNameFragment,
        bool useFirstEdge,
        Vector3 worldOffset)
    {
        this.attachmentNameFragment = attachmentNameFragment;
        this.useFirstEdge = useFirstEdge;
        this.worldOffset = worldOffset;
    }
}

/// <summary>
/// Per-character extension point for recovered APK battles. The shared player
/// owns loading and sequencing; subclasses own recovered data and exceptional
/// effect placement for one card only.
/// </summary>
public abstract class EdenRecoveredCharacterBattle
{
    private static readonly string[] EmptyBundles = new string[0];

    public abstract string CardId { get; }

    public abstract EdenRecoveredSkillConfiguration CreateConfiguration();

    public virtual string[] AdditionalInspectionBundles
    {
        get { return EmptyBundles; }
    }

    public virtual bool UsesUltimateProjectileAlignment
    {
        get { return false; }
    }

    public virtual float UltimateProjectileAlignmentTime
    {
        get { return 0f; }
    }

    public virtual string UltimateProjectileObjectName
    {
        get { return string.Empty; }
    }

    public virtual string UltimateMuzzleObjectName
    {
        get { return string.Empty; }
    }

    public virtual EdenRecoveredWeaponAnchor UltimateWeaponAnchor
    {
        get
        {
            return new EdenRecoveredWeaponAnchor(
                "wuqi",
                false,
                Vector3.zero);
        }
    }

    public virtual void ConfigureSpawnedEffect(
        EdenRecoveredBattlePreview preview,
        string bundleFile,
        GameObject effect)
    {
    }

    /// <summary>
    /// 自动查找特效根节点下所有可见内容所属的最上层分组，
    /// 并相对于特效根节点进行纯 X 轴镜像。
    ///
    /// 每个最上层分组只处理一次，避免嵌套节点被重复翻转。
    /// 不修改 Y、Z 坐标，也不使用 Y 轴旋转。
    /// </summary>
    protected static int FlipEffectHierarchyX(GameObject effect)
    {
        if (effect == null)
            return 0;

        Transform effectRoot = effect.transform;

        // 如果 Prefab 根节点本身就是 Renderer，直接翻转整个根节点。
        // 根节点的缩放会自动传递给下面全部子节点。
        if (effectRoot.GetComponent<Renderer>() != null ||
            effectRoot.childCount == 0)
        {
            FlipTransformAroundParentX(effectRoot);
            return 1;
        }

        Renderer[] renderers =
            effect.GetComponentsInChildren<Renderer>(true);

        HashSet<Transform> topLevelGroups =
            new HashSet<Transform>();

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null)
                continue;

            Transform group = renderer.transform;

            // 从 Renderer 向上查找，直到找到 effectRoot 的直接子节点。
            while (group.parent != null &&
                   group.parent != effectRoot)
            {
                group = group.parent;
            }

            if (group.parent == effectRoot)
                topLevelGroups.Add(group);
        }

        foreach (Transform group in topLevelGroups)
            FlipTransformAroundParentX(group);

        return topLevelGroups.Count;
    }

    /// <summary>
    /// 将一个分组相对于其父节点进行二维水平镜像。
    /// </summary>
    private static void FlipTransformAroundParentX(Transform target)
    {
        if (target == null)
            return;

        // 镜像整个分组的位置。
        Vector3 position = target.localPosition;
        position.x = -position.x;
        target.localPosition = position;

        // 镜像分组自身以及其全部后代。
        Vector3 scale = target.localScale;
        scale.x = -scale.x;
        target.localScale = scale;
    }






}
