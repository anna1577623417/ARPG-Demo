using UnityEngine;

/// <summary>
/// 输入路由：记录当前在场角色的 <see cref="PlayerController"/> 与根节点，供 HUD、教程、仲裁等查询。
/// 单机下后台队员通过 <see cref="GameObject.SetActive(false)"/> 停掉其 Update；路由侧保留权威「当前操作角色」引用。
/// </summary>
public interface IInputRouter
{
    PlayerController ActivePlayerController { get; }

    Transform ActivePlayerRoot { get; }

    void SetActivePlayer(PlayerController controller, Transform root);
}
