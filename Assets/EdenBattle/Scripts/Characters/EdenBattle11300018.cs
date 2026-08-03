public sealed class EdenBattle11300018 : EdenRecoveredCharacterBattle
{
    private static readonly string[] InspectionBundles =
    {
        "eft_labi_shouji.aab"
    };

    public override string CardId
    {
        get { return "11300018"; }
    }

    public override string[] AdditionalInspectionBundles
    {
        get { return InspectionBundles; }
    }

    public override EdenRecoveredSkillConfiguration CreateConfiguration()
    {
        return new EdenRecoveredSkillConfiguration
        {
            cardId = CardId,
            timelineName = Skill11300018ApkBattleLogic.TimelineName,
            videoFileName = "FX_timeline_11300018_xp.m4v",
            ultimateTotalHitCount =
                Skill11300018ApkBattleLogic.UltimateTotalHitCount,
            normalReturnTime =
                Skill11300018ApkBattleLogic.NormalReturnTime,
            normalCleanupTime =
                Skill11300018ApkBattleLogic.NormalCleanupTime,
            burstReturnTime =
                Skill11300018ApkBattleLogic.BurstReturnTime,
            burstCleanupTime =
                Skill11300018ApkBattleLogic.BurstCleanupTime,
            ultimateVideoStartTime =
                Skill11300018ApkBattleLogic.UltimateVideoStartTime,
            ultimateVideoEndTime =
                Skill11300018ApkBattleLogic.UltimateVideoEndTime,
            ultimateAttackerInvisibleTime =
                Skill11300018ApkBattleLogic.UltimateAttackerInvisibleTime,
            ultimateDefendersVisibleTime =
                Skill11300018ApkBattleLogic.UltimateDefendersVisibleTime,
            ultimateEffectRevealTime =
                Skill11300018ApkBattleLogic.UltimateSwordRevealTime,
            ultimateReturnTime =
                Skill11300018ApkBattleLogic.UltimateReturnTime,
            ultimateIdleTime =
                Skill11300018ApkBattleLogic.UltimateReturnTime,
            ultimatePresentationEndTime =
                Skill11300018ApkBattleLogic.UltimateReturnTime,
            ultimateCleanupTime =
                Skill11300018ApkBattleLogic.UltimateCleanupTime,
            normalEffectBundles =
                Skill11300018ApkBattleLogic.NormalEffectBundles,
            burstEffectBundles =
                Skill11300018ApkBattleLogic.BurstEffectBundles,
            normalVoiceResources = new string[0],
            burstVoiceResources = new string[0],
            ultimateVoiceResources = new string[0],
            normalHitTimes = Skill11300018ApkBattleLogic.NormalHitTimes,
            burstHitTimes = Skill11300018ApkBattleLogic.BurstHitTimes,
            ultimateHits = ConvertUltimateHits()
        };
    }

    private static EdenRecoveredSkillHit[] ConvertUltimateHits()
    {
        Skill11300018UltimateHit[] source =
            Skill11300018ApkBattleLogic.UltimateHits;
        EdenRecoveredSkillHit[] result =
            new EdenRecoveredSkillHit[source.Length];
        for (int index = 0; index < source.Length; index++)
        {
            result[index] = new EdenRecoveredSkillHit(
                source[index].timeSeconds,
                source[index].defenderState,
                source[index].isFinal);
        }
        return result;
    }
}
