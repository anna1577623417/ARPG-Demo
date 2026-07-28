/// <summary>
/// 220.6.1 C2：向通用 ReactionResolver 暴露受击方的 Profile。
/// 实体只提供配置，不直接参与 Reaction 解析。
/// </summary>
public interface IReactionProfileOwner
{
    ReactionProfileSO ReactionProfile { get; }
}
