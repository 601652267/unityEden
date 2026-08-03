using UnityEngine;

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
}
