using System;
using UnityEngine;

/// <summary>
/// 单槽技能运行时状态：CD、充能、连招、蓄力占位等。
/// </summary>
public sealed class SkillRuntime
{
    public readonly SkillDataSO Data;

    public int Level = 1;
    public int CurrentStageIndex;
    public bool IsActive;

    public float CdRemaining;
    public int CurrentCharges;
    public float RechargeTimer;

    public int ComboIndex;
    public float LastInputTime = -999f;

    public float ChargeStartTime;
    public bool IsCharging;

    public bool SecondStageAvailable;
    public float SecondStageExpireTime;

    /// <summary>OnLastCast：多段前半段退场时跳过本次 CD，由末尾段退场再结算。</summary>
    public bool SuppressLastCastCooldownThisExit;

    public SkillRuntime(SkillDataSO data)
    {
        Data = data;
        if (data != null && data.maxCharges > 1)
        {
            CurrentCharges = data.maxCharges;
        }
    }

    public bool CanCast(IStatSet stats, IResourcePool resources, ref GameplayTagContainer tags, Entity self)
    {
        if (Data == null)
        {
            return false;
        }

        if (self != null && self.IsDead)
        {
            return false;
        }

        if (tags.HasAny(TagCategory.Status, (ulong)StatusTag.Stun))
        {
            return false;
        }

        if (!IsCooldownReady())
        {
            return false;
        }

        if (!HasEnoughResource(resources))
        {
            return false;
        }

        if (CurrentStageIndex > 0 && !SecondStageAvailable)
        {
            return false;
        }

        return true;
    }

    public bool IsCooldownReady()
    {
        if (Data == null)
        {
            return false;
        }

        if (Data.maxCharges > 1)
        {
            return CurrentCharges > 0;
        }

        return CdRemaining <= 0f;
    }

    public bool HasEnoughResource(IResourcePool pool)
    {
        if (Data.costs == null || pool == null)
        {
            return true;
        }

        for (var i = 0; i < Data.costs.Length; i++)
        {
            var cost = Data.costs[i];
            var amount = GetScaledCost(cost);
            if (pool.GetCurrent(cost.resourceType) < amount)
            {
                return false;
            }
        }

        return true;
    }

    public float GetScaledCooldown()
    {
        if (Data == null)
        {
            return 0f;
        }

        return Data.cooldownByLevel != null && Data.cooldownByLevel.length > 0
            ? Data.cooldownByLevel.Evaluate(Level)
            : Data.baseCooldown;
    }

    public float ApplyCooldownReduction(IStatSet stats, float baseSeconds)
    {
        if (stats == null)
        {
            return baseSeconds;
        }

        var cdr = Mathf.Clamp(stats.Get(StatType.CooldownReduction), 0f, 0.4f);
        return baseSeconds * (1f - cdr);
    }

    public float GetScaledCost(SkillCost cost)
    {
        if (Data?.costByLevel != null && Data.costByLevel.length > 0)
        {
            return Data.costByLevel.Evaluate(Level);
        }

        return cost.baseAmount;
    }

    public float GetScaledDamage(SkillDataSO sheet)
    {
        var s = sheet != null ? sheet : Data;
        if (s == null)
        {
            return 0f;
        }

        return s.damageByLevel != null && s.damageByLevel.length > 0
            ? s.damageByLevel.Evaluate(Level)
            : 0f;
    }

    public bool HasTrait(SkillTrait trait)
    {
        if (Data == null)
        {
            return false;
        }

        var effective = Data.traits;
        if (Data.traitUnlocks != null)
        {
            for (var i = 0; i < Data.traitUnlocks.Length; i++)
            {
                var u = Data.traitUnlocks[i];
                if (Level >= u.requiredLevel)
                {
                    effective |= u.trait;
                }
            }
        }

        return (effective & trait) != 0;
    }

    public void StartCooldown(IStatSet stats)
    {
        if (Data == null)
        {
            return;
        }

        var cd = ApplyCooldownReduction(stats, GetScaledCooldown());

        if (Data.maxCharges > 1)
        {
            CurrentCharges = Mathf.Max(0, CurrentCharges - 1);
            if (RechargeTimer <= 0f && CurrentCharges < Data.maxCharges)
            {
                RechargeTimer = ApplyCooldownReduction(stats, Data.rechargeTime);
            }
        }
        else
        {
            CdRemaining = cd;
        }
    }

    public void TickCooldown(float deltaTime, IStatSet stats)
    {
        if (Data == null)
        {
            return;
        }

        if (CdRemaining > 0f)
        {
            CdRemaining -= deltaTime;
        }

        if (Data.maxCharges > 1 && CurrentCharges < Data.maxCharges)
        {
            RechargeTimer -= deltaTime;
            if (RechargeTimer <= 0f)
            {
                CurrentCharges++;
                if (CurrentCharges < Data.maxCharges)
                {
                    RechargeTimer = ApplyCooldownReduction(stats, Data.rechargeTime);
                }
                else
                {
                    RechargeTimer = 0f;
                }
            }
        }
    }

    public void ModifyCooldown(CooldownOp op, float value)
    {
        switch (op)
        {
            case CooldownOp.Reset:
                CdRemaining = 0f;
                if (Data != null && Data.maxCharges > 1)
                {
                    CurrentCharges = Data.maxCharges;
                    RechargeTimer = 0f;
                }

                break;
            case CooldownOp.ReduceFlat:
                CdRemaining = Mathf.Max(0f, CdRemaining - value);
                break;
            case CooldownOp.ReducePercent:
                CdRemaining *= 1f - value;
                break;
            case CooldownOp.Set:
                CdRemaining = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(op), op, null);
        }
    }

    public void AdvanceCombo()
    {
        ComboIndex++;
        LastInputTime = Time.time;
        if (Data?.comboChain != null && ComboIndex >= Data.comboChain.Length)
        {
            ResetCombo();
        }
    }

    public void ResetCombo()
    {
        ComboIndex = 0;
    }

    public bool IsComboExpired()
    {
        if (Data == null || Data.comboChain == null || Data.comboChain.Length == 0)
        {
            return false;
        }

        return Time.time - LastInputTime > Data.comboResetTime;
    }

    public SkillStageSO GetCurrentStage()
    {
        if (Data?.stages == null || CurrentStageIndex >= Data.stages.Length)
        {
            return null;
        }

        return Data.stages[CurrentStageIndex];
    }

    public bool HasNextStage()
    {
        return Data?.stages != null && CurrentStageIndex + 1 < Data.stages.Length;
    }

    public void AdvanceStage()
    {
        CurrentStageIndex++;
    }

    public void ResetStage()
    {
        CurrentStageIndex = 0;
        SecondStageAvailable = false;
        IsActive = false;
        SuppressLastCastCooldownThisExit = false;
    }

    /// <summary>刷新二段可用窗口截止时间。</summary>
    public void OpenSecondStageWindow(float secondsFromNow)
    {
        SecondStageAvailable = true;
        SecondStageExpireTime = Time.time + secondsFromNow;
    }

    public void TickSecondStageWindow()
    {
        if (!SecondStageAvailable || Data == null)
        {
            return;
        }

        if (Time.time > SecondStageExpireTime)
        {
            SecondStageAvailable = false;
            CurrentStageIndex = 0;
        }
    }
}
