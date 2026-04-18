using UnityEngine;

/// <summary>
/// Scene composition root: registers lightweight DI, spawns party from <see cref="TeamDefinition"/>,
/// constructs <see cref="PlayerManager"/>, and wires <see cref="GameplayInputRouter"/>.
///
/// ═══ 推荐层级（示例）════════════════════════════════════
/// SystemRoot  (本组件)
///   ├── Systems/
///   │     ├── InputRouter        ← <see cref="GameplayInputRouter"/>（可选子物体；也可挂在 SystemRoot 上）
///   │     └── CameraBinder       ← <see cref="ActiveCharacterCameraBinder"/>（状态机：FreeLook / 换人 SwitchBlend / 预留 LockOn）
///   └── World/
///         └── SpawnPoints/
///               └── Primary      ← <see cref="SpawnPoint"/>
/// ═══════════════════════════════════════════════════════
///
/// ═══ 依赖注入（本类职责）════════════════════════════════
/// 本对象是 Composition Root：在 <see cref="Awake"/> 内注册 <see cref="ServiceRegistry"/>、创建纯 C# 服务
/// （<see cref="PlayerManager"/>），并对需要场景引用的 MonoBehaviour 调用 <c>Construct(...)</c> 完成显式注入。
/// 子系统不应再 <c>FindObjectOfType&lt;SystemRoot&gt;</c> 去“猜”管理器从哪来 —— 由本根统一装配。
/// ═══════════════════════════════════════════════════════
/// </summary>
[DefaultExecutionOrder(-300)]
[AddComponentMenu("GameMain/MultiCharacter/System Root")]
public class SystemRoot : MonoBehaviour
{
    [Header("Party Data")]
    [SerializeField] private TeamDefinition teamDefinition;

    [SerializeField] private InputReader sharedInputReader;

    [Header("World")]
    [SerializeField] private SpawnPoint primarySpawn;

    [Tooltip("当 <see cref=\"SwitchSpawnPolicy.SlotAnchors\"/> 时按槽位取坐标；索引与阵容槽一致。")]
    [SerializeField] private Transform[] slotAnchors;

    [SerializeField] private SwitchSpawnPolicy switchSpawnPolicy = SwitchSpawnPolicy.InheritPreviousActivePose;

    [SerializeField] private Transform partyParent;

    [Header("Systems")]
    [SerializeField] private GameplayInputRouter gameplayInputRouter;

    [Tooltip("若为空，仅在 SystemRoot 子层级内 GetComponentInChildren（不全局 Find）。")]
    [SerializeField] private ActiveCharacterCameraBinder activeCharacterCameraBinder;

    private ServiceRegistry _services;

    /// <summary>Resolve services from scene bootstrap (e.g. UI debuggers).</summary>
    public IServiceResolver Services => _services;

    private void Awake()
    {
        _services = new ServiceRegistry();
        _services.RegisterSingleton<IServiceResolver>(_services);

        var eventBusAdapter = new GlobalGameplayEventBusAdapter();
        _services.RegisterSingleton<IGameplayEventBus>(eventBusAdapter);

        if (primarySpawn == null)
        {
            primarySpawn = FindFirstObjectByType<SpawnPoint>();
        }

        if (partyParent == null)
        {
            var go = new GameObject("PlayerParty");
            go.transform.SetParent(transform, false);
            partyParent = go.transform;
        }

        if (teamDefinition == null)
        {
            Debug.LogError("[SystemRoot] Assign Team Definition on the component.");
            return;
        }

        var party = PlayerPartyFactory.SpawnParty(teamDefinition, primarySpawn, partyParent, sharedInputReader);

        var manager = new PlayerManager(
            party,
            switchSpawnPolicy,
            primarySpawn,
            slotAnchors,
            eventBusAdapter);

        _services.RegisterSingleton<IPlayerManager>(manager);
        manager.ActivateInitialMember();

        if (gameplayInputRouter == null)
        {
            gameplayInputRouter = GetComponentInChildren<GameplayInputRouter>(true);
        }

        if (gameplayInputRouter != null)
        {
            gameplayInputRouter.Construct(manager, sharedInputReader, eventBusAdapter);
        }
        else
        {
            Debug.LogWarning("[SystemRoot] No GameplayInputRouter found — party switch hotkeys disabled.");
        }

        if (activeCharacterCameraBinder == null)
        {
            activeCharacterCameraBinder = GetComponentInChildren<ActiveCharacterCameraBinder>(true);
        }

        if (activeCharacterCameraBinder != null)
        {
            activeCharacterCameraBinder.Construct(manager);
            _services.RegisterSingleton<IActionGameplayCameraDirector>(activeCharacterCameraBinder);

            var lockOnGateway = activeCharacterCameraBinder.GetComponent<CombatLockOnInputGateway>();
            if (lockOnGateway == null)
            {
                lockOnGateway = activeCharacterCameraBinder.gameObject.AddComponent<CombatLockOnInputGateway>();
            }

            lockOnGateway.Construct(activeCharacterCameraBinder, eventBusAdapter, null);
        }
    }
}
