using UnityEngine;

/// <summary>
/// 玩家移动向量相对「世界 / 相机」投影所需的最小上下文，由 <see cref="GameModeManager"/> 提供并由组合根注入。
/// </summary>
public interface IGameModeMovementContext
{
    /// <summary>当前激活的相机控制器（可为 null）。</summary>
    CameraController ActiveCameraController { get; }

    /// <summary>当前模式是否采用相机相对移动。</summary>
    bool IsCameraRelativeMovement { get; }

    /// <summary>移动参考的水平旋转（偏航）。</summary>
    Quaternion GetMovementReferenceRotation();
}
