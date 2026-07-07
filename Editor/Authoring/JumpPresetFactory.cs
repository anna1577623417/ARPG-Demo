#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 175.2 — Jump 系统资产工厂（菜单已移除；保留 API 供脚本调用）。
/// </summary>
public static class JumpPresetFactory
{
    const string Folder = "Assets/GameMain/4_Data/Jump/Presets";

    public static void CreateAll()
    {
        EnsureFolder(Folder);

        var baseConfig = CreateOrGetBaseConfig();

        CreateVariant("JumpVariant_Normal", JumpVariantId.Normal, 100, baseConfig, v =>
        {
            v.DisplayName = "Normal Jump";
            v.Condition = JumpTriggerCondition.Any;
            v.Condition.RequireGroundedTri = 1;
        });

        CreateVariant("JumpVariant_Run", JumpVariantId.Run, 80, baseConfig, v =>
        {
            v.DisplayName = "Run Jump";
            v.Condition = JumpTriggerCondition.Any;
            v.Condition.RequireGroundedTri = 1;
            v.Condition.RequireSprintHeld = true;
            v.Condition.MinHorizontalSpeed = 4f;
            v.UpwardMul = 1f;
            v.HorizontalBoost = 2.4f;  // ≈ 1.2× 当前水平
        });

        CreateVariant("JumpVariant_Long", JumpVariantId.Long, 50, baseConfig, v =>
        {
            v.DisplayName = "Long Jump";
            v.Condition = JumpTriggerCondition.Any;
            v.Condition.RequireGroundedTri = 1;
            v.Condition.RequireCrouchHeld = true;
            v.Condition.RequireSprintHeld = true;
            v.UpwardMul = 0.6f;
            v.HorizontalBoost = 8f;
            v.LockHorizontalDir = true;
        });

        CreateVariant("JumpVariant_Back", JumpVariantId.Back, 40, baseConfig, v =>
        {
            v.DisplayName = "Backflip";
            v.Condition = JumpTriggerCondition.Any;
            v.Condition.RequireGroundedTri = 1;
            v.Condition.RequirePivotWithinSec = 0.15f;
            v.UpwardMul = 1.5f;
            v.BackwardImpulse = new Vector3(0f, 0f, -6f);
            v.YawSpin = 180f;
        });

        CreateVariant("JumpVariant_Side", JumpVariantId.Side, 45, baseConfig, v =>
        {
            v.DisplayName = "Sideflip";
            v.Condition = JumpTriggerCondition.Any;
            v.Condition.RequireGroundedTri = 1;
            v.Condition.RequirePivotWithinSec = 0.15f;
            v.Condition.MinHorizontalSpeed = 2f;
            v.UpwardMul = 1.4f;
            v.YawSpin = 180f;
        });

        CreateVariant("JumpVariant_Triple", JumpVariantId.Triple, 20, baseConfig, v =>
        {
            v.DisplayName = "Triple Jump";
            v.Condition = JumpTriggerCondition.Any;
            v.Condition.RequireGroundedTri = 1;
            v.Condition.RequireComboIndex = 2;
            v.Condition.RequireLandWithinSec = 0.20f;
            v.UpwardMul = 1.8f;
        });

        CreateVariant("JumpVariant_GroundPound", JumpVariantId.GroundPound, 30, baseConfig, v =>
        {
            v.DisplayName = "Ground Pound";
            v.Condition = JumpTriggerCondition.Any;
            v.Condition.RequireGroundedTri = 0;
            v.Condition.RequireDownInput = true;
            v.UpwardMul = -2.5f;
            v.LockHorizontalDir = true;
        });

        CreateVariant("JumpVariant_DoubleAir", JumpVariantId.DoubleAir, 60, baseConfig, v =>
        {
            v.DisplayName = "Double Air Jump";
            v.Condition = JumpTriggerCondition.Any;
            v.Condition.RequireGroundedTri = 0;
            v.Condition.RequireRemainingJumpsAtLeast = 1;
            v.UpwardMul = 0.85f;
        });

        CreateVariant("JumpVariant_Wall", JumpVariantId.Wall, 35, baseConfig, v =>
        {
            v.DisplayName = "Wall Jump";
            v.Condition = JumpTriggerCondition.Any;
            v.Condition.RequireGroundedTri = 0;
            v.Condition.RequireWallContact = true;
            v.UpwardMul = 1.1f;
            v.BackwardImpulse = new Vector3(0f, 0f, -4f);
        });

        CreateVariant("JumpVariant_Dive", JumpVariantId.Dive, 38, baseConfig, v =>
        {
            v.DisplayName = "Dive";
            v.Condition = JumpTriggerCondition.Any;
            v.Condition.RequireGroundedTri = 0;
            v.Condition.RequireAttackPressed = true;
            v.UpwardMul = -0.4f;
            v.HorizontalBoost = 6f;
        });

        CreateModifierHopoo();
        CreateModifierWaxQuail();
        CreateLandingProfile();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Jump] All P0 assets created under {Folder}");
    }

    static JumpAbilityConfigSO CreateOrGetBaseConfig()
    {
        var path = $"{Folder}/JumpAbilityConfig_Default.asset";
        var existing = AssetDatabase.LoadAssetAtPath<JumpAbilityConfigSO>(path);
        if (existing != null) return existing;
        var cfg = ScriptableObject.CreateInstance<JumpAbilityConfigSO>();
        AssetDatabase.CreateAsset(cfg, path);
        return cfg;
    }

    static void CreateVariant(string name, JumpVariantId id, int priority,
        JumpAbilityConfigSO baseConfig, System.Action<JumpVariantSO> configure)
    {
        var path = $"{Folder}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<JumpVariantSO>(path) != null)
        {
            if (!EditorUtility.DisplayDialog("Variant 已存在",
                    $"{name} 已存在，覆盖？", "覆盖", "跳过"))
            {
                return;
            }
            AssetDatabase.DeleteAsset(path);
        }
        var v = ScriptableObject.CreateInstance<JumpVariantSO>();
        v.Id = id;
        v.Priority = priority;
        v.BaseConfig = baseConfig;
        configure(v);
        AssetDatabase.CreateAsset(v, path);
    }

    static void CreateModifierHopoo()
    {
        var path = $"{Folder}/JumpModifier_HopooFeather.asset";
        if (AssetDatabase.LoadAssetAtPath<JumpModifierSO>(path) != null) return;
        var m = ScriptableObject.CreateInstance<JumpModifierSO>();
        m.DisplayName = "Hopoo Feather";
        m.MaxStack = -1;
        m.AddOneAirJumpPerStack = true;
        AssetDatabase.CreateAsset(m, path);
    }

    static void CreateModifierWaxQuail()
    {
        var path = $"{Folder}/JumpModifier_WaxQuail.asset";
        if (AssetDatabase.LoadAssetAtPath<JumpModifierSO>(path) != null) return;
        var m = ScriptableObject.CreateInstance<JumpModifierSO>();
        m.DisplayName = "Wax Quail";
        m.MaxStack = -1;
        m.EnableDashJump = true;
        m.DashJumpHorizontalPerStack = 10f;
        m.FieldMods = new[]
        {
            new JumpFieldModifier { Field = JumpField.HorizontalBoost, Op = ModifierOp.Add, Value = 4f },
        };
        AssetDatabase.CreateAsset(m, path);
    }

    static void CreateLandingProfile()
    {
        var path = $"{Folder}/LandingTierProfile_Default.asset";
        if (AssetDatabase.LoadAssetAtPath<LandingTierProfileSO>(path) != null) return;
        var p = ScriptableObject.CreateInstance<LandingTierProfileSO>();
        p.SoftThreshold = 1.5f;
        p.NormalThreshold = 3f;
        p.HeavyThreshold = 5f;
        p.RollThreshold = 8f;
        p.RollHpLossRatio = 0.05f;
        p.Light.CameraShakeDeg = 0f;
        p.Soft.CameraShakeDeg = 0.5f;
        p.Normal.CameraShakeDeg = 1f;
        p.Heavy.CameraShakeDeg = 2f;
        p.Heavy.HitStopSec = 0.05f;
        p.Heavy.ControlLockSec = 0.20f;
        p.Roll.CameraShakeDeg = 3f;
        p.Roll.ControlLockSec = 0.40f;
        p.Pound.CameraShakeDeg = 5f;
        p.Pound.HitStopSec = 0.10f;
        p.Pound.ControlLockSec = 0.30f;
        AssetDatabase.CreateAsset(p, path);
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
}
#endif
