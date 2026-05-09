using UnityEngine;

/// <summary>
/// CastType.Charge：按住时长 → 覆盖段/动作/伤害倍率。
/// Primary+轻击 用 <see cref="GameplayIntent.PrimaryHoldDurationSeconds"/>；Secondary+重击 用 <see cref="GameplayIntent.SecondaryHoldDurationSeconds"/>。
/// Ability1+CastAbility1 用 Primary；Ultimate+CastUltimate 用 Secondary（与 <see cref="SkillSystem"/> 侧取 hold 的口径一致）。
/// </summary>
public static class SkillChargeCommit
{
    public static bool ShouldUseRootChordInsteadOfCombo(
        SkillDataSO root,
        SkillSlotType slot,
        GameplayIntentKind kind,
        float chargeHoldSeconds)
    {
        if (root == null || root.castType != CastType.Charge)
        {
            return false;
        }

        var threshold = Mathf.Max(0f, root.chargeThreshold);
        return (slot == SkillSlotType.Skill_Primary_01
                && kind == GameplayIntentKind.LightAttack
                && chargeHoldSeconds >= threshold)
               || (slot == SkillSlotType.Skill_Primary_03
                   && kind == GameplayIntentKind.ChargeAttack
                   && chargeHoldSeconds >= threshold)
               || (slot == SkillSlotType.Secondary_04
                   && kind == GameplayIntentKind.HeavyAttack
                   && chargeHoldSeconds >= threshold)
               || (slot == SkillSlotType.Ability_06
                   && kind == GameplayIntentKind.CastAbility1
                   && chargeHoldSeconds >= threshold)
               || (slot == SkillSlotType.Ultimate_05
                   && kind == GameplayIntentKind.CastUltimate
                   && chargeHoldSeconds >= threshold);
    }

    /// <summary>
    /// 在已选定 <paramref name="segment"/>/<paramref name="stage"/> 后应用蓄力分档。
    /// </summary>
    public static bool TryApplyChargeOverride(
        SkillDataSO segment,
        SkillSlotType slot,
        GameplayIntentKind kind,
        float chargeHoldSeconds,
        ref SkillStageSO stage,
        ref ActionDataSO actionFromIntent,
        out float damageMultiplier)
    {
        damageMultiplier = 1f;
        if (segment == null
            || segment.castType != CastType.Charge)
        {
            return false;
        }

        var appliesToSlot =
            (slot == SkillSlotType.Skill_Primary_01 && kind == GameplayIntentKind.LightAttack)
            || (slot == SkillSlotType.Skill_Primary_02 && kind == GameplayIntentKind.ComboAttack)
            || (slot == SkillSlotType.Skill_Primary_03 && kind == GameplayIntentKind.ChargeAttack)
            || (slot == SkillSlotType.Secondary_04 && kind == GameplayIntentKind.HeavyAttack)
            || (slot == SkillSlotType.Ability_06 && kind == GameplayIntentKind.CastAbility1)
            || (slot == SkillSlotType.Ultimate_05 && kind == GameplayIntentKind.CastUltimate);

        if (!appliesToSlot
            || chargeHoldSeconds < Mathf.Max(0f, segment.chargeThreshold))
        {
            return false;
        }

        if (segment.chargeLevels != null && segment.chargeLevels.Length > 0)
        {
            if (TryPickChargeLevel(segment.chargeLevels, chargeHoldSeconds, out var level))
            {
                damageMultiplier = Mathf.Max(0f, level.damageMultiplier);
                if (level.stageOverride != null)
                {
                    stage = level.stageOverride;
                }

                if (level.action != null)
                {
                    actionFromIntent = level.action;
                }
                else if (stage != null && stage.action != null)
                {
                    actionFromIntent = stage.action;
                }

                return actionFromIntent != null;
            }
        }

        // 无双档命中 / 未配分档：尝试使用第二 Stage 作为长按段。
        if (segment.stages != null && segment.stages.Length > 1)
        {
            stage = segment.stages[1];
            actionFromIntent = stage != null ? stage.action : actionFromIntent;
            return actionFromIntent != null;
        }

        return false;
    }

    static bool TryPickChargeLevel(ChargeLevel[] levels, float holdSeconds, out ChargeLevel picked)
    {
        picked = default;
        if (levels == null || levels.Length == 0)
        {
            return false;
        }

        var bestIdx = -1;
        var bestT = float.NegativeInfinity;
        for (var i = 0; i < levels.Length; i++)
        {
            var t = levels[i].minHoldTime;
            if (holdSeconds + 1e-4f >= t && t >= bestT)
            {
                bestT = t;
                bestIdx = i;
            }
        }

        if (bestIdx < 0)
        {
            return false;
        }

        picked = levels[bestIdx];
        return true;
    }
}
