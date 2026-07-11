using UnityEngine;

/// <summary>
/// 216.3 M6 L2 — 命中预览假人（场景锚点）。
/// <para>仅提供位置/半径；变色与 Overlap 模拟由 Editor <c>HitPreviewDummy</c> 绘制，不参与运行时战斗。</para>
/// </summary>
[AddComponentMenu("GameMain/Testing/Hit Preview Dummy")]
[DisallowMultipleComponent]
public sealed class HitPreviewDummyHost : MonoBehaviour
{
    [Tooltip("预览用受击球半径（米）。")]
    [Min(0.05f)]
    public float Radius = 0.45f;

    [Tooltip("相对 Transform 的球心偏移。")]
    public Vector3 CenterOffset = new Vector3(0f, 0.9f, 0f);

    public Vector3 WorldCenter => transform.TransformPoint(CenterOffset);
}
