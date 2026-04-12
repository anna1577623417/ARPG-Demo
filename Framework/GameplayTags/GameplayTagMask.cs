using System.Runtime.CompilerServices;

/// <summary>
/// 64 位游戏标签掩码包装。
/// 设计原因：
/// - 用 ulong 位运算做 O(1) 判定，避免字符串哈希与堆分配。
/// - 与 <see cref="StateTag"/> 组合使用；物理/阶段/能力分区由 StateTag 的位移约定保证。
/// </summary>
public struct GameplayTagMask
{
    public ulong Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAll(ulong bits) => (Value & bits) == bits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAny(ulong bits) => (Value & bits) != 0UL;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(in GameplayTagMask other) => Value |= other.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ulong bits) => Value |= bits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove(ulong bits) => Value &= ~bits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(ulong bits) => Value = bits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => Value = 0UL;
}
