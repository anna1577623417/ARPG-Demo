using System;

/// <summary>
/// 局部 Anim 曲线三点规格（归一化时间上的倍率）。中点时间默认 0.5。
/// </summary>
[Serializable]
public struct AnimSpeedThreePointSpec
{
    public float MidTime;
    public float Start;
    public float Mid;
    public float End;
    public AnimSpeedCurveSolveTarget SolveTarget;

    public static AnimSpeedThreePointSpec DefaultConserve => new AnimSpeedThreePointSpec
    {
        MidTime = 0.5f,
        Start = 1f,
        Mid = 1f,
        End = 1f,
        SolveTarget = AnimSpeedCurveSolveTarget.End,
    };
}
