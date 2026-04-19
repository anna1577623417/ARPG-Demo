using UnityEngine;

/// <summary>
/// 后台队员运行期数据（非 Mono）：当前跟踪槽位与是否已实例化；技能冷却、属性快照等可后续在同结构上扩展。
/// </summary>
public sealed class TeamMemberRuntimeStub
{
    public int SlotIndex { get; }

    /// <summary>是否已在场景中实例化过根物体（可能当前为 <see cref="GameObject.SetActive(false)"/>）。</summary>
    public bool HasSpawnedInstance { get; set; }

    public TeamMemberRuntimeStub(int slotIndex)
    {
        SlotIndex = slotIndex;
    }
}
