using UnityEngine;

/// <summary>
/// 世界 UI 距离 LOD：带滞后阈值，避免边界闪烁。
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldUILOD : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Transform cameraTransform;
    [SerializeField] float showDistance = 28f;
    [SerializeField] float hideDistance = 34f;

    bool _isVisible = true;

    public void Bind(Transform followTarget, Transform cam)
    {
        target = followTarget;
        cameraTransform = cam;
    }

    void LateUpdate()
    {
        if (target == null || cameraTransform == null)
        {
            return;
        }

        var dist = Vector3.Distance(cameraTransform.position, target.position);

        if (_isVisible && dist > hideDistance)
        {
            _isVisible = false;
            gameObject.SetActive(false);
        }
        else if (!_isVisible && dist < showDistance)
        {
            _isVisible = true;
            gameObject.SetActive(true);
        }
    }
}
