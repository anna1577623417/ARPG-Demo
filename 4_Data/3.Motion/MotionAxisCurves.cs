using System;
using UnityEngine;

/// <summary>
/// 局部空间三轴位置曲线：归一化 Evaluate(t) × Scale（米）。
/// X = 角色 Right，Y = World Up，Z = 角色 Forward（与 MotionExecutor 朝向一致）。
/// </summary>
[Serializable]
public struct MotionAxisCurves
{
    [Tooltip("左右位移曲线（负 = 左）。")]
    public AnimationCurve XCurve;

    [Tooltip("上下位移曲线（负 = 下）。")]
    public AnimationCurve YCurve;

    [Tooltip("前后位移曲线（负 = 后撤）。")]
    public AnimationCurve ZCurve;

    [Tooltip("X 轴幅度（米），× 曲线值。")]
    public float XScale;

    [Tooltip("Y 轴幅度（米），× 曲线值。")]
    public float YScale;

    [Tooltip("Z 轴幅度（米），× 曲线值。")]
    public float ZScale;

    public bool HasAnyCurve =>
        HasCurve(XCurve) || HasCurve(YCurve) || HasCurve(ZCurve);

    public Vector3 SampleLocalPosition(float normalizedTime, float motionScale = 1f)
    {
        var scale = Mathf.Max(0f, motionScale);
        return new Vector3(
            SampleAxis(XCurve, XScale, normalizedTime) * scale,
            SampleAxis(YCurve, YScale, normalizedTime) * scale,
            SampleAxis(ZCurve, ZScale, normalizedTime) * scale);
    }

    public Vector3 SampleLocalDelta(float prevT, float currT, float motionScale = 1f)
    {
        var a = SampleLocalPosition(prevT, motionScale);
        var b = SampleLocalPosition(currT, motionScale);
        return b - a;
    }

    static bool HasCurve(AnimationCurve curve) => curve != null && curve.length > 0;

    static float SampleAxis(AnimationCurve curve, float axisScale, float t)
    {
        if (!HasCurve(curve))
        {
            return 0f;
        }

        return curve.Evaluate(Mathf.Clamp01(t)) * axisScale;
    }
}
