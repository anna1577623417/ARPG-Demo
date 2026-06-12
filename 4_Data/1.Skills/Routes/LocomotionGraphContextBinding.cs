using System;
using UnityEngine;

/// <summary>
/// 157.2/157.3 — Locomotion 相位 Action 与 Combat Flow Graph 来源节点的绑定。
/// 运行时由 Airborne/Locomotion 支柱写入 <see cref="Player.GraphContextAction"/>，供 Graph Resolve 锚点推导。
/// </summary>
[Serializable]
public struct LocomotionGraphContextBinding
{
    [Tooltip("起跳相位（JumpStart）；Graph 中 SourceOnly 来源节点。")]
    public ActionDataSO JumpStart;

    [Tooltip("滞空循环相位（JumpLoop）；空中派生（如 JumpLoop→DownSlash）的 Graph 锚点。")]
    public ActionDataSO JumpLoop;

    [Tooltip("落地相位（JumpLand）；可选：着地时切 Action 播后摇并允许 Locomotion 打断。")]
    public ActionDataSO JumpLand;

    [Header("164.1 L11 — 多级降落（设施就位，EnableTieredLanding=false 时不读）")]
    public ActionDataSO JumpLandLight;
    public ActionDataSO JumpLandHeavy;
    public ActionDataSO JumpLandRoll;

    public bool HasAny =>
        JumpStart != null || JumpLoop != null || JumpLand != null
        || JumpLandLight != null || JumpLandHeavy != null || JumpLandRoll != null;
}
