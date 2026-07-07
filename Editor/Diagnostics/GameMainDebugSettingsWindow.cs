#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>GameMain → Debug → Log Settings — 全项目 Play 诊断 Log 唯一入口。</summary>
public sealed class GameMainDebugSettingsWindow : EditorWindow
{
    Vector2 _scroll;

    [MenuItem("GameMain/Debug/Log Settings...", false, 0)]
    public static void Open()
    {
        var win = GetWindow<GameMainDebugSettingsWindow>();
        win.titleContent = new GUIContent("GameMain Log Settings");
        win.minSize = new Vector2(420f, 520f);
        win.Show();
    }

    [MenuItem("GameMain/Debug/Disable All Logs", false, 1)]
    static void MenuDisableAll()
    {
        GameMainDebugSettings.ResetAll();
        Debug.Log("[GameMain.Debug] 已全部关闭。");
    }

    void OnEnable() => GameMainDebugSettings.LoadFromEditorPrefs();

    void OnGUI()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("GameMain Log Settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "所有 Play 诊断 Log 只在此处开关。\n" +
            "Console 过滤示例：[Action]、[Loco]、[SkillRoute]、[DirInput213]、[HudBug]。\n" +
            "进入 Play Mode 前修改会写入 EditorPrefs 并自动加载。",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全部开启", GUILayout.Height(22f)))
        {
            GameMainDebugSettings.EnableAllLogs();
            Repaint();
        }

        if (GUILayout.Button("全部关闭", GUILayout.Height(22f)))
        {
            GameMainDebugSettings.ResetAll();
            Repaint();
        }

        if (GUILayout.Button("保存", GUILayout.Height(22f)))
        {
            GameMainDebugSettings.SaveToEditorPrefs();
            HudBugProbe.OnSettingsChanged();
        }

        if (GUILayout.Button("重载", GUILayout.Height(22f)))
        {
            GameMainDebugSettings.LoadFromEditorPrefs();
            HudBugProbe.OnSettingsChanged();
            Repaint();
        }

        EditorGUILayout.EndHorizontal();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawSection("Skill / Route", () =>
        {
            Toggle(() => GameMainDebugSettings.SkillRouteGraph, v => GameMainDebugSettings.SkillRouteGraph = v,
                "Skill Route Graph", "[SkillRoute][Graph] / [CombatGraph][Finisher]");
            Toggle(() => GameMainDebugSettings.SkillRouteDodge4, v => GameMainDebugSettings.SkillRouteDodge4 = v,
                "Skill Route Dodge4 / Dodge8", "[SkillRoute][Dodge4] [Dodge8]");
            Toggle(() => GameMainDebugSettings.SkillRouteRoll4, v => GameMainDebugSettings.SkillRouteRoll4 = v,
                "Skill Route Roll4", "[SkillRoute][Roll4]");
            Toggle(() => GameMainDebugSettings.SkillAbility, v => GameMainDebugSettings.SkillAbility = v,
                "Skill Ability Gate", "[Ability]");
            Toggle(() => GameMainDebugSettings.DirectionalInputDiagLog,
                v => GameMainDebugSettings.DirectionalInputDiagLog = v,
                "Directional Input Diag (213.1)", "[DirInput213] Space/Shift+WASD 全链路 + WARN");
            Toggle(() => GameMainDebugSettings.DodgeChord8Log, v => GameMainDebugSettings.DodgeChord8Log = v,
                "Dodge Chord8 / SplitFrame (legacy)", "[DodgeChord8] [SplitFrame]");
            Toggle(() => GameMainDebugSettings.HoldMotionDodgeLog, v => GameMainDebugSettings.HoldMotionDodgeLog = v,
                "Hold Motion Dodge", "[HoldMotionDodge]");
            Toggle(() => GameMainDebugSettings.ActionYawLog, v => GameMainDebugSettings.ActionYawLog = v,
                "Action Yaw", "[ActionYaw]");
        });

        DrawSection("Locomotion / Action / Stop", () =>
        {
            Toggle(() => GameMainDebugSettings.Locomotion, v => GameMainDebugSettings.Locomotion = v,
                "Locomotion 165", "[Action][AnimSync] [Action][Exit] [Loco] [Jump]");
            Toggle(() => GameMainDebugSettings.LocomotionTrace, v => GameMainDebugSettings.LocomotionTrace = v,
                "Locomotion Trace", "[Loco] 输入/移动/转身节流");
            Toggle(() => GameMainDebugSettings.Stop, v => GameMainDebugSettings.Stop = v,
                "Stop Authoring", "[Action][Exit] stop/residual + [Stop]");
            Toggle(() => GameMainDebugSettings.RotationGate, v => GameMainDebugSettings.RotationGate = v,
                "Rotation Gate", "[RotGate]");
            Toggle(() => GameMainDebugSettings.TurnSubState, v => GameMainDebugSettings.TurnSubState = v,
                "Turn Sub-State", "[Turn][Sub]");
        });

        DrawSection("Combat / Intent", () =>
        {
            Toggle(() => GameMainDebugSettings.InterruptFlow, v => GameMainDebugSettings.InterruptFlow = v,
                "Interrupt Flow", "[Interrupt]");
            Toggle(() => GameMainDebugSettings.ComboAirGate, v => GameMainDebugSettings.ComboAirGate = v,
                "Combo Air Gate", "[ComboAirGate]");
            Toggle(() => GameMainDebugSettings.IntentArbitration, v => GameMainDebugSettings.IntentArbitration = v,
                "Intent Arbitration", "StateManager 意图仲裁");
        });

        DrawSection("HUD", () =>
        {
            ToggleHud(() => GameMainDebugSettings.HudBugLog, v => GameMainDebugSettings.HudBugLog = v,
                "HUD Bug Log", "[HudBug] 加载/重复/CD");
        });

        DrawSection("Editor Authoring", () =>
        {
            Toggle(() => GameMainDebugSettings.ActionTimelinePreviewLog,
                v => GameMainDebugSettings.ActionTimelinePreviewLog = v,
                "Action Timeline Preview", "[ActTimelineDiag]");
            Toggle(() => GameMainDebugSettings.MirrorDiagLog, v => GameMainDebugSettings.MirrorDiagLog = v,
                "Mirror Diag", "[MirrorDiag]");
            Toggle(() => GameMainDebugSettings.SimulateLockOnLocomotion,
                v => GameMainDebugSettings.SimulateLockOnLocomotion = v,
                "Simulate LockOn (非 Log)", "Play 验收 Strafe；非 Console Log");
        });

        EditorGUILayout.EndScrollView();
    }

    static void DrawSection(string title, System.Action drawBody)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        drawBody();
        EditorGUI.indentLevel--;
    }

    static void ToggleHud(System.Func<bool> get, System.Action<bool> set, string label, string tooltip)
    {
        EditorGUI.BeginChangeCheck();
        var next = EditorGUILayout.Toggle(new GUIContent(label, tooltip), get());
        if (EditorGUI.EndChangeCheck())
        {
            set(next);
            GameMainDebugSettings.SaveToEditorPrefs();
            HudBugProbe.OnSettingsChanged();
        }
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
