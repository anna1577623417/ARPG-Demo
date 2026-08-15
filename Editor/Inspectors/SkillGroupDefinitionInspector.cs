#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 173.3 — SkillGroup 自定义 Inspector：分区 Foldout + 3×3 八向圆盘 + Fallback 二态 HelpBox。
/// </summary>
[CustomEditor(typeof(SkillGroupDefinition))]
public sealed class SkillGroupDefinitionInspector : Editor
{
    const string PrefIdentity   = "CDA.SkillGroupInspector.Identity";
    const string PrefDirectional = "CDA.SkillGroupInspector.Directional";
    const string PrefNeutral    = "CDA.SkillGroupInspector.Neutral";
    const string PrefSplitFrame = "CDA.SkillGroupInspector.SplitFrame";
    const string PrefMotion     = "CDA.SkillGroupInspector.Motion";
    const string PrefAdvanced   = "CDA.SkillGroupInspector.Advanced";

    SerializedProperty m_displayName;
    SerializedProperty m_icon;
    SerializedProperty m_showOnHud;
    SerializedProperty m_baseCooldownSeconds;
    SerializedProperty m_costs;
    SerializedProperty m_requiredAbilityTags;
    SerializedProperty m_routes;
    SerializedProperty m_forward;
    SerializedProperty m_forwardLeft;
    SerializedProperty m_forwardRight;
    SerializedProperty m_backward;
    SerializedProperty m_backwardLeft;
    SerializedProperty m_backwardRight;
    SerializedProperty m_left;
    SerializedProperty m_right;
    SerializedProperty m_useFallbackOnNeutral;
    SerializedProperty m_fallbackRoute;
    SerializedProperty m_motionForwardRoute;
    SerializedProperty m_directionalInputFrame;
    SerializedProperty m_useMotionCurveBasisOverride;
    SerializedProperty m_motionCurveBasisOverride;

    static GUIStyle s_sectionHeader;
    static GUIStyle s_cellLabel;

    void OnEnable()
    {
        if (target == null)
        {
            return;
        }

        m_displayName = serializedObject.FindProperty("displayName");
        m_icon = serializedObject.FindProperty("icon");
        m_showOnHud = serializedObject.FindProperty("showOnHud");
        m_baseCooldownSeconds = serializedObject.FindProperty("baseCooldownSeconds");
        m_costs = serializedObject.FindProperty("costs");
        m_requiredAbilityTags = serializedObject.FindProperty("requiredAbilityTags");
        m_routes = serializedObject.FindProperty("routes");
        m_forward = serializedObject.FindProperty("forward");
        m_forwardLeft = serializedObject.FindProperty("forwardLeft");
        m_forwardRight = serializedObject.FindProperty("forwardRight");
        m_backward = serializedObject.FindProperty("backward");
        m_backwardLeft = serializedObject.FindProperty("backwardLeft");
        m_backwardRight = serializedObject.FindProperty("backwardRight");
        m_left = serializedObject.FindProperty("left");
        m_right = serializedObject.FindProperty("right");
        m_useFallbackOnNeutral = serializedObject.FindProperty("useFallbackOnNeutral");
        m_fallbackRoute = serializedObject.FindProperty("fallbackRoute");
        m_motionForwardRoute = serializedObject.FindProperty("motionForwardRoute");
        m_directionalInputFrame = serializedObject.FindProperty("directionalInputFrame");
        m_useMotionCurveBasisOverride = serializedObject.FindProperty("useMotionCurveBasisOverride");
        m_motionCurveBasisOverride = serializedObject.FindProperty("motionCurveBasisOverride");
    }

    public override void OnInspectorGUI()
    {
        if (target == null || serializedObject == null)
        {
            return;
        }

        EnsureStyles();
        serializedObject.Update();

        DrawIdentitySection();
        EditorGUILayout.Space(4f);
        DrawDirectionalSection();
        EditorGUILayout.Space(4f);
        DrawSplitFrameSection();
        EditorGUILayout.Space(4f);
        DrawNeutralSection();
        EditorGUILayout.Space(4f);
        DrawMotionSection();
        EditorGUILayout.Space(4f);
        DrawAdvancedSection();

        serializedObject.ApplyModifiedProperties();
    }

