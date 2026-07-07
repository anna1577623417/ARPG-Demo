using UnityEngine;



/// <summary>

/// IRouteRuntimeHandle 默认实现 — 由 SkillEntryService 在 Rebuild 时为每条 ShowOnHud Route 创建。

/// HUD 直接通过本句柄读取运行时态，无需感知 RouteRuntime 子类。

/// </summary>

public sealed class RouteRuntimeHandle : IRouteRuntimeHandle

{

    readonly Player _owner;

    readonly SkillRouteDefinition _def;

    readonly SkillRouteRuntime _runtime;

    readonly SkillEntrySlot _slot;

    readonly string _keyLabel;

    readonly SkillEntryService _service;

    readonly string _hudIdentity;

    readonly PresentationContext _presentationCtx;



    public RouteRuntimeHandle(

        Player owner,

        SkillRouteDefinition def,

        SkillRouteRuntime rt,

        SkillEntrySlot slot,

        string keyLabel,

        SkillEntryService service,

        string hudIdentity)

    {

        _owner = owner;

        _def = def;

        _runtime = rt;

        _slot = slot;

        _keyLabel = keyLabel ?? string.Empty;

        _service = service;

        _hudIdentity = hudIdentity ?? string.Empty;

        var entry = (service as IComboSessionHost)?.ResolveEntry(slot);

        _presentationCtx = new PresentationContext(def, null, rt, entry, slot, owner);

    }



    public SkillEntrySlot EntrySlot => _slot;

    public Sprite Icon => SkillPresentationResolver.ResolveIcon(in _presentationCtx);

    public string DisplayName => SkillPresentationResolver.ResolveDisplayName(in _presentationCtx);

    public string KeyLabel => _keyLabel;

    public bool ShowOnHud => _def != null && _def.IsHudVisible();

    public string PresentationId => _def != null ? _def.PresentationId : string.Empty;
    public string UnitId => _def != null ? _def.GetEffectiveUnitId() : string.Empty;
    public string HudUnitKind => "Route";
    public string AssetFolder => HudAssetDebug.GetFolder(_def);
    public string AssetPath => HudAssetDebug.GetPath(_def);
    public int PresentationRevision => _runtime != null ? _runtime.PresentationRevision : 0;

    public string HudIdentity => _hudIdentity;

    public SkillPresentationState PresentationState =>

        SkillPresentationResolver.ResolvePresentationState(in _presentationCtx);

    public PresentationBadge PrimaryBadge =>

        SkillPresentationResolver.ResolvePrimaryBadge(in _presentationCtx);

    public PresentationBadge SecondaryBadge =>

        SkillPresentationResolver.ResolveSecondaryBadge(in _presentationCtx);



    public float CdProgress01

    {

        get

        {

            if (_runtime == null || _runtime.CdScaledTotalSeconds <= 0.0001f) return 1f;

            return Mathf.Clamp01(1f - _runtime.CdRemainingSeconds / _runtime.CdScaledTotalSeconds);

        }

    }



    public float CdRemainingSeconds => _runtime != null ? _runtime.CdRemainingSeconds : 0f;

    public int CurrentCharges => -1;

    public int MaxCharges => -1;



    public bool HasChargeBar => _def is ChargeRouteDefinition;

    public float ChargeProgress01

    {

        get

        {

            if (_runtime is ChargeRouteRuntime ch) return ch.ChargeProgress01;

            return 0f;

        }

    }



    public bool HasComboOverlay => _def is ComboRouteDefinition;

    public int ComboStep

    {

        get

        {

            var combo = _service?.GetCombo(_slot);

            return combo != null && combo.IsComboWindowOpen(Time.time) ? combo.ComboIndex : -1;

        }

    }

    public float ComboWindowRemainingSeconds

    {

        get

        {

            var combo = _service?.GetCombo(_slot);

            return combo != null ? combo.ComboWindowRemain(Time.time) : 0f;

        }

    }



    public bool HasMultiStageOverlay => _def is MultiStageRouteDefinition;

    public int MultiStageIndex => _runtime != null && _runtime.IsActive ? _runtime.CurrentStageIndex : -1;

    public float CurrentStageProgress01 => _runtime?.Stage != null ? _runtime.Stage.NormalizedTime : 0f;

    public float ActiveTransitionWindowRemainingSeconds

    {

        get

        {

            if (_runtime is MultiStageRouteRuntime ms && ms.HasPendingNextStage)

            {

                return ms.PendingWindowRemainingSeconds;

            }



            return _runtime?.Stage?.ArmedTransitionWindowRemain ?? 0f;

        }

    }



    public bool IsOnCooldown => _runtime != null && _runtime.CdRemainingSeconds > 0.0001f;



    public bool HasEnoughResources

    {

        get

        {

            if (_def == null || _owner == null) return true;

            var costs = _def.Costs;

            if (costs == null || costs.Length == 0) return true;



            var resources = _owner.Resources;

            if (resources == null) return true;



            for (var i = 0; i < costs.Length; i++)

            {

                var c = costs[i];

                if (c.ConsumeOnlyOnHit) continue;

                if (resources.GetCurrent(c.ResourceType) < c.BaseAmount)

                {

                    return false;

                }

            }



            return true;

        }

    }



    public bool IsHighlightSuppressed => !HasEnoughResources || IsCastConditionBlocked;



    /// <summary>释放条件门（如击飞才能 R）；未接条件系统前恒 false。</summary>

    bool IsCastConditionBlocked => false;



    public bool CanCastNow

    {

        get

        {

            if (_runtime == null) return false;

            if (_runtime is MultiStageRouteRuntime ms && ms.HasPendingNextStage)

            {

                return ms.PendingWindowRemainingSeconds > 0f

                       && _runtime.CdRemainingSeconds <= 0f

                       && HasEnoughResources;

            }



            if (IsOnCooldown) return false;

            return HasEnoughResources;

        }

    }

}

