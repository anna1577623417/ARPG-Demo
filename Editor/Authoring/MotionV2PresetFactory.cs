#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 174.2 — V2 Preset 工厂（菜单已移除；保留 API 供脚本调用）。
/// </summary>
public static class MotionV2PresetFactory
{
    const string PresetsFolder = "Assets/GameMain/4_Data/3.Motion/Presets";

    public static void CreateStinger() => CreatePreset(
        "Preset_Stinger_DMC", ConfigureStinger,
        "DMC Stinger 冲刺斩：Z 0→4 / 中段锁朝向+锁移动 / 后段恢复输入");

    public static void CreateRisingoryu() => CreatePreset(
        "Preset_Risingoryu_ZZZ", ConfigureRisingoryu,
        "绝区零升龙：Y Apex Hold / Gravity 0→0→3 强制下坠 / Facing 0.3 弱跟随");

    public static void CreateSpinSlash() => CreatePreset(
        "Preset_SpinSlash_Elden", ConfigureSpinSlash,
        "法环回旋斩：Yaw 0→180° / 蓄力段锁移动 / Anim 节奏 S 形");

    public static void CreateLockedDash() => CreatePreset(
        "Preset_LockedDash_Sekiro", ConfigureLockedDash,
        "只狼锁敌追踪斩：Z 0→6 / Track 1→0.3 / Space=LockTarget");

    public static void CreateSpartanRage() => CreatePreset(
        "Preset_Spartan_Rage", ConfigureSpartanRage,
        "战神 Rage 终结技：Y 上挑 / GravW Bell / Hitstop Spike");

    public static void CreateRoll() => CreatePreset(
        "Preset_Roll_Dodge", ConfigureRoll,
        "通用翻滚：Z 0→4 / Gravity Suspend / 中段闪避无敌帧");

    public static void CreateBackstep() => CreatePreset(
        "Preset_Backstep", ConfigureBackstep,
        "后撤步：Z 0→-2 / 全锁输入");

    public static void CreateAirCombo() => CreatePreset(
        "Preset_AirCombo_Loop", ConfigureAirCombo,
        "空连段：Y 微升 / GravW 0.3 / FacingW 0.5 半跟随");

    public static void CreateGroundSlam() => CreatePreset(
        "Preset_GroundSlam", ConfigureGroundSlam,
        "下劈：GroundTargeted + GravW 2 强制下坠");

    public static void CreateChargeHold() => CreatePreset(
        "Preset_Charge_Hold", ConfigureChargeHold,
        "蓄力：原地 / AnimSpeed 0.5x / FacingW=1 允许调整朝向");

    public static void CreateAll()
    {
        CreateStinger();
        CreateRisingoryu();
        CreateSpinSlash();
        CreateLockedDash();
        CreateSpartanRage();
        CreateRoll();
        CreateBackstep();
        CreateAirCombo();
        CreateGroundSlam();
        CreateChargeHold();
    }

    // ─── 三大范本配置 ──────────────────────────────────────────

    static void ConfigureStinger(MotionProfileSO p)
    {
        // Z 急速 0→4
        p.AxisCurves.ZCurve = MotionCurveLibrary.Make(MotionCurveLibrary.Segment.EaseOutCubic);
        p.AxisCurves.ZScale = 4f;
        p.MotionSpace = MotionSpace.CharacterForward;

        // 中段（0.10~0.35）锁朝向 + 锁移动；后段渐恢复
        p.V2FacingInputWeight = BuildPiecewise01(
            (0f, 1f), (0.10f, 0f), (0.35f, 0f), (1f, 1f));
        p.V2MoveInputWeight = BuildPiecewise01(
            (0f, 0f), (0.35f, 0f), (1f, 1f));

        // Anim Speed：前摇 1.5x 压缩 / 后摇 0.7x 拉长
        p.AnimSpeedMode = AnimSpeedMode.Curve;
        p.SpeedOverTime = BuildPiecewise(
            (0f, 1.5f), (0.10f, 1.5f), (0.35f, 1f), (1f, 0.7f));
    }

    static void ConfigureRisingoryu(MotionProfileSO p)
    {
        // Y Apex Hold（升 + 持平 + 落）
        p.AxisCurves.YCurve = MotionCurveLibrary.Make(MotionCurveLibrary.Segment.ApexHold);
        p.AxisCurves.YScale = 5f;
        p.YMotion = YMotionMode.Curve;
        p.V2YStrategy = YStrategyV2.HoverHold;

        // Gravity Weight：0→0→3 强制下坠
        p.V2GravityWeightMode = GravityWeightMode.Curve;
        p.V2GravityWeight = BuildPiecewise(
            (0f, 0f), (0.20f, 0f), (0.60f, 0f), (1f, 3f));

        // Facing 弱跟随
        p.V2FacingInputWeight = AnimationCurve.Constant(0f, 1f, 0.3f);
        p.V2MoveInputWeight = AnimationCurve.Constant(0f, 1f, 0f);
    }

