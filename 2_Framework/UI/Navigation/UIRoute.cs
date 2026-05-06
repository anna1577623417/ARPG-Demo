/// <summary>可选导航层使用的路由载荷；Payload 应由处理器向下转型为具体 Props。</summary>
public struct UIRoute
{
    public string ScreenId;
    public object Payload;
    public string Anchor;
    public string DeepLinkPath;
}