    // ─────────── Sections ───────────

    void DrawIdentitySection()
    {
        var open = SessionState.GetBool(PrefIdentity, true);
        var next = BeginSection("Identity (技能组 · 共享属性)", open);
        SessionState.SetBool(PrefIdentity, next);
        if (next)
        {
            EditorGUILayout.PropertyField(m_displayName);
            EditorGUILayout.PropertyField(m_icon);
            EditorGUILayout.PropertyField(m_showOnHud, new GUIContent("Show On Hud"));
            EditorGUILayout.HelpBox(
                "勾选后 HUD 显示本组一个图标（用上方 Icon / Display Name / Cooldown）。\n" +
                "组内子 Route 无需再勾选 Show On Hud。",
                MessageType.None);
            EditorGUILayout.PropertyField(m_baseCooldownSeconds, new GUIContent("Cooldown (sec)"));
            EditorGUILayout.PropertyField(m_costs, true);
            EditorGUILayout.PropertyField(m_requiredAbilityTags);
        }

        EndSection();
    }

    void DrawDirectionalSection()
    {
        var open = SessionState.GetBool(PrefDirectional, true);
        var next = BeginSection("Directional Routes (8-Dir · 艾尔登式)", open);
        SessionState.SetBool(PrefDirectional, next);
        if (next)
        {
            DrawCoverageBanner();
            EditorGUILayout.Space(4f);
            DrawDirectionalDisk();
            EditorGUILayout.Space(2f);
            EditorGUILayout.HelpBox(
                "提示：4 个斜向槽（FL/FR/BL/BR）若留空，运行时按象限主轴回落 — FL/FR → Forward，BL/BR → Backward。",
                MessageType.None);
        }

        EndSection();
    }

    void DrawSplitFrameSection()
    {
        var open = SessionState.GetBool(PrefSplitFrame, true);
        var next = BeginSection("SplitFrame Directional (209.3 · 输入 + 位移分轨)", open);
        SessionState.SetBool(PrefSplitFrame, next);
        if (next)
        {
            DrawInputFramePopup();
            DrawInputFrameRuntimeHelp();
            EditorGUILayout.PropertyField(
                m_useMotionCurveBasisOverride,
                new GUIContent("Override Motion Curve Basis"));
            using (new EditorGUI.DisabledScope(!m_useMotionCurveBasisOverride.boolValue))
            {
                EditorGUILayout.PropertyField(
                    m_motionCurveBasisOverride,
                    new GUIContent("Motion Curve Basis", "Motion 曲线 XYZ → 世界"));
            }

            EditorGUILayout.HelpBox(
                "推荐默认（屏感 Route + 体轴位移）：\n" +
                "· Input Frame = Body Fixed\n" +
                "· Override Motion Curve Basis = ✓\n" +
                "· Motion Curve Basis = Character Forward\n\n" +
                "Slide 斜向（210.2）：Profile Motion Space = Displacement Aligned + Rotation = Align To Velocity。\n" +
                "Logic Projected = 相对 SnapshotBasisFacing 投影分槽（T7：相机约 90° 时 D 可走 Forward，与 BodyFixed Right 分叉）。\n" +
                "Character Stick / World Stick 已塌缩为 Body Fixed；下拉只对新资产露出两个有效值。既有 int=2/3 仍可读，不写资产。",
                MessageType.Info);
        }

        EndSection();
    }

