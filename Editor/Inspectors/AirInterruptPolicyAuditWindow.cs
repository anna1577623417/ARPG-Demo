#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 168.3 W6 —— 批量扫描全工程 SkillEntryLoadoutSO，审计 AirInterruptPolicy 三段 mask。
///
/// ═══ 颜色规则 ═══
///   · 🟢 Universal / 三段全开 / 设计为 ACT 风格
///   · 🟡 部分开放（DescendingOnly / 黑魂风格）
///   · 🔴 全 None — 必触发 168.1 BUG（升龙 → Air → LM 必拒）
///   · ⚠ Ascending == 0 但 Descending != 0 — 升龙连段会 Block，可能是 BUG
/// </summary>
public sealed class AirInterruptPolicyAuditWindow : EditorWindow
{
    [MenuItem("GameMain/Audit/Air Interrupt Policy")]
    static void Open() => GetWindow<AirInterruptPolicyAuditWindow>("AirInterrupt Audit");

    Vector2 _scroll;
    List<Row> _rows = new();
    bool _includeRedOnly;

    struct Row
    {
        public SkillEntryLoadoutSO Loadout;
        public string AssetPath;
        public AirInterruptPolicy Policy;
        public Severity Sev;
        public string Note;
    }

    enum Severity { Green, Yellow, Red, Warn }

    void OnEnable() => Rescan();

    void Rescan()
    {
        _rows.Clear();
        var guids = AssetDatabase.FindAssets($"t:{nameof(SkillEntryLoadoutSO)}");
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var asset = AssetDatabase.LoadAssetAtPath<SkillEntryLoadoutSO>(path);
            if (asset == null) continue;
            _rows.Add(BuildRow(asset, path));
        }
        _rows.Sort((a, b) => ((int)b.Sev).CompareTo((int)a.Sev));
    }

    static Row BuildRow(SkillEntryLoadoutSO loadout, string path)
    {
        var p = loadout.AirInterruptPolicy;
        var allNone = p.AscendingAllowed == 0 && p.ApexAllowed == 0 && p.DescendingAllowed == 0;
        var allOpen = p.AscendingAllowed == ActionCategoryPresets.AllPillar
                      && p.ApexAllowed == ActionCategoryPresets.AllPillar
                      && p.DescendingAllowed == ActionCategoryPresets.AllPillar;

        Severity sev; string note;
        if (allNone)
        {
            sev = Severity.Red;
            note = "🔴 三段全 None — 168.1 BUG 必复现：升龙后空中 LM 全部 Block。请填写三段 mask 或选 Universal 预设。";
        }
        else if (allOpen)
        {
            sev = Severity.Green;
            note = "🟢 Universal（三段全开）—— 适合 ACT 灵活角色（鬼泣 / 战神）。";
        }
        else if (p.AscendingAllowed == 0 && p.DescendingAllowed != 0)
        {
            sev = Severity.Warn;
            note = "⚠ Ascending=None / Descending≠None —— 黑魂式起跳冲量神圣，升龙连段会被 Block。若非预期请补 Ascending mask。";
        }
        else
        {
            sev = Severity.Yellow;
            note = "🟡 自定义画像 —— 部分阶段开放。";
        }

        return new Row { Loadout = loadout, AssetPath = path, Policy = p, Sev = sev, Note = note };
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("AirInterruptPolicy Audit — 168.3 W6", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rescan", GUILayout.Width(80))) Rescan();
            _includeRedOnly = EditorGUILayout.ToggleLeft("仅显示 🔴 / ⚠", _includeRedOnly, GUILayout.Width(120));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"扫描 {_rows.Count} 份 Loadout", GUILayout.Width(140));
        }

        EditorGUILayout.Space(2);

        // Header
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Loadout", EditorStyles.toolbarButton, GUILayout.Width(180));
            GUILayout.Label("Asc", EditorStyles.toolbarButton, GUILayout.Width(120));
            GUILayout.Label("Apex", EditorStyles.toolbarButton, GUILayout.Width(120));
            GUILayout.Label("Desc", EditorStyles.toolbarButton, GUILayout.Width(120));
            GUILayout.Label("ApexVy", EditorStyles.toolbarButton, GUILayout.Width(60));
            GUILayout.Label("Note", EditorStyles.toolbarButton);
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            if (_includeRedOnly && row.Sev != Severity.Red && row.Sev != Severity.Warn) continue;

            var bg = GUI.backgroundColor;
            GUI.backgroundColor = SevColor(row.Sev);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button(row.Loadout.name, EditorStyles.linkLabel, GUILayout.Width(180)))
                {
                    Selection.activeObject = row.Loadout;
                    EditorGUIUtility.PingObject(row.Loadout);
                }
                GUILayout.Label(((int)row.Policy.AscendingAllowed == 0 ? "None" : row.Policy.AscendingAllowed.ToString()),
                    GUILayout.Width(120));
                GUILayout.Label(((int)row.Policy.ApexAllowed == 0 ? "None" : row.Policy.ApexAllowed.ToString()),
                    GUILayout.Width(120));
                GUILayout.Label(((int)row.Policy.DescendingAllowed == 0 ? "None" : row.Policy.DescendingAllowed.ToString()),
                    GUILayout.Width(120));
                GUILayout.Label(row.Policy.ApexVyThreshold.ToString("F2"), GUILayout.Width(60));
                EditorGUILayout.LabelField(row.Note, EditorStyles.wordWrappedLabel);
            }
            GUI.backgroundColor = bg;
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "判定规则：\n" +
            "🟢 Universal 三段全开 — ACT 风格\n" +
            "🟡 自定义画像（部分开放）\n" +
            "🔴 三段全 None — 触发 168.1 BUG（升龙→Air→LM 全 Block）\n" +
            "⚠ Ascending=0 / Descending≠0 — 黑魂式（起跳神圣），升龙连段会拒；非预期请补 Ascending",
            MessageType.Info);
    }

    static Color SevColor(Severity s) => s switch
    {
        Severity.Green => new Color(0.7f, 1f, 0.7f, 0.6f),
        Severity.Yellow => new Color(1f, 0.95f, 0.6f, 0.6f),
        Severity.Red => new Color(1f, 0.6f, 0.6f, 0.8f),
        Severity.Warn => new Color(1f, 0.78f, 0.5f, 0.7f),
        _ => Color.white,
    };
}
#endif
