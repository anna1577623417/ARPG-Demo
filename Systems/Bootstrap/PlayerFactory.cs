using UnityEngine;

/// <summary>
/// 运行时生成玩家预制体时在「创建点」注入移动上下文（推模式），避免依赖场景扫描或 Service Locator。
/// </summary>
public sealed class PlayerFactory
{
    private readonly IGameModeMovementContext _movementContext;

    public PlayerFactory(IGameModeMovementContext movementContext)
    {
        _movementContext = movementContext;
    }

    /// <summary>实例化并对接移动依赖；返回根物体。</summary>
    public GameObject InstantiatePlayer(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null)
    {
        var root = Object.Instantiate(prefab, position, rotation, parent);
        InjectMovementIntoHierarchy(root);
        return root;
    }

    /// <summary>对已存在的玩家根物体补注入（例如对象池取出后重用）。</summary>
    public void InjectMovementIntoHierarchy(GameObject playerRoot)
    {
        if (playerRoot == null || _movementContext == null)
        {
            return;
        }

        var controllers = playerRoot.GetComponentsInChildren<PlayerController>(true);
        for (var i = 0; i < controllers.Length; i++)
        {
            controllers[i].InjectMovementContext(_movementContext);
        }
    }
}
