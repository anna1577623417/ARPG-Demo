/// <summary>
/// Contact 判定 Provider 选择（单一真相，禁止 Volume+Trace 双跑）。
/// </summary>
public enum HitShapeMode : byte
{
    /// <summary>Volume：HitShapeSO Overlap ∪ Sweep（投射物/AOE/贴身盒）。</summary>
    Volume = 0,

    /// <summary>WeaponTrace：WeaponSocketSet 多点 prev→curr SphereCast（近战防穿模）。</summary>
    WeaponTrace = 1,
}
