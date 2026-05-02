using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 转身「背部流转」表现层：纯 Shader + 叠绘制，零额外贴图。
/// Why：逻辑旋转过快时 Turn 切片几乎看不清；在背部叠加程序化流光可拉长视觉滞留。
/// How：每帧 BakeMesh 当前姿态，用 additive Pass 叠画一遍（不改原材质、不需拆 submesh）。
/// 驱动数据：只读 <see cref="Player.CurrentTurnInfo"/> 与 <see cref="PlayerStateManager.LocomotionTurnSettings"/>。
/// </summary>
[AddComponentMenu("GameMain/Presentation/Player Turn Back-Flow (Shader FX)")]
[DisallowMultipleComponent]
public sealed class PlayerTurnBackFlowPresentation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private SkinnedMeshRenderer skinnedMesh;

    [Tooltip("留空则在运行时从 Shader 名 GameMain/Presentation/PlayerTurnBackFlowAdditive 创建实例材质。")]
    [SerializeField] private Material turnAdditiveMaterial;

    [Header("Master")]
    [SerializeField] private bool enableEffect = true;

    [Header("Intensity")]
    [Tooltip("TurnInfo.Angle 映射到强度的整体倍率。")]
    [SerializeField, Min(0f)] private float intensityScale = 1.15f;

    [Tooltip("Turn90 相对 Turn180 的强度倍率（小转身仍可见）。")]
    [SerializeField, Min(0f)] private float smallTurnTypeMultiplier = 0.75f;

    [SerializeField, Min(0f)] private float largeTurnTypeMultiplier = 1f;

    [Tooltip("朝强度目标爬升时间（秒）。")]
    [SerializeField, Min(0.01f)] private float rampSmoothTime = 0.06f;

    [Tooltip("转身结束后的衰减时间（秒），拉长拖尾感。")]
    [SerializeField, Min(0.01f)] private float decaySmoothTime = 0.42f;

    [Header("Shader look")]
    [SerializeField] private Color turnTint = new Color(0.35f, 0.82f, 1f, 1f);

    [SerializeField, Min(0.01f)] private float flowFrequency = 4f;
    [SerializeField] private float flowSpeed = 6f;
    [SerializeField, Min(0.01f)] private float backSharpness = 2.2f;
    [SerializeField, Min(0.01f)] private float rimPower = 2.4f;

    [Header("Back anchor (character-facing space)")]
    [Tooltip("沿角色「侧向轴」平移背部高光中心（米量级为小数，约 -0.3~0.3）。")]
    [SerializeField] private float biasRight;

    [Tooltip("沿世界上方向微调背部中心。")]
    [SerializeField] private float biasUp;

    private MaterialPropertyBlock _block;
    private Mesh _bakedMesh;
    private float _smoothIntensity;
    private float _smoothVel;
    private float _lastTurnFlowSign = 1f;
    private bool _ownsRuntimeMaterial;

    private static readonly int TurnIntensityId = Shader.PropertyToID("_TurnIntensity");
    private static readonly int TurnColorId = Shader.PropertyToID("_TurnColor");
    private static readonly int TurnForwardWsId = Shader.PropertyToID("_TurnForwardWS");
    private static readonly int TurnSignId = Shader.PropertyToID("_TurnSign");
    private static readonly int BiasRightId = Shader.PropertyToID("_BiasRight");
    private static readonly int BiasUpId = Shader.PropertyToID("_BiasUp");
    private static readonly int BackSharpnessId = Shader.PropertyToID("_BackSharpness");
    private static readonly int FlowFrequencyId = Shader.PropertyToID("_FlowFrequency");
    private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
    private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");

    private void Reset()
    {
        player = GetComponent<Player>();
        skinnedMesh = GetComponentInChildren<SkinnedMeshRenderer>();
    }

    private void Awake()
    {
        if (player == null)
        {
            player = GetComponent<Player>();
        }

        if (skinnedMesh == null)
        {
            skinnedMesh = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        if (turnAdditiveMaterial == null)
        {
            var sh = Shader.Find("GameMain/Presentation/PlayerTurnBackFlowAdditive");
            if (sh != null)
            {
                turnAdditiveMaterial = new Material(sh) { name = "PlayerTurnBackFlow (Instance)" };
                turnAdditiveMaterial.hideFlags = HideFlags.HideAndDontSave;
                _ownsRuntimeMaterial = true;
            }
        }

        _block ??= new MaterialPropertyBlock();
        _bakedMesh ??= new Mesh { name = "TurnBackFlow_Baked" };
    }

    private void OnDestroy()
    {
        if (turnAdditiveMaterial != null && _ownsRuntimeMaterial)
        {
            Destroy(turnAdditiveMaterial);
        }

        if (_bakedMesh != null)
        {
            Destroy(_bakedMesh);
        }
    }

    private void LateUpdate()
    {
        if (!enableEffect || turnAdditiveMaterial == null || skinnedMesh == null || player == null)
        {
            return;
        }

        if (player.States != null && !player.States.LocomotionTurnSettings.EnableTurnInPlacePresentation)
        {
            TargetIntensityZero();
            ApplySmoothAndMaybeDraw();
            return;
        }

        var turn = player.CurrentTurnInfo;
        var target = ComputeTargetIntensity(in turn);

        var smoothT = turn.IsTurning ? rampSmoothTime : decaySmoothTime;
        _smoothIntensity = Mathf.SmoothDamp(_smoothIntensity, target, ref _smoothVel, smoothT,
            Mathf.Infinity, Time.deltaTime);

        ApplySmoothAndMaybeDraw();
    }

    private float ComputeTargetIntensity(in TurnInfo turn)
    {
        if (!turn.IsTurning)
        {
            return 0f;
        }

        var mag = Mathf.Clamp01(turn.Angle / 180f);
        var typeMul = turn.Type == TurnType.Turn180 ? largeTurnTypeMultiplier : smallTurnTypeMultiplier;
        return Mathf.Clamp01(mag * typeMul * intensityScale);
    }

    private void TargetIntensityZero()
    {
        _smoothIntensity = Mathf.SmoothDamp(_smoothIntensity, 0f, ref _smoothVel, decaySmoothTime,
            Mathf.Infinity, Time.deltaTime);
    }

    private void ApplySmoothAndMaybeDraw()
    {
        if (_smoothIntensity < 0.002f)
        {
            return;
        }

        var forward = player.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 1e-6f)
        {
            forward.Normalize();
        }
        else
        {
            forward = Vector3.forward;
        }

        var turn = player.CurrentTurnInfo;
        if (turn.IsTurning)
        {
            var sign = Mathf.Abs(turn.SignedAngle) > 0.01f
                ? Mathf.Sign(turn.SignedAngle)
                : (turn.Direction != 0 ? turn.Direction : 1);
            _lastTurnFlowSign = sign;
        }

        _block.Clear();
        _block.SetFloat(TurnIntensityId, _smoothIntensity);
        _block.SetColor(TurnColorId, turnTint);
        _block.SetVector(TurnForwardWsId, forward);
        _block.SetFloat(TurnSignId, _lastTurnFlowSign);
        _block.SetFloat(BiasRightId, biasRight);
        _block.SetFloat(BiasUpId, biasUp);
        _block.SetFloat(BackSharpnessId, backSharpness);
        _block.SetFloat(FlowFrequencyId, flowFrequency);
        _block.SetFloat(FlowSpeedId, flowSpeed);
        _block.SetFloat(RimPowerId, rimPower);

        skinnedMesh.BakeMesh(_bakedMesh);
        Graphics.DrawMesh(
            _bakedMesh,
            skinnedMesh.transform.localToWorldMatrix,
            turnAdditiveMaterial,
            skinnedMesh.gameObject.layer,
            camera: null,
            submeshIndex: 0,
            properties: _block,
            castShadows: ShadowCastingMode.Off,
            receiveShadows: false,
            probeAnchor: skinnedMesh.probeAnchor != null ? skinnedMesh.probeAnchor : skinnedMesh.transform);
    }
}
