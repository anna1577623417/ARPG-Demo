using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 连段路由 — 「链式技能调度器」，不是动作本体。
///
/// ═══ 116.1 语义：Transition 边，不是平行 Window 数组 ═══
///   · N 个 Combo Node → N-1 条 Transition（chain[i] → chain[i+1]）。
///   · 每条边独立配置 Min/Max 输入间隔；窗口属于「跳转」，不属于「节点下标」。
///   · ComboSessionResetTime = Session 级断档上限（Semantic Resolver 粗窗口）。
///   · 细窗口由 comboTransitions[edgeIndex] 驱动，经 Player 注入 InputSemanticResolver + SkillEntryService 双点校验。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/SkillRoute/Route/Combo Route", fileName = "Route_Combo_")]
public sealed class ComboRouteDefinition : SkillRouteDefinition
{
    /// <summary>单条有向边 chain[from] → chain[to] 的衔接规则。</summary>
    [Serializable]
    public struct ComboTransition
    {
        [SerializeField, Min(0f),
         Tooltip("进入下一段前，相对「上一段按键提交时刻」的最短间隔（秒）。例 0.2 — 防过快 Cancel。")]
        float minInputTime;

        [SerializeField, Min(0f),
         Tooltip("进入下一段前，相对「上一段按键」的最长间隔（秒）。0 = 仅受 Combo Session Reset Time 约束。")]
        float maxInputTime;

        [SerializeField, Tooltip("是否要求上一段至少命中一次才允许沿本边衔接（预留；当前仅配置校验）。")]
        bool requireHit;

        [SerializeField, Tooltip("是否允许在上一段子招中段缓冲下一段输入（与 AllowEarlyBufferInput 配合）。")]
        bool allowInputBuffer;

        public float MinInputTime => minInputTime;
        public float MaxInputTime => maxInputTime;
        public bool RequireHit => requireHit;
        public bool AllowInputBuffer => allowInputBuffer;

        public static ComboTransition CreateDefault(float min = 0.2f, float max = 0f)
        {
            return new ComboTransition
            {
                minInputTime = min,
                maxInputTime = max,
                allowInputBuffer = true,
            };
        }
    }

    /// <summary>供 InputSemanticResolver 注入的边时间快照（0-GC 只读数组）。</summary>
    public struct TransitionTimingSnapshot
    {
        public float MinGap;
        public float MaxGap;
    }

    [Header("Combo Nodes (SubRoutes)")]
    [SerializeField, Tooltip("连段节点 — 每项为一条 SubRoute（通常 NormalRoute）。容器 Stages[] 应为空。")]
    SkillRouteDefinition[] comboChain;

    [Header("Combo Transitions (Edges)")]
    [SerializeField, Tooltip(
        "衔接边 — 长度 = ComboChain.Length - 1。\n" +
        "transitions[i] = comboChain[i] → comboChain[i+1] 的输入许可区间。")]
    ComboTransition[] comboTransitions;

    [Header("Combo Session")]
    [FormerlySerializedAs("comboResetTime")]
    [SerializeField, Min(0f),
     Tooltip("Session 级断档上限：距「上一连段自然结束」超过此值则 Session 结束并进容器 CD。\n" +
             "同步到 SemanticConfig.ComboWindow / Resolver.ComboWindow。")]
    float comboSessionResetTime = 1.2f;

    [SerializeField, Tooltip("Session 内是否允许在子招动画中段预输入下一段。")]
    bool allowEarlyBufferInput = true;

    [Header("Combat Flow (147.1)")]
    [SerializeField, Tooltip(
        "允许 CombatGraph OnSegmentComplete 沿 Flow 边自动施放下一段 Combo 子 Route。\n" +
        "默认 false — 地面连段仍由 ComboRoute + link window + Resolve 负责。")]
    bool allowFlowSegmentAdvance;

    [SerializeField, HideInInspector, Tooltip("已废弃 — 仅用于从旧资产迁移到 comboTransitions。")]
    ComboLinkInputWindowLegacy[] linkInputWindows;

    [Serializable]
    struct ComboLinkInputWindowLegacy
    {
        public float minIntervalAfterPrevious;
        public float maxIntervalAfterPrevious;
    }

    [SerializeField, Tooltip("已废弃：段位由 InputSemanticResolver 维护。")]
    bool fallbackToFirstOnExpire = true;

