using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 220.6.1 C1：受击方的冲量语义到 Reaction 路线匹配表。
/// <para>Profile 描述“受击方如何响应”，不在攻击方 ActionData 上硬编码目标行为。</para>
/// <para>C1 只提供可创建的数据资产；ReactionResolver 在 C2 才消费此表。</para>
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Combat/Reaction/Reaction Profile", fileName = "ReactionProfile_")]
public sealed class ReactionProfileSO : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        [Header("Match")]
        [Tooltip("要匹配的冲量语义；由 FeedbackRouter 翻译后的 ImpulseRequest 提供。")]
        public ImpulseKind ImpulseKind;

        [Tooltip("该行最低冲量力度；同一 Kind 时优先匹配更高门槛的行。")]
        [Min(0f)]
        public float MinimumForce;

        [Tooltip("ReactionSet 中的稳定路线键；C2 Resolver 负责解析。")]
        public string ReactionRouteId;

        [Tooltip("受击来源相对目标的方向；Any 用于方向未知或通用路线。")]
        public ReactionDirection HitDirection;

        [Header("Channels")]
        [Tooltip("是否把冲量交给目标 Motor；关闭时由 Reaction Action/Motion 承担位移。")]
        public bool ApplyImpulseMotor;

        [Tooltip("是否生成 HitReact 意图；C2 才接入 IntentHost。")]
        public bool EnqueueHitReact;

        [Tooltip("该路线是否允许打断目标当前 Action；具体策略由 ReactionSet 条目提供。")]
        public bool CanInterruptAction;

        [Tooltip("目标带 SuperArmor 时的降级策略；默认只保留反馈/冲量，不切换 HitReact 状态。")]
        public ReactionSuperArmorDisposition SuperArmorDisposition;

        [Tooltip("同一 Profile 内的显式优先级；C2 用于消除阈值重叠时的歧义。")]
        public int Priority;
    }

    [Header("Route Set")]
    [Tooltip("路线的 Action/Motion/Interrupt 配置集合。")]
    public ReactionSetSO ReactionSet;

    [Header("Profile Entries")]
    [Tooltip("受击方按 ImpulseKind + MinimumForce 选择路线的规则表。")]
    public Entry[] Entries = Array.Empty<Entry>();

    [Header("HitReact Intent")]
    [Tooltip("HitReact 意图的有效缓冲时间；C2 Resolver 生成 ReactionPlan 时快照。")]
    [Min(0.01f)]
    public float HitReactIntentBufferSeconds = 0.25f;

    void OnValidate()
    {
        if (Entries == null)
        {
            return;
        }

        var keys = new HashSet<string>();
        for (var i = 0; i < Entries.Length; i++)
        {
            var entry = Entries[i];
            var key = $"{entry.ImpulseKind}|{entry.HitDirection}|{entry.MinimumForce:F4}|{entry.Priority}";
            if (!keys.Add(key))
            {
                Debug.LogError(
                    $"[ReactionProfile] duplicate match rule key={key} profile={name}",
                    this);
            }

            if (string.IsNullOrWhiteSpace(entry.ReactionRouteId))
            {
                Debug.LogError(
                    $"[ReactionProfile] missing ReactionRouteId index={i} profile={name}",
                    this);
                continue;
            }

            if (ReactionSet == null)
            {
                Debug.LogError(
                    $"[ReactionProfile] missing ReactionSet route={entry.ReactionRouteId} profile={name}",
                    this);
                continue;
            }

            var routeFound = false;
            if (ReactionSet.Entries != null)
            {
                for (var routeIndex = 0; routeIndex < ReactionSet.Entries.Length; routeIndex++)
                {
                    var route = ReactionSet.Entries[routeIndex];
                    if (!string.Equals(route.RouteId, entry.ReactionRouteId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    routeFound = true;
                    if (route.Action == null)
                    {
                        Debug.LogError(
                            $"[ReactionProfile] route has no Action route={entry.ReactionRouteId} profile={name}",
                            this);
                    }

                    if (entry.ApplyImpulseMotor
                        && route.MotionAuthority == ReactionMotionAuthority.ActionMotion)
                    {
                        Debug.LogError(
                            $"[ReactionProfile] motion authority conflict route={entry.ReactionRouteId} profile={name}",
                            this);
                    }

                    break;
                }
            }

            if (!routeFound)
            {
                Debug.LogError(
                    $"[ReactionProfile] route not found route={entry.ReactionRouteId} profile={name}",
                    this);
            }
        }
    }
}

/// <summary>受击来源相对目标的方向；不是冲量方向，而是攻击者所在方向。</summary>
public enum ReactionDirection : byte
{
    Any = 0,
    Front = 1,
    Back = 2,
    Left = 3,
    Right = 4,
    Up = 5,
}

public enum ReactionSuperArmorDisposition : byte
{
    KeepImpulseOnly = 0,
    QueueAfterAction = 1,
    AllowHitReact = 2,
}
