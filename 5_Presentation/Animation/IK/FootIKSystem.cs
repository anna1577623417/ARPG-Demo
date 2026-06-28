using UnityEngine;

/// <summary>
/// 运行时脚部 IK —— 复用 <see cref="FootIKKernel"/>，Editor 预览也走同一套（见 EditorFootIK）。
///
/// 204.x 重构：旧版用 OnAnimatorIK + SetIKPosition，Editor AnimationMode 下完全不触发；
/// 改用 LateUpdate 直接操作 bone（Hips 抬升 + 双脚旋转贴地），Runtime / Editor 行为一致。
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class FootIKSystem : MonoBehaviour
{
    [Header("IK 参数")]
    [Tooltip("地面所在的 Layer。")]
    [SerializeField] LayerMask groundLayer = ~0;

    [Range(0f, 1f)]
    [Tooltip("IK 强度：0 关闭、1 全力贴地。")]
    [SerializeField] float ikWeight = 1f;

    [Tooltip("脚底到地面的微小抬高（防穿模）。")]
    [SerializeField] float footOffset = 0.05f;

    [Tooltip("从脚尖往上多少米开始 raycast。")]
    [SerializeField] float upRaycastHeight = 0.5f;

    [Tooltip("Raycast 向下总距离。")]
    [SerializeField] float downRaycastDistance = 1.2f;

    [Tooltip("是否旋转脚以贴合地面法线。")]
    [SerializeField] bool applyFootRotation = true;

    public LayerMask GroundLayer => groundLayer;
    public float IkWeight => ikWeight;
    public float FootOffset => footOffset;
    public float UpRaycastHeight => upRaycastHeight;
    public float DownRaycastDistance => downRaycastDistance;
    public bool ApplyFootRotation => applyFootRotation;

    Animator _anim;

    public FootIKKernel.Settings ResolveSettings() => new FootIKKernel.Settings(
        groundLayer: groundLayer,
        footOffset: footOffset,
        upHeight: upRaycastHeight,
        downDist: downRaycastDistance,
        weight: ikWeight,
        applyRotation: applyFootRotation);

    void Awake() => _anim = GetComponent<Animator>();

    void LateUpdate()
    {
        if (_anim == null) return;
        if (!FootIKKernel.TryResolveHumanoidFeet(_anim, out var pelvis, out var leftFoot, out var rightFoot)) return;
        FootIKKernel.Apply(pelvis, leftFoot, rightFoot, ResolveSettings());
    }
}
