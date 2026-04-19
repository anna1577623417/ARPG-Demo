using UnityEngine;

/// <summary>
/// 挂在角色预制体根（或任意子物体）：在预制体上指定 Follow / LookAt 锚点，
/// 运行时由 <see cref="PlayerManager"/> 读出并交给 <see cref="ActionCameraController"/> 绑定 Cinemachine。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("GameMain/Player/Player Camera Anchor")]
public sealed class PlayerCameraAnchor : MonoBehaviour
{
    [Header("Camera Anchors (prefab-local)")]
    [Tooltip("第三人称跟随锚点（通常在角色根下、非骨骼上的空物体）。")]
    [SerializeField] private Transform followTarget;

    [Tooltip("可选；若为空则 LookAt 与 Follow 相同。")]
    [SerializeField] private Transform lookAtTarget;

    public Transform FollowTarget => followTarget;

    public Transform LookAtTarget => lookAtTarget != null ? lookAtTarget : followTarget;
}
