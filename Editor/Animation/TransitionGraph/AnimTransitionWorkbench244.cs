#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>策划工作台只读入口：Preview、Impact、Legacy/Migration。Apply/Import deliberately absent.</summary>
public sealed class AnimTransitionWorkbench244
{
    readonly AnimTransitionGraphWindow window;
    readonly VisualElement root;
    readonly Label status;
    readonly AnimTransitionMigrationPanel244 migration;

    public VisualElement Root => root;

    public AnimTransitionWorkbench244(AnimTransitionGraphWindow owner)
    {
        window = owner;
        root = new VisualElement { name = "AnimTransitionWorkbench" };
        root.style.position = Position.Absolute;
        root.style.left = 270f;
        root.style.right = 390f;
        root.style.top = 42f;
        root.style.bottom = 30f;
        root.style.paddingLeft = 12f;
        root.style.paddingRight = 12f;
        root.style.paddingTop = 8f;
        root.style.backgroundColor = new Color(0.075f, 0.09f, 0.11f);
        root.style.display = DisplayStyle.None;
        root.Add(new Label("Transition Workbench · Read Only"));
        status = new Label { name = "AnimTransitionWorkbenchStatus" };
        status.style.whiteSpace = WhiteSpace.Normal;
        root.Add(status);
        migration = new AnimTransitionMigrationPanel244(root);
        root.Add(new Label("Preview uses compiled typed evaluator; Impact is invalidated by graph hash. Asset Apply/Import is reserved for 244.9."));
    }

    public void Toggle()
    {
        root.style.display = root.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
        Refresh();
    }

    public void Refresh()
    {
        var graph = window != null ? window.AuthoringGraph : null;
        if (status == null) return;
        if (graph == null)
        {
            status.text = "Select an AnimTransitionAuthoringGraph asset from the Project window.";
            migration.Refresh(null);
            return;
        }

        var index = AnimTransitionImpactIndex244.Build(graph);
        status.text = index.Describe() + "\nPreview/Impact are read-only; Runtime Live = 244.9 OPEN.";
        migration.Refresh(graph);
    }
}
#endif