    static void ConfigureSpinSlash(MotionProfileSO p)
    {
        // 原地无位移
        p.AxisCurves.XScale = 0f;
        p.AxisCurves.YScale = 0f;
        p.AxisCurves.ZScale = 0f;

        // 210.5 Action Yaw：0→180 S 形
        p.YawPolicy = YawPolicyMode.Curve;
        p.YawStartDegrees = 0f;
        p.YawEndDegrees = 180f;
        p.YawBlendOverTime = BuildPiecewise(
            (0f, 0f), (0.30f, 0f), (0.70f, 1f), (1f, 1f));

        // Anim Speed 节奏：0.5x 蓄力 / 1.2x 收尾
        p.AnimSpeedMode = AnimSpeedMode.Curve;
        p.SpeedOverTime = BuildPiecewise(
            (0f, 0.5f), (0.30f, 0.5f), (0.70f, 1.2f), (1f, 1.2f));

        // 全程锁移动 / 全程锁朝向
        p.V2MoveInputWeight = AnimationCurve.Constant(0f, 1f, 0f);
        p.V2FacingInputWeight = AnimationCurve.Constant(0f, 1f, 0f);
    }

    static void ConfigureLockedDash(MotionProfileSO p)
    {
        p.AxisCurves.ZCurve = MotionCurveLibrary.Make(MotionCurveLibrary.Segment.EaseOutCubic);
        p.AxisCurves.ZScale = 6f;
        p.MotionSpace = MotionSpace.LockTarget;
        p.V2TargetTrackingWeight = BuildPiecewise01(
            (0f, 1f), (0.60f, 1f), (1f, 0.3f));
        p.V2MoveInputWeight = AnimationCurve.Constant(0f, 1f, 0f);
        p.V2FacingInputWeight = AnimationCurve.Constant(0f, 1f, 0f);
    }

    static void ConfigureSpartanRage(MotionProfileSO p)
    {
        // 抓取期 (0~0.2) 无位移；上挑 (0.2~0.5) Y+3；砸落 (0.5~1) Y -3
        p.AxisCurves.YCurve = BuildPiecewise(
            (0f, 0f), (0.20f, 0f), (0.50f, 1f), (1f, -1f));
        p.AxisCurves.YScale = 3f;
        p.YMotion = YMotionMode.Curve;

        // Gravity Bell：砸落瞬间高重力
        p.V2GravityWeightMode = GravityWeightMode.Curve;
        p.V2GravityWeight = MotionCurveLibrary.Make(MotionCurveLibrary.Segment.Bell);

        // Hitstop Spike：命中瞬间放大震屏
        p.V2HitstopMultiplier = MotionCurveLibrary.Make(MotionCurveLibrary.Segment.Spike);
        // 重塑为 1→2 峰值（默认 Spike 0→1，乘以倍率）
        for (var i = 0; i < p.V2HitstopMultiplier.length; i++)
        {
            var k = p.V2HitstopMultiplier[i];
            k.value = 1f + k.value;
            p.V2HitstopMultiplier.MoveKey(i, k);
        }

        p.V2FacingInputWeight = AnimationCurve.Constant(0f, 1f, 0f);
        p.V2MoveInputWeight = AnimationCurve.Constant(0f, 1f, 0f);
    }

    static void ConfigureRoll(MotionProfileSO p)
    {
        p.AxisCurves.ZCurve = MotionCurveLibrary.Make(MotionCurveLibrary.Segment.EaseOutCubic);
        p.AxisCurves.ZScale = 4f;
        p.MotionSpace = MotionSpace.CameraForward;
        p.Gravity = GravityMode.SuspendGravity;  // 翻滚不浮空（V1 行为）

        // 中段（0.2~0.5）"无敌窗口"由 InverseSpike 充当占位 —— 实际无敌帧由 Combat 层管控
        p.V2GravityWeightMode = GravityWeightMode.Curve;
        p.V2GravityWeight = AnimationCurve.Constant(0f, 1f, 0f);

        // 全程允许少量朝向调整 = 0.3
        p.V2FacingInputWeight = AnimationCurve.Constant(0f, 1f, 0.3f);
        p.V2MoveInputWeight = AnimationCurve.Constant(0f, 1f, 0f);
    }

