using UnityEngine;

/// <summary>
/// 战斗实体根接口。用于统一接入测试组件、玩家与未来怪物实体。
/// </summary>
public interface IEntity : ITagOwner
{
    Transform Transform { get; }
    IReadOnlyStatSet Stats { get; }
    IResourcePool Resources { get; }
    bool IsAlive { get; }
}
