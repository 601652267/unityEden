using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class EdenBattle11301005 :
    EdenRecoveredCharacterBattle
{

    // 以下偏移都使用 APK Prefab 的原始坐标单位。公共播放器会
    // 按当前特效缩放比例转换到 Stage 坐标，因此不依赖屏幕尺寸、
    // 相机大小或运行时生成的父节点层级。

    // 受击特效相对于训练怪物中心的微调。
    // X 正数向右，负数向左；Y 正数向上，负数向下。
    private static readonly Vector3 NormalHitEffectOffset =
        Vector3.zero;

    // 普通攻击主斩击 attack.aab 的位置修正。
    // X 正数向右，负数向左；Y 正数向上，负数向下。
    private static readonly Vector3 NormalSlashEffectOffset =
        new Vector3(0f, 0f, 0f);


    private static readonly Vector3 BurstHitEffectOffset =
                new Vector3(25f, 0f, 0f);

    private static readonly Vector3 BurstHitEffectOffset2 =
                new Vector3(20f, 0f, 0f);

    public override void ConfigureSpawnedEffect(
        EdenRecoveredBattlePreview preview,
        string bundleFile,
        GameObject effect)
    {
        if (preview == null || effect == null)
            return;

        if (bundleFile == "eft_fx_11301005_skill.aab")
        {
            // 这两个爆气位置已经过人工校准。先保持原来的视觉
            // 结果，只把局部坐标加法迁移到公共 Stage 换算接口。
            FlipEffectHierarchyX(effect);
            preview.OffsetSpawnedEffectInStage(
                effect,
                bundleFile,
                BurstHitEffectOffset);
            return;
        }

        if (bundleFile == "eft_fx_11301005_skill_2.aab")
        {
            FlipEffectHierarchyX(effect);
            preview.OffsetSpawnedEffectInStage(
                effect,
                bundleFile,
                BurstHitEffectOffset2);
            return;
        }

        if (bundleFile == "eft_fx_11301005_attack.aab")
        {
            // 主斩击固定在近战到位点，不再取决于生成该帧人物
            // 尚未完成的移动插值。attack_2 的受击定位不受影响。
            FlipEffectHierarchyX(effect);
            preview.AlignSpawnedEffectToStageAnchor(
                effect,
                bundleFile,
                EdenRecoveredEffectAnchor.AttackApproach,
                NormalSlashEffectOffset);

            return;
        }

        if (bundleFile == "eft_fx_11301005_attack_2.aab")
        {
            FlipEffectHierarchyX(effect);
            preview.AlignSpawnedEffectToStageAnchor(
                effect,
                bundleFile,
                EdenRecoveredEffectAnchor.EnemyFocus,
                NormalHitEffectOffset);
            return;
        }
    }

    private static readonly string[] InspectionBundles =
    {
        // attack_air 是对空替代特效，不是每次普通攻击都播放。
        "eft_fx_11301005_attack_air.aab"
    };

    public override string CardId
    {
        get { return "11301005"; }
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

            // 奥义
            timelineName = "Fx_timeline_11301005_xp",
            videoFileName = "FX_timeline_11301005_xp.m4v",
            ultimateTotalHitCount = 11,

            // 普通攻击：首轮校时值。
            // 通用播放器的近战位移为 0.24 秒，而 attack Prefab
            // 的第一批可见粒子自带约 0.10 秒 startDelay。
            // 因此在 0.14 秒时生成 Prefab，第一批粒子会在
            // 约 0.24 秒时显示，与人物到达攻击位置基本同步。
            // 这是从动作开始计算的绝对时间，不是到达目标后
            // 再额外等待 0.14 秒。
            normalMovesToTarget = true,
            normalPrimaryEffectAtCaster = true,
            normalEffectStartTime = 0.14f,
            normalReturnTime = 0.90f,
            // 原首轮清理时间也顺延约 0.14 秒，避免末尾粒子
            // 因特效整体延迟而被提前 ClearEffects。
            normalCleanupTime = 1.2f,
            // 0.90 秒时动作和主要命中已结束，隐藏一帧并恢复到
            // 出生位置；树叶、烟雾等尾迹仍播放到 1.20 秒再清理。
            // 返回时刻与清理时刻分离后，人物不会保持攻击末帧发呆。
            normalReturnMode = EdenRecoveredAttackReturnMode
                .HideAndTeleportAtReturnTime,

            // 爆气：目前保持动作开始时立即生成特效。
            // 显式写出 0f 是为了说明该角色的校时意图；不写时
            // float 默认值同样是 0f，不会改变旧角色的行为。
            burstMovesToTarget = true,
            burstPrimaryEffectAtCaster = true,
            burstEffectStartTime = 0f,
            burstReturnTime = 2.20f,
            burstCleanupTime = 2.50f,
            // 爆气也在动作返回时刻复位，尾迹独立播放到清理点。
            burstReturnMode = EdenRecoveredAttackReturnMode
                .HideAndTeleportAtReturnTime,

            // Timeline 中视频轨道：1.000～3.867 秒
            ultimateVideoStartTime = 1.000000f,
            ultimateVideoEndTime = 3.867000f,

            // 来自 SkillScript11301005.lua
            ultimateAttackerInvisibleTime = 4.599540f,
            ultimateAttackerReappearTime = 6.965980f,
            ultimateAttackerSecondInvisibleTime = -1f,
            ultimateDefendersVisibleTime = 6.166050f,
            ultimateEffectRevealTime = 6.166050f,

            // Lua 的 11 表示约 11 秒
            ultimateReturnTime = 11.000000f,
            ultimateIdleTime = 11.000000f,

            // Timeline 实际长度为 12.5 秒
            ultimatePresentationEndTime = 12.500000f,
            ultimateCleanupTime = 12.550000f,

            // 如果奥义整体位置偏高/偏低，只改这里
            timelineContainerYOffset = 0f,

            // 普通攻击入口特效
            normalEffectBundles = new[]
            {
                "eft_fx_11301005_attack.aab",
                "eft_fx_11301005_attack_2.aab"
            },

            // 爆气入口特效
            burstEffectBundles = new[]
            {
                "eft_fx_11301005_skill.aab",
                "eft_fx_11301005_skill_2.aab"
            },

            normalVoiceResources = new[]
            {
                "EdenBattle/Voices/11301005/Xiao_Battle_N_1",
                "EdenBattle/Voices/11301005/Xiao_Battle_N_2",
                "EdenBattle/Voices/11301005/Xiao_Battle_N_3",
                "EdenBattle/Voices/11301005/Xiao_Battle_N_4",
                "EdenBattle/Voices/11301005/Xiao_Battle_N_5",
                "EdenBattle/Voices/11301005/Xiao_Battle_N_6"
            },

            burstVoiceResources = new[]
            {
                "EdenBattle/Voices/11301005/Xiao_Battle_H_1",
                "EdenBattle/Voices/11301005/Xiao_Battle_H_2"
            },

            ultimateVoiceResources = new[]
            {
                "EdenBattle/Voices/11301005/Xiao_Battle_C_1",
                "EdenBattle/Voices/11301005/Xiao_Battle_C_2"
            },

            // baseSkillShowData 只能确定普通攻击是 4 段，
            // hurt_section 中的 2500:2500:2500:2500 是伤害权重，
            // 不是攻击时间。normalHitTimes 是从普通攻击开始
            // 计算的受击时刻，供训练怪播放受击动作。
            // 由于特效现在整体延迟 0.14 秒生成，这里也将
            // 之前从粒子延迟推算的时刻整体加上 0.14 秒。
            // 这些仍然是首轮估算值，最终应根据原版录像逐帧校准。
            normalHitTimes = new[]
            {
                0.24f,
                0.44f,
                0.74f,
                0.79f
            },

            // baseSkillShowData 确定爆气是5段。
            burstHitTimes = new[]
            {
                0.05f,
                0.30f,
                0.50f,
                1.80f,
                1.90f
            },

            // Lua 中能够精确得到的11段奥义受击时间
            ultimateHits = new[]
            {
                new EdenRecoveredSkillHit(
                    7.33260f, "hit_1", false),
                new EdenRecoveredSkillHit(
                    7.56591f, "hit_2", false),
                new EdenRecoveredSkillHit(
                    9.49905f, "hit_1", false),
                new EdenRecoveredSkillHit(
                    9.73236f, "hit_2", false),
                new EdenRecoveredSkillHit(
                    9.86568f, "hit_1", false),
                new EdenRecoveredSkillHit(
                    9.99900f, "hit_2", false),
                new EdenRecoveredSkillHit(
                    10.13232f, "hit_1", false),
                new EdenRecoveredSkillHit(
                    10.23231f, "hit_2", false),
                new EdenRecoveredSkillHit(
                    10.36563f, "hit_1", false),
                new EdenRecoveredSkillHit(
                    10.49895f, "hit_2", false),
                new EdenRecoveredSkillHit(
                    10.66560f, "hit_1", true)
            }
        };
    }
}
