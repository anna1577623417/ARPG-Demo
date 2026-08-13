#if UNITY_EDITOR
using System;

internal enum ContactAuthoringEditLayer : byte
{
    EventOverride = 0,
    SharedDefinition = 1,
    SharedPreset = 2,
}

internal readonly struct ContactAuthoringSelection
{
    public readonly ActionDataSO Action;
    public readonly string EventId;
    public readonly CombatObjectDefinitionSO Definition;
    public readonly ContactAuthoringEditLayer EditLayer;
    public readonly float PreviewTime;
    public readonly uint Revision;

    public ContactAuthoringSelection(
        ActionDataSO action,
        string eventId,
        CombatObjectDefinitionSO definition,
        ContactAuthoringEditLayer editLayer,
        float previewTime,
        uint revision)
    {
        Action = action;
        EventId = eventId;
        Definition = definition;
        EditLayer = editLayer;
        PreviewTime = previewTime;
        Revision = revision;
    }

    public bool IsValid => Action != null && ContactEventId.IsValid(EventId);
}

/// <summary>Timeline、Inspector 与 Scene View 的稳定选择总线；不保存数组索引或 SerializedProperty。</summary>
internal static class ContactAuthoringSelectionContext
{
    static ContactAuthoringSelection s_current;
    static uint s_revision;

    public static event Action<ContactAuthoringSelection> Changed;

    public static bool TryGet(out ContactAuthoringSelection selection)
    {
        selection = s_current;
        return selection.IsValid;
    }

    public static void Publish(
        ActionDataSO action,
        string eventId,
        CombatObjectDefinitionSO definition,
        ContactAuthoringEditLayer editLayer,
        float previewTime)
    {
        if (action == null || !ContactEventId.IsValid(eventId))
        {
            Clear();
            return;
        }

        if (s_current.Action == action
            && s_current.EventId == eventId
            && s_current.Definition == definition
            && s_current.EditLayer == editLayer
            && Math.Abs(s_current.PreviewTime - previewTime) < 0.0001f)
        {
            return;
        }

        s_current = new ContactAuthoringSelection(
            action,
            eventId,
            definition,
            editLayer,
            previewTime,
            ++s_revision);
        Changed?.Invoke(s_current);
    }

    public static void Clear()
    {
        if (!s_current.IsValid)
        {
            return;
        }

        s_current = default;
        s_revision++;
        Changed?.Invoke(default);
    }
}
#endif