    void DrawInputFramePopup()
    {
        int current = m_directionalInputFrame.intValue;
        var labels = new List<GUIContent>(4);
        var values = new List<int>(4);
        labels.Add(new GUIContent("Body Fixed (屏感 Route · 本体分槽)"));
        values.Add((int)DirectionalInputFrame.BodyFixed);
        labels.Add(new GUIContent("Logic Projected (逻辑投影分槽)"));
        values.Add((int)DirectionalInputFrame.LogicProjected);
#pragma warning disable CS0618
        if (current == (int)DirectionalInputFrame.CharacterStick)
        {
            labels.Add(new GUIContent("Character Stick（已塌缩→Body Fixed）"));
            values.Add(current);
        }
        else if (current == (int)DirectionalInputFrame.WorldStick)
        {
            labels.Add(new GUIContent("World Stick（已塌缩→Body Fixed）"));
            values.Add(current);
        }
#pragma warning restore CS0618

        EditorGUILayout.IntPopup(
            m_directionalInputFrame,
            labels.ToArray(),
            values.ToArray(),
            new GUIContent("Directional Input Frame", "WSAD → 八向 Route 槽；新资产只选 BodyFixed / LogicProjected"));
    }

    void DrawInputFrameRuntimeHelp()
    {
        string runtime;
        int frame = m_directionalInputFrame.intValue;
        if (frame == (int)DirectionalInputFrame.LogicProjected)
        {
            runtime = "Runtime Source（Chord 窗内）：SnapshotBasisFacing。T7 Play：相机约 90° 时 D → Forward，与 BodyFixed Right 分叉。";
        }
#pragma warning disable CS0618
        else if (frame == (int)DirectionalInputFrame.CharacterStick
                 || frame == (int)DirectionalInputFrame.WorldStick)
#pragma warning restore CS0618
        {
            runtime = "Runtime Source（Chord 窗内）：已塌缩 ScreenStickRaw，与 BodyFixed 同一公式。T7：WorldStick 大夹角 D 仍 Right；CharacterStick 90° 几何本样本未覆盖。不写资产。";
        }
        else
        {
            runtime = "Runtime Source（Chord 窗内）：ScreenStickRaw。屏感 D → Right 槽。";
        }

        EditorGUILayout.HelpBox(
            "Configured vs Runtime（只读，不写字段）\n" +
            runtime +
            "\n持续移动过 Chord 窗再 Space → MotionForward 旁路，忽略本枚举（T8）。\n" +
            "本字段只选 Route 槽，不改 Action FacingPolicy。",
            MessageType.None);
    }

    void DrawNeutralSection()
    {
        var open = SessionState.GetBool(PrefNeutral, true);
        var next = BeginSection("Neutral & Fallback (无方向输入)", open);
        SessionState.SetBool(PrefNeutral, next);
        if (next)
        {
            EditorGUILayout.PropertyField(m_useFallbackOnNeutral,
                new GUIContent("Use Fallback On Neutral"));
            DrawNeutralFeedback();
        }

        EndSection();
    }

    void DrawMotionSection()
    {
        var open = SessionState.GetBool(PrefMotion, true);
        var next = BeginSection("Motion Forward Route (持续移动 + Space)", open);
        SessionState.SetBool(PrefMotion, next);
        if (next)
        {
            EditorGUILayout.PropertyField(
                m_motionForwardRoute,
                new GUIContent("Motion Forward Route"));
            EditorGUILayout.HelpBox(
                "留空 → 持续移动 + Space 复用 Forward 槽；\n" +
                "配置 → 持续移动 + Space 走此专属 Route（前冲翻滚等）。",
                MessageType.None);
        }

        EndSection();
    }

    void DrawAdvancedSection()
    {
        var open = SessionState.GetBool(PrefAdvanced, false);
        var next = BeginSection("Advanced (自动同步 · 调试用)", open);
        SessionState.SetBool(PrefAdvanced, next);
        if (next)
        {
            EditorGUILayout.HelpBox(
                "Routes[] 数组由 OnValidate 从八向 + Fallback 槽自动聚合 — 只读展示。",
                MessageType.None);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(m_routes, new GUIContent("Routes (read-only)"), true);
            }
        }

