using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 216.3 M0 — Phase【单一真相】衍生：由 <b>判定窗</b>（HitboxActive_Window）与 <b>打断窗</b>（InterruptibleByCategories）
/// 计算 前摇 / 判定 / 后摇 归一化区间。
/// <para>编辑器 Phase Ribbon（M0 L2）与运行时 <see cref="ActionDataSO.EvaluatePhaseTags"/>（M0 L3）共用此算法，
/// 杜绝手工 Phase 标签与判定/打断的双源漂移（见 216.3 §15.2 / §16）。</para>
/// <para>层级：置于 4_Data（与 <see cref="ActionDataSO"/> / <see cref="ActionWindow"/> 同层，纯数据衍生，
/// 不引入 3_Gameplay 依赖）。M1 落地 HitClip 后，判定源切到 HitClip.Active，读取源保持单一（非双轨）。</para>
/// </summary>
public static class PhaseDerivation
{
    public enum Phase : byte
    {
        Startup = 0,
        Active = 1,
        Recovery = 2,
        Neutral = 3, // 后摇结束后可取消 / 收尾（无阶段语义）
    }

    /// <summary>衍生出的阶段边界（归一化 0~1）。</summary>
    public readonly struct Spans
    {
        public readonly bool HasActive;
        public readonly float ActiveStart;
        public readonly float ActiveEnd;
        public readonly float StartupEnd;    // 前摇终点
        public readonly float RecoveryStart; // 后摇起点
        public readonly float RecoveryEnd;   // 后摇终点（= 首个打断窗起点 ?? 1）

        public Spans(
            bool hasActive, float activeStart, float activeEnd,
            float startupEnd, float recoveryStart, float recoveryEnd)
        {
            HasActive = hasActive;
            ActiveStart = activeStart;
            ActiveEnd = activeEnd;
            StartupEnd = startupEnd;
            RecoveryStart = recoveryStart;
            RecoveryEnd = recoveryEnd;
        }

        public readonly Phase PhaseAt(float t)
        {
            t = Mathf.Clamp01(t);
            if (HasActive && t >= ActiveStart && t <= ActiveEnd)
            {
                return Phase.Active;
            }

            if (t < StartupEnd)
            {
                return Phase.Startup;
            }

            if (t <= RecoveryEnd)
            {
                return Phase.Recovery;
            }

            return Phase.Neutral;
        }
    }

    /// <summary>从 ActionData 的时间窗衍生阶段区间。</summary>
    public static Spans Compute(ActionDataSO action)
    {
        return action == null ? Empty() : Compute(action.Windows);
    }

    /// <summary>从时间窗列表衍生阶段区间（编辑器/运行时共用核心）。</summary>
    public static Spans Compute(IReadOnlyList<ActionWindow> windows)
    {
        if (windows == null || windows.Count == 0)
        {
            return Empty();
        }

        var hasActive = false;
        var activeStart = 1f;
        var activeEnd = 0f;
        var firstCancel = 1f;
        var hasCancel = false;

        for (var i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            var mask = w.ToInternalTagMask();

            // 判定源（过渡：HitboxActive_Window；M1 后切 HitClip.Active）
            if ((mask & (ulong)StateTag.HitboxActive_Window) != 0UL)
            {
                hasActive = true;
                activeStart = Mathf.Min(activeStart, w.NormalizedStart);
                activeEnd = Mathf.Max(activeEnd, w.NormalizedEnd);
            }

            // 打断源（单一：InterruptibleByCategories）
            if (w.InterruptibleByCategories != ActionCategory.None)
            {
                hasCancel = true;
                firstCancel = Mathf.Min(firstCancel, w.NormalizedStart);
            }
        }

        if (!hasCancel)
        {
            firstCancel = 1f;
        }

        if (hasActive)
        {
            var startupEnd = activeStart;
            var recoveryStart = activeEnd;
            var recoveryEnd = Mathf.Clamp(firstCancel, recoveryStart, 1f);
            return new Spans(true, activeStart, activeEnd, startupEnd, recoveryStart, recoveryEnd);
        }

        // 无判定：全程 前摇→后摇，由首个打断窗切分
        return new Spans(false, 0f, 0f, firstCancel, firstCancel, 1f);
    }

    /// <summary>216.3 M0 L3 — 阶段枚举 → 对应 StateTag 位（Neutral 返回 0）。</summary>
    public static ulong ToStateBit(Phase phase) => phase switch
    {
        Phase.Startup => (ulong)StateTag.PhaseStartup,
        Phase.Active => (ulong)StateTag.PhaseActive,
        Phase.Recovery => (ulong)StateTag.PhaseRecovery,
        _ => 0UL,
    };

    static Spans Empty() => new Spans(false, 0f, 0f, 1f, 1f, 1f);
}
