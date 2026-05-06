/// <summary>
/// 标签持有者接口。用于阵营过滤与状态查询。
/// </summary>
public interface ITagOwner
{
    GameplayTagContainer Tags { get; }
    bool HasTag(GameplayTagMask mask);
}
