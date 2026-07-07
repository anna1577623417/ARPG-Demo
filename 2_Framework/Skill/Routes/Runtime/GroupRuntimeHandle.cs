using UnityEngine;

/// <summary>
/// 技能组 HUD 句柄 — 一组八向/共享 CD Route 在 HUD 上只占一个 Widget。
/// 显示名/图标/CD/消耗读 <see cref="SkillGroupDefinition"/>；子 Route 的 ShowOnHud 被忽略。
/// </summary>
public sealed class GroupRuntimeHandle : IRouteRuntimeHandle
{
    readonly Player _owner;
    readonly SkillGroupDefinition _group;
    readonly SkillEntrySlot _slot;
    readonly string _keyLabel;
    readonly SkillEntryService _service;
    readonly string _hudIdentity;
    readonly PresentationContext _presentationCtx;

    public GroupRuntimeHandle(
        Player owner,
        SkillGroupDefinition group,
        SkillEntrySlot slot,
        string keyLabel,
        SkillEntryService service,
        string hudIdentity)
    {
        _owner = owner;
        _group = group;
        _slot = slot;
        _keyLabel = keyLabel ?? string.Empty;
        _service = service;
        _hudIdentity = hudIdentity ?? string.Empty;
        var entry = (service as IComboSessionHost)?.ResolveEntry(slot);
        _presentationCtx = new PresentationContext(null, group, null, entry, slot, owner);
    }

    public SkillEntrySlot EntrySlot => _slot;
    public Sprite Icon => SkillPresentationResolver.ResolveIcon(in _presentationCtx);
    public string DisplayName => SkillPresentationResolver.ResolveDisplayName(in _presentationCtx);
    public string KeyLabel => _keyLabel;
    public bool ShowOnHud => _group != null && _group.IsHudVisible();
    public string PresentationId => _group != null ? _group.PresentationId : string.Empty;
    public string UnitId => _group != null ? _group.GetEffectiveUnitId() : string.Empty;
    public string HudUnitKind => "Group";
    public string AssetFolder => HudAssetDebug.GetFolder(_group);
    public string AssetPath => HudAssetDebug.GetPath(_group);
    public int PresentationRevision => 0;
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
            if (_service == null || _group == null
                || !_service.TryGetGroupCooldownState(_group, out var remaining, out var total)
                || total <= 0.0001f)
            {
                return 1f;
            }

            return Mathf.Clamp01(1f - remaining / total);
        }
    }

    public float CdRemainingSeconds =>
        _service != null && _group != null
        && _service.TryGetGroupCooldownState(_group, out var remaining, out _)
            ? remaining
            : 0f;

    public int CurrentCharges => -1;
    public int MaxCharges => -1;
    public bool HasChargeBar => false;
    public float ChargeProgress01 => 0f;
    public bool HasComboOverlay => false;
    public int ComboStep => -1;
    public float ComboWindowRemainingSeconds => 0f;
    public bool HasMultiStageOverlay => false;
    public int MultiStageIndex => -1;
    public float CurrentStageProgress01 => 0f;
    public float ActiveTransitionWindowRemainingSeconds => 0f;
    public bool IsOnCooldown => CdRemainingSeconds > 0.0001f;

    public bool HasEnoughResources
    {
        get
        {
            if (_group == null || _owner == null)
            {
                return true;
            }

            var costs = _group.Costs;
            if (costs == null || costs.Length == 0)
            {
                return true;
            }

            var resources = _owner.Resources;
            if (resources == null)
            {
                return true;
            }

            for (var i = 0; i < costs.Length; i++)
            {
                var c = costs[i];
                if (c.ConsumeOnlyOnHit)
                {
                    continue;
                }

                if (resources.GetCurrent(c.ResourceType) < c.BaseAmount)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public bool IsHighlightSuppressed => !HasEnoughResources;
    public bool CanCastNow => !IsOnCooldown && HasEnoughResources;
}
