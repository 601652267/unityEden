using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class EdenBattle11301023 : EdenRecoveredCharacterBattle
{
    public override string CardId
    {
        get { return "11301023"; }
    }

    public override void ConfigureSpawnedEffect(
        EdenRecoveredBattlePreview preview,
        string bundleFile,
        GameObject effect)
    {
        if (preview == null || effect == null)
            return;

        if (bundleFile == "eft_fx_11301023_attack.aab")
        {
            FlipEffectHierarchyX(effect);
            return;
        }

        if (bundleFile == "eft_fx_11301023_skill.aab")
        {
            FlipEffectHierarchyX(effect);
            return;
        }
    }

    public override EdenRecoveredSkillConfiguration CreateConfiguration()
    {
        return new EdenRecoveredSkillConfiguration
        {
            cardId = CardId,
            timelineName = "FX_timeline_11301023_xp",
            videoFileName = "Fx_timeline_11301023_xp.m4v",
            ultimateTotalHitCount = 8,
            normalReturnTime = 1.60f,
            normalCleanupTime = 1.60f,
            burstReturnTime = 2.05f,
            burstCleanupTime = 2.05f,
            // 11301023 的普通攻击和爆气都是近战。人物在动作返回
            // 时刻隐藏一帧并直接恢复到出生位置；尚未结束的烟雾、
            // 刀光尾迹继续播放到各自 cleanupTime，不再让人物为了
            // 等待装饰粒子而保持攻击末帧。
            normalReturnMode = EdenRecoveredAttackReturnMode
                .HideAndTeleportAtReturnTime,
            burstReturnMode = EdenRecoveredAttackReturnMode
                .HideAndTeleportAtReturnTime,
            ultimateVideoStartTime = 1.500000f,
            ultimateVideoEndTime = 6.000000f,
            ultimateAttackerInvisibleTime = 2.000000f,
            ultimateDefendersVisibleTime = 7.199280f,
            ultimateEffectRevealTime = 7.166667f,
            ultimateReturnTime = 9.999000f,
            ultimateIdleTime = 9.999000f,
            ultimatePresentationEndTime = 9.999000f,
            ultimateCleanupTime = 10.733334f,
            normalEffectBundles = new[]
            {
                "eft_fx_11301023_attack.aab",
                "eft_fx_11301023_attack_2.aab"
            },
            burstEffectBundles = new[]
            {
                "eft_fx_11301023_skill.aab",
                "eft_fx_11301023_skill_2.aab"
            },
            normalVoiceResources = new[]
            {
                "EdenBattle/Voices/11301023/Ad_Forze_Battle_N_1",
                "EdenBattle/Voices/11301023/Ad_Forze_Battle_N_2",
                "EdenBattle/Voices/11301023/Ad_Forze_Battle_N_3",
                "EdenBattle/Voices/11301023/Ad_Forze_Battle_N_4",
                "EdenBattle/Voices/11301023/Ad_Forze_Battle_N_5",
                "EdenBattle/Voices/11301023/Ad_Forze_Battle_N_6"
            },
            burstVoiceResources = new[]
            {
                "EdenBattle/Voices/11301023/Ad_Forze_Battle_H_1",
                "EdenBattle/Voices/11301023/Ad_Forze_Battle_H_2"
            },
            ultimateVoiceResources = new[]
            {
                "EdenBattle/Voices/11301023/Ad_Forze_Battle_C_1",
                "EdenBattle/Voices/11301023/Ad_Forze_Battle_C_2"
            },
            normalHitTimes = new[] { 0.45f },
            burstHitTimes = new[] { 0.45f, 0.85f },
            ultimateHits = new[]
            {
                new EdenRecoveredSkillHit(8.765f, "hit_2", false),
                new EdenRecoveredSkillHit(8.915f, "hit_1", false),
                new EdenRecoveredSkillHit(9.065f, "hit_2", false),
                new EdenRecoveredSkillHit(9.215f, "hit_1", false),
                new EdenRecoveredSkillHit(9.365f, "hit_2", false),
                new EdenRecoveredSkillHit(9.515f, "hit_1", false),
                new EdenRecoveredSkillHit(9.665f, "hit_2", false),
                new EdenRecoveredSkillHit(9.999f, "hit_1", true)
            }
        };
    }
}
