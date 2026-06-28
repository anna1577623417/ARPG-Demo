#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>152.1 — Combat Flow 右侧 Inspector 共享布局常量与 ActionCategory 展示。</summary>
public static class CombatFlowGraphInspectorLayout
{
    public const float LabelWidth = 90f;
    public const float SectionSpacing = 8f;

    public const int PaddingLeft = 8;
    public const int PaddingRight = 8;
    public const int PaddingTop = 6;
    public const int PaddingBottom = 6;

    public static GUIStyle ContentPaddingStyle { get; private set; }

    public static void EnsureStyles()
    {
        if (ContentPaddingStyle != null)
        {
            return;
        }

        ContentPaddingStyle = new GUIStyle
        {
            padding = new RectOffset(PaddingLeft, PaddingRight, PaddingTop, PaddingBottom),
        };
    }

    /// <summary>将 [Flags] ActionCategory 展开为「Offense | Movement」；无则 None。</summary>
    public static string FormatActionCategories(ActionCategory categories)
    {
        if (categories == ActionCategory.None)
        {
            return "None";
        }

        var sb = new StringBuilder(48);
        AppendCategory(sb, categories, ActionCategory.Movement, "Movement");
        AppendCategory(sb, categories, ActionCategory.Offense, "Offense");
        AppendCategory(sb, categories, ActionCategory.Defensive, "Defensive");
        AppendCategory(sb, categories, ActionCategory.Utility, "Utility");
        AppendCategory(sb, categories, ActionCategory.Locomotion, "Locomotion");
        AppendCategory(sb, categories, ActionCategory.IdleFallback, "IdleFallback");
        return sb.Length > 0 ? sb.ToString() : categories.ToString();
    }

    public static int CountActionCategories(ActionCategory categories)
    {
        if (categories == ActionCategory.None)
        {
            return 0;
        }

        var count = 0;
        if ((categories & ActionCategory.Movement) != 0)
        {
            count++;
        }

        if ((categories & ActionCategory.Offense) != 0)
        {
            count++;
        }

        if ((categories & ActionCategory.Defensive) != 0)
        {
            count++;
        }

        if ((categories & ActionCategory.Utility) != 0)
        {
            count++;
        }

        return count;
    }

    static void AppendCategory(StringBuilder sb, ActionCategory mask, ActionCategory bit, string label)
    {
        if ((mask & bit) == 0)
        {
            return;
        }

        if (sb.Length > 0)
        {
            sb.Append(" | ");
        }

        sb.Append(label);
    }

    public static void SectionGap()
    {
        GUILayout.Space(SectionSpacing);
    }
}
#endif
