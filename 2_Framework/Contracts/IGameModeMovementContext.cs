using UnityEngine;

/// <summary>
/// 玩家平面移动输入投影所需的最小契约：由 <see cref="GameModeManager"/> 实现，
/// 在实例创建时通过构造/工厂注入到 <see cref="PlayerController"/>（禁止暴露具体相机控制器类型）。
/// </summary>
public interface IGameModeMovementContext
{
    /// <summary>当前模式是否采用相机相对移动（为假时使用世界轴输入）。</summary>
    bool IsCameraRelativeMovement { get; }

    /// <summary>相机相对移动时的水平参考旋转。</summary>
    Quaternion GetMovementReferenceRotation();
}

