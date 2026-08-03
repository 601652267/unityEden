using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 梅伊（11301006）的还原数据和特殊挂点处理。
/// 调整普通攻击法杖特效时，只需要修改 NormalWeaponEffectOffset。
/// </summary>
public sealed class EdenBattle11301006 : EdenRecoveredCharacterBattle
{
    // 世界坐标：X 正数向右，Y 正数向上。只影响普通攻击法杖特效。
    private static readonly Vector3 NormalWeaponEffectOffset =
        Vector3.zero;

    private static readonly EdenRecoveredWeaponAnchor NormalWeaponAnchor =
        new EdenRecoveredWeaponAnchor(
            "wuqi",
            false,
            NormalWeaponEffectOffset);

    private static readonly EdenRecoveredWeaponAnchor BurstWeaponAnchor =
        new EdenRecoveredWeaponAnchor(
            "wuqi",
            false,
            Vector3.zero);

    private static readonly EdenRecoveredWeaponAnchor UltimateAnchor =
        new EdenRecoveredWeaponAnchor(
            "wuqi",
            false,
            Vector3.zero);

    public override string CardId
    {
        get { return "11301006"; }
    }

    public override bool UsesUltimateProjectileAlignment
    {
        get { return true; }
    }

    public override float UltimateProjectileAlignmentTime
    {
        get { return 4.5f; }
    }

    public override string UltimateProjectileObjectName
    {
        get { return "FX_zidan01"; }
    }

    public override string UltimateMuzzleObjectName
    {
        get { return "Shoot-baofa"; }
    }

    public override EdenRecoveredWeaponAnchor UltimateWeaponAnchor
    {
        get { return UltimateAnchor; }
    }

    public override void ConfigureSpawnedEffect(
        EdenRecoveredBattlePreview preview,
        string bundleFile,
        GameObject effect)
    {
        if (preview == null || effect == null)
            return;

        bool attack = bundleFile.Equals(
            "eft_fx_11301006_attack.aab",
            StringComparison.OrdinalIgnoreCase);
        bool skill = bundleFile.Equals(
            "eft_fx_11301006_skill.aab",
            StringComparison.OrdinalIgnoreCase);
        if (!attack && !skill)
            return;

        List<Transform> targets = new List<Transform>();
        if (attack)
        {
            // The visible staff flash is the complete glow group. Moving only
            // its FX_daoguang_004_sh child leaves the surrounding glow, rings
            // and smoke at the prefab's original lower position.
            Transform attackWeaponEffect = effect.transform.Find(
                "FX_11301006_attack_grp/glow (17)");
            if (attackWeaponEffect != null)
                targets.Add(attackWeaponEffect);
        }

        ParticleSystem[] particles =
            effect.GetComponentsInChildren<ParticleSystem>(true);
        for (int index = 0; index < particles.Length; index++)
        {
            ParticleSystem particle = particles[index];
            ParticleSystemRenderer renderer =
                particle.GetComponent<ParticleSystemRenderer>();
            if (renderer == null)
                continue;

            if (!skill)
                continue;
            ParticleSystem.MainModule main = particle.main;
            float delay = main.startDelay.constant;
            if (delay < 1.70f || delay > 1.90f)
                continue;
            if (EdenRecoveredBattlePreview.RendererUsesTexture(
                    renderer,
                    "Glow_04_kf") ||
                EdenRecoveredBattlePreview.RendererUsesTexture(
                    renderer,
                    "FX_glow_022_sh") ||
                EdenRecoveredBattlePreview.RendererUsesTexture(
                    renderer,
                    "sfx_tx_11200001_07"))
            {
                targets.Add(particle.transform);
            }
        }

        if (targets.Count == 0)
        {
            preview.LogCharacterDetail(
                "Weapon FX anchors unavailable: " + bundleFile);
            return;
        }

        preview.StartWeaponEffectFollower(
            targets,
            attack ? 0.85f : 2.35f,
            attack ? NormalWeaponAnchor : BurstWeaponAnchor,
            bundleFile);
    }

