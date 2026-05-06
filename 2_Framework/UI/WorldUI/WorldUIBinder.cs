using UnityEngine;

/// <summary>
/// 世界 UI 跟随：将 worldPos 映射到屏幕坐标（LateUpdate）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class WorldUIBinder : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 worldOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] Camera targetCamera;
    [SerializeField] bool hideWhenBehindCamera = true;

    RectTransform _rect;

    void Awake()
    {
        _rect = (RectTransform)transform;
    }

    public void Bind(Transform followTarget, Camera cam, Vector3 offset)
    {
        target = followTarget;
        targetCamera = cam;
        worldOffset = offset;
    }

    void LateUpdate()
    {
        if (target == null || targetCamera == null || _rect == null)
        {
            return;
        }

        var wp = target.position + worldOffset;
        var sp = targetCamera.WorldToScreenPoint(wp);

        if (hideWhenBehindCamera && sp.z < 0f)
        {
            if (_rect.gameObject.activeSelf)
            {
                _rect.gameObject.SetActive(false);
            }

            return;
        }

        if (!_rect.gameObject.activeSelf)
        {
            _rect.gameObject.SetActive(true);
        }

        _rect.position = sp;
    }
}
