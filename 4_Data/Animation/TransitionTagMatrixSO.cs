using System;
using UnityEngine;

/// <summary>
/// 178.1 W1 — Tag×Tag 12×12 主矩阵（资料 §190-227）。
/// <para>展开为一维数组存 12² = 144 项；通过 (from, to) 索引器访问。</para>
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Animation/Transition Tag Matrix", fileName = "TransitionTagMatrix_")]
public sealed class TransitionTagMatrixSO : ScriptableObject
{
    public const int TagCount = 12;

    [Serializable]
    public struct Entry
    {
        [Range(0f, 0.5f)] public float BlendDuration;
        public TransitionMode Mode;
        public AnimationCurve EaseCurve;

        [Tooltip("策划备注：为什么这个值？")]
        public string DesignerNote;
    }

    [SerializeField, Tooltip("行优先存储：index = from * TagCount + to。")]
    Entry[] m_entries = new Entry[TagCount * TagCount];

    public Entry GetEntry(TransitionTag from, TransitionTag to)
    {
        var idx = (int)from * TagCount + (int)to;
        if (m_entries == null || idx < 0 || idx >= m_entries.Length)
        {
            return default;
        }
        return m_entries[idx];
    }

    public void SetEntry(TransitionTag from, TransitionTag to, in Entry e)
    {
        EnsureCapacity();
        m_entries[(int)from * TagCount + (int)to] = e;
    }

    public int Length => m_entries != null ? m_entries.Length : 0;

    void EnsureCapacity()
    {
        if (m_entries == null || m_entries.Length != TagCount * TagCount)
        {
            m_entries = new Entry[TagCount * TagCount];
        }
    }

    void OnValidate() => EnsureCapacity();

    /// <summary>
    /// 178.1 §2.2 推荐默认矩阵 —— 3A 经验基线，策划可微调。
    /// 单位：秒；0 = Snap；&lt;0 = 占位"不支持"用 0.10 兜底。
    /// </summary>
    public static readonly float[,] DefaultBlendTable = new float[TagCount, TagCount]
    {
        // To:  Idle Loco Piv  Stop Strt LiAt HvAt Skl  Hit  Rxn  Air  Crch
        /*Idle*/ { 0.00f,0.15f,0.10f,0.10f,0.08f,0.05f,0.08f,0.10f,0.00f,0.00f,0.06f,0.20f},
        /*Loco*/ { 0.12f,0.15f,0.08f,0.06f,0.10f,0.04f,0.06f,0.10f,0.00f,0.00f,0.05f,0.18f},
        /*Piv */ { 0.08f,0.10f,0.05f,0.06f,0.08f,0.06f,0.08f,0.10f,0.00f,0.00f,0.08f,0.15f},
        /*Stop*/ { 0.10f,0.08f,0.06f,0.10f,0.05f,0.05f,0.08f,0.10f,0.00f,0.00f,0.06f,0.15f},
        /*Strt*/ { 0.08f,0.10f,0.08f,0.06f,0.10f,0.05f,0.06f,0.10f,0.00f,0.00f,0.06f,0.15f},
        /*LiAt*/ { 0.06f,0.08f,0.08f,0.06f,0.06f,0.04f,0.05f,0.08f,0.00f,0.00f,0.08f,0.20f},
        /*HvAt*/ { 0.08f,0.10f,0.10f,0.08f,0.08f,0.05f,0.05f,0.08f,0.00f,0.00f,0.10f,0.22f},
        /*Skl */ { 0.10f,0.12f,0.10f,0.10f,0.10f,0.08f,0.10f,0.08f,0.00f,0.00f,0.10f,0.25f},
        /*Hit */ { 0.00f,0.00f,0.00f,0.00f,0.00f,0.00f,0.00f,0.00f,0.00f,0.00f,0.00f,0.00f},
        /*Rxn */ { 0.10f,0.10f,0.10f,0.10f,0.10f,0.10f,0.10f,0.10f,0.10f,0.10f,0.10f,0.10f},
        /*Air */ { 0.10f,0.08f,0.08f,0.06f,0.06f,0.10f,0.12f,0.10f,0.05f,0.00f,0.05f,0.10f},
        /*Crch*/ { 0.20f,0.15f,0.12f,0.10f,0.10f,0.08f,0.10f,0.12f,0.00f,0.00f,0.15f,0.15f},
    };

    /// <summary>哪些 Tag→Tag 对默认为 PhaseMatch。</summary>
    public static bool IsPhaseMatchPair(TransitionTag from, TransitionTag to)
    {
        // Loco↔Loco / Crouch↔Crouch
        if (from == TransitionTag.Locomotion && to == TransitionTag.Locomotion) return true;
        if (from == TransitionTag.Crouch && to == TransitionTag.Crouch) return true;
        return false;
    }
}
