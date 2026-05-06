/// <summary>
/// 效果接收者接口。陷阱/投射物/技能都通过该接口投递效果。
/// </summary>
public interface IEffectReceiver : ITagOwner
{
    IBuffStack BuffStack { get; }
    IReadOnlyStatSet Stats { get; }
    IResourcePool Resources { get; }
}
