/// <summary>Shared Player/Enemy presentation capability contract. Implementers expose only facts they actually own.</summary>
public interface IAnimationPresentationProfile243
{
    int EntityInstanceId { get; }
    AnimationRequestDomain SupportedDomains { get; }
    string VariantKey { get; }
    TransitionChannelCapabilities243 ChannelCapabilities { get; }
    bool TryGetPresentationFact(string key, out string value);
}
