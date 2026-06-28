#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 177.1 W0+W2 — Stance 资产工厂菜单。
/// <para>一键产 StanceDefinition_Normal + StanceDefinition_Crouch + LocomotionProfile_Crouch 模板。</para>
/// <para>LocomotionProfile_Crouch 的 Bindings 由策划在 Inspector 拖入 17 份 Crouch_* Action（资产已就绪）。</para>
/// </summary>
public static class StancePresetFactory
{
    const string Folder = "Assets/GameMain/4_Data/Stance/Presets";
    const string CrouchActionFolder = "Assets/GameMain/4_Data/2.Actions/2.1CombatActionLibrary/2.GhostSamurai_Action/Crounch";

    [MenuItem("GameMain/Stance/Create All P0 Assets")]
    public static void CreateAll()
    {
        EnsureFolder(Folder);
        CreateNormalStance();
        CreateCrouchStance();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Stance] All P0 assets created under {Folder}");
    }

    [MenuItem("GameMain/Stance/Create Stance · Normal")]
    public static void CreateNormalStance()
    {
        var path = $"{Folder}/StanceDefinition_Normal.asset";
        if (AssetDatabase.LoadAssetAtPath<StanceDefinitionSO>(path) != null) return;
        var s = ScriptableObject.CreateInstance<StanceDefinitionSO>();
        s.Id = StanceId.Normal;
        s.DisplayName = "Normal";
        s.HeightScale = 1f;
        s.WalkMultiplier = 1f;
        s.RunMultiplier = 1f;
        s.RotationSpeedDegPerSec = 540f;
        s.JumpContextStanceByte = (byte)StanceId.Normal;
        AssetDatabase.CreateAsset(s, path);
    }

    [MenuItem("GameMain/Stance/Create Stance · Crouch")]
    public static void CreateCrouchStance()
    {
        EnsureFolder(Folder);
        var stancePath = $"{Folder}/StanceDefinition_Crouch.asset";
        var profilePath = $"{Folder}/LocomotionProfile_Crouch.asset";

        // LocomotionProfile_Crouch（空 Bindings，策划在 Inspector 填）
        if (AssetDatabase.LoadAssetAtPath<LocomotionProfile>(profilePath) == null)
        {
            var profile = ScriptableObject.CreateInstance<LocomotionProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }
        var crouchProfile = AssetDatabase.LoadAssetAtPath<LocomotionProfile>(profilePath);

        // StanceDefinition_Crouch
        if (AssetDatabase.LoadAssetAtPath<StanceDefinitionSO>(stancePath) == null)
        {
            var s = ScriptableObject.CreateInstance<StanceDefinitionSO>();
            s.Id = StanceId.Crouch;
            s.DisplayName = "Crouch";
            s.LocomotionProfile = crouchProfile;
            s.HeightScale = 0.65f;
            s.WalkMultiplier = 0.55f;
            s.RunMultiplier = 0.85f;
            s.RotationSpeedDegPerSec = 360f;
            s.RequireHeadClearanceOnExit = true;
            s.HeadClearanceProbeHeight = 1.8f;
            s.HeadClearanceProbeRadius = 0.30f;

            // 175.2 兼容：Crouch 不屏蔽 Jump（Crouch+Sprint+Space → LongJump Variant）
            s.BlockedAbilityNames = new[] { "Dodge.Roll" };
            s.GrantedAbilityNames = new[] { "Stealth.Step" };
            s.JumpContextStanceByte = (byte)StanceId.Crouch;

            // 自动拖入 APose2Crouch / Crouch2APose
            s.EnterAction = AssetDatabase.LoadAssetAtPath<ActionDataSO>(
                $"{CrouchActionFolder}/GhostSamurai_APose2Crouch_ActionData.asset");
            s.ExitAction = AssetDatabase.LoadAssetAtPath<ActionDataSO>(
                $"{CrouchActionFolder}/GhostSamurai_Crouch2APose_ActionData.asset");

            AssetDatabase.CreateAsset(s, stancePath);
        }
    }

    /// <summary>177.1 W2 — 列出策划需在 LocomotionProfile_Crouch 中绑定的 17 份 Crouch 资产。</summary>
    [MenuItem("GameMain/Stance/Crouch · Print Asset Checklist")]
    public static void PrintCrouchAssetChecklist()
    {
        var paths = new[]
        {
            "GhostSamurai_APose2Crouch_ActionData.asset",
            "GhostSamurai_Crouch2APose_ActionData.asset",
            "GhostSamurai_Crouch_Loop_ActionData.asset",
            "GhostSamurai_Crouch_Run_Start_ActionData.asset",
            "GhostSamurai_Crouch_Run_Loop_ActionData.asset",
            "GhostSamurai_Crouch_Run_End_ActionData.asset",
            "GhostSamurai_Crouch_Walk_F_Loop_ActionData.asset",
            "GhostSamurai_Crouch_Walk_F_End_ActionData.asset",
            "GhostSamurai_Crouch_Walk_B_ActionData.asset",
            "GhostSamurai_Crouch_Walk_L_ActionData.asset",
            "GhostSamurai_Crouch_Walk_R_ActionData.asset",
            "GhostSamurai_Crouch_Walk_FL_ActionData.asset",
            "GhostSamurai_Crouch_Walk_FR_ActionData.asset",
            "GhostSamurai_Crouch_Walk_BL_ActionData.asset",
            "GhostSamurai_Crouch_Walk_BR_ActionData.asset",
        };

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[Stance · Crouch Asset Checklist]");
        sb.AppendLine($"Folder: {CrouchActionFolder}");
        sb.AppendLine();
        for (var i = 0; i < paths.Length; i++)
        {
            var full = $"{CrouchActionFolder}/{paths[i]}";
            var ok = AssetDatabase.LoadAssetAtPath<ActionDataSO>(full) != null;
            sb.AppendLine($"  [{(ok ? "✓" : "✗")}] {paths[i]}");
        }
        sb.AppendLine();
        sb.AppendLine("→ 在 LocomotionProfile_Crouch.asset 的 Bindings 列表里逐条绑定上述 Action。");
        Debug.Log(sb.ToString());
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
