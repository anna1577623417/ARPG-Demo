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

    // ─── Combat / Interrupt ───
    public static bool InterruptFlow { get; set; }
    public static bool ComboAirGate { get; set; }

    /// <summary>214.3 — CombatObject Spawn / Overlap / Damage 诊断。</summary>
    public static bool CombatHit { get; set; }

    // ─── Intent / Simulation ───
    public static bool IntentArbitration { get; set; }
    public static bool SimulateLockOnLocomotion { get; set; }

    // ─── HUD ───
    public static bool HudBugLog { get; set; }

    // ─── Editor Authoring ───
    public static bool ActionTimelinePreviewLog { get; set; }
    public static bool MirrorDiagLog { get; set; }

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
        InterruptFlow = EditorPrefsGetBool(nameof(InterruptFlow));
        ComboAirGate = EditorPrefsGetBool(nameof(ComboAirGate));
        CombatHit = EditorPrefsGetBool(nameof(CombatHit));
        IntentArbitration = EditorPrefsGetBool(nameof(IntentArbitration));
        SimulateLockOnLocomotion = EditorPrefsGetBool(nameof(SimulateLockOnLocomotion));
        HudBugLog = EditorPrefsGetBool(nameof(HudBugLog));
        ActionTimelinePreviewLog = EditorPrefsGetBool(nameof(ActionTimelinePreviewLog))
            || UnityEditor.EditorPrefs.GetBool("Core-Drive/ActionTimelinePreviewProbe/EnableLog", false);
        MirrorDiagLog = EditorPrefsGetBool(nameof(MirrorDiagLog))
            || UnityEditor.EditorPrefs.GetBool("Core-Drive/MirroredClipSampler/EnableDiag", false);
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
        EditorPrefsSetBool(nameof(InterruptFlow), InterruptFlow);
        EditorPrefsSetBool(nameof(ComboAirGate), ComboAirGate);
        EditorPrefsSetBool(nameof(CombatHit), CombatHit);
        EditorPrefsSetBool(nameof(IntentArbitration), IntentArbitration);
        EditorPrefsSetBool(nameof(SimulateLockOnLocomotion), SimulateLockOnLocomotion);
        EditorPrefsSetBool(nameof(HudBugLog), HudBugLog);
        EditorPrefsSetBool(nameof(ActionTimelinePreviewLog), ActionTimelinePreviewLog);
        EditorPrefsSetBool(nameof(MirrorDiagLog), MirrorDiagLog);
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
        InterruptFlow = true;
        ComboAirGate = true;
        CombatHit = true;
        IntentArbitration = true;
        HudBugLog = true;
        ActionTimelinePreviewLog = true;
        MirrorDiagLog = true;
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
        InterruptFlow = false;
        ComboAirGate = false;
        CombatHit = false;
        SimulateLockOnLocomotion = false;
        IntentArbitration = false;
        HudBugLog = false;
        ActionTimelinePreviewLog = false;
        MirrorDiagLog = false;
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
        InterruptFlow = false;
        ComboAirGate = false;
        CombatHit = false;
        SimulateLockOnLocomotion = false;
        IntentArbitration = false;
        HudBugLog = false;
        ActionTimelinePreviewLog = false;
        MirrorDiagLog = false;
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadOnPlay() => LoadFromEditorPrefs();
#endif
}
