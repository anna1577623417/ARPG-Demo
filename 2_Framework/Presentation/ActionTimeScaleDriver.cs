using UnityEngine;

/// <summary>
/// 动作时间轴驱动的全局 Time.timeScale（HitStop / SlowMo / BulletTime 区间）。
/// 挂场景常驻节点（建议与 GameModeManager 同级）。
/// </summary>
[DefaultExecutionOrder(200)]
[AddComponentMenu("GameMain/Presentation/Action Time Scale Driver")]
public sealed class ActionTimeScaleDriver : MonoBehaviour
{
    static ActionTimeScaleDriver s_instance;

    [SerializeField, Range(0.01f, 1f)]
    float hitStopScale = 0.02f;

    float _savedScale = 1f;
    float _hitStopUntilUnscaled;
    float _zoneScale = 1f;
    int _zoneRefCount;

    public static ActionTimeScaleDriver Instance
    {
        get
        {
            if (s_instance == null)
            {
                s_instance = FindObjectOfType<ActionTimeScaleDriver>();
            }

            return s_instance;
        }
    }

    void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Debug.LogWarning("[ActionTimeScale] 场景中存在多个 ActionTimeScaleDriver，保留首个。", this);
            return;
        }

        s_instance = this;
        _savedScale = Time.timeScale;
    }

    void OnDestroy()
    {
        if (s_instance == this)
        {
            Time.timeScale = _savedScale;
            s_instance = null;
        }
    }

    void Update()
    {
        if (Time.unscaledTime < _hitStopUntilUnscaled)
        {
            Time.timeScale = hitStopScale;
            return;
        }

        Time.timeScale = _zoneRefCount > 0 ? Mathf.Clamp(_zoneScale, 0.01f, 1f) : _savedScale;
    }

    public void RequestHitStop(float realSeconds)
    {
        if (realSeconds <= 0f)
        {
            return;
        }

        _hitStopUntilUnscaled = Mathf.Max(_hitStopUntilUnscaled, Time.unscaledTime + realSeconds);
    }

    public void PushZoneScale(float scale)
    {
        _zoneRefCount++;
        _zoneScale = Mathf.Clamp(scale, 0.01f, 1f);
    }

    public void PopZoneScale()
    {
        _zoneRefCount = Mathf.Max(0, _zoneRefCount - 1);
        if (_zoneRefCount == 0)
        {
            _zoneScale = 1f;
        }
    }

    public void ResetAll()
    {
        _hitStopUntilUnscaled = 0f;
        _zoneRefCount = 0;
        _zoneScale = 1f;
        Time.timeScale = _savedScale;
    }
}
