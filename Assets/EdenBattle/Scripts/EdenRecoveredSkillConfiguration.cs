/// <summary>
/// Controls how a recovered normal or burst attack returns a character from
/// the target side to the character's original battle position.
/// </summary>
public enum EdenRecoveredAttackReturnMode
{
    /// <summary>
    /// Use the original shared-preview behaviour: at the configured return
    /// time, keep the current Spine pose visible while smoothly moving the
    /// character back. This is the zero/default value so existing character
    /// configurations keep their previous behaviour without any edits.
    /// </summary>
    SmoothMove = 0,

    /// <summary>
    /// Wait until both the configured return time and effect cleanup time have
    /// elapsed, remove the action effects, hide the character for one rendered
    /// frame, teleport it home, switch to idle, and show it again. This avoids
    /// sliding backward in a frozen attack pose and is intended for melee
    /// characters whose original game presentation used an invisible reset.
    /// </summary>
    HideAndTeleportAfterEffects = 1,

    /// <summary>
    /// At the configured return time, hide the character for one rendered
    /// frame, teleport it home and switch it to idle. Unlike
    /// HideAndTeleportAfterEffects, do not clear or wait for decorative effect
    /// tails: they remain alive until the separately configured cleanup time.
    /// This prevents a completed melee attack from freezing in its final pose
    /// merely because leaves, smoke or glow particles are still fading out.
    /// </summary>
    HideAndTeleportAtReturnTime = 2
}

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

    /// <summary>
    /// Seconds from the beginning of the normal-attack sequence to spawning
    /// its effect prefabs. The character starts its approach movement before
    /// this delay is evaluated, allowing melee characters to reach the target
    /// before their slash and impact particles become visible.
    ///
    /// Existing character configurations do not have to assign this field:
    /// the C# default is 0f, which preserves the original behaviour of
    /// spawning normal-attack effects immediately.
    /// </summary>
    public float normalEffectStartTime;

    /// <summary>
    /// Seconds from the beginning of the burst sequence to spawning its
    /// effect prefabs. Like normalEffectStartTime, this is an absolute time
    /// measured from the action start rather than an additional delay after
    /// movement. Its default value is 0f for backward compatibility.
    /// </summary>
    public float burstEffectStartTime;

    public float normalReturnTime;
    public float normalCleanupTime;
    public float burstReturnTime;
    public float burstCleanupTime;

    /// <summary>
    /// Return presentation used after a normal attack. Defaults to SmoothMove
    /// for backward compatibility. Melee characters can independently choose
    /// whether their hidden teleport waits for all effects or leaves decorative
    /// tails playing until normalCleanupTime.
    /// </summary>
    public EdenRecoveredAttackReturnMode normalReturnMode;

    /// <summary>
    /// Return presentation used after a burst attack. This is separate from
    /// normalReturnMode because some characters move during only one action or
    /// use different original-game return presentations for the two actions.
    /// </summary>
    public EdenRecoveredAttackReturnMode burstReturnMode;

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
