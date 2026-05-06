using UnityEngine;

/// <summary>
/// 引入阶段自检：开局对 UI 关键组件做一次引用校验并输出摘要。
/// </summary>
[DisallowMultipleComponent]
public sealed class UIFrameworkHealthCheck : MonoBehaviour
{
    [SerializeField] UIRoot uiRoot;
    [SerializeField] UIInputReaderBridge inputBridge;
    [SerializeField] DamageTextSystem damageTextSystem;
    [SerializeField] UIInputBlocker inputBlocker;
    [SerializeField] UIThemeHealthCheck themeHealthCheck;
    [SerializeField] bool runOnStart = true;
    [SerializeField] bool verboseLog = true;

    void Start()
    {
        if (!runOnStart)
        {
            return;
        }

        RunCheck();
    }

    [ContextMenu("Run UI Health Check")]
    public void RunCheck()
    {
        var ok = true;

        if (uiRoot == null && !UIRoot.TryGetPersistentInstance(out uiRoot))
        {
            ok = false;
            Debug.LogError("[UIHealthCheck] UIRoot missing in scene.", this);
        }

        if (inputBridge == null)
        {
            Debug.LogWarning("[UIHealthCheck] UIInputReaderBridge not assigned (input mode sync may be missing).", this);
        }

        if (inputBlocker == null)
        {
            Debug.LogWarning("[UIHealthCheck] UIInputBlocker not assigned (pointer blocking may be missing).", this);
        }
        else
        {
            inputBlocker.LogExpectedStructure();
        }

        if (damageTextSystem == null)
        {
            Debug.LogWarning("[UIHealthCheck] DamageTextSystem not assigned.", this);
        }

        if (themeHealthCheck == null)
        {
            Debug.LogWarning("[UIHealthCheck] UIThemeHealthCheck not assigned (theme chain not explicitly validated).", this);
        }

        if (verboseLog)
        {
            if (uiRoot != null)
            {
                Debug.Log($"[UIHealthCheck] {uiRoot.BuildDebugSnapshot()}", this);
            }

            if (damageTextSystem != null)
            {
                Debug.Log($"[UIHealthCheck] {damageTextSystem.BuildRuntimeSummary()}", this);
            }

            if (themeHealthCheck != null)
            {
                themeHealthCheck.RunCheck();
            }
        }

        if (ok)
        {
            Debug.Log("[UIHealthCheck] Basic checks passed.", this);
        }
    }
}