    public SkillRouteDefinition[] ComboChain => comboChain;
    public ComboTransition[] ComboTransitions => comboTransitions;
    public float ComboSessionResetTime => comboSessionResetTime;
    public bool AllowEarlyBufferInput => allowEarlyBufferInput;
    public bool AllowFlowSegmentAdvance => allowFlowSegmentAdvance;
    public float ComboResetTime => comboSessionResetTime;
    public bool FallbackToFirstOnExpire => fallbackToFirstOnExpire;

    public int ChainLength => comboChain != null ? comboChain.Length : 0;
    public int TransitionCount => Mathf.Max(0, ChainLength - 1);

    public bool ContainsSubRoute(SkillRouteDefinition subRoute)
    {
        if (subRoute == null || comboChain == null)
        {
            return false;
        }

        for (var i = 0; i < comboChain.Length; i++)
        {
            if (comboChain[i] == subRoute)
            {
                return true;
            }
        }

        return false;
    }

    public SkillRouteDefinition GetSubRoute(int nodeIndex)
    {
        if (comboChain == null || comboChain.Length == 0) return null;
        nodeIndex = Mathf.Clamp(nodeIndex, 0, comboChain.Length - 1);
        return comboChain[nodeIndex];
    }

    /// <summary>取边 transitions[edgeIndex]，表示 chain[edgeIndex] → chain[edgeIndex+1]。</summary>
    public ComboTransition GetTransition(int edgeIndex)
    {
        if (comboTransitions == null || comboTransitions.Length == 0) return default;
        edgeIndex = Mathf.Clamp(edgeIndex, 0, comboTransitions.Length - 1);
        return comboTransitions[edgeIndex];
    }

    /// <summary>
    /// 检查进入节点 <paramref name="toNodeIndex"/> 时，与上一段按键提交的间隔是否可衔接。
    /// toNodeIndex=0 恒 true；toNodeIndex=1 使用 transitions[0]（A→B）。
    /// Session 进行中仅校验 min（防连点）；max 断档由 <see cref="ComboSessionResetTime"/> 在 Service 超时统一收口。
    /// </summary>
    public bool IsTransitionGapValid(int toNodeIndex, float gapSeconds, bool comboSessionActive, out string rejectReason)
    {
        rejectReason = null;
        if (toNodeIndex <= 0) return true;

        var edgeIndex = toNodeIndex - 1;
        if (edgeIndex < 0 || edgeIndex >= TransitionCount)
        {
            return true;
        }

        var edge = GetTransition(edgeIndex);
        if (edge.MinInputTime > 0.0001f && gapSeconds < edge.MinInputTime)
        {
            rejectReason = $"edge[{edgeIndex}] gap {gapSeconds:F2}s < min {edge.MinInputTime:F2}s";
            return false;
        }

        // Session 内：动画播完再按是常态，per-edge max 不能结束 Session（见 Log 0.77s > max 0.50s）。
        if (comboSessionActive)
        {
            return true;
        }

        var maxEffective = edge.MaxInputTime > 0.0001f ? edge.MaxInputTime : comboSessionResetTime;
        if (maxEffective > 0.0001f && gapSeconds > maxEffective)
        {
            rejectReason = $"edge[{edgeIndex}] gap {gapSeconds:F2}s > max {maxEffective:F2}s";
            return false;
        }

        return true;
    }

    /// <summary>构建 Resolver 注入用的边时间快照（长度 = TransitionCount）。</summary>
    public TransitionTimingSnapshot[] BuildTransitionTimingsForResolver()
    {
        var n = TransitionCount;
        if (n <= 0) return Array.Empty<TransitionTimingSnapshot>();

        var arr = new TransitionTimingSnapshot[n];
        for (var i = 0; i < n; i++)
        {
            var e = GetTransition(i);
            arr[i] = new TransitionTimingSnapshot
            {
                MinGap = e.MinInputTime,
                MaxGap = e.MaxInputTime,
            };
        }
        return arr;
    }

    /// <summary>兼容旧 API。</summary>
    public bool IsLinkInputGapValid(int comboIdx, float gapSeconds, out string rejectReason)
        => IsTransitionGapValid(comboIdx, gapSeconds, comboSessionActive: false, out rejectReason);

    public override RouteKind Kind => RouteKind.Combo;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (comboSessionResetTime <= 0.0001f)
        {
            comboSessionResetTime = 1.2f;
        }

        MigrateLegacyLinkWindows();
        EnsureTransitionCount();

