#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 243.10 Editor UX iteration — exposes the graph editor directly from the asset Inspector.
/// The Inspector is built through one UIElements tree so the entry panel and default fields
/// participate in the same layout pass and cannot overlap.
/// </summary>
[CustomEditor(typeof(AnimTransitionAuthoringGraph))]
public sealed class AnimTransitionAuthoringGraphInspector : Editor
{
    const float OpenButtonHeight = 30f;

    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement
        {
            name = "AnimTransitionAuthoringGraphInspector"
        };

        root.Add(BuildEditorEntry());
        // Keep the custom entry and Unity's serialized fields in one visual tree. Mixing this
        // with an IMGUI OnInspectorGUI implementation causes duplicate/overlapping Inspector rows.
        InspectorElement.FillDefaultInspector(root, serializedObject, this);
        return root;
    }

    VisualElement BuildEditorEntry()
    {
        var graph = target as AnimTransitionAuthoringGraph;
        var container = new VisualElement
        {
            name = "AnimTransitionGraphEditorEntry"
        };
        container.style.marginBottom = 6f;

        var title = new Label("Animation Transition Graph")
        {
            name = "AnimTransitionGraphEditorEntryTitle"
        };
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginBottom = 3f;
        container.Add(title);

        var button = new Button(() => AnimTransitionGraphWindow.Open(graph))
        {
            name = "OpenTransitionGraphEditorButton",
            text = "Open Transition Graph Editor"
        };
        button.style.height = OpenButtonHeight;
        button.tooltip = "Open this asset in the visual transition graph editor.";
        button.SetEnabled(graph != null);
        container.Add(button);
        return container;
    }
}
#endif
