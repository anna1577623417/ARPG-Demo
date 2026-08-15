using UnityEngine;

/// <summary>
/// 全局 Debug Log 开关 — 由 GameMain → Debug → Log Settings 窗口统一管理。
/// 所有 Probe 只读本类；禁止散落 EditorPrefs 独立开关。
/// </summary>
public static class GameMainDebugSettings
{
    const string Prefix = "GameMain.Debug.";

    // ─── Skill / Route ───
    public static bool SkillRouteGraph { get; set; }
    public static bool SkillRouteDodge4 { get; set; }
    public static bool SkillRouteRoll4 { get; set; }
    public static bool SkillAbility { get; set; }
    public static bool DodgeChord8Log { get; set; }
    public static bool HoldMotionDodgeLog { get; set; }

    /// <summary>213.1 — Space/Shift + WASD 全链路（DISPATCH→PLAY + WARN）。</summary>
    public static bool DirectionalInputDiagLog { get; set; }

    // ─── Locomotion / Action / Stop ───
    public static bool Locomotion { get; set; }
    public static bool LocomotionTrace { get; set; }
    public static bool RotationGate { get; set; }
    public static bool Stop { get; set; }
    public static bool TurnSubState { get; set; }
    public static bool ActionYawLog { get; set; }

    /// <summary>226 — AutoFit/Free × MP Anim 曲线积分守恒（BEGIN/END）。</summary>
    public static bool AnimSpeed226Log { get; set; }

    /// <summary>228 — FreeFrontAutoTail Bake/运行探针（BEGIN/END/SOLVE/REJECT），独立于 226。</summary>
    public static bool AnimSpeed228Log { get; set; }

    /// <summary>227.5.1 — Idle/连续表现与双端口 Mixer（REQUEST/RESOLVE/BEGIN/SAMPLE/END）。</summary>
    public static bool AnimTransition227Log { get; set; }

    /// <summary>227.5.1.1 — Idle→Run / Jump 卡壳异常专用探针；与主链核心 Log 独立开关。</summary>
    public static bool AnimTransition227BugLog { get; set; }

    /// <summary>227 — Jump/RunStart 与 WASD 位置结算、Motor 外写入、显式 Teleport 专项探针。</summary>
    public static bool LocomotionTransition227BugLog { get; set; }

    /// <summary>233 — 摄像机、移动参考、逻辑转向与表现转向权威链专项探针。</summary>
    public static bool CameraTurn233Log { get; set; }

    /// <summary>233.4 — 角色逻辑/根/表现转向与 Walk/Run 过渡最小位移专项探针。</summary>
    public static bool CharacterTurnDisplacement233Log { get; set; }

    /// <summary>233.5 — Walk/Run 点按、Start/Loop/End 与 MotionProfile 有效位移来源专项探针。</summary>
    public static bool LocomotionMotion233Log { get; set; }

    /// <summary>234.6 — Run 点按 Stop 位移：v_entry vs RunSpeed、积分 D、转向重叠。</summary>
    public static bool StopTap234Log { get; set; }

    /// <summary>235 — 即时逻辑转向后的 90/180 补偿性 Turn 表现时机、选片与播放专项探针。</summary>
    public static bool LocomotionTurnPresentation235Log { get; set; }

    /// <summary>237 — WASD+Space 八向 SkillGroup 逻辑转向、ChordReframe 与姿势抢占专项探针。</summary>
    public static bool SkillGroupTurn237Log { get; set; }

    /// <summary>237.3 L0 — 方向意图 / 朝向权威合同探针。可与 SkillGroupTurn237Log 同开。</summary>
    public static bool DirectionAuthority237Log { get; set; }

    // ─── Combat / Interrupt ───
    public static bool InterruptFlow { get; set; }
    public static bool ComboAirGate { get; set; }

    /// <summary>214.3 — CombatObject Spawn / Overlap / Damage 诊断。</summary>
    public static bool CombatHit { get; set; }

    /// <summary>220.6 — ReactionSet 方向解析、HitReact 路线与表现链诊断。</summary>
    public static bool ReactionDirection2206Log { get; set; }

    /// <summary>220.7 — AIController、Blackboard 与行为树运行链诊断。</summary>
    public static bool AIBrain2207Log { get; set; }

    /// <summary>220.8 — EnemyPerception 写入 Blackboard 的感知链诊断。</summary>
    public static bool EnemyPerception2208Log { get; set; }

    // ─── Intent / Simulation ───
    public static bool IntentArbitration { get; set; }
    public static bool SimulateLockOnLocomotion { get; set; }

