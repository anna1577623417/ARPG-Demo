using Cinemachine;
using UnityEngine;

/// <summary>
/// Cinemachine <see cref="Cinemachine3rdPersonFollow"/> 防遮挡射线可视化：
/// 使用与 VCam 相同的 <see cref="Cinemachine3rdPersonFollow.CameraCollisionFilter"/>，
/// 对「真实渲染相机 → Follow 锚点」做 Physics.Linecast，在 Scene 视图绘制遮挡命中与自由段。
///
/// 挂载：与 Action Virtual Camera 同级或任意调试物体；拖入对应 <see cref="CinemachineVirtualCamera"/>。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("GameMain/Camera/Debug/Camera Collision Ray Visualizer")]
public sealed class CameraCollisionRayVisualizer : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;

    [Tooltip("为空则用 Camera.main（须由 CinemachineBrain 驱动）。")]
    [SerializeField] private Camera outputCamera;

    [Header("Visibility")]
    [SerializeField] private bool drawInPlayMode = true;

    [SerializeField] private bool drawInEditMode;

    [Tooltip("选中物体才绘制（减轻 Scene  clutter）；关闭则始终绘制。")]
    [SerializeField] private bool onlyWhenSelected = true;

    [Header("Style")]
    [SerializeField] private Color obstructedSegmentColor = new Color(1f, 0.35f, 0.2f, 1f);

    [SerializeField] private Color freeSegmentColor = new Color(0.25f, 1f, 0.45f, 0.85f);

    [SerializeField] private Color missRayColor = new Color(0.35f, 0.85f, 1f, 0.7f);

    [SerializeField] private Color hitNormalColor = Color.cyan;

    [SerializeField] private float hitPointRadius = 0.06f;

    [SerializeField] private float normalDrawLength = 0.35f;

    [Header("Probe")]
    [Tooltip("附加半径：对 Linecast 等价线段做一次 SphereCast，更接近粗射线手感（0=纯线段）。")]
    [SerializeField, Range(0f, 0.25f)] private float sphereCastRadius;

    private void Reset()
    {
        virtualCamera = GetComponent<CinemachineVirtualCamera>();
        if (virtualCamera == null)
        {
            virtualCamera = GetComponentInParent<CinemachineVirtualCamera>();
        }
    }

    private void OnDrawGizmos()
    {
        if (onlyWhenSelected)
        {
            return;
        }

        DrawProbe();
    }

    private void OnDrawGizmosSelected()
    {
        if (!onlyWhenSelected)
        {
            return;
        }

        DrawProbe();
    }

    private void DrawProbe()
    {
        if (!ShouldDrawForCurrentMode())
        {
            return;
        }

        if (virtualCamera == null || virtualCamera.Follow == null)
        {
            return;
        }

        var camTf = ResolveOutputCameraTransform();
        if (camTf == null)
        {
            return;
        }

        var thirdPerson = virtualCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        var layerMask = thirdPerson != null ? thirdPerson.CameraCollisionFilter.value : Physics.AllLayers;

        var from = camTf.position;
        var to = virtualCamera.Follow.position;

        var delta = to - from;
        var dist = delta.magnitude;
        if (dist < 1e-4f)
        {
            return;
        }

        var dir = delta / dist;

        RaycastHit hit;
        var hitFound = sphereCastRadius > 1e-5f
            ? Physics.SphereCast(from, sphereCastRadius, dir, out hit, dist, layerMask, QueryTriggerInteraction.Ignore)
            : Physics.Linecast(from, to, out hit, layerMask, QueryTriggerInteraction.Ignore);

        if (hitFound)
        {
            Gizmos.color = obstructedSegmentColor;
            Gizmos.DrawLine(from, hit.point);

            Gizmos.color = freeSegmentColor;
            Gizmos.DrawLine(hit.point, to);

            Gizmos.color = hitNormalColor;
            Gizmos.DrawRay(hit.point, hit.normal * normalDrawLength);

#if UNITY_EDITOR
            UnityEditor.Handles.color = new Color(hitNormalColor.r, hitNormalColor.g, hitNormalColor.b, 0.35f);
            UnityEditor.Handles.SphereHandleCap(0, hit.point, Quaternion.identity, hitPointRadius * 2f,
                EventType.Repaint);
#endif
            Gizmos.color = obstructedSegmentColor;
            Gizmos.DrawSphere(hit.point, hitPointRadius);
        }
        else
        {
            Gizmos.color = missRayColor;
            Gizmos.DrawLine(from, to);
        }
    }

    private bool ShouldDrawForCurrentMode()
    {
        if (Application.isPlaying && !drawInPlayMode)
        {
            return false;
        }

        if (!Application.isPlaying && !drawInEditMode)
        {
            return false;
        }

        return true;
    }

    private Transform ResolveOutputCameraTransform()
    {
        if (outputCamera != null)
        {
            return outputCamera.transform;
        }

        return Camera.main != null ? Camera.main.transform : null;
    }
}