    static void ConfigureBackstep(MotionProfileSO p)
    {
        p.AxisCurves.ZCurve = MotionCurveLibrary.Make(MotionCurveLibrary.Segment.EaseOutCubic);
        p.AxisCurves.ZScale = -2f;
        p.MotionSpace = MotionSpace.CharacterForward;
        p.V2FacingInputWeight = AnimationCurve.Constant(0f, 1f, 0f);
        p.V2MoveInputWeight = AnimationCurve.Constant(0f, 1f, 0f);
    }

    static void ConfigureAirCombo(MotionProfileSO p)
    {
        // Y 微升 0→0.5
        p.AxisCurves.YCurve = MotionCurveLibrary.Make(MotionCurveLibrary.Segment.ApexHold);
        p.AxisCurves.YScale = 0.5f;
        p.YMotion = YMotionMode.Curve;

        p.V2GravityWeightMode = GravityWeightMode.Curve;
        p.V2GravityWeight = AnimationCurve.Constant(0f, 1f, 0.3f);

        // 空中可半跟随调整朝向
        p.V2FacingInputWeight = AnimationCurve.Constant(0f, 1f, 0.5f);
        p.V2MoveInputWeight = AnimationCurve.Constant(0f, 1f, 0.2f);
    }

    static void ConfigureGroundSlam(MotionProfileSO p)
    {
        p.YMotion = YMotionMode.GroundTargeted;
        p.LandingOffset = 0f;
        p.LandingDetectionRadius = 20f;
        p.LandingCurve = MotionCurveLibrary.Make(MotionCurveLibrary.Segment.EaseInCubic);

        p.V2GravityWeightMode = GravityWeightMode.Curve;
        p.V2GravityWeight = AnimationCurve.Constant(0f, 1f, 2f);

        p.V2FacingInputWeight = AnimationCurve.Constant(0f, 1f, 0f);
        p.V2MoveInputWeight = AnimationCurve.Constant(0f, 1f, 0f);
    }

    static void ConfigureChargeHold(MotionProfileSO p)
    {
        p.AxisCurves.XScale = 0f;
        p.AxisCurves.YScale = 0f;
        p.AxisCurves.ZScale = 0f;
        p.AnimSpeedMode = AnimSpeedMode.Curve;
        p.SpeedOverTime = AnimationCurve.Constant(0f, 1f, 0.5f);
        p.V2FacingInputWeight = AnimationCurve.Constant(0f, 1f, 1f);
        p.V2MoveInputWeight = AnimationCurve.Constant(0f, 1f, 0f);
    }

    // ─── Utility ──────────────────────────────────────────────

    static void CreatePreset(string assetName, System.Action<MotionProfileSO> configure, string description)
    {
        if (!AssetDatabase.IsValidFolder(PresetsFolder))
        {
            EnsureFolder(PresetsFolder);
        }

        var assetPath = $"{PresetsFolder}/{assetName}.asset";
        if (AssetDatabase.LoadAssetAtPath<MotionProfileSO>(assetPath) != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "Preset 已存在",
                    $"{assetPath} 已存在，是否覆盖？",
                    "覆盖", "取消"))
            {
                return;
            }
            AssetDatabase.DeleteAsset(assetPath);
        }

        var profile = ScriptableObject.CreateInstance<MotionProfileSO>();
        profile.ApplyDefaultZeroAxisDisplacement();
        configure(profile);
        // 241.2：V2 预设已经明确写入三权字段，不能继续落入旧 LegacyMapping。
        profile.SetYAxisV2Configured(true);
        AssetDatabase.CreateAsset(profile, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(profile);
        Debug.Log($"[Motion V2] Created {assetPath} — {description}");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        var leaf = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    static AnimationCurve BuildPiecewise(params (float t, float v)[] points)
    {
        var c = new AnimationCurve();
        for (var i = 0; i < points.Length; i++)
        {
            c.AddKey(new Keyframe(points[i].t, points[i].v, 0f, 0f));
        }
        return c;
    }

    static AnimationCurve BuildPiecewise01(params (float t, float v)[] points)
    {
        var c = BuildPiecewise(points);
        for (var i = 0; i < c.length; i++)
        {
            var k = c[i];
            k.value = Mathf.Clamp01(k.value);
            c.MoveKey(i, k);
        }
        return c;
    }
}
#endif