    // ─── HUD ───
    public static bool HudBugLog { get; set; }

    // ─── Editor Authoring ───
    public static bool ActionTimelinePreviewLog { get; set; }
    public static bool MirrorDiagLog { get; set; }

    /// <summary>224.0 — Scene 主 Shape 绘制源标签 / 同帧冲突观察。</summary>
    public static bool CombatSceneDrawSource { get; set; }

    /// <summary>224.0 — Contact Preview/Runtime Pose·Geometry 单次基线快照。</summary>
    public static bool CombatContactBaseline { get; set; }

#if UNITY_EDITOR
    public static void LoadFromEditorPrefs()
    {
        SkillRouteGraph = EditorPrefsGetBool(nameof(SkillRouteGraph));
        SkillRouteDodge4 = EditorPrefsGetBool(nameof(SkillRouteDodge4));
        SkillRouteRoll4 = EditorPrefsGetBool(nameof(SkillRouteRoll4));
        SkillAbility = EditorPrefsGetBool(nameof(SkillAbility));
        DodgeChord8Log = EditorPrefsGetBool(nameof(DodgeChord8Log))
                         || UnityEditor.EditorPrefs.GetBool("Core-Drive/DodgeChord8Probe/EnableLog", false);
        HoldMotionDodgeLog = EditorPrefsGetBool(nameof(HoldMotionDodgeLog))
                               || UnityEditor.EditorPrefs.GetBool("Core-Drive/HoldMotionDodgeProbe/EnableLog", false);
        DirectionalInputDiagLog = EditorPrefsGetBool(nameof(DirectionalInputDiagLog))
            || EditorPrefsGetBool(nameof(DodgeChord8Log))
            || EditorPrefsGetBool(nameof(HoldMotionDodgeLog));
        Locomotion = EditorPrefsGetBool(nameof(Locomotion));
        LocomotionTrace = EditorPrefsGetBool(nameof(LocomotionTrace));
        RotationGate = EditorPrefsGetBool(nameof(RotationGate));
        Stop = EditorPrefsGetBool(nameof(Stop));
        TurnSubState = EditorPrefsGetBool(nameof(TurnSubState));
        ActionYawLog = EditorPrefsGetBool(nameof(ActionYawLog))
                       || UnityEditor.EditorPrefs.GetBool("Core-Drive/ActionYawProbe/EnableLog", false);
        AnimSpeed226Log = EditorPrefsGetBool(nameof(AnimSpeed226Log));
        AnimSpeed228Log = EditorPrefsGetBool(nameof(AnimSpeed228Log));
        AnimTransition227Log = EditorPrefsGetBool(nameof(AnimTransition227Log));
        AnimTransition227BugLog = EditorPrefsGetBool(nameof(AnimTransition227BugLog));
        LocomotionTransition227BugLog = EditorPrefsGetBool(nameof(LocomotionTransition227BugLog));
        CameraTurn233Log = EditorPrefsGetBool(nameof(CameraTurn233Log));
        CharacterTurnDisplacement233Log = EditorPrefsGetBool(nameof(CharacterTurnDisplacement233Log));
        LocomotionMotion233Log = EditorPrefsGetBool(nameof(LocomotionMotion233Log));
        StopTap234Log = EditorPrefsGetBool(nameof(StopTap234Log));
        LocomotionTurnPresentation235Log = EditorPrefsGetBool(nameof(LocomotionTurnPresentation235Log));
        SkillGroupTurn237Log = EditorPrefsGetBool(nameof(SkillGroupTurn237Log));
        DirectionAuthority237Log = EditorPrefsGetBool(nameof(DirectionAuthority237Log));
        InterruptFlow = EditorPrefsGetBool(nameof(InterruptFlow));
        ComboAirGate = EditorPrefsGetBool(nameof(ComboAirGate));
        CombatHit = EditorPrefsGetBool(nameof(CombatHit));
        ReactionDirection2206Log = EditorPrefsGetBool(nameof(ReactionDirection2206Log));
        AIBrain2207Log = EditorPrefsGetBool(nameof(AIBrain2207Log));
        EnemyPerception2208Log = EditorPrefsGetBool(nameof(EnemyPerception2208Log));
        IntentArbitration = EditorPrefsGetBool(nameof(IntentArbitration));
        SimulateLockOnLocomotion = EditorPrefsGetBool(nameof(SimulateLockOnLocomotion));
        HudBugLog = EditorPrefsGetBool(nameof(HudBugLog));
        ActionTimelinePreviewLog = EditorPrefsGetBool(nameof(ActionTimelinePreviewLog))
            || UnityEditor.EditorPrefs.GetBool("Core-Drive/ActionTimelinePreviewProbe/EnableLog", false);
        MirrorDiagLog = EditorPrefsGetBool(nameof(MirrorDiagLog))
            || UnityEditor.EditorPrefs.GetBool("Core-Drive/MirroredClipSampler/EnableDiag", false);
        CombatSceneDrawSource = EditorPrefsGetBool(nameof(CombatSceneDrawSource));
        CombatContactBaseline = EditorPrefsGetBool(nameof(CombatContactBaseline));
    }

