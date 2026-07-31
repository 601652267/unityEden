using System;

[Serializable]
public struct Skill11300018UltimateHit
{
    public float timeSeconds;
    public string defenderState;
    public bool isFinal;

    public Skill11300018UltimateHit(
        float timeSeconds,
        string defenderState,
        bool isFinal)
    {
        this.timeSeconds = timeSeconds;
        this.defenderState = defenderState;
        this.isFinal = isFinal;
    }
}

/// <summary>
/// Character-specific battle presentation recovered from the 1.0.19 XAPK.
/// The ultimate timings below come directly from SkillScript11300018 Lua
/// bytecode. Normal/burst timing arrays are presentation cues reconstructed
/// from the original prefabs; they are not damage or server battle logic.
/// </summary>
public static class Skill11300018ApkBattleLogic
{
    public const string XapkSha256 =
        "de98bb8fdc12ff803b265f500737bbf00534002ef295a8950e2e2164fe550f5d";
    public const string SkillScriptSha256 =
        "516cfe0c0dbf3b6e98148c6c7d2eb05737d5a3a8e9306d6ef530c93fa8a50b6c";
    public const string TimelineName = "Fx_timeline_11300018_xp";
    public const int UltimateTotalHitCount = 9;

    public const float NormalReturnTime = 1.40f;
    public const float NormalCleanupTime = 1.82f;
    public const float BurstReturnTime = 2.05f;
    public const float BurstCleanupTime = 2.42f;

    public const float UltimateVideoStartTime = 1.300000f;
    public const float UltimateVideoEndTime = 4.333333f;
    public const float UltimateAttackerInvisibleTime = 5.6994f;
    public const float UltimateDefendersVisibleTime = 5.700f;
    public const float UltimateSwordRevealTime = 6.700f;
    public const float UltimateReturnTime = 9.532f;
    public const float UltimateCleanupTime = 9.970f;

    public static readonly string[] NormalEffectBundles =
    {
        "eft_fx_11300018_attack_2.aab"
    };

    public static readonly string[] BurstEffectBundles =
    {
        "eft_fx_11300018_attack.aab",
        "eft_fx_11300018_skill.aab",
        "eft_fx_11300018_skill_2.aab"
    };

    // These belong to the airborne branch and are retained in the resource
    // set, but the three-button ground demo does not invoke that branch.
    public static readonly string[] AirAttackEffectBundles =
    {
        "eft_fx_11300018_attack_air.aab",
        "eft_fx_11300018_attack_air_hit.aab"
    };

    public static readonly float[] NormalHitTimes =
    {
        0.25f,
        0.66f,
        1.20f
    };

    public static readonly float[] BurstHitTimes =
    {
        0.50f,
        1.15f,
        1.35f,
        1.85f,
        1.92f
    };

    public static readonly Skill11300018UltimateHit[] UltimateHits =
    {
        new Skill11300018UltimateHit(6.865f, "hit_2", false),
        new Skill11300018UltimateHit(7.232f, "hit_1", false),
        new Skill11300018UltimateHit(7.599f, "hit_2", false),
        new Skill11300018UltimateHit(8.665f, "hit_1", false),
        new Skill11300018UltimateHit(8.815f, "hit_2", false),
        new Skill11300018UltimateHit(8.965f, "hit_1", false),
        new Skill11300018UltimateHit(9.115f, "hit_2", false),
        new Skill11300018UltimateHit(9.265f, "hit_1", false),
        new Skill11300018UltimateHit(9.415f, "hit_2", true)
    };
}
