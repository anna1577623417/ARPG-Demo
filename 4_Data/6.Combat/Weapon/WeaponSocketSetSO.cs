using System;
using UnityEngine;

/// <summary>
/// 216.3 M4 — 武器上单个检测点（绑 Humanoid 骨骼 + 局部偏移 + 扫掠半径）。
/// </summary>
[Serializable]
public struct WeaponSocketDef
{
    [Tooltip("调试名（tip / mid / base…），进 [Trace] SOCKET 日志。")]
    public string DebugName;

    [Tooltip("绑定的 Humanoid 骨骼。")]
    public HumanBodyBones Bone;

    [Tooltip("相对骨骼的局部偏移。")]
    public Vector3 LocalOffset;

    [Tooltip("该点扫掠半径（M4 L2 CapsuleCast 用；L1 仅采样）。")]
    [Min(0.01f)]
    public float Radius;
}

/// <summary>
/// 216.3 M4 — 武器检测点集合（短剑 3 点 / 长枪 8 点…仅数据不同，Provider 代码一致）。
/// </summary>
[CreateAssetMenu(
    menuName = "GameMain/Combat/Weapon Socket Set",
    fileName = "WeaponSocketSet_")]
public sealed class WeaponSocketSetSO : ScriptableObject
{
    [Tooltip("检测点列表（沿武器从根到尖）。")]
    public WeaponSocketDef[] Sockets = Array.Empty<WeaponSocketDef>();

    public int Count => Sockets != null ? Sockets.Length : 0;
}
