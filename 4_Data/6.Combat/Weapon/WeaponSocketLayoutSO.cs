using System;
using UnityEngine;

public enum WeaponSocketSlot : byte
{
    Base = 0,
    Middle = 1,
    Tip = 2,
    Custom = 3,
}

[Serializable]
public struct WeaponSocketBinding
{
    public WeaponSocketSlot Slot;
    public string Key;
    public string TransformPath;
    public Vector3 RootLocalPosition;
    public Quaternion RootLocalRotation;
    [Min(0.001f)] public float Radius;
}

/// <summary>由 Weapon Prefab Authoring 烘焙出的 Runtime-safe Socket 布局。</summary>
[CreateAssetMenu(menuName = "GameMain/Combat/Weapon Socket Layout", fileName = "WeaponSocketLayout_")]
public sealed class WeaponSocketLayoutSO : ScriptableObject
{
    public GameObject SourcePrefab;
    [Min(0)] public int BakeVersion;
    public WeaponSocketBinding[] Bindings = Array.Empty<WeaponSocketBinding>();
}
