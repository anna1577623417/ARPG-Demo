using UnityEngine;

/// <summary>
/// 场景组合根：装配场景级单例、向登记册暴露，并在 <see cref="Awake"/> 中把 <see cref="IGameModeMovementContext"/>
/// 推入本场景内<strong>已显式登记</strong>的 <see cref="PlayerController"/>（不扫树、不查表、角色预制体不引用本类）。
/// 运行时动态创建的角色由 <see cref="PlayerFactory"/> 在生成点注入；多角色时此数组可列多个或留空改由 <c>PlayerManager</c>+Factory 负责。
/// </summary>
[DefaultExecutionOrder(-120)]
[AddComponentMenu("GameMain/Systems/System Root")]
public sealed class SystemRoot : MonoBehaviour
{
    [Header("Services (scene singletons)")]
    [Tooltip("相机/模式服务；实现 IGameModeMovementContext，供本根与工厂注入。")]
    [SerializeField] private GameModeManager gameModeManager;

    [Header("Bootstrap — editor-placed players only")]
    [Tooltip("仅填「本场景里已摆好」的 PlayerController；从玩家根拖入即可。动态生成者留空，用 PlayerFactory 注入。可填多个，服务多角色开发期场景。")]
    [SerializeField] private PlayerController[] scenePlayerControllers;

    private ServiceRegistry _registry;

    /// <summary>可选服务表（UI / 调试）；核心管线勿依赖此处做隐式查找。</summary>
    public IServiceResolver Services => _registry;

    /// <summary>直接暴露移动上下文引用，供工厂/绑定器构造注入（不经查表）。</summary>
    public IGameModeMovementContext MovementContext => gameModeManager;

    /// <summary>具体实现；仅当边缘代码确实需要相机模式 API 时使用。</summary>
    public GameModeManager GameMode => gameModeManager;

    private void Awake()
    {
        _registry = new ServiceRegistry();

        if (gameModeManager != null)
        {
            _registry.Register(gameModeManager);
            _registry.RegisterAs<IGameModeMovementContext>(gameModeManager);
        }

        PushMovementContextToScenePlayers();
    }

    /// <summary>
    /// 在本帧早期（早于 PlayerController 默认 Awake）完成注入：纯显式列表，无反射、无层级扫描。
    /// </summary>
    private void PushMovementContextToScenePlayers()
    {
        var ctx = MovementContext;
        if (ctx == null || scenePlayerControllers == null || scenePlayerControllers.Length == 0)
        {
            return;
        }

        for (var i = 0; i < scenePlayerControllers.Length; i++)
        {
            var pc = scenePlayerControllers[i];
            if (pc != null)
            {
                pc.InjectMovementContext(ctx);
            }
        }
    }
}