        EndSection();
    }

    // ─────────── 八向圆盘 ───────────

    void DrawDirectionalDisk()
    {
        // 单元格宽度 = (Inspector 内容宽 - 间距) / 3
        var totalWidth = EditorGUIUtility.currentViewWidth - 60f;
        var cellWidth = Mathf.Clamp((totalWidth - 16f) / 3f, 80f, 200f);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawDiskRow(cellWidth,
                ("↖ FL", m_forwardLeft),
                ("↑ F",  m_forward),
                ("↗ FR", m_forwardRight));

            DrawDiskRow(cellWidth,
                ("← L",  m_left),
                ("FB",   m_fallbackRoute),
                ("→ R",  m_right));

            DrawDiskRow(cellWidth,
                ("↙ BL", m_backwardLeft),
                ("↓ B",  m_backward),
                ("↘ BR", m_backwardRight));
        }
    }

    void DrawDiskRow(float cellWidth,
        (string label, SerializedProperty prop) c0,
        (string label, SerializedProperty prop) c1,
        (string label, SerializedProperty prop) c2)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawDiskCell(c0.label, c0.prop, cellWidth);
            DrawDiskCell(c1.label, c1.prop, cellWidth);
            DrawDiskCell(c2.label, c2.prop, cellWidth);
        }
    }

    void DrawDiskCell(string label, SerializedProperty prop, float width)
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(width)))
        {
            EditorGUILayout.LabelField(label, s_cellLabel, GUILayout.Width(width));
            var filled = prop.objectReferenceValue != null;
            var prevColor = GUI.color;
            if (!filled) GUI.color = new Color(1f, 1f, 1f, 0.55f);
            EditorGUILayout.PropertyField(prop, GUIContent.none, GUILayout.Width(width));
            GUI.color = prevColor;
        }
    }

    // ─────────── 状态反馈 ───────────

    void DrawCoverageBanner()
    {
        int filled = 0;
        int total = 8;
        if (m_forward.objectReferenceValue != null) filled++;
        if (m_forwardLeft.objectReferenceValue != null) filled++;
        if (m_forwardRight.objectReferenceValue != null) filled++;
        if (m_left.objectReferenceValue != null) filled++;
        if (m_right.objectReferenceValue != null) filled++;
        if (m_backward.objectReferenceValue != null) filled++;
        if (m_backwardLeft.objectReferenceValue != null) filled++;
        if (m_backwardRight.objectReferenceValue != null) filled++;

        var msg = $"槽位填充：{filled} / {total}";
        var type = filled == 0 ? MessageType.Warning
                 : filled < 4 ? MessageType.Info
                 : MessageType.None;
        EditorGUILayout.HelpBox(msg, type);
    }

    void DrawNeutralFeedback()
    {
        var useFallback = m_useFallbackOnNeutral.boolValue;
        var fallbackSet = m_fallbackRoute.objectReferenceValue != null;
        var forwardSet = m_forward.objectReferenceValue != null;

        if (useFallback)
        {
            if (!fallbackSet)
            {
                EditorGUILayout.HelpBox(
                    "⚠ 勾选了 Use Fallback On Neutral，但 Fallback Route 为空 — 中性输入会 drop（无任何动作）。",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"✓ 艾尔登式：中性输入 → {((Object)m_fallbackRoute.objectReferenceValue).name}",
                    MessageType.Info);
            }
        }
        else
        {
            if (!forwardSet)
            {
                EditorGUILayout.HelpBox(
                    "⚠ 旧 4 槽兼容模式下中性会回落到 Forward 槽，但 Forward 槽为空。",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "○ 旧 4 槽兼容：中性输入 → Forward 槽。",
                    MessageType.None);
            }
        }
    }

    // ─────────── Section chrome ───────────

    bool BeginSection(string title, bool open)
    {
        var rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        var headerRect = GUILayoutUtility.GetRect(GUIContent.none, s_sectionHeader, GUILayout.ExpandWidth(true));
        var next = EditorGUI.Foldout(headerRect, open, title, true, s_sectionHeader);
        _ = rect;
        return next;
    }

    static void EndSection()
    {
        EditorGUILayout.EndVertical();
    }

    static void EnsureStyles()
    {
        if (s_sectionHeader == null)
        {
            s_sectionHeader = new GUIStyle(EditorStyles.foldoutHeader)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
            };
        }
        if (s_cellLabel == null)
        {
            s_cellLabel = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
            };
        }
    }
}
#endif
