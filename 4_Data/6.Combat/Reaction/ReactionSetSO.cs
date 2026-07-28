using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 220.6.1 C1：受击反应路线集合。
/// <para>只保存路线到 Action/Motion/打断策略的配置，不参与命中解析。</para>
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Combat/Reaction/Reaction Set", fileName = "ReactionSet_")]
public sealed class ReactionSetSO : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        [Tooltip("稳定路线标识；ReactionProfile 通过此键引用，不直接复制动作字段。")]
        public string RouteId;

        [Header("Action / Motion")]
        [Tooltip("该受击路线消费的 ActionData；C3 HitReact 柱将使用它。")]
        public ActionDataSO Action;

        [Tooltip("可选的受击位移 MotionProfile；为空表示不使用 Action Motion。")]
        public MotionProfileSO MotionProfile;

        [Tooltip("本路线的唯一位移权威，避免 Action Motion 与 Motor Impulse 双重位移。")]
        public ReactionMotionAuthority MotionAuthority;

        [Header("Interrupt")]
        [Tooltip("当前动作存在时，本受击路线如何处理。")]
        public ReactionInterruptDisposition InterruptDisposition;

        [Tooltip("路线完成后是否回到 Locomotion；C3 默认开启。")]
        public bool ReturnToLocomotion;
    }

    [Header("Reaction Routes")]
    [Tooltip("受击路线表；RouteId 必须稳定且唯一。")]
    public Entry[] Entries = Array.Empty<Entry>();

    void OnValidate()
    {
        if (Entries == null)
        {
            return;
        }

        var ids = new HashSet<string>();
        for (var i = 0; i < Entries.Length; i++)
        {
            var entry = Entries[i];
            if (string.IsNullOrWhiteSpace(entry.RouteId))
            {
                Debug.LogError($"[ReactionSet] missing RouteId index={i} set={name}", this);
            }
            else if (!ids.Add(entry.RouteId))
            {
                Debug.LogError($"[ReactionSet] duplicate RouteId={entry.RouteId} set={name}", this);
            }

            if (entry.Action == null)
            {
                Debug.LogError($"[ReactionSet] missing Action route={entry.RouteId} set={name}", this);
            }

            if (entry.MotionAuthority == ReactionMotionAuthority.ActionMotion
                && entry.MotionProfile == null
                && entry.Action?.MotionProfile == null)
            {
                Debug.LogError(
                    $"[ReactionSet] ActionMotion requires MotionProfile route={entry.RouteId} set={name}",
                    this);
            }
        }
    }
}

/// <summary>220.6.1 C1：受击位移的唯一权威。</summary>
public enum ReactionMotionAuthority : byte
{
    None = 0,
    MotorImpulse = 1,
    ActionMotion = 2,
}

/// <summary>220.6.1 C1：受击进入时对当前动作的处理策略。</summary>
public enum ReactionInterruptDisposition : byte
{
    Ignore = 0,
    QueueAfterAction = 1,
    CancelAction = 2,
}
