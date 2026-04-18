using UnityEngine;

/// <summary>
/// Party/“可切换角色”元数据与表现挂点。由 <see cref="PlayerPartyFactory"/> 在生成时写入槽位索引。
/// 相机：<b>Follow</b>（身体/质心附近）与 <b>LookAt</b>（胸口/头）分离，供 Cinemachine FreeLook 解耦轨道与朝向。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Player))]
[AddComponentMenu("GameMain/MultiCharacter/Player Character (Party Slot)")]
public class PlayerCharacter : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("FreeLook Follow：腰/身体中心附近（世界位置轨道中心）。")]
    [SerializeField] private Transform cameraFollowTarget;

    [Tooltip("FreeLook Look At：胸口或头；不指定则在 Follow 基础上自动生成略高、略前的点。")]
    [SerializeField] private Transform cameraLookAtTarget;

    [Tooltip("自动创建 CameraFollowTarget 时相对角色根的本地高度（米）。")]
    [SerializeField] private float autoCameraFollowHeight = 1.65f;

    [Tooltip("自动生成 LookAt 时，相对 Follow 锚点额外抬高（米）。仅本地 Y，不加前向偏移，避免 FreeLook 某些角度 LookAt 探到镜头前导致贴脸。")]
    [SerializeField] private float autoLookAtHeightAboveFollow = 0.22f;

    public Player Player => _player ??= GetComponent<Player>();

    /// <summary>阵容槽（0 … TeamSize-1）。</summary>
    public int SlotIndex { get; private set; } = -1;

    private Player _player;

    /// <summary>当前角色用于 Cinemachine <b>Follow</b> 的 Transform。</summary>
    public Transform CameraFollowOrRoot
    {
        get
        {
            EnsureCameraFollowAnchor();
            return cameraFollowTarget != null ? cameraFollowTarget : transform;
        }
    }

    /// <summary>当前角色用于 Cinemachine <b>Look At</b> 的 Transform（与 Follow 分离以修正纵深与 Composer）。</summary>
    public Transform CameraLookAtOrFollow
    {
        get
        {
            EnsureCameraFollowAnchor();
            EnsureCameraLookAtAnchor();
            return cameraLookAtTarget != null ? cameraLookAtTarget : cameraFollowTarget != null ? cameraFollowTarget : transform;
        }
    }

    private void Awake()
    {
        EnsureCameraFollowAnchor();
        EnsureCameraLookAtAnchor();
    }

    internal void BindPartySlot(int slotIndex, InputReader sharedInput)
    {
        SlotIndex = slotIndex;
        EnsureCameraFollowAnchor();
        EnsureCameraLookAtAnchor();
        if (sharedInput != null && Player != null)
        {
            Player.BindSharedInputReader(sharedInput);
        }
    }

    /// <summary>保证存在子节点 <c>CameraFollowTarget</c>。</summary>
    private void EnsureCameraFollowAnchor()
    {
        if (cameraFollowTarget != null)
        {
            return;
        }

        var existing = transform.Find("CameraFollowTarget");
        if (existing != null)
        {
            cameraFollowTarget = existing;
            return;
        }

        var go = new GameObject("CameraFollowTarget");
        var t = go.transform;
        t.SetParent(transform, false);
        t.localPosition = new Vector3(0f, Mathf.Max(0.5f, autoCameraFollowHeight), 0f);
        t.localRotation = Quaternion.identity;
        cameraFollowTarget = t;
    }

    /// <summary>保证存在 <c>CameraLookAtPoint</c> 或使用序列化 LookAt；与 Follow 错开以利 Composer。</summary>
    private void EnsureCameraLookAtAnchor()
    {
        if (cameraLookAtTarget != null)
        {
            return;
        }

        var existing = transform.Find("CameraLookAtPoint");
        if (existing != null)
        {
            cameraLookAtTarget = existing;
            return;
        }

        EnsureCameraFollowAnchor();
        if (cameraFollowTarget == null)
        {
            return;
        }

        var go = new GameObject("CameraLookAtPoint");
        var t = go.transform;
        t.SetParent(transform, false);
        var f = cameraFollowTarget.localPosition;
        t.localPosition = new Vector3(
            0f,
            f.y + Mathf.Max(0f, autoLookAtHeightAboveFollow),
            0f);
        t.localRotation = Quaternion.identity;
        cameraLookAtTarget = t;
    }
}
