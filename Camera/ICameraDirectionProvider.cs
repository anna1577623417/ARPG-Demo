using UnityEngine;

/// <summary>
/// 角色相机相对移动在 XZ 平面上的唯一方向源：由当前激活的 <see cref="CameraController"/>
/// 在 <c>LateUpdate</c> 末尾根据 Brain 输出（或模式特例）写入；<see cref="PlayerController"/> 只读，不自行猜 <see cref="Camera.main"/>。
/// </summary>
public interface ICameraDirectionProvider
{
    /// <summary>已压平到 XZ、归一化；与屏幕“向前”一致。</summary>
    Vector3 Forward { get; }

    /// <summary>已压平到 XZ、归一化；与屏幕“向右”一致。</summary>
    Vector3 Right { get; }
}
