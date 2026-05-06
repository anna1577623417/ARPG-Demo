using UnityEngine;

/// <summary>
/// 飘字系统对外入口：调用 Spawn，内部按模式路由。
/// </summary>
[DisallowMultipleComponent]
public sealed class DamageTextSystem : MonoBehaviour
{
    [SerializeField] DamageTextSettingsSO settings;
    [SerializeField] DamageTextView cpuViewPrefab;
    [SerializeField] Transform cpuLayerRoot;
    [SerializeField] Material gpuMaterial;

    DamageTextModeController _controller;
    DamageTextSpaceMode _spaceMode;
    GPUInstancedDamageTextRenderer _gpuRenderer;
    bool _warnedFallbackToCpu;
    bool _warnedSpawnWithoutController;

    void Awake()
    {
        if (cpuViewPrefab == null)
        {
            Debug.LogError("[DamageTextSystem] cpuViewPrefab is null. DamageText disabled.", this);
            enabled = false;
            return;
        }

        var preload = settings != null ? settings.cpuPreloadCount : 32;
        var up = settings != null ? settings.switchUpThreshold : 220;
        var down = settings != null ? settings.switchDownThreshold : 110;
        var defaultLife = settings != null ? settings.defaultLifeSeconds : 0.85f;
        _spaceMode = settings != null ? settings.spaceMode : DamageTextSpaceMode.ScreenSpace;

        var cpu = new CPUDamageTextRenderer(cpuViewPrefab, cpuLayerRoot != null ? cpuLayerRoot : transform, preload);
        var gpu = TryCreateGpuRenderer(defaultLife);
        _controller = new DamageTextModeController(cpu, gpu, up, down);
    }

    public DamageTextSpaceMode SpaceMode => _spaceMode;

    public void Spawn(DamageTextData data)
    {
        if (_controller == null)
        {
            if (!_warnedSpawnWithoutController)
            {
                _warnedSpawnWithoutController = true;
                Debug.LogWarning("[DamageTextSystem] Spawn ignored: controller not initialized.", this);
            }

            return;
        }

        _controller?.Current?.Spawn(in data);
    }

    void Update()
    {
        if (_controller == null)
        {
            return;
        }

        _controller.Current.Tick(Time.deltaTime);
        _controller.CheckSwitch();
    }

    void OnDestroy()
    {
        _gpuRenderer?.Release();
        _gpuRenderer = null;
    }

    public string BuildRuntimeSummary()
    {
        var mode = _controller != null ? _controller.CurrentMode.ToString() : "Uninitialized";
        var active = _controller?.Current?.ActiveCount ?? 0;
        return $"DamageText mode={mode} active={active} space={_spaceMode}";
    }

    IDamageTextRenderer TryCreateGpuRenderer(float defaultLife)
    {
        if (settings == null || !settings.enableGpuPath)
        {
            return null;
        }

        if (gpuMaterial == null)
        {
            WarnFallback("gpuMaterial is null.");
            return null;
        }

        if (settings.digitsAtlas == null)
        {
            WarnFallback("digitsAtlas is null.");
            return null;
        }

        if (!SystemInfo.supportsInstancing)
        {
            WarnFallback("System does not support GPU instancing.");
            return null;
        }

        if (!SystemInfo.supportsComputeShaders)
        {
            WarnFallback("System does not support compute buffers.");
            return null;
        }

        _gpuRenderer = new GPUInstancedDamageTextRenderer(gpuMaterial, settings, defaultLife);
        return _gpuRenderer;
    }

    void WarnFallback(string reason)
    {
        if (_warnedFallbackToCpu)
        {
            return;
        }

        _warnedFallbackToCpu = true;
        Debug.LogWarning($"[DamageTextSystem] GPU path fallback to CPU: {reason}", this);
    }
}
