using UnityEngine;

/// <summary>
/// 运行时：MotionProfile 动作期在 LateUpdate 剥离 Clip Hips 平面位移，与 Editor MotionDriven 预览同口径。
/// </summary>
[DisallowMultipleComponent]
public sealed class MotionProfileInPlacePresenter : MonoBehaviour
{
    Transform _anchor;
    Vector3 _baselineHipsLocal;
    bool _active;
    bool _hasBaseline;

    public bool IsActive => _active;

    public void Begin(Transform anchor)
    {
        _anchor = anchor;
        _active = anchor != null;
        _hasBaseline = false;
    }

    public void End()
    {
        _active = false;
        _hasBaseline = false;
        _anchor = null;
    }

    void LateUpdate()
    {
        if (!_active || _anchor == null)
        {
            return;
        }

        if (!MotionProfileInPlaceBoneCompensator.TryResolveHipsBone(_anchor, out var hips))
        {
            return;
        }

        if (!_hasBaseline)
        {
            _baselineHipsLocal = MotionProfileInPlaceBoneCompensator.ReadHipsLocalOnAnchor(_anchor, hips);
            _hasBaseline = true;
        }

        MotionProfileInPlaceBoneCompensator.CompensateHipsPlanarFromBaseline(
            _anchor, hips, in _baselineHipsLocal);
    }
}
