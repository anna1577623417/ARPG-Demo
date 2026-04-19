using UnityEngine;

/// <summary>
/// 运行时队伍与在场角色：从 <see cref="TeamDefinitionSO"/> 取预制体，生成/激活当前操作角色；
/// Q/R、数字键经 <see cref="InputReader"/> 脉冲驱动切换；默认在「上一在场角色」位置交接（可改为固定刷新点）。
/// </summary>
[DefaultExecutionOrder(-80)]
[AddComponentMenu("GameMain/Party/Player Manager")]
public sealed class PlayerManager : MonoBehaviour
{
    public enum PartySpawnPositionMode
    {
        [Tooltip("新上场角色出现在上一在场角色 Transform 位置（默认）。")]
        AtPreviousActiveCharacter = 0,

        [Tooltip("始终使用 Spawn Points 中与槽位对应的刷新点（首登也如此）。")]
        AtConfiguredSpawnPointsOnly = 1
    }

    [Header("Data")]
    [SerializeField] private TeamDefinitionSO teamDefinition;

    [Header("Spawn")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("开局在场的队伍槽索引（0 起，且该槽须有预制体）。")]
    [SerializeField] private int initialSlotIndex;

    [Tooltip("实例父节点；为空则用当前刷新点 Transform。")]
    [SerializeField] private Transform playerParentOverride;

    [Header("Switch")]
    [SerializeField] private PartySpawnPositionMode spawnPositionMode = PartySpawnPositionMode.AtPreviousActiveCharacter;

    [Header("Camera (scene)")]
    [SerializeField] private ActionCameraController actionCamera;

    [Header("Input")]
    [Tooltip("与玩家 prefab 上一致的 InputReader 资产（同一份引用）。若留空，将在生成角色后从在场 Player 自动解析。")]
    [SerializeField] private InputReader inputReader;

    [Header("Movement injection")]
    [SerializeField] private GameModeManager gameModeManager;

    [Header("Optional")]
    [SerializeField] private InputRouter inputRouter;

    private PlayerFactory _playerFactory;
    private readonly GameObject[] _rootsBySlot = new GameObject[TeamDefinitionSO.MaxSlots];
    private readonly TeamMemberRuntimeStub[] _stubBySlot = new TeamMemberRuntimeStub[TeamDefinitionSO.MaxSlots];

    private int _activeSlotIndex = -1;

    private bool _loggedMissingInputReader;

    private bool _loggedInspectorReaderMismatch;

    private bool _loggedMissingPlayerController;

    /// <summary>当前在场角色根物体。</summary>
    public GameObject ActivePlayerRoot { get; private set; }

    /// <summary>当前在场队伍槽（0～7）。无则 -1。</summary>
    public int ActiveSlotIndex => _activeSlotIndex;

    /// <summary>调试：当前用于队伍切换脉冲消费的 InputReader（优先在场 Player，与 gameplay 同源）。</summary>
    public InputReader DebugEffectiveInputReader => ResolveEffectiveInputReader();

    private void Awake()
    {
        for (var i = 0; i < TeamDefinitionSO.MaxSlots; i++)
        {
            _stubBySlot[i] = new TeamMemberRuntimeStub(i);
        }

        if (gameModeManager != null)
        {
            _playerFactory = new PlayerFactory(gameModeManager);
        }
        else
        {
            Debug.LogWarning("[PlayerManager] 未指派 GameModeManager，无法注入 IGameModeMovementContext。", this);
        }

        if (inputRouter == null)
        {
            inputRouter = GetComponent<InputRouter>();
        }
    }

    private void Start()
    {
        if (teamDefinition == null)
        {
            return;
        }

        if (!TrySpawnInitial())
        {
            Debug.LogWarning("[PlayerManager] 无法在初始槽生成角色，请检查 TeamDefinition 与刷新点。", this);
        }
    }

