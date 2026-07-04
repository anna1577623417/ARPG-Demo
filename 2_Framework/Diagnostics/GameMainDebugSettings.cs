using UnityEngine;

/// <summary>
/// 全局 Debug 开关 — 由 Tools/GameMain/Debug Settings 窗口管理，EditorPrefs 持久化。
/// Player / StateManager 不再暴露 Inspector Toggle；新 Probe 在此注册。
/// </summary>
public static class GameMainDebugSettings
{
    const string Prefix = "GameMain.Debug.";

    // ─── Skill / Route ───
    public static bool SkillRouteGraph { get; set; }
    public static bool SkillRouteDodge4 { get; set; }
    public static bool SkillRouteRoll4 { get; set; }
    public static bool SkillAbility { get; set; }

    // ─── Locomotion / Turn / Stop ───
    public static bool Locomotion { get; set; }
    public static bool LocomotionTrace { get; set; }
    public static bool RotationGate { get; set; }
    public static bool Stop { get; set; }
    public static bool TurnSubState { get; set; }

    // ─── Combat / Interrupt ───
    public static bool InterruptFlow { get; set; }
    public static bool ComboAirGate { get; set; }

    // ─── Simulation（非 Log，Play 验收用）───
    public static bool SimulateLockOnLocomotion { get; set; }

    // ─── Intent 仲裁 ───
    public static bool IntentArbitration { get; set; }

#if UNITY_EDITOR
    public static void LoadFromEditorPrefs()
    {
        SkillRouteGraph = EditorPrefsGetBool(nameof(SkillRouteGraph));
        SkillRouteDodge4 = EditorPrefsGetBool(nameof(SkillRouteDodge4));
        SkillRouteRoll4 = EditorPrefsGetBool(nameof(SkillRouteRoll4));
        SkillAbility = EditorPrefsGetBool(nameof(SkillAbility));
        Locomotion = EditorPrefsGetBool(nameof(Locomotion));
        LocomotionTrace = EditorPrefsGetBool(nameof(LocomotionTrace));
        RotationGate = EditorPrefsGetBool(nameof(RotationGate));
        Stop = EditorPrefsGetBool(nameof(Stop));
        TurnSubState = EditorPrefsGetBool(nameof(TurnSubState));
        InterruptFlow = EditorPrefsGetBool(nameof(InterruptFlow));
        ComboAirGate = EditorPrefsGetBool(nameof(ComboAirGate));
        SimulateLockOnLocomotion = EditorPrefsGetBool(nameof(SimulateLockOnLocomotion));
        IntentArbitration = EditorPrefsGetBool(nameof(IntentArbitration));
    }

    public static void SaveToEditorPrefs()
    {
        EditorPrefsSetBool(nameof(SkillRouteGraph), SkillRouteGraph);
        EditorPrefsSetBool(nameof(SkillRouteDodge4), SkillRouteDodge4);
        EditorPrefsSetBool(nameof(SkillRouteRoll4), SkillRouteRoll4);
        EditorPrefsSetBool(nameof(SkillAbility), SkillAbility);
        EditorPrefsSetBool(nameof(Locomotion), Locomotion);
        EditorPrefsSetBool(nameof(LocomotionTrace), LocomotionTrace);
        EditorPrefsSetBool(nameof(RotationGate), RotationGate);
        EditorPrefsSetBool(nameof(Stop), Stop);
        EditorPrefsSetBool(nameof(TurnSubState), TurnSubState);
        EditorPrefsSetBool(nameof(InterruptFlow), InterruptFlow);
        EditorPrefsSetBool(nameof(ComboAirGate), ComboAirGate);
        EditorPrefsSetBool(nameof(SimulateLockOnLocomotion), SimulateLockOnLocomotion);
        EditorPrefsSetBool(nameof(IntentArbitration), IntentArbitration);
    }

    public static void ResetAll()
    {
        SkillRouteGraph = false;
        SkillRouteDodge4 = false;
        SkillRouteRoll4 = false;
        SkillAbility = false;
        Locomotion = false;
        LocomotionTrace = false;
        RotationGate = false;
        Stop = false;
        TurnSubState = false;
        InterruptFlow = false;
        ComboAirGate = false;
        SimulateLockOnLocomotion = false;
        IntentArbitration = false;
        SaveToEditorPrefs();
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
        Locomotion = false;
        LocomotionTrace = false;
        RotationGate = false;
        Stop = false;
        TurnSubState = false;
        InterruptFlow = false;
        ComboAirGate = false;
        SimulateLockOnLocomotion = false;
        IntentArbitration = false;
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadOnPlay()
    {
        LoadFromEditorPrefs();
    }
#endif
}