    public static void SaveToEditorPrefs()
    {
        EditorPrefsSetBool(nameof(SkillRouteGraph), SkillRouteGraph);
        EditorPrefsSetBool(nameof(SkillRouteDodge4), SkillRouteDodge4);
        EditorPrefsSetBool(nameof(SkillRouteRoll4), SkillRouteRoll4);
        EditorPrefsSetBool(nameof(SkillAbility), SkillAbility);
        EditorPrefsSetBool(nameof(DodgeChord8Log), DodgeChord8Log);
        EditorPrefsSetBool(nameof(HoldMotionDodgeLog), HoldMotionDodgeLog);
        EditorPrefsSetBool(nameof(DirectionalInputDiagLog), DirectionalInputDiagLog);
        EditorPrefsSetBool(nameof(Locomotion), Locomotion);
        EditorPrefsSetBool(nameof(LocomotionTrace), LocomotionTrace);
        EditorPrefsSetBool(nameof(RotationGate), RotationGate);
        EditorPrefsSetBool(nameof(Stop), Stop);
        EditorPrefsSetBool(nameof(TurnSubState), TurnSubState);
        EditorPrefsSetBool(nameof(ActionYawLog), ActionYawLog);
        EditorPrefsSetBool(nameof(AnimSpeed226Log), AnimSpeed226Log);
        EditorPrefsSetBool(nameof(AnimSpeed228Log), AnimSpeed228Log);
        EditorPrefsSetBool(nameof(AnimTransition227Log), AnimTransition227Log);
        EditorPrefsSetBool(nameof(AnimTransition227BugLog), AnimTransition227BugLog);
        EditorPrefsSetBool(nameof(LocomotionTransition227BugLog), LocomotionTransition227BugLog);
        EditorPrefsSetBool(nameof(CameraTurn233Log), CameraTurn233Log);
        EditorPrefsSetBool(nameof(CharacterTurnDisplacement233Log), CharacterTurnDisplacement233Log);
        EditorPrefsSetBool(nameof(LocomotionMotion233Log), LocomotionMotion233Log);
        EditorPrefsSetBool(nameof(StopTap234Log), StopTap234Log);
        EditorPrefsSetBool(nameof(LocomotionTurnPresentation235Log), LocomotionTurnPresentation235Log);
        EditorPrefsSetBool(nameof(SkillGroupTurn237Log), SkillGroupTurn237Log);
        EditorPrefsSetBool(nameof(DirectionAuthority237Log), DirectionAuthority237Log);
        EditorPrefsSetBool(nameof(InterruptFlow), InterruptFlow);
        EditorPrefsSetBool(nameof(ComboAirGate), ComboAirGate);
        EditorPrefsSetBool(nameof(CombatHit), CombatHit);
        EditorPrefsSetBool(nameof(ReactionDirection2206Log), ReactionDirection2206Log);
        EditorPrefsSetBool(nameof(AIBrain2207Log), AIBrain2207Log);
        EditorPrefsSetBool(nameof(EnemyPerception2208Log), EnemyPerception2208Log);
        EditorPrefsSetBool(nameof(IntentArbitration), IntentArbitration);
        EditorPrefsSetBool(nameof(SimulateLockOnLocomotion), SimulateLockOnLocomotion);
        EditorPrefsSetBool(nameof(HudBugLog), HudBugLog);
        EditorPrefsSetBool(nameof(ActionTimelinePreviewLog), ActionTimelinePreviewLog);
        EditorPrefsSetBool(nameof(MirrorDiagLog), MirrorDiagLog);
        EditorPrefsSetBool(nameof(CombatSceneDrawSource), CombatSceneDrawSource);
        EditorPrefsSetBool(nameof(CombatContactBaseline), CombatContactBaseline);
    }

