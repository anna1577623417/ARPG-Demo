#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum CombatAuthoringChangeKind : byte
{
    ContactConfig = 0,
    GeometryReference = 1,
    GeometryDimensions = 2,
    WindowTiming = 3,
    ContactBinding = 4,
    Migration = 5,
}

public readonly struct CombatAuthoringChange
{
    public readonly UnityEngine.Object Asset;
    public readonly CombatAuthoringChangeKind Kind;
    public readonly string StableId;
    public readonly int Revision;

    public CombatAuthoringChange(
        UnityEngine.Object asset,
        CombatAuthoringChangeKind kind,
        string stableId,
        int revision)
    {
        Asset = asset;
        Kind = kind;
        StableId = stableId ?? string.Empty;
        Revision = revision;
    }
}

/// <summary>
/// 224.1 L2 — 作者变更总线。Undo/Redo、Dirty、Revision、Repaint 的通知点；
/// handler 内禁止再次写入同一资产。
/// </summary>
[InitializeOnLoad]
public static class CombatAuthoringChangeBus
{
    static readonly HashSet<int> s_publishing = new HashSet<int>();

    public static event Action<CombatAuthoringChange> Changed;

    static CombatAuthoringChangeBus()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    public static void Publish(in CombatAuthoringChange change)
    {
        if (change.Asset == null) return;
        var id = change.Asset.GetInstanceID();
        if (!s_publishing.Add(id))
        {
            Debug.LogWarning(
                $"[CombatAuthoring] ChangeBus re-entrancy blocked asset={change.Asset.name} kind={change.Kind}");
            return;
        }

        try
        {
            Changed?.Invoke(change);
        }
        finally
        {
            s_publishing.Remove(id);
        }

        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }

    public static void PublishContactConfig(CombatObjectDefinitionSO definition, CombatAuthoringChangeKind kind)
    {
        if (definition == null) return;
        Publish(new CombatAuthoringChange(
            definition,
            kind,
            string.IsNullOrEmpty(definition.Id) ? definition.name : definition.Id,
            definition.DefinitionRevision));
    }

    static void OnUndoRedo()
    {
        CombatObjectReferenceIndex.Invalidate();
        var selected = Selection.activeObject as CombatObjectDefinitionSO;
        if (selected != null)
        {
            PublishContactConfig(selected, CombatAuthoringChangeKind.ContactConfig);
        }
        else
        {
            SceneView.RepaintAll();
        }
    }
}
#endif
