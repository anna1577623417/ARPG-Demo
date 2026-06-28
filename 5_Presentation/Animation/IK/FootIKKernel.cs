using UnityEngine;

/// <summary>
/// 204.x：Foot IK 内核 — runtime + editor preview 共用。
///
/// 算法（轻量「Pelvis Adjust + Foot Plant」）：
///   1. 读两脚世界位置
///   2. 各脚朝下 raycast 找地面
///   3. 计算"穿地深度"（max(0, hit.y + offset - foot.y)）
///   4. 取两脚最大穿地深度 → 把 Hips 整体抬高同样数值
///   5. 单独每只脚旋转对齐地面法线（Plant 感）
///
/// 优势：
///   - 零 IK 求解器 → 0 GC、O(1)
///   - 不破坏 Pose 内部相对关系（双脚同步抬升）
///   - Editor / Runtime 通吃（直接操作 Transform，不依赖 Animator.SetIKPosition）
///   - Humanoid + Generic 均可（Generic 需手动传 bone Transform）
/// </summary>
public static class FootIKKernel
{
    /// <summary>IK 输入参数。Runtime 从 MonoBehaviour 字段读，Editor 从 PreviewController 字段读。</summary>
    public readonly struct Settings
    {
        public readonly LayerMask GroundLayer;
        public readonly float FootOffset;        // 脚底到地面的微小偏移（防穿模）
        public readonly float UpRaycastHeight;   // 脚尖往上多少米开始 raycast
        public readonly float DownRaycastDist;   // 总 raycast 距离
        public readonly float Weight;            // 0~1 IK 强度，0=完全关
        public readonly bool ApplyRotation;      // 是否旋转脚以贴合地面法线

        public Settings(LayerMask groundLayer, float footOffset, float upHeight, float downDist, float weight, bool applyRotation)
        {
            GroundLayer = groundLayer;
            FootOffset = footOffset;
            UpRaycastHeight = upHeight;
            DownRaycastDist = downDist;
            Weight = weight;
            ApplyRotation = applyRotation;
        }

        public static Settings Default => new Settings(
            groundLayer: ~0,
            footOffset: 0.05f,
            upHeight: 0.5f,
            downDist: 1.2f,
            weight: 1f,
            applyRotation: true);
    }

    /// <summary>
    /// Pelvis-Adjust + Foot Plant 一站式 IK。
    /// pelvis：Hips bone（被抬高的根关节）。leftFoot/rightFoot：脚 bone。两端均必填。
    /// </summary>
    public static void Apply(Transform pelvis, Transform leftFoot, Transform rightFoot, in Settings s)
    {
        if (pelvis == null || leftFoot == null || rightFoot == null) return;
        if (s.Weight <= 0.0001f) return;

        // 1. 双脚 raycast
        var leftDelta = ComputeLiftDelta(leftFoot.position, in s, out var leftHit, out var leftHasHit);
        var rightDelta = ComputeLiftDelta(rightFoot.position, in s, out var rightHit, out var rightHasHit);

        // 2. 最大抬升 = max(深度)
        var maxLift = Mathf.Max(leftDelta, rightDelta);
        if (maxLift > 0.0001f)
        {
            var lift = maxLift * s.Weight;
            var p = pelvis.position;
            p.y += lift;
            pelvis.position = p;
        }

        // 3. 每只脚单独贴合地面法线（旋转）
        if (s.ApplyRotation)
        {
            if (leftHasHit)  AlignFootToNormal(leftFoot, leftHit.normal, s.Weight);
            if (rightHasHit) AlignFootToNormal(rightFoot, rightHit.normal, s.Weight);
        }
    }

    /// <summary>计算"穿地深度"：max(0, hit.y + offset - foot.y)。无 hit → 0。</summary>
    static float ComputeLiftDelta(Vector3 footWorld, in Settings s, out RaycastHit hit, out bool hasHit)
    {
        var origin = footWorld + Vector3.up * s.UpRaycastHeight;
        hasHit = Physics.Raycast(origin, Vector3.down, out hit, s.UpRaycastHeight + s.DownRaycastDist, s.GroundLayer, QueryTriggerInteraction.Ignore);
        if (!hasHit) return 0f;

        var targetY = hit.point.y + s.FootOffset;
        var delta = targetY - footWorld.y;
        return delta > 0f ? delta : 0f;
    }

    static void AlignFootToNormal(Transform foot, Vector3 groundNormal, float weight)
    {
        // 仅 Y 倾斜 — 不动 yaw（朝向沿用原 Pose）
        var fwd = foot.forward;
        fwd = Vector3.ProjectOnPlane(fwd, groundNormal);
        if (fwd.sqrMagnitude < 0.0001f) return;

        var target = Quaternion.LookRotation(fwd.normalized, groundNormal);
        foot.rotation = Quaternion.Slerp(foot.rotation, target, weight);
    }

    /// <summary>Humanoid Avatar 解 LeftFoot/RightFoot/Hips bone — Editor / Runtime 通用。</summary>
    public static bool TryResolveHumanoidFeet(Animator animator, out Transform pelvis, out Transform leftFoot, out Transform rightFoot)
    {
        pelvis = leftFoot = rightFoot = null;
        if (animator == null || !animator.isHuman) return false;
        pelvis = animator.GetBoneTransform(HumanBodyBones.Hips);
        leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        return pelvis != null && leftFoot != null && rightFoot != null;
    }
}