    public static void EnableAllLogs()
    {
        SkillRouteGraph = true;
        SkillRouteDodge4 = true;
        SkillRouteRoll4 = true;
        SkillAbility = true;
        DodgeChord8Log = true;
        HoldMotionDodgeLog = true;
        DirectionalInputDiagLog = true;
        Locomotion = true;
        LocomotionTrace = true;
        RotationGate = true;
        Stop = true;
        TurnSubState = true;
        ActionYawLog = true;
        AnimSpeed226Log = true;
        AnimSpeed228Log = true;
        AnimTransition227Log = true;
        AnimTransition227BugLog = true;
        LocomotionTransition227BugLog = true;
        CameraTurn233Log = true;
        CharacterTurnDisplacement233Log = true;
        LocomotionMotion233Log = true;
        StopTap234Log = true;
        LocomotionTurnPresentation235Log = true;
        SkillGroupTurn237Log = true;
        DirectionAuthority237Log = true;
        InterruptFlow = true;
        ComboAirGate = true;
        CombatHit = true;
        ReactionDirection2206Log = true;
        AIBrain2207Log = true;
        EnemyPerception2208Log = true;
        IntentArbitration = true;
        HudBugLog = true;
        ActionTimelinePreviewLog = true;
        MirrorDiagLog = true;
        CombatSceneDrawSource = true;
        CombatContactBaseline = true;
        SaveToEditorPrefs();
        HudBugProbe.OnSettingsChanged();
    }

    public static void ResetAll()
    {
        SkillRouteGraph = false;
        SkillRouteDodge4 = false;
        SkillRouteRoll4 = false;
        SkillAbility = false;
        DodgeChord8Log = false;
        HoldMotionDodgeLog = false;
        DirectionalInputDiagLog = false;
        Locomotion = false;
        LocomotionTrace = false;
        RotationGate = false;
        Stop = false;
        TurnSubState = false;
        ActionYawLog = false;
        AnimSpeed226Log = false;
        AnimSpeed228Log = false;
        AnimTransition227Log = false;
        AnimTransition227BugLog = false;
        LocomotionTransition227BugLog = false;
        CameraTurn233Log = false;
        CharacterTurnDisplacement233Log = false;
        LocomotionMotion233Log = false;
        StopTap234Log = false;
        LocomotionTurnPresentation235Log = false;
        SkillGroupTurn237Log = false;
        DirectionAuthority237Log = false;
        InterruptFlow = false;
        ComboAirGate = false;
        CombatHit = false;
        ReactionDirection2206Log = false;
        AIBrain2207Log = false;
        EnemyPerception2208Log = false;
        SimulateLockOnLocomotion = false;
        IntentArbitration = false;
        HudBugLog = false;
        ActionTimelinePreviewLog = false;
        MirrorDiagLog = false;
        CombatSceneDrawSource = false;
        CombatContactBaseline = false;
        SaveToEditorPrefs();
        HudBugProbe.OnSettingsChanged();
    }

    static bool EditorPrefsGetBool(string key) =>
        UnityEditor.EditorPrefs.GetBool(Prefix + key, false);

    static void EditorPrefsSetBool(string key, bool value) =>
        UnityEditor.EditorPrefs.SetBool(Prefix + key, value);
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        SkillRouteGraph = false;
        SkillRouteDodge4 = false;
        SkillRouteRoll4 = false;
        SkillAbility = false;
        DodgeChord8Log = false;
        HoldMotionDodgeLog = false;
        DirectionalInputDiagLog = false;
        Locomotion = false;
        LocomotionTrace = false;
        RotationGate = false;
        Stop = false;
        TurnSubState = false;
        ActionYawLog = false;
        AnimSpeed226Log = false;
        AnimSpeed228Log = false;
        AnimTransition227Log = false;
        AnimTransition227BugLog = false;
        LocomotionTransition227BugLog = false;
        CameraTurn233Log = false;
        CharacterTurnDisplacement233Log = false;
        LocomotionMotion233Log = false;
        StopTap234Log = false;
        LocomotionTurnPresentation235Log = false;
        SkillGroupTurn237Log = false;
        DirectionAuthority237Log = false;
        InterruptFlow = false;
        ComboAirGate = false;
        CombatHit = false;
        ReactionDirection2206Log = false;
        AIBrain2207Log = false;
        EnemyPerception2208Log = false;
        SimulateLockOnLocomotion = false;
        IntentArbitration = false;
        HudBugLog = false;
        ActionTimelinePreviewLog = false;
        MirrorDiagLog = false;
        CombatSceneDrawSource = false;
        CombatContactBaseline = false;
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadOnPlay() => LoadFromEditorPrefs();
#endif
}
