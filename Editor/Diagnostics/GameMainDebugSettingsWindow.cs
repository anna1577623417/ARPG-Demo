#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Tools/GameMain/Debug Settings — 集中管理 Probe 开关，替代 Player Inspector Debug 区。</summary>
public sealed class GameMainDebugSettingsWindow : EditorWindow
{
    Vector2 _scroll;

    [MenuItem("Tools/GameMain/Debug Settings...", false, 100)]
    public static void Open()
    {
        var win = GetWindow<GameMainDebugSettingsWindow>();
        win.titleContent = new GUIContent("GameMain Debug");
        win.minSize = new Vector2(360f, 420f);
        win.Show();
    }

    [MenuItem("Tools/GameMain/Debug/Reset All Toggles", false, 200)]
    static void MenuResetAll()
    {
        GameMainDebugSettings.ResetAll();
        Debug.Log("[GameMain.Debug] 全部开关已重置为关。");
    }

    void OnEnable() => GameMainDebugSettings.LoadFromEditorPrefs();

    void OnGUI()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("GameMain Debug Settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Probe 开关集中在此管理，不再挂在 Player Prefab 上。\n" +
            "Play 前修改会写入 EditorPrefs；进入 Play Mode 自动加载。\n" +
            "Console 过滤示例：[SkillRoute]、[Loco]、[Interrupt]、[ComboAirGate]。",
            MessageType.Info);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawSection("Skill / Route", () =>
        {
            Toggle(
                () => GameMainDebugSettings.SkillRouteGraph,
                v => GameMainDebugSettings.SkillRouteGraph = v,
                "Skill Route Graph",
                "Combat Graph 专向：[SkillRoute][Graph] / [CombatGraph][Finisher]");
            Toggle(
                () => GameMainDebugSettings.SkillRouteDodge4,
                v => GameMainDebugSettings.SkillRouteDodge4 = v,
                "Skill Route Dodge4 / Dodge8",
                "四向站立闪避 [SkillRoute][Dodge4]、[Dodge8]");
            Toggle(
                () => GameMainDebugSettings.SkillRouteRoll4,
                v => GameMainDebugSettings.SkillRouteRoll4 = v,
                "Skill Route Roll4",
                "武侠翻滚 Group [SkillRoute][Roll4]");
            Toggle(
                () => GameMainDebugSettings.SkillAbility,
                v => GameMainDebugSettings.SkillAbility = v,
                "Skill Ability Gate",
                "能力准入 [Ability] route gate / loadout map");
        });

        DrawSection("Locomotion / Turn / Stop", () =>
        {
            Toggle(
                () => GameMainDebugSettings.Locomotion,
                v => GameMainDebugSettings.Locomotion = v,
                "Locomotion Resolver",
                "Resolver 决策边沿 L2-L5");
            Toggle(
                () => GameMainDebugSettings.LocomotionTrace,
                v => GameMainDebugSettings.LocomotionTrace = v,
                "Locomotion Trace",
                "输入/移动/转身节流 [Loco]");
            Toggle(
                () => GameMainDebugSettings.RotationGate,
                v => GameMainDebugSettings.RotationGate = v,
                "Rotation Gate",
                "转向闸门翻转 [RotGate]");
            Toggle(
                () => GameMainDebugSettings.Stop,
                v => GameMainDebugSettings.Stop = v,
                "Stop Authoring",
                "停步探针 [Stop]");
            Toggle(
                () => GameMainDebugSettings.TurnSubState,
                v => GameMainDebugSettings.TurnSubState = v,
                "Turn Sub-State",
                "四向 Turn ENTER/PLAY/EXIT [Turn][Sub]");
        });

        DrawSection("Combat / Interrupt", () =>
        {
            Toggle(
                () => GameMainDebugSettings.InterruptFlow,
                v => GameMainDebugSettings.InterruptFlow = v,
                "Interrupt Flow",
                "打断链路 [Interrupt] allow/deny");
            Toggle(
                () => GameMainDebugSettings.ComboAirGate,
                v => GameMainDebugSettings.ComboAirGate = v,
                "Combo Air Gate",
                "空中 Intent 接纳/拒绝 [ComboAirGate]");
        });

        DrawSection("Intent / Simulation", () =>
        {
            Toggle(
                () => GameMainDebugSettings.IntentArbitration,
                v => GameMainDebugSettings.IntentArbitration = v,
                "Intent Arbitration",
                "StateManager 意图仲裁 Trace");
            Toggle(
                () => GameMainDebugSettings.SimulateLockOnLocomotion,
                v => GameMainDebugSettings.SimulateLockOnLocomotion = v,
                "Simulate LockOn (Locomotion)",
                "Play 验收：模拟 LockOn 启用 Strafe；非 Log");
        });

        DrawSection("Legacy Mono Switches（仍可用）", () =>
        {
            EditorGUILayout.HelpBox(
                "InputActionProbeSwitch / ActionTurnProbeSwitch 等 MonoBehaviour 开关仍可在场景中挂载；\n" +
                "新 Probe 请优先注册到 GameMainDebugSettings。",
                MessageType.None);
        });

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save", GUILayout.Height(24f)))
        {
            GameMainDebugSettings.SaveToEditorPrefs();
            Debug.Log("[GameMain.Debug] 已保存到 EditorPrefs。");
        }

        if (GUILayout.Button("Reload", GUILayout.Height(24f)))
        {
            GameMainDebugSettings.LoadFromEditorPrefs();
            Repaint();
        }

        if (GUILayout.Button("Reset All", GUILayout.Height(24f)))
        {
            GameMainDebugSettings.ResetAll();
            Repaint();
        }

        EditorGUILayout.EndHorizontal();
    }

    static void DrawSection(string title, System.Action drawBody)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        drawBody();
        EditorGUI.indentLevel--;
    }

    static void Toggle(System.Func<bool> get, System.Action<bool> set, string label, string tooltip)
    {
        EditorGUI.BeginChangeCheck();
        var next = EditorGUILayout.Toggle(new GUIContent(label, tooltip), get());
        if (EditorGUI.EndChangeCheck())
        {
            set(next);
            GameMainDebugSettings.SaveToEditorPrefs();
        }
    }
}
#endif
