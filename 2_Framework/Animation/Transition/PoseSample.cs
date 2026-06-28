using UnityEngine;

/// <summary>
/// 178.1 W5 — 单帧姿态采样数据。
/// 离线烘焙 ClipMotionExtractor → SampleAnimation 时填充；运行时不构造。
/// </summary>
public struct PoseSample
{
    /// <summary>按 HumanBodyBones 索引的局部旋转。空槽用 Quaternion.identity。</summary>
    public Quaternion[] LocalRot;

    /// <summary>髋部世界位置。</summary>
    public Vector3 HipPos;

    /// <summary>髋部世界速度（相邻帧差分）。</summary>
    public Vector3 HipVel;

    /// <summary>足相位：0=左脚支撑 / 0.5=右脚支撑。-1=未提取。</summary>
    public float FootPhase;

    public static PoseSample Empty => new PoseSample
    {
        LocalRot = System.Array.Empty<Quaternion>(),
        FootPhase = -1f,
    };
}

/// <summary>178.1 W5 — 关节权重表（PoseCost 用）。</summary>
[System.Serializable]
public struct PoseJointWeight
{
    public int BoneIndex;     // HumanBodyBones index
    public float Weight;
    public string DebugName;
}
