using UnityEngine;

/// <summary>
/// 换人 <see cref="SwitchBlendCameraState"/>：优先<strong>世界位置</strong>用虚拟枢轴 + SmoothDamp 贴向新角色锚点；
/// FOV 为可选「呼吸」层（与 Cinemachine Brain 混合并存）。
/// 若将来需要「双角色同框」构图，可改用 Cinemachine Target Group 做权重中点，与本 Dummy 方案二选一或分层组合。
/// </summary>
[System.Serializable]
public struct SwitchBlendSettings
{
    [Tooltip("位置 SmoothDamp 时间常数（秒）；越小贴得越快。")]
    public float positionSmoothTime;

    [Tooltip("位置误差小于该值（米）视为已贴上目标。")]
    public float snapEpsilon;

    [Tooltip("强制结束混合的最长时间（秒），防止目标高速移动永远追不上。")]
    public float maxBlendDuration;

    [Tooltip("至少经过该时间（秒）后才允许因 snap 提前结束，避免第 0 帧直接退出。")]
    public float minElapsedBeforeSnapExit;

    [Tooltip("若勾选，在混合期间临时拉宽/收回 FOV（与位置并行）。")]
    public bool blendFieldOfView;

    [Tooltip("混合期间相对起始 FOV 的偏移（可为正拉远视野）。")]
    public float fieldOfViewDelta;

    [Tooltip("FOV 向目标值逼近的平滑时间（秒）。")]
    public float fieldOfViewSmoothTime;
}
