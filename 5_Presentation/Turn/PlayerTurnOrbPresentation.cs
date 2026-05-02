using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 转身光球表现层：场景中有实体 Transform，可在 Editor 中选中超球调位置/缩放；运行时跟人后背，
/// 根据转身逻辑（TurnInfo + 实际偏航角速度）驱动 Shader 流动，模拟衣物甩动残留的视觉滞留。
/// </summary>
[AddComponentMenu("GameMain/Presentation/Player Turn Orb (behind body)")]
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PlayerTurnOrbPresentation : MonoBehaviour
{
    private const string OrbChildName = "Presentation_TurnOrb";

    [Header("References")]
    [SerializeField] private Player player;

    [Tooltip("留空使用本物体上的 Player。")]
    [SerializeField] private Transform followAnchor;

    [Header("Orb placement (character space: +Z = 角色前方)")]
    [SerializeField] private float distanceBehind = 0.42f;

    [SerializeField] private float heightAboveGround = 1.25f;

    [SerializeField] private Vector3 localFineTune;

    [Tooltip("世界空间半径（Unity 默认球直径为 1，脚本会按半径缩放）。")]
    [SerializeField, Min(0.02f)] private float orbWorldRadius = 0.18f;

    [Header("Drive")]
    [SerializeField] private bool enableEffect = true;

    [SerializeField, Min(0f)] private float intensityScale = 1.2f;

    [SerializeField, Min(0f)] private float yawVelocityBoostScale = 1.35f;

    [SerializeField, Min(1f)] private float yawVelocityNormalize = 540f;

    [SerializeField, Min(0.01f)] private float rampSmoothTime = 0.08f;

    [SerializeField, Min(0.01f)] private float decaySmoothTime = 0.45f;

    [Header("Optional light (easy to spot)")]
    [SerializeField] private bool addPointLight = true;

    [SerializeField, Min(0f)] private float lightIntensityMax = 1.2f;

    [SerializeField, Min(0f)] private float lightRange = 2.5f;

    [Header("Shader (material uses GameMain/Presentation/PlayerTurnOrbFlow)")]
    [SerializeField] private Color orbTint = new Color(0.35f, 0.85f, 1f, 0.55f);

    [SerializeField] private float flowSpeed = 5f;

    [SerializeField] private float bandFrequency = 8f;

    [SerializeField] private float rimPower = 2.2f;

    [Header("Editor")]
    [Tooltip("未运行时：仍把光球摆在后背，便于摆位。关闭则不每帧更新 Transform。")]
    [SerializeField] private bool editorFollowWhenNotPlaying = true;

    [Tooltip("未运行时：用手动强度预览流动（不依赖 Player）。")]
    [SerializeField, Range(0f, 1f)] private float editorPreviewIntensity = 0.35f;

    private Transform _orbRoot;
    private MeshRenderer _orbRenderer;
    private Material _orbMaterial;
    private Light _orbLight;
    private float _smoothIntensity;
    private float _smoothVel;
    private float _lastYaw;
    private bool _hasYawSample;
    private float _lastTwistSign = 1f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
    private static readonly int BandFreqId = Shader.PropertyToID("_BandFreq");
    private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
    private static readonly int TwistSignId = Shader.PropertyToID("_TwistSign");
    private static readonly int SpinAmountId = Shader.PropertyToID("_SpinAmount");

    private Transform ResolveAnchor() => followAnchor != null ? followAnchor : transform;

    private void Reset()
    {
        player = GetComponent<Player>();
        followAnchor = transform;
    }

    private void OnEnable()
    {
        EnsureOrbHierarchy();
        _lastYaw = ResolveAnchor().eulerAngles.y;
        _hasYawSample = true;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && editorFollowWhenNotPlaying)
        {
            EnsureOrbHierarchy();
            PlaceOrbBehind();
            ApplyOrbVisuals(editorPreviewIntensity, 0f, 1f);
        }
    }

    /// <summary>Edit Mode 下 LateUpdate 不可靠，用 Update 驱动光球摆位与预览强度。</summary>
    private void Update()
    {
        if (Application.isPlaying)
        {
            return;
        }

        if (!enableEffect || !editorFollowWhenNotPlaying)
        {
            return;
        }

        EnsureOrbHierarchy();
        PlaceOrbBehind();
        ApplyOrbVisuals(editorPreviewIntensity, 0f, 1f);
        SetLight(editorPreviewIntensity);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!enableEffect)
        {
            SmoothTowards(0f);
            ApplyOrbVisuals(_smoothIntensity, 0f, 1f);
            PlaceOrbBehind();
            SetLight(0f);
            return;
        }

        if (player == null)
        {
            player = GetComponent<Player>();
        }

        EnsureOrbHierarchy();

        var anchor = ResolveAnchor();
        var yaw = anchor.eulerAngles.y;
        var yawVel = _hasYawSample
            ? Mathf.DeltaAngle(_lastYaw, yaw) / Mathf.Max(Time.deltaTime, 1e-5f)
            : 0f;
        _lastYaw = yaw;
        _hasYawSample = true;

        var spinNorm = Mathf.Clamp01(Mathf.Abs(yawVel) / Mathf.Max(10f, yawVelocityNormalize));

        float targetIntensity = 0f;

        if (player != null && player.States != null &&
            !player.States.LocomotionTurnSettings.EnableTurnInPlacePresentation)
        {
            SmoothTowards(0f);
            PlaceOrbBehind();
            ApplyOrbVisuals(_smoothIntensity, yawVel, spinNorm);
            SetLight(_smoothIntensity);
            return;
        }

        if (player != null)
        {
            var turn = player.CurrentTurnInfo;
            if (turn.IsTurning)
            {
                var mag = Mathf.Clamp01(turn.Angle / 180f) * intensityScale;
                targetIntensity = Mathf.Max(targetIntensity, mag);
                var ts = Mathf.Abs(turn.SignedAngle) > 0.01f
                    ? Mathf.Sign(turn.SignedAngle)
                    : (turn.Direction != 0 ? Mathf.Sign(turn.Direction) : _lastTwistSign);
                _lastTwistSign = ts;
            }
        }

        var yawBoost = Mathf.Clamp01(Mathf.Abs(yawVel) / Mathf.Max(10f, yawVelocityNormalize)) *
                       yawVelocityBoostScale;
        targetIntensity = Mathf.Clamp01(Mathf.Max(targetIntensity, yawBoost));

        SmoothTowards(targetIntensity);

        PlaceOrbBehind();
        ApplyOrbVisuals(_smoothIntensity, yawVel, spinNorm);
        SetLight(_smoothIntensity);
    }

    private void SmoothTowards(float target)
    {
        var t = target > _smoothIntensity ? rampSmoothTime : decaySmoothTime;
        _smoothIntensity = Mathf.SmoothDamp(_smoothIntensity, target, ref _smoothVel, t,
            Mathf.Infinity, Application.isPlaying ? Time.deltaTime : 0.016f);
    }

    private void EnsureOrbHierarchy()
    {
        if (_orbRoot != null && _orbRenderer != null)
        {
            return;
        }

        var anchor = ResolveAnchor();
        var existing = FindChildByName(anchor, OrbChildName);
        if (existing != null)
        {
            _orbRoot = existing;
            _orbRenderer = existing.GetComponent<MeshRenderer>();
            var mf = existing.GetComponent<MeshFilter>();
            if (_orbRenderer == null || mf == null)
            {
                Debug.LogWarning("[PlayerTurnOrb] Child exists but missing MeshRenderer/MeshFilter.", this);
            }
        }
        else
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = OrbChildName;
            var col = sphere.GetComponent<Collider>();
            if (col != null)
            {
                DestroyCollider(col);
            }
            _orbRoot = sphere.transform;
            _orbRoot.SetParent(anchor, false);
            _orbRenderer = sphere.GetComponent<MeshRenderer>();
        }

        if (_orbMaterial == null)
        {
            var sh = Shader.Find("GameMain/Presentation/PlayerTurnOrbFlow");
            if (sh != null)
            {
                _orbMaterial = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                if (_orbRenderer != null)
                {
                    _orbRenderer.sharedMaterial = _orbMaterial;
                }
            }
            else
            {
                Debug.LogError("[PlayerTurnOrb] Shader GameMain/Presentation/PlayerTurnOrbFlow not found.", this);
            }
        }

        if (_orbRenderer != null)
        {
            _orbRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _orbRenderer.receiveShadows = false;
        }

        if (addPointLight && _orbLight == null && _orbRoot != null)
        {
            var lg = _orbRoot.GetComponentInChildren<Light>();
            if (lg == null)
            {
                var go = new GameObject("OrbPointLight");
                go.transform.SetParent(_orbRoot, false);
                _orbLight = go.AddComponent<Light>();
                _orbLight.type = LightType.Point;
            }
            else
            {
                _orbLight = lg;
            }
        }
    }

    private void OnDestroy()
    {
        if (_orbMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(_orbMaterial);
            }
            else
            {
                DestroyImmediate(_orbMaterial);
            }
        }
    }

    private static void DestroyCollider(Collider c)
    {
        if (Application.isPlaying)
        {
            Destroy(c);
        }
        else
        {
            DestroyImmediate(c);
        }
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != root && t.name == childName)
            {
                return t;
            }
        }

        return null;
    }

    private void PlaceOrbBehind()
    {
        if (_orbRoot == null)
        {
            return;
        }

        var anchor = ResolveAnchor();
        var flatFwd = anchor.forward;
        flatFwd.y = 0f;
        if (flatFwd.sqrMagnitude < 1e-6f)
        {
            flatFwd = Vector3.forward;
        }
        else
        {
            flatFwd.Normalize();
        }

        var back = -flatFwd;
        var worldPos = anchor.position + Vector3.up * heightAboveGround + back * distanceBehind +
                       anchor.TransformDirection(localFineTune);
        _orbRoot.position = worldPos;

        _orbRoot.rotation = Quaternion.LookRotation(flatFwd, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);

        var scale = orbWorldRadius * 2f;
        _orbRoot.localScale = new Vector3(scale, scale, scale);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            SceneView.RepaintAll();
        }
