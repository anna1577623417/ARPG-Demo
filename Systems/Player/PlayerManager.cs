using UnityEngine;

/// <summary>
/// 运行时生成玩家：从 <see cref="TeamDefinitionSO"/> 取预制体，在刷新点实例化，
/// 并调用 <see cref="ActionCameraController.RebindFollowAndLookAt"/> 绑定场景内相机（玩家不在场景中预摆时必需）。
/// </summary>
[DefaultExecutionOrder(-80)]
[AddComponentMenu("GameMain/Systems/Player Manager")]
public sealed class PlayerManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private TeamDefinitionSO teamDefinition;

    [Header("Spawn")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("开局生成的队伍槽位（0 起）。")]
    [SerializeField] private int initialSlotIndex;

    [Tooltip("实例挂到哪个父节点；为空则用 spawn 点父级或本物体。")]
    [SerializeField] private Transform playerParentOverride;

    [Header("Camera (scene)")]
    [SerializeField] private ActionCameraController actionCamera;

    [Header("Movement injection")]
    [SerializeField] private GameModeManager gameModeManager;

    private PlayerFactory _playerFactory;

    /// <summary>当前生成的玩家根物体（单机流程一条）；未生成时为 null。</summary>
    public GameObject ActivePlayerRoot { get; private set; }

    private void Awake()
    {
        if (gameModeManager != null)
        {
            _playerFactory = new PlayerFactory(gameModeManager);
        }
        else
        {
            Debug.LogWarning("[PlayerManager] 未指派 GameModeManager，无法注入 IGameModeMovementContext。", this);
        }
    }

    private void Start()
    {
        SpawnInitialPlayerIfConfigured();
    }

    private void SpawnInitialPlayerIfConfigured()
    {
        if (teamDefinition == null)
        {
            return;
        }

        var prefab = teamDefinition.GetPlayerPrefab(initialSlotIndex);
        if (prefab == null)
        {
            Debug.LogWarning($"[PlayerManager] TeamDefinition 槽位 {initialSlotIndex} 未配置预制体。", this);
            return;
        }

        ActivePlayerRoot = SpawnPlayer(prefab, initialSlotIndex);
    }

    /// <summary>
    /// 在指定槽位对应的刷新点（循环使用 spawn 列表）实例化预制体并绑定相机。
    /// </summary>
    public GameObject SpawnPlayer(GameObject prefab, int teamSlotIndex)
    {
        if (prefab == null)
        {
            return null;
        }

        var spawn = ResolveSpawnTransform(teamSlotIndex);
        if (spawn == null)
        {
            Debug.LogError("[PlayerManager] 无可用刷新点（spawnPoints 为空或未配置）。", this);
            return null;
        }

        var parent = playerParentOverride != null ? playerParentOverride : spawn;
        GameObject root;
        if (_playerFactory != null)
        {
            root = _playerFactory.InstantiatePlayer(prefab, spawn.position, spawn.rotation, parent);
        }
        else
        {
            root = Instantiate(prefab, spawn.position, spawn.rotation, parent);
        }

        BindActionCamera(root);
        return root;
    }

    private Transform ResolveSpawnTransform(int teamSlotIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return null;
        }

        var i = teamSlotIndex % spawnPoints.Length;
        return spawnPoints[i];
    }

    private void BindActionCamera(GameObject playerRoot)
    {
        if (actionCamera == null || playerRoot == null)
        {
            return;
        }

        var anchor = playerRoot.GetComponentInChildren<PlayerCameraAnchor>(true);
        if (anchor == null)
        {
            Debug.LogError("[PlayerManager] 玩家预制体上未找到 PlayerCameraAnchor，无法绑定相机 Follow/LookAt。", playerRoot);
            return;
        }

        var follow = anchor.FollowTarget;
        var look = anchor.LookAtTarget;
        if (follow == null)
        {
            Debug.LogError("[PlayerManager] PlayerCameraAnchor 未配置 Follow Target。", anchor);
            return;
        }

        actionCamera.RebindFollowAndLookAt(follow, look);
    }
}
