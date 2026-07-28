using UnityEngine;

/// <summary>
/// 220.7 D3：连续移动意图宿主契约。
/// <para>Controller/AI 只提交方向；Locomotion State 再把方向转换为 Motor 速度。</para>
/// </summary>
public interface IMovementIntentHost
{
    bool HasMovementIntent { get; }
    Vector3 MovementIntent { get; }
    bool WantsRun { get; }

    void SetMovementIntent(Vector3 worldDirection, bool wantsRun);
    void ClearMovementIntent();
}
