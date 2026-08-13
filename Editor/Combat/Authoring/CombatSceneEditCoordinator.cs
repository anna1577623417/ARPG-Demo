#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public enum CombatSceneEditMode : byte
{
    Inactive = 0,
    TimelineReadOnly = 1,
    TimelineContactEdit = 2,
    CombatObjectAssetEdit = 3,
    GeometryAssetEdit = 4,
    RuntimeDiagnosticOverlay = 5,
}

public readonly struct CombatSceneEditSession
{
    public readonly CombatSceneEditMode Mode;
    public readonly string ActionName;
    public readonly string WindowId;
    public readonly string EventId;
    public readonly string DefinitionName;
    public readonly int Revision;
    public readonly bool Writable;

    public CombatSceneEditSession(
        CombatSceneEditMode mode,
        string actionName,
        string windowId,
        string eventId,
        string definitionName,
        int revision,
        bool writable)
    {
        Mode = mode;
        ActionName = actionName ?? string.Empty;
        WindowId = windowId ?? string.Empty;
        EventId = eventId ?? string.Empty;
        DefinitionName = definitionName ?? string.Empty;
        Revision = revision;
        Writable = writable;
    }

    public static CombatSceneEditSession Inactive =>
        new CombatSceneEditSession(CombatSceneEditMode.Inactive, null, null, null, null, 0, false);
}

/// <summary>
/// 224.1 L5 — Scene 主 Shape 绘制/编辑唯一协调者（Lease）。
/// Selection 变更不是状态转换；显式 Edit 请求才授予可写会话。
/// </summary>
[InitializeOnLoad]
public static class CombatSceneEditCoordinator
{
    static CombatSceneEditSession s_session = CombatSceneEditSession.Inactive;
    static bool s_registered;

    public static CombatSceneEditSession Session => s_session;

    public static bool HasWritablePrimaryOwner =>
        s_session.Writable
        && s_session.Mode is CombatSceneEditMode.TimelineContactEdit
            or CombatSceneEditMode.CombatObjectAssetEdit
            or CombatSceneEditMode.GeometryAssetEdit;

    static CombatSceneEditCoordinator()
    {
        EnsureRegistered();
        EditorApplication.playModeStateChanged += _ => Release("PlayMode");
        AssemblyReloadEvents.beforeAssemblyReload += () => Release("DomainReload");
    }

    public static void EnsureRegistered()
    {
        if (s_registered) return;
        SceneView.duringSceneGui += OnSceneGui;
        s_registered = true;
    }

    public static void RequestTimelineReadOnly(string actionName)
    {
        Grant(new CombatSceneEditSession(
            CombatSceneEditMode.TimelineReadOnly,
            actionName,
            null,
            null,
            null,
            0,
            writable: false));
    }

    public static void RequestTimelineContactEdit(
        string actionName,
        string windowId,
        string eventId,
        string definitionName,
        int revision)
    {
        Grant(new CombatSceneEditSession(
            CombatSceneEditMode.TimelineContactEdit,
            actionName,
            windowId,
            eventId,
            definitionName,
            revision,
            writable: true));
    }

    public static void RequestCombatObjectAssetEdit(CombatObjectDefinitionSO definition)
    {
        if (definition == null) return;
        Grant(new CombatSceneEditSession(
            CombatSceneEditMode.CombatObjectAssetEdit,
            null,
            null,
            null,
            definition.name,
            definition.DefinitionRevision,
            writable: true));
    }

    public static void Release(string reason)
    {
        if (s_session.Mode == CombatSceneEditMode.Inactive) return;
        s_session = CombatSceneEditSession.Inactive;
        SceneView.RepaintAll();
        if (GameMainDebugSettings.CombatSceneDrawSource)
        {
            Debug.Log($"[224.0][Coordinator] RELEASE reason={reason}");
        }
    }

    /// <summary>Legacy HitVolume 是否允许成为主绘制 owner。</summary>
    public static bool AllowsLegacyHitVolumeOwner()
    {
        if (ActionDataTimelineEditor.ActiveInstance != null) return false;
        if (ContactAuthoringSelectionContext.TryGet(out _)) return false;
        if (HasWritablePrimaryOwner) return false;
        return s_session.Mode == CombatSceneEditMode.Inactive
               || s_session.Mode == CombatSceneEditMode.CombatObjectAssetEdit;
    }

    /// <summary>HitShape 独立 Preview 是否允许主绘制。</summary>
    public static bool AllowsHitShapeGizmoOwner()
    {
        if (ContactAuthoringSelectionContext.TryGet(out _)) return false;
        if (ActionDataTimelineEditor.ActiveInstance != null
            && s_session.Mode == CombatSceneEditMode.TimelineContactEdit)
        {
            return false;
        }

        return !HasWritablePrimaryOwner
               || s_session.Mode == CombatSceneEditMode.GeometryAssetEdit;
    }

    static void Grant(in CombatSceneEditSession next)
    {
        s_session = next;
        EnsureRegistered();
        SceneView.RepaintAll();
        if (GameMainDebugSettings.CombatSceneDrawSource)
        {
            Debug.Log(
                $"[224.0][Coordinator] GRANT mode={next.Mode} writable={next.Writable} " +
                $"action={next.ActionName} event={next.EventId} def={next.DefinitionName} rev={next.Revision}");
        }
    }

    static void OnSceneGui(SceneView view)
    {
        if (s_session.Mode == CombatSceneEditMode.Inactive) return;
        Handles.BeginGUI();
        GUI.Box(
            new Rect(12f, 64f, 720f, 22f),
            $"[Coordinator] {s_session.Mode} writable={s_session.Writable} " +
            $"event={s_session.EventId} def={s_session.DefinitionName}");
        Handles.EndGUI();
    }
}
#endif
