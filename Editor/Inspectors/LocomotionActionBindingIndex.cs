#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

/// <summary>227.4：缓存 ActionData → LocomotionProfile/State 的编辑期反向索引。</summary>
[InitializeOnLoad]
public static class LocomotionActionBindingIndex
{
    public readonly struct Entry
    {
        public LocomotionProfile Profile { get; }
        public LocomotionStateId State { get; }
        public bool IsContinuous => State.IsContinuous();
        public bool IsDiscrete => State.IsDiscrete();

        public Entry(LocomotionProfile profile, LocomotionStateId state)
        {
            Profile = profile;
            State = state;
        }
    }

    static readonly Dictionary<int, List<Entry>> EntriesByActionId = new Dictionary<int, List<Entry>>();
    static bool s_dirty = true;

    static LocomotionActionBindingIndex()
    {
        EditorApplication.projectChanged += Invalidate;
    }

    public static IReadOnlyList<Entry> GetBindings(ActionDataSO action)
    {
        if (s_dirty)
        {
            Rebuild();
        }

        if (action != null && EntriesByActionId.TryGetValue(action.GetInstanceID(), out var entries))
        {
            return entries;
        }

        return System.Array.Empty<Entry>();
    }

    public static void Invalidate() => s_dirty = true;

    static void Rebuild()
    {
        EntriesByActionId.Clear();
        var guids = AssetDatabase.FindAssets("t:LocomotionProfile");
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var profile = AssetDatabase.LoadAssetAtPath<LocomotionProfile>(path);
            if (profile == null) continue;

            var bindings = profile.EditorGetBindingsCopy();
            for (var j = 0; j < bindings.Length; j++)
            {
                var action = bindings[j].ResolveLocomotionAction();
                if (action == null) continue;

                var id = action.GetInstanceID();
                if (!EntriesByActionId.TryGetValue(id, out var entries))
                {
                    entries = new List<Entry>();
                    EntriesByActionId.Add(id, entries);
                }

                entries.Add(new Entry(profile, bindings[j].State));
            }
        }

        s_dirty = false;
    }
}
#endif
