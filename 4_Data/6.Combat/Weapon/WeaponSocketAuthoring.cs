using System;
using UnityEngine;

[Serializable]
public struct WeaponSocketAuthoringPoint
{
    public WeaponSocketSlot Slot;
    public string Key;
    public Transform Transform;
    [Min(0.001f)] public float Radius;
}

/// <summary>
/// 武器 Prefab 上的作者组件。Transform 是空间真相；Runtime 消费 Bake 后的 Layout。
/// </summary>
[DisallowMultipleComponent]
public sealed class WeaponSocketAuthoring : MonoBehaviour
{
    public WeaponSocketLayoutSO BakeTarget;
    public WeaponSocketAuthoringPoint[] Points = Array.Empty<WeaponSocketAuthoringPoint>();
}