    /// <summary>
    /// 在本帧输入回调之后再消费队伍脉冲，避免与 Unity 帧顺序错位；未在检视器指派 InputReader 时从在场 <see cref="Player"/> 解析。
    /// </summary>
    private void LateUpdate()
    {
        ProcessPartySwitchInput();
    }

    private void ProcessPartySwitchInput()
    {
        if (teamDefinition == null || _activeSlotIndex < 0)
        {
            return;
        }

        var reader = ResolveEffectiveInputReader();
        if (reader == null)
        {
            if (!_loggedMissingInputReader)
            {
                Debug.LogWarning(
                    "[PlayerManager] 无法解析 InputReader（检视器未指派且在场 Player 上无 InputReader）；队伍切换无效。",
                    this);
                _loggedMissingInputReader = true;
            }

            return;
        }

        _loggedMissingInputReader = false;

        if (reader.ConsumePartySlotSelectPressed(out var directSlot))
        {
            if (directSlot >= 0 && directSlot < teamDefinition.SlotCount)
            {
                TrySwitchTo(directSlot);
            }

            return;
        }

        if (reader.ConsumePartyNextPressed())
        {
            TrySwitchRelative(+1);
        }
        else if (reader.ConsumePartyPrevPressed())
        {
            TrySwitchRelative(-1);
        }
    }

    private InputReader ResolveEffectiveInputReader()
    {
        if (ActivePlayerRoot != null)
        {
            var player = ActivePlayerRoot.GetComponent<Player>() ?? ActivePlayerRoot.GetComponentInChildren<Player>(true);
            if (player != null && player.InputReader != null)
            {
                if (inputReader != null && inputReader != player.InputReader && !_loggedInspectorReaderMismatch)
                {
                    Debug.LogWarning(
                        "[PlayerManager] Inspector 上的 InputReader 与在场 Player 不一致；队伍切换使用 Player 上的资产（与 Q/R/数字键同源）。",
                        this);
                    _loggedInspectorReaderMismatch = true;
                }

                return player.InputReader;
            }
        }

        return inputReader;
    }

    private bool TrySpawnInitial()
    {
        var slot = Mathf.Clamp(initialSlotIndex, 0, Mathf.Max(0, teamDefinition.SlotCount - 1));
        if (teamDefinition.GetPlayerPrefab(slot) == null)
        {
            if (!TryFindFirstOccupiedSlot(out slot))
            {
                return false;
            }
        }

        return TrySwitchToInternal(slot, true);
    }

    /// <summary>切换到指定槽；若该槽无预制体则失败。</summary>
    public bool TrySwitchTo(int targetSlotIndex)
    {
        return TrySwitchToInternal(targetSlotIndex, false);
    }

    private bool TrySwitchToInternal(int targetSlotIndex, bool isInitial)
    {
        if (teamDefinition == null)
        {
            return false;
        }

        if (targetSlotIndex < 0 || targetSlotIndex >= teamDefinition.SlotCount)
        {
            return false;
        }

        var prefab = teamDefinition.GetPlayerPrefab(targetSlotIndex);
        if (prefab == null)
        {
            return false;
        }

        if (!isInitial && targetSlotIndex == _activeSlotIndex)
        {
            return false;
        }

        Vector3 pos;
        Quaternion rot;
        ResolveSpawnTransform(targetSlotIndex, isInitial, out pos, out rot);

        if (!isInitial && ActivePlayerRoot != null)
        {
            ActivePlayerRoot.SetActive(false);
        }

        var existing = _rootsBySlot[targetSlotIndex];
        GameObject root;
        if (existing != null)
        {
            root = existing;
            root.transform.SetPositionAndRotation(pos, rot);
            root.SetActive(true);
        }
        else
        {
            var parent = playerParentOverride != null ? playerParentOverride : GetDefaultParent(targetSlotIndex);
            if (_playerFactory != null)
            {
                root = _playerFactory.InstantiatePlayer(prefab, pos, rot, parent);
            }
            else
            {
                root = Instantiate(prefab, pos, rot, parent);
            }

            _rootsBySlot[targetSlotIndex] = root;
            _stubBySlot[targetSlotIndex].HasSpawnedInstance = true;
        }

        ActivePlayerRoot = root;
        _activeSlotIndex = targetSlotIndex;

        RegisterActiveWithRouter(root);
        BindActionCamera(root);

        var reader = ResolveEffectiveInputReader();
        reader?.RestoreGameplayControlsWhileFocused();

        return true;
    }

