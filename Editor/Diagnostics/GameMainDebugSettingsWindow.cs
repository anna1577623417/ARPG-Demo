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
            Toggle(() => GameMainDebugSettings.AbilityGate242Log,
                v => GameMainDebugSettings.AbilityGate242Log = v,
                "Ability Gate 242 / Double Gate", "[Ability242] Transition → Air L1 → Route L2 → ActionWindow → State");
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
            Toggle(() => GameMainDebugSettings.AnimSpeed226Log, v => GameMainDebugSettings.AnimSpeed226Log = v,
                "Anim Speed 226", "[AnimSpeed226] BEGIN/END 积分与 profileFactor");
            Toggle(() => GameMainDebugSettings.AnimSpeed228Log, v => GameMainDebugSettings.AnimSpeed228Log = v,
                "Anim Speed 228", "[AnimSpeed228] FreeFrontAutoTail SOLVE/BEGIN/END/REJECT");
            Toggle(() => GameMainDebugSettings.AnimTransition227Log, v => GameMainDebugSettings.AnimTransition227Log = v,
                "Anim Transition 227", "[AnimTransition227] 边沿核心事件：RESOLVE/REQUEST/BEGIN/50%/END/SUPERSEDE");
            Toggle(() => GameMainDebugSettings.AnimTransition227BugLog, v => GameMainDebugSettings.AnimTransition227BugLog = v,
                "Anim Transition 227 Bug", "[AnimTransition227Bug] 同 Clip 拦截/过渡抢占/空中基线分流");
            Toggle(() => GameMainDebugSettings.LocomotionTransition227BugLog,
                v => GameMainDebugSettings.LocomotionTransition227BugLog = v,
                "Locomotion 227 Bug", "[Locomotion227Bug] Jump/RunStart + WASD 位置结算、Motor 后二次写入与显式 Teleport");
            Toggle(() => GameMainDebugSettings.CameraTurn233Log,
                v => GameMainDebugSettings.CameraTurn233Log = v,
                "Camera / Turn 233", "[CameraTurn233] WASD→移动参考→逻辑/表现转向→Chase→Proxy→实际渲染相机");
            Toggle(() => GameMainDebugSettings.CharacterTurnDisplacement233Log,
                v => GameMainDebugSettings.CharacterTurnDisplacement233Log = v,
                "Character Turn / Move 233.4", "[CharacterTurn233] 输入转向→Logic/Root/Visual + Walk/Run Start/Loop/End 位移摘要");
            Toggle(() => GameMainDebugSettings.LocomotionMotion233Log,
                v => GameMainDebugSettings.LocomotionMotion233Log = v,
                "Locomotion Motion 233.5", "[LocomotionMotion233] 点按→Start/Loop/End + MP作者/Stop有效/Executor/KCC 位移对账");
            Toggle(() => GameMainDebugSettings.StopTap234Log,
                v => GameMainDebugSettings.StopTap234Log = v,
                "Stop Tap 234.6", "[StopTap234] 终态：点按丢掉余速 v0=2D/T 铺满尾段、无限连点 Toggle，边沿最多3行");
            Toggle(() => GameMainDebugSettings.LocomotionTurnPresentation235Log,
                v => GameMainDebugSettings.LocomotionTurnPresentation235Log = v,
                "Locomotion Turn Presentation 235", "[LocoTurn235] INPUT→DECIDE→SELECT→PLAY→END，有限边沿、无堆栈");
            Toggle(() => GameMainDebugSettings.SkillGroupTurn237Log,
                v => GameMainDebugSettings.SkillGroupTurn237Log = v,
                "SkillGroup Turn 237", "[Turn237] WASD+Space 八向选路 / ImmediateCommit / Turn抢占 / 姿势硬贴");
            Toggle(() => GameMainDebugSettings.DirectionAuthority237Log,
                v => GameMainDebugSettings.DirectionAuthority237Log = v,
                "Direction Authority 237 v3", "[DIR] rid / INPUT_EDGE / LOCO_CHANGE / DIR_ROUTE_PICK / FACING_REQ；Held 改向与 Selection Frame 取证，不改行为");
            Toggle(() => GameMainDebugSettings.YAxis241Log,
                v => GameMainDebugSettings.YAxis241Log = v,
                "Y Axis 241 / AD-MP 三权", "[Y241] AD/MP 资产身份 + V2/Legacy 来源 + YMotion/Gravity/GroundConstraint 穿线与最终结果");
        });

        DrawSection("Combat / Intent", () =>
        {
            Toggle(() => GameMainDebugSettings.InterruptFlow, v => GameMainDebugSettings.InterruptFlow = v,
                "Interrupt Flow", "[Interrupt]");
            Toggle(() => GameMainDebugSettings.ComboAirGate, v => GameMainDebugSettings.ComboAirGate = v,
                "Combo Air Gate", "[ComboAirGate]");
            Toggle(() => GameMainDebugSettings.CombatHit, v => GameMainDebugSettings.CombatHit = v,
                "Combat Hit (214.3)", "[CombatHit] SPAWN/OVERLAP/DAMAGE");
            Toggle(() => GameMainDebugSettings.ReactionDirection2206Log,
                v => GameMainDebugSettings.ReactionDirection2206Log = v,
                "Reaction Direction (220.6)", "[220.6] 四向/Up Reaction resolve → HitReact → Playback → Present");
            Toggle(() => GameMainDebugSettings.AIBrain2207Log,
                v => GameMainDebugSettings.AIBrain2207Log = v,
                "AI Brain (220.7)", "[220.7] AIController / Blackboard / BT / SkillSelector");
            Toggle(() => GameMainDebugSettings.EnemyPerception2208Log,
                v => GameMainDebugSettings.EnemyPerception2208Log = v,
                "Enemy Perception (220.8)", "[220.8] 感知扫描 / 目标获取 / Blackboard 写入");
            Toggle(() => GameMainDebugSettings.IntentArbitration, v => GameMainDebugSettings.IntentArbitration = v,
                "Intent Arbitration (220.5)", "[220.5] StateManager / Enemy Runtime 意图仲裁");
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
            Toggle(() => GameMainDebugSettings.CombatSceneDrawSource,
                v => GameMainDebugSettings.CombatSceneDrawSource = v,
                "224.0 Scene Draw Source", "[224.0][DrawSource] Scene 主 Shape source/owner 标签");
            Toggle(() => GameMainDebugSettings.CombatContactBaseline,
                v => GameMainDebugSettings.CombatContactBaseline = v,
                "224.0 Contact Baseline", "[224.0][Baseline] Preview/Runtime Pose·Geometry 单次快照");
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
        var next = EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), get());
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
        // ToggleLeft removes the fixed Inspector label column. Long diagnostic labels
        // no longer run underneath the checkbox on narrow Log Settings windows.
        var next = EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), get());
        if (EditorGUI.EndChangeCheck())
        {
            set(next);
            GameMainDebugSettings.SaveToEditorPrefs();
        }
    }
}
#endif
