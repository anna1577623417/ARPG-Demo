using UnityEngine;

/// <summary>
/// 178.1 W5 — PoseCost 计算（关节加权欧氏 + 速度匹配）。
/// 纯函数，可独立 EditMode 测试，不依赖 Unity 运行时 API。
/// </summary>
public static class PoseCostCalculator
{
    /// <summary>178.1 §4.2 默认关节权重（Hip 3 / Spine 2 / Hand 1.5 / Foot 1.0 / Toe 0.5）。</summary>
    public static readonly PoseJointWeight[] DefaultJointWeights =
    {
        new PoseJointWeight { BoneIndex = (int)HumanBodyBones.Hips,         Weight = 3.0f, DebugName = "Hips" },
        new PoseJointWeight { BoneIndex = (int)HumanBodyBones.Spine,        Weight = 2.0f, DebugName = "Spine" },
        new PoseJointWeight { BoneIndex = (int)HumanBodyBones.Chest,        Weight = 2.0f, DebugName = "Chest" },
        new PoseJointWeight { BoneIndex = (int)HumanBodyBones.LeftHand,     Weight = 1.5f, DebugName = "LeftHand" },
        new PoseJointWeight { BoneIndex = (int)HumanBodyBones.RightHand,    Weight = 1.5f, DebugName = "RightHand" },
        new PoseJointWeight { BoneIndex = (int)HumanBodyBones.LeftFoot,     Weight = 1.0f, DebugName = "LeftFoot" },
        new PoseJointWeight { BoneIndex = (int)HumanBodyBones.RightFoot,    Weight = 1.0f, DebugName = "RightFoot" },
        new PoseJointWeight { BoneIndex = (int)HumanBodyBones.LeftToes,     Weight = 0.5f, DebugName = "LeftToes" },
        new PoseJointWeight { BoneIndex = (int)HumanBodyBones.RightToes,    Weight = 0.5f, DebugName = "RightToes" },
    };

    /// <summary>178.1 §4.2 — 计算两帧姿态间的 cost ∈ [0,1]。0=完美对齐 / 1=完全错位。</summary>
    public static float ComputeCost(in PoseSample a, in PoseSample b, PoseJointWeight[] weights = null)
    {
        weights ??= DefaultJointWeights;

        if (a.LocalRot == null || b.LocalRot == null
            || a.LocalRot.Length == 0 || b.LocalRot.Length == 0)
        {
            return 0f;
        }

        var totalWeight = 0f;
        var rotCost = 0f;
        for (var i = 0; i < weights.Length; i++)
        {
            ref readonly var jw = ref weights[i];
            if (jw.BoneIndex < 0
                || jw.BoneIndex >= a.LocalRot.Length
                || jw.BoneIndex >= b.LocalRot.Length)
            {
                continue;
            }
            var ra = a.LocalRot[jw.BoneIndex];
            var rb = b.LocalRot[jw.BoneIndex];
            // Quaternion.Angle 返回度，归一到 [0,1]
            var angle = Quaternion.Angle(ra, rb);
            rotCost += jw.Weight * (angle / 180f);
            totalWeight += jw.Weight;
        }

        var rotComponent = totalWeight > 0.001f ? rotCost / totalWeight : 0f;

        // 速度匹配项：Hip 速度差归一到 5 m/s 上限
        var velComponent = Mathf.Clamp01(Vector3.Distance(a.HipVel, b.HipVel) / 5f);

        // 70% 旋转 + 30% 速度
        return Mathf.Clamp01(0.7f * rotComponent + 0.3f * velComponent);
    }

    /// <summary>178.1 §2.5 推荐 Blend 公式：SmoothStep(min, max)。</summary>
    public static float RecommendedBlend(float poseCost, float min = 0.03f, float max = 0.20f)
    {
        var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(poseCost));
        return Mathf.Lerp(min, max, t);
    }
}
