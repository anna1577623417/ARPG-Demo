using UnityEngine;

/// <summary>
/// 场景组合根：本场景内服务的唯一注册点，并在启动时向下游显式注入依赖。
/// 请在 Inspector 中指派「需要被注入」的组件引用；避免在业务脚本里使用全局 <c>Find*</c> 解析控制流依赖。
/// </summary>
[DefaultExecutionOrder(-120)]
[AddComponentMenu("GameMain/Systems/System Root")]
public sealed class SystemRoot : MonoBehaviour
{
    [Header("Services (scene singletons)")]
    [Tooltip("相机/模式上下文；供 PlayerController 的移动方向投影。")]
    [SerializeField] private GameModeManager gameModeManager;

    [Header("Inject — Player")]
    [Tooltip("需要注入 IGameModeMovementContext 的 PlayerController；可留空并由下方选项自动收集。")]
    [SerializeField] private PlayerController[] playerControllers;

    [Tooltip("勾选时在本物体子层级中查找 PlayerController 并注入（仅限子树，不用全场景 Find）。")]
    [SerializeField] private bool gatherPlayerControllersInChildren;

    private ServiceRegistry _registry;

    /// <summary>场景服务表，供 UI / 调试模块在运行时解析。</summary>
    public IServiceResolver Services => _registry;

    private void Awake()
    {
        _registry = new ServiceRegistry();

        if (gameModeManager != null)
        {
            _registry.Register(gameModeManager);
            _registry.RegisterAs<IGameModeMovementContext>(gameModeManager);
        }

        WireMovementContextInjection();
    }

    private void WireMovementContextInjection()
    {
        if (!_registry.TryResolve(out IGameModeMovementContext ctx))
        {
#if UNITY_EDITOR
            if (HasTargetsNeedingInjection())
            {
                Debug.LogWarning(
                    "[SystemRoot] 已配置需要移动的 PlayerController，但未指派 GameModeManager；" +
                    "移动将退回世界轴或 Camera.main。请在 Inspector 中完成注册。",
                    this);
            }
#endif
            return;
        }

        foreach (var pc in ResolvePlayerControllers())
        {
            if (pc != null)
            {
                pc.InjectMovementContext(ctx);
            }
        }
    }

    private PlayerController[] ResolvePlayerControllers()
    {
        if (gatherPlayerControllersInChildren)
        {
            return GetComponentsInChildren<PlayerController>(true);
        }

        return playerControllers ?? System.Array.Empty<PlayerController>();
    }

    private bool HasTargetsNeedingInjection()
    {
        if (gatherPlayerControllersInChildren)
        {
            return GetComponentsInChildren<PlayerController>(true).Length > 0;
        }

        return playerControllers != null && playerControllers.Length > 0;
    }
}