#endif
    }

    private void ApplyOrbVisuals(float intensity, float yawVelocityDegPerSec, float spinNorm)
    {
        if (_orbMaterial == null)
        {
            return;
        }

        var col = orbTint;
        col.a = Mathf.Clamp01(intensity) * Mathf.Clamp01(orbTint.a);
        _orbMaterial.SetColor(BaseColorId, col);
        _orbMaterial.SetFloat(IntensityId, Mathf.Clamp01(intensity));
        _orbMaterial.SetFloat(FlowSpeedId, flowSpeed * (1f + Mathf.Abs(yawVelocityDegPerSec) / 720f));
        _orbMaterial.SetFloat(BandFreqId, bandFrequency);
        _orbMaterial.SetFloat(RimPowerId, rimPower);
        _orbMaterial.SetFloat(TwistSignId, _lastTwistSign);
        _orbMaterial.SetFloat(SpinAmountId, Mathf.Clamp01(spinNorm));
    }

    private void SetLight(float intensityNorm)
    {
        if (!addPointLight || _orbLight == null)
        {
            return;
        }

        _orbLight.enabled = intensityNorm > 0.02f;
        _orbLight.intensity = intensityNorm * lightIntensityMax;
        _orbLight.range = lightRange;
        _orbLight.color = orbTint;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!enabled)
        {
            return;
        }

        var anchor = ResolveAnchor();
        var flatFwd = anchor.forward;
        flatFwd.y = 0f;
        if (flatFwd.sqrMagnitude < 1e-6f)
        {
            flatFwd = Vector3.forward;
        }
        else
        {
            flatFwd.Normalize();
        }

        var back = -flatFwd;
        var center = anchor.position + Vector3.up * heightAboveGround + back * distanceBehind +
                     anchor.TransformDirection(localFineTune);
        Gizmos.color = new Color(0.2f, 0.95f, 1f, 0.85f);
        Gizmos.DrawWireSphere(center, orbWorldRadius);
        Gizmos.DrawLine(center, center + back * 0.35f);
    }
#endif
}
