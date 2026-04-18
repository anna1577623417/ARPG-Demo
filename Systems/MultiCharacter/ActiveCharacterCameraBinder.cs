using UnityEngine;

/// <summary>
/// Composition hook: owns <see cref="GameplayCameraStateMachine"/> for Action 模式下的跟随与换人过渡。
/// 初始跟随与 <see cref="ActiveCharacterChangedEvent"/> 走状态（<see cref="FreeLookCameraState"/> / <see cref="SwitchBlendCameraState"/>）。
/// 换人时使用子物体虚拟枢轴 + SmoothDamp（见 <see cref="SwitchBlendSettings"/>）。
/// </summary>
[AddComponentMenu("GameMain/MultiCharacter/Active Character Camera Binder")]
public class ActiveCharacterCameraBinder : MonoBehaviour, IActionGameplayCameraDirector
{
    [SerializeField] private ActionCameraController actionCamera;

    [SerializeField] private SwitchBlendSettings switchBlendSettings = new SwitchBlendSettings
    {
        positionSmoothTime = 0.18f,
        snapEpsilon = 0.06f,
        maxBlendDuration = 0.5f,
        minElapsedBeforeSnapExit = 0.05f,
        blendFieldOfView = false,
        fieldOfViewDelta = 6f,
        fieldOfViewSmoothTime = 0.12f,
    };

    private IPlayerManager _playerManager;
    private CameraGameplayContext _ctx;
    private GameplayCameraStateMachine _machine;
    private Transform _switchBlendDummyPivot;

    public GameplayCameraStateMachine StateMachine => _machine;

    public bool IsLockOnActive => _machine != null && _machine.Current is LockOnCameraState;

    private void Awake()
    {
        var go = new GameObject("CameraSwitchBlendDummyPivot");
        go.transform.SetParent(transform, false);
        _switchBlendDummyPivot = go.transform;
    }

    public void Construct(IPlayerManager playerManager)
    {
        _playerManager = playerManager;
    }

    private void Start()
    {
        if (actionCamera == null || _playerManager == null)
        {
            return;
        }

        _ctx = new CameraGameplayContext(actionCamera, _playerManager);
        _machine = new GameplayCameraStateMachine();
        _machine.Switch(new FreeLookCameraState(_ctx));
    }

    private void Update()
    {
        _machine?.Tick(Time.deltaTime);
    }

    private void OnEnable()
    {
        GlobalEventBus.Subscribe<ActiveCharacterChangedEvent>(OnActiveCharacterChanged);
    }

    private void OnDisable()
    {
        GlobalEventBus.Unsubscribe<ActiveCharacterChangedEvent>(OnActiveCharacterChanged);
    }

    private void OnActiveCharacterChanged(ActiveCharacterChangedEvent evt)
    {
        if (_machine == null || _ctx == null || _switchBlendDummyPivot == null)
        {
            return;
        }

        var follow = evt.CameraFollowTargetOrRoot;
        if (follow == null)
        {
            _machine.Switch(new FreeLookCameraState(_ctx));
            return;
        }

        _machine.Switch(new SwitchBlendCameraState(
            _machine,
            _ctx,
            _switchBlendDummyPivot,
            follow,
            evt.CameraLookAtTargetOrFollow,
            switchBlendSettings));
    }

    public void RequestLockOn(Transform enemy)
    {
        if (_machine == null || _ctx == null)
        {
            return;
        }

        if (enemy == null)
        {
            RequestUnlock();
            return;
        }

        _machine.Switch(new LockOnCameraState(_machine, _ctx, enemy));
    }

    public void RequestUnlock()
    {
        if (_machine == null || _ctx == null)
        {
            return;
        }

        _machine.Switch(new FreeLookCameraState(_ctx));
    }
}
