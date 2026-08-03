/// <summary>
/// Runtime data recovered from the original XAPK. Character-specific values
/// live in EdenBattle&lt;cardId&gt;.cs; this type only describes the data consumed
/// by the shared player.
/// </summary>
public sealed class EdenRecoveredSkillConfiguration
{
    public string cardId;
    public string timelineName;
    public string videoFileName;
    public int ultimateTotalHitCount;
    public float normalReturnTime;
    public float normalCleanupTime;
    public float burstReturnTime;
    public float burstCleanupTime;
    public bool normalMovesToTarget = true;
    public bool burstMovesToTarget = true;
    public bool normalPrimaryEffectAtCaster;
    public bool burstPrimaryEffectAtCaster;
    public float ultimateVideoStartTime;
    public float ultimateVideoEndTime;
    public float ultimateAttackerInvisibleTime;
    public float ultimateAttackerReappearTime = -1f;
    public float ultimateAttackerSecondInvisibleTime = -1f;
    public float ultimateDefendersVisibleTime;
    public float ultimateEffectRevealTime;
    public float ultimateReturnTime;
    public float ultimateIdleTime;
    public float ultimatePresentationEndTime;
    public float ultimateCleanupTime;
    public float timelineContainerYOffset;
    public string[] normalEffectBundles;
    public string[] burstEffectBundles;
    public string[] normalVoiceResources;
    public string[] burstVoiceResources;
    public string[] ultimateVoiceResources;
    public float[] normalHitTimes;
    public float[] burstHitTimes;
    public EdenRecoveredSkillHit[] ultimateHits;

    public string BundleDirectoryName
    {
        get { return "Skill" + cardId + "Original"; }
    }

    public string VideoDirectoryName
    {
        get { return "Skill" + cardId; }
    }

    public string CharacterBundleName
    {
        get { return "m_cardspine_" + cardId + ".aab"; }
    }

    public string TimelineBundleName
    {
        get { return "eft_fx_timeline_" + cardId + "_xp.aab"; }
    }

    // Compatibility facade for existing controller/editor callers.
    public static bool Supports(string cardId)
    {
        return EdenRecoveredCharacterBattleRegistry.Supports(cardId);
    }

    public static EdenRecoveredSkillConfiguration ForCard(string cardId)
    {
        return EdenRecoveredCharacterBattleRegistry
            .ForCard(cardId)
            .CreateConfiguration();
    }
}
