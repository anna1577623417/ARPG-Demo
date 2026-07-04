using UnityEngine;

/// <summary>
/// Player 技能域组件（208.3 L4）— SkillEntryService / InputSemantic / Loadout 语义刷新。
/// Player 保留 Facade 与 SerializeField；装配逻辑迁出 Player.cs。
/// </summary>
public sealed class PlayerSkillComponent
{
    readonly Player _owner;
    SkillEntryService _service;
    InputSemanticResolver _inputSemantic;
    SkillEntryLoadoutSO _loadout;
    SemanticConfigSO _semanticConfig;

    public PlayerSkillComponent(Player owner) => _owner = owner;

    public SkillEntryService Service => _service;
    public InputSemanticResolver InputSemantic => _inputSemantic;
    public SkillEntryLoadoutSO Loadout => _loadout;

    public void Initialize(
        SkillEntryLoadoutSO loadout,
        SemanticConfigSO semanticConfig,
        InputContextResolver inputContext)
    {
        _loadout = loadout;
        _semanticConfig = semanticConfig;
        _service = new SkillEntryService(_owner);
        _service.Rebuild(loadout);
        _inputSemantic = new InputSemanticResolver(_owner);
        RefreshSemanticConfigFromLoadout(inputContext);
    }

    public void RefreshSemanticConfigFromLoadout(InputContextResolver inputContext)
    {
        if (_inputSemantic == null || _loadout?.Bindings == null)
        {
            return;
        }

        for (var i = 0; i < _loadout.Bindings.Length; i++)
        {
            var b = _loadout.Bindings[i];
            var entry = b.Entry;
            if (entry == null)
            {
                continue;
            }

            InputSemanticResolver.PerSlotConfig cfg;
            if (_semanticConfig != null && _semanticConfig.TryResolve(b.Slot, out cfg))
            {
                // SemanticConfigSO 命中 — 玩家级阈值为权威来源。
            }
            else
            {
                cfg = new InputSemanticResolver.PerSlotConfig
                {
                    TapThreshold = entry.ChargeRoute != null ? entry.ChargeRoute.TapThreshold : 0f,
                    ComboWindow = entry.ComboRoute != null ? entry.ComboRoute.ComboSessionResetTime : 0f,
                    EnableDirectional = entry.PrimaryGroup != null
                        || LoadoutHasDirectionalContext(b.Slot),
                };
            }

            if (entry.ComboRoute is ComboRouteDefinition comboDef)
            {
                cfg.ComboChainLength = comboDef.ChainLength;
                cfg.ComboEdgeTimings = comboDef.BuildTransitionTimingsForResolver();
                if (cfg.ComboWindow <= 0.0001f)
                {
                    cfg.ComboWindow = comboDef.ComboSessionResetTime;
                }
            }
            else
            {
                cfg.ComboChainLength = 0;
                cfg.ComboEdgeTimings = null;
            }

            _inputSemantic.ConfigureSlot(b.Slot, in cfg);
            _service?.SyncComboSemanticConfig(b.Slot);
        }

        RefreshInputContextFromLoadout(inputContext);
    }

    static void RefreshInputContextFromLoadout(
        InputContextResolver inputContext,
        InputSemanticResolver inputSemantic,
        SkillEntryLoadoutSO loadout)
    {
        if (inputContext == null || loadout?.Bindings == null)
        {
            inputContext?.SetLoadoutHasDirectionalModifier(false);
            return;
        }

        var anyDirectional = false;
        for (var i = 0; i < loadout.Bindings.Length; i++)
        {
            var slot = loadout.Bindings[i].Slot;
            if (inputSemantic.GetConfig(slot).EnableDirectional)
            {
                anyDirectional = true;
                break;
            }
        }

        inputContext.SetLoadoutHasDirectionalModifier(anyDirectional);
    }

    void RefreshInputContextFromLoadout(InputContextResolver inputContext) =>
        RefreshInputContextFromLoadout(inputContext, _inputSemantic, _loadout);

    bool LoadoutHasDirectionalContext(SkillEntrySlot slot)
    {
        var groups = _loadout?.ContextGroups;
        if (groups == null)
        {
            return false;
        }

        for (var i = 0; i < groups.Length; i++)
        {
            var g = groups[i];
            if (g == null || g.TargetGroup == null)
            {
                continue;
            }

            if (g.RequiredSlot == SkillEntrySlot.Any || g.RequiredSlot == slot)
            {
                return true;
            }
        }

        return false;
    }
}