        if (comboChain == null) return;

        ValidateChainAndTransitions();
    }

    void MigrateLegacyLinkWindows()
    {
        var edgeCount = TransitionCount;
        if (edgeCount <= 0)
        {
            comboTransitions = Array.Empty<ComboTransition>();
            return;
        }

        if (comboTransitions != null && comboTransitions.Length == edgeCount)
        {
            return;
        }

        var next = new ComboTransition[edgeCount];
        var oldLen = comboTransitions?.Length ?? 0;
        for (var edge = 0; edge < edgeCount; edge++)
        {
            if (edge < oldLen)
            {
                next[edge] = comboTransitions[edge];
                continue;
            }

            // 旧 linkInputWindows[i] 对齐 chain[i]；边 edge 对应 windows[edge+1]。
            if (linkInputWindows != null && edge + 1 < linkInputWindows.Length)
            {
                var leg = linkInputWindows[edge + 1];
                next[edge] = ComboTransition.CreateDefault(leg.minIntervalAfterPrevious, leg.maxIntervalAfterPrevious);
            }
            else
            {
                next[edge] = ComboTransition.CreateDefault(0.2f, 0f);
            }
        }

        if (linkInputWindows != null && linkInputWindows.Length > 0)
        {
            linkInputWindows = null;
        }

        comboTransitions = next;
    }

    void EnsureTransitionCount()
    {
        var edgeCount = TransitionCount;
        if (edgeCount == 0)
        {
            comboTransitions = Array.Empty<ComboTransition>();
            return;
        }

        if (comboTransitions == null || comboTransitions.Length != edgeCount)
        {
            MigrateLegacyLinkWindows();
        }
    }

    void ValidateChainAndTransitions()
    {
        for (var i = 0; i < comboChain.Length; i++)
        {
            var sub = comboChain[i];
            if (sub == null)
            {
                Debug.LogWarning($"[ComboRoute] '{name}' node[{i}] 为空。", this);
                continue;
            }

            if (sub is ComboRouteDefinition)
            {
                Debug.LogError($"[ComboRoute] '{name}' node[{i}] 不能嵌套 ComboRoute。", this);
            }

            if (sub.BaseCooldownSeconds > 0.0001f)
            {
                Debug.LogWarning(
                    $"[ComboRoute] '{name}' node[{i}]='{sub.name}' BaseCooldown={sub.BaseCooldownSeconds:F2}s — SubRoute 应为 0。",
                    this);
            }
        }

        if (Stages != null && Stages.Length > 0)
        {
            Debug.LogWarning($"[ComboRoute] '{name}' 容器 Stages[] 应为空。", this);
        }

        if (CooldownPolicy == RouteCooldownPolicy.OnRouteStart)
        {
            Debug.LogWarning($"[ComboRoute] '{name}' CooldownPolicy=OnRouteStart 不符合连招语义，请用 On Last SubRoute End。", this);
        }

        if (CooldownPolicy == RouteCooldownPolicy.OnRouteEnd)
        {
            cooldownPolicy = RouteCooldownPolicy.OnLastSubRouteEnd;
        }

        if (ResourceConsumePolicy == RouteResourceConsumePolicy.OnRouteStart)
        {
            resourceConsumePolicy = RouteResourceConsumePolicy.OnFirstSubRouteStart;
        }

        if (ChainLength > 0 && (comboTransitions == null || comboTransitions.Length != TransitionCount))
        {
            Debug.LogError(
                $"[ComboRoute] '{name}' 需要 {TransitionCount} 条 Transition（=节点数-1），当前={comboTransitions?.Length ?? 0}。",
                this);
        }

        for (var e = 0; e < TransitionCount; e++)
        {
            var t = comboTransitions[e];
            var from = comboChain[e]?.name ?? "?";
            var to = comboChain[e + 1]?.name ?? "?";
            if (t.MaxInputTime > 0.0001f && t.MinInputTime > t.MaxInputTime)
            {
                Debug.LogWarning($"[ComboRoute] '{name}' {from}→{to} min > max。", this);
            }

            if (t.MaxInputTime > comboSessionResetTime + 0.0001f)
            {
                Debug.LogWarning(
                    $"[ComboRoute] '{name}' {from}→{to} max={t.MaxInputTime:F2}s > SessionReset={comboSessionResetTime:F2}s。",
                    this);
            }
        }
    }
#endif
}
