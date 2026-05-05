using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 数值系统启动器：集中注入 DamagePipeline 默认 Stage 链，并提供可选替换入口。
/// </summary>
public static class GameMathBootstrap
{
    static bool s_initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitializeOnLoad()
    {
        EnsureInitialized();
    }

    public static void EnsureInitialized()
    {
        if (s_initialized)
        {
            return;
        }

        InstallDefaultDamageStages();
        s_initialized = true;
    }

    /// <summary>安装项目默认的伤害计算阶段链。</summary>
    public static void InstallDefaultDamageStages()
    {
        DamagePipeline.ReplaceDefaultStages(BuildDefaultDamageStages());
    }

    /// <summary>可选替换入口：外部系统可注入任意 Stage 链（例如关卡、模式、角色专属）。</summary>
    public static void ReplaceDamageStages(IEnumerable<IDamageStage> stages)
    {
        DamagePipeline.ReplaceDefaultStages(stages);
    }

    static IEnumerable<IDamageStage> BuildDefaultDamageStages()
    {
        yield return new BaseDamageStage();
        yield return new DefenseReductionStage();
        yield return new CritStage();
        yield return new FinalClampStage();
    }
}
