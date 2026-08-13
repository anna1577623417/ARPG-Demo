#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 216.3 M0 L1 — Action Timeline 战斗轨道分层【单一真相】注册表。
/// <para>取代散落在主文件的 ActiveTracks[] / GetTrackLabel / GetTrackColor / IsPhaseTrack，
/// 新增轨只改此处一处（见 216.3 §15.9 / §16）。</para>
/// <para>分层顺序：战斗核心 → 输入 → 运动 → 表现 → 衍生阶段(只读) → 其它。</para>
/// </summary>
internal enum CombatTrackTier : byte
{
    CombatCore = 0,   // Tier A：打断 / 判定 / 受击 / 无敌（上移）
    Input = 1,        // Tier B：连段 / 转向输入
    Motion = 2,       // Tier C：位移 / 传送
    Presentation = 3, // Tier D：FX / Audio / Camera / TimeScale
    DerivedPhase = 4, // Tier E：阶段衍生条（只读，下放最小面）
    Other = 5,        // 其它：通用运行时事件
}

public sealed partial class ActionDataTimelineEditor
{
    /// <summary>单轨描述符：分层 / 层内序 / 是否可编辑 / 是否衍生 / 标签 / 颜色。</summary>
    readonly struct TrackDescriptor
    {
        public readonly CombatTrackTier Tier;
        public readonly int OrderInTier;
        public readonly bool Editable;   // false = 只读（衍生轨，禁止双击/拖拽创建）
        public readonly bool Derived;    // true = 由其它作者来源衍生（Phase）
        public readonly string Label;
        public readonly Color Color;

        public TrackDescriptor(
            CombatTrackTier tier, int orderInTier, bool editable, bool derived, string label, Color color)
        {
            Tier = tier;
            OrderInTier = orderInTier;
            Editable = editable;
            Derived = derived;
            Label = label;
            Color = color;
        }
    }

    static readonly Color ColGray = new Color(0.5f, 0.5f, 0.5f, 1f);

    // ActionContact 是唯一的攻击盒作者来源；高饱和红色用于与防御/受击/生成轨道形成稳定区分。
    internal static readonly Color ContactHitboxColor = new Color(1f, 0.04f, 0.02f, 0.98f);

    /// <summary>轨道注册表（单点）。新增轨在此加一行即可。</summary>
    static readonly Dictionary<TrackId, TrackDescriptor> TrackRegistry = new()
    {
        // ── Tier A · 战斗核心（上移：打断 / 伤害判定 / 受击 / 无敌） ──
        [TrackId.WindowContainer] = new(CombatTrackTier.CombatCore, 0, true, false, "Window Container", new Color(0.55f, 0.75f, 0.95f, 0.75f)),
        [TrackId.Interrupt]     = new(CombatTrackTier.CombatCore, 1, true,  false, "Cancel / Interrupt", new Color(0.95f, 0.45f, 0.2f, 0.88f)),
        [TrackId.Contact]       = new(CombatTrackTier.CombatCore, 2, true,  false, "Hitbox",             ContactHitboxColor),
        // 216.3 M5 L1：DefenseClip 防御轨（Guard Volume / Parry / Invincible 窗）。
        [TrackId.Guard]         = new(CombatTrackTier.CombatCore, 4, true,  false, "Guard (Defense)",    new Color(0.35f, 0.72f, 0.98f, 0.92f)),
        [TrackId.Combat]        = new(CombatTrackTier.CombatCore, 5, true,  false, "Combat Spawn ◆",     new Color(0.95f, 0.45f, 0.15f, 0.92f)),
        [TrackId.Hurtbox]       = new(CombatTrackTier.CombatCore, 6, true,  false, "Hurtbox ★",          new Color(0.85f, 0.35f, 0.95f, 0.88f)),
        [TrackId.Invincible]    = new(CombatTrackTier.CombatCore, 7, true,  false, "Invincible ★",       new Color(0.95f, 0.88f, 0.15f, 0.88f)),

        // ── Tier B · 输入 / 连段 ──
        [TrackId.ComboInput]    = new(CombatTrackTier.Input, 0, true, false, "Combo Input",            new Color(0.3f, 0.85f, 0.9f, 0.75f)),
        [TrackId.RotationInput] = new(CombatTrackTier.Input, 1, true, false, "Rotation Input (198.3)", new Color(0.25f, 0.85f, 0.65f, 0.85f)),

        // ── Tier C · 运动 ──
        [TrackId.RootMotion]    = new(CombatTrackTier.Motion, 0, true, false, "Root Motion", new Color(0.7f, 0.7f, 0.7f, 0.72f)),
        [TrackId.Teleport]      = new(CombatTrackTier.Motion, 1, true, false, "Teleport ◆",  ColGray),

        // ── Tier D · 表现 ──
        [TrackId.Fx]            = new(CombatTrackTier.Presentation, 0, true, false, "FX ◆",      ColGray),
        [TrackId.Audio]         = new(CombatTrackTier.Presentation, 1, true, false, "Audio ◆",   ColGray),
        [TrackId.Camera]        = new(CombatTrackTier.Presentation, 2, true, false, "Camera",    ColGray),
        [TrackId.TimeScale]     = new(CombatTrackTier.Presentation, 3, true, false, "TimeScale", ColGray),

        // ── Tier E · 衍生阶段（只读单条 Ribbon，下放最小面；由 判定+打断 衍生，§15.8） ──
        [TrackId.PhaseRibbon]   = new(CombatTrackTier.DerivedPhase, 0, false, true, "Phase 衍生 (前摇/判定/后摇)", new Color(0.2f, 0.75f, 0.45f, 0.78f)),

        // ── 其它 ──
        [TrackId.RuntimeEvent]  = new(CombatTrackTier.Other, 0, true, false, "Runtime Events", new Color(0.85f, 0.65f, 0.15f, 0.68f)),
    };

