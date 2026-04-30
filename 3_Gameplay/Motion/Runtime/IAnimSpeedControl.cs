/// <summary>
/// 动画速率控制抽象。
/// Why: Gameplay 不直接引用表现层组件，遵守逻辑层→表现层事件驱动边界。
/// </summary>
public interface IAnimSpeedControl
{
    void SetSpeed(float speed);
}
