using UnityEngine;

/// <summary>
/// 输入总线 → 锁定意图：在接入敌人前根据 <see cref="ILockOnTargetResolver"/> 决定是否调用相机；
/// 无解析器或无可锁目标时发布 <see cref="LockOnEngageRejectedEvent"/>。
/// </summary>
[AddComponentMenu("GameMain/Combat/Lock-On Input Gateway")]
public sealed class CombatLockOnInputGateway : MonoBehaviour
{
    private IActionGameplayCameraDirector _cameraDirector;
    private IGameplayEventBus _eventBus;
    private ILockOnTargetResolver _resolver;
    private bool _listening;

    public void Construct(
        IActionGameplayCameraDirector cameraDirector,
        IGameplayEventBus eventBus,
        ILockOnTargetResolver resolver = null)
    {
        if (_listening && _eventBus != null)
        {
            _eventBus.Unsubscribe<LockOnToggleInputEvent>(OnLockOnToggleInput);
            _listening = false;
        }

        _cameraDirector = cameraDirector;
        _eventBus = eventBus;
        _resolver = resolver;

        if (_eventBus != null)
        {
            _eventBus.Subscribe<LockOnToggleInputEvent>(OnLockOnToggleInput);
            _listening = true;
        }
    }

    private void OnDestroy()
    {
        if (_listening && _eventBus != null)
        {
            _eventBus.Unsubscribe<LockOnToggleInputEvent>(OnLockOnToggleInput);
            _listening = false;
        }
    }

    private void OnLockOnToggleInput(LockOnToggleInputEvent evt)
    {
        if (_cameraDirector == null)
        {
            return;
        }

        if (_cameraDirector.IsLockOnActive)
        {
            _cameraDirector.RequestUnlock();
            return;
        }

        if (_resolver != null && _resolver.TryGetLockOnTarget(out var target) && target != null)
        {
            _cameraDirector.RequestLockOn(target);
            return;
        }

        _eventBus?.Publish(new LockOnEngageRejectedEvent(LockOnRejectReason.NoValidTarget));
    }
}