    /// <summary>按 (Tier, OrderInTier) 排好序的活动轨道列表（渲染 / 命中共用）。</summary>
    static readonly TrackId[] ActiveTracks = BuildActiveTracks();

    static TrackId[] BuildActiveTracks()
    {
        var list = new List<TrackId>(TrackRegistry.Count);
        foreach (var kv in TrackRegistry)
        {
            list.Add(kv.Key);
        }

        list.Sort((a, b) =>
        {
            var da = TrackRegistry[a];
            var db = TrackRegistry[b];
            var tierCmp = ((byte)da.Tier).CompareTo((byte)db.Tier);
            return tierCmp != 0 ? tierCmp : da.OrderInTier.CompareTo(db.OrderInTier);
        });
        return list.ToArray();
    }

    /// <summary>活动轨道中出现的不同 Tier 数（布局高度计算：每个 Tier 一条分隔行）。</summary>
    static int TierCount()
    {
        var count = 0;
        var hasPrev = false;
        var prev = CombatTrackTier.CombatCore;
        for (var i = 0; i < ActiveTracks.Length; i++)
        {
            var tier = GetTrackTier(ActiveTracks[i]);
            if (!hasPrev || prev != tier)
            {
                count++;
                hasPrev = true;
                prev = tier;
            }
        }

        return count;
    }

    static TrackDescriptor Describe(TrackId track) =>
        TrackRegistry.TryGetValue(track, out var d)
            ? d
            : new TrackDescriptor(CombatTrackTier.Other, 99, true, false, track.ToString(), ColGray);

    static string GetTrackLabel(TrackId track) => Describe(track).Label;

    static Color GetTrackColor(TrackId track) => Describe(track).Color;

    static CombatTrackTier GetTrackTier(TrackId track) => Describe(track).Tier;

    /// <summary>是否衍生轨（Phase）——只读，禁止手工创建/拖拽。</summary>
    static bool IsDerivedTrack(TrackId track) => Describe(track).Derived;

    /// <summary>是否可编辑（衍生轨返回 false）。</summary>
    static bool IsTrackEditable(TrackId track) => Describe(track).Editable;

    static bool IsPhaseTrack(TrackId track) => GetTrackTier(track) == CombatTrackTier.DerivedPhase;

    static bool IsCombatCoreTrack(TrackId track) => GetTrackTier(track) == CombatTrackTier.CombatCore;

    /// <summary>Tier 中文分隔标题（分隔行绘制用）。</summary>
    static string GetTierHeader(CombatTrackTier tier) => tier switch
    {
        CombatTrackTier.CombatCore   => "战斗核心 · 打断 / 判定 / 受击",
        CombatTrackTier.Input        => "输入 / 连段",
        CombatTrackTier.Motion       => "运动",
        CombatTrackTier.Presentation => "表现",
        CombatTrackTier.DerivedPhase => "阶段（衍生 · 只读）",
        CombatTrackTier.Other        => "其它",
        _ => tier.ToString(),
    };
}
#endif