    private void RegisterActiveWithRouter(GameObject root)
    {
        if (inputRouter == null || root == null)
        {
            return;
        }

        var pc = root.GetComponent<PlayerController>() ?? root.GetComponentInChildren<PlayerController>(true);
        if (pc == null && !_loggedMissingPlayerController)
        {
            Debug.LogWarning(
                "[PlayerManager] 在场角色根上未找到 PlayerController；InputRouter 将无法绑定控制器（仍记录 Transform）。",
                root);
            _loggedMissingPlayerController = true;
        }

        inputRouter.SetActivePlayer(pc, root.transform);
    }

    private void TrySwitchRelative(int dir)
    {
        if (_activeSlotIndex < 0 || teamDefinition == null)
        {
            return;
        }

        if (dir > 0)
        {
            if (TryFindNextOccupied(_activeSlotIndex, out var next))
            {
                TrySwitchToInternal(next, false);
            }
        }
        else if (TryFindPrevOccupied(_activeSlotIndex, out var prev))
        {
            TrySwitchToInternal(prev, false);
        }
    }

    private bool TryFindFirstOccupiedSlot(out int slot)
    {
        slot = -1;
        var n = teamDefinition.SlotCount;
        for (var i = 0; i < n; i++)
        {
            if (teamDefinition.GetPlayerPrefab(i) != null)
            {
                slot = i;
                return true;
            }
        }

        return false;
    }

    private bool TryFindNextOccupied(int fromSlot, out int slot)
    {
        slot = -1;
        var n = teamDefinition.SlotCount;
        if (n <= 1)
        {
            return false;
        }

        for (var k = 1; k < n; k++)
        {
            var idx = (fromSlot + k) % n;
            if (teamDefinition.GetPlayerPrefab(idx) != null)
            {
                slot = idx;
                return true;
            }
        }

        return false;
    }

    private bool TryFindPrevOccupied(int fromSlot, out int slot)
    {
        slot = -1;
        var n = teamDefinition.SlotCount;
        if (n <= 1)
        {
            return false;
        }

        for (var k = 1; k < n; k++)
        {
            var idx = (fromSlot - k + n * 100) % n;
            if (teamDefinition.GetPlayerPrefab(idx) != null)
            {
                slot = idx;
                return true;
            }
        }

        return false;
    }

    private void ResolveSpawnTransform(int targetSlotIndex, bool isInitial, out Vector3 position, out Quaternion rotation)
    {
        var usePrevious =
            !isInitial
            && spawnPositionMode == PartySpawnPositionMode.AtPreviousActiveCharacter
            && ActivePlayerRoot != null;

        if (usePrevious)
        {
            var t = ActivePlayerRoot.transform;
            position = t.position;
            rotation = t.rotation;
            return;
        }

        var sp = ResolveSpawnTransformOrNull(targetSlotIndex);
        if (sp != null)
        {
            position = sp.position;
            rotation = sp.rotation;
            return;
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;
    }

    private Transform ResolveSpawnTransformOrNull(int teamSlotIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return null;
        }

        var i = teamSlotIndex % spawnPoints.Length;
        return spawnPoints[i];
    }

    private Transform GetDefaultParent(int teamSlotIndex)
    {
        var sp = ResolveSpawnTransformOrNull(teamSlotIndex);
        return sp != null ? sp : transform;
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
