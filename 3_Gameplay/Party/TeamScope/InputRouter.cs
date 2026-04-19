using UnityEngine;

/// <summary>
/// 默认输入路由实现：记录当前在场玩家，供 HUD、教程、后续网络/切人仲裁查询。
/// </summary>
[AddComponentMenu("GameMain/Party/Input Router")]
public sealed class InputRouter : MonoBehaviour, IInputRouter
{
    public PlayerController ActivePlayerController { get; private set; }

    public Transform ActivePlayerRoot { get; private set; }

    public void SetActivePlayer(PlayerController controller, Transform root)
    {
        ActivePlayerController = controller;
        ActivePlayerRoot = root;
    }
}
