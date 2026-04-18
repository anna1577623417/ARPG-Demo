using UnityEngine;

/// <summary>
/// 视角输入在进入 <see cref="ActionCameraController"/> 前的唯一预处理：死区（抑制鼠标 delta / 摇杆零漂累加到 FreeLook 轴）。
/// </summary>
public static class CameraLookInputAdapter
{
    /// <param name="linearDeadZone">各分量意义上的阈值；实际判定为 <c>sqrMagnitude &lt; d²</c>。</param>
    public static Vector2 ApplyDeadZone(Vector2 raw, float linearDeadZone)
    {
        var d = Mathf.Max(0f, linearDeadZone);
        return raw.sqrMagnitude < d * d ? Vector2.zero : raw;
    }
}