    public override EdenRecoveredSkillConfiguration CreateConfiguration()
    {
        return new EdenRecoveredSkillConfiguration
        {
            cardId = CardId,
            timelineName = "Fx_timeline_11301006_xp",
            videoFileName = "FX_timeline_11301006_XP.m4v",
            ultimateTotalHitCount = 16,
            normalReturnTime = 0f,
            normalCleanupTime = 0.85f,
            burstReturnTime = 0f,
            // skill_2 的收尾爆炸粒子在 3.0/3.3/3.4 秒出现。
            // 过早清理会直接截掉最后一击及其 0.35 秒余辉。
            burstCleanupTime = 4.10f,
            normalMovesToTarget = false,
            burstMovesToTarget = false,
            normalPrimaryEffectAtCaster = true,
            burstPrimaryEffectAtCaster = true,
            ultimateVideoStartTime = 1.500000f,
            ultimateVideoEndTime = 4.500000f,
            ultimateAttackerInvisibleTime = 1.499000f,
            ultimateAttackerReappearTime = 4.499000f,
            ultimateAttackerSecondInvisibleTime = 5.799000f,
            ultimateDefendersVisibleTime = 5.999000f,
            ultimateEffectRevealTime = 5.999000f,
            ultimateReturnTime = 8.665000f,
            ultimateIdleTime = 10.000000f,
            ultimatePresentationEndTime = 10.665000f,
            ultimateCleanupTime = 10.667000f,
            timelineContainerYOffset = -4f,
            normalEffectBundles = new[]
            {
                "eft_fx_11301006_attack.aab",
                "eft_fx_11301006_attack_2.aab"
            },
            burstEffectBundles = new[]
            {
                "eft_fx_11301006_skill.aab",
                "eft_fx_11301006_skill_2.aab"
            },
            normalVoiceResources = new[]
            {
                "EdenBattle/Voices/11301006/May_Battle_N_1",
                "EdenBattle/Voices/11301006/May_Battle_N_2",
                "EdenBattle/Voices/11301006/May_Battle_N_3",
                "EdenBattle/Voices/11301006/May_Battle_N_4",
                "EdenBattle/Voices/11301006/May_Battle_N_5"
            },
            burstVoiceResources = new[]
            {
                "EdenBattle/Voices/11301006/May_Battle_H_1",
                "EdenBattle/Voices/11301006/May_Battle_H_2"
            },
            ultimateVoiceResources = new[]
            {
                "EdenBattle/Voices/11301006/May_Battle_C_1",
                "EdenBattle/Voices/11301006/May_Battle_C_2"
            },
            normalHitTimes = new[] { 0.33f, 0.46f, 0.59f },
            burstHitTimes = new[] { 3.00f, 3.30f, 3.40f },
            ultimateHits = new[]
            {
                new EdenRecoveredSkillHit(7.432f, "hit_1", false),
                new EdenRecoveredSkillHit(7.599f, "hit_2", false),
                new EdenRecoveredSkillHit(7.765f, "hit_1", false),
                new EdenRecoveredSkillHit(7.932f, "hit_2", false),
                new EdenRecoveredSkillHit(8.065f, "hit_1", false),
                new EdenRecoveredSkillHit(8.199f, "hit_2", false),
                new EdenRecoveredSkillHit(8.365f, "hit_1", false),
                new EdenRecoveredSkillHit(8.565f, "hit_2", false),
                new EdenRecoveredSkillHit(8.732f, "hit_1", false),
                new EdenRecoveredSkillHit(8.965f, "hit_2", false),
                new EdenRecoveredSkillHit(9.165f, "hit_1", false),
                new EdenRecoveredSkillHit(9.332f, "hit_2", false),
                new EdenRecoveredSkillHit(9.499f, "hit_1", false),
                new EdenRecoveredSkillHit(9.665f, "hit_2", false),
                new EdenRecoveredSkillHit(9.832f, "hit_1", false),
                new EdenRecoveredSkillHit(9.999f, "hit_2", true)
            }
        };
    }
}
