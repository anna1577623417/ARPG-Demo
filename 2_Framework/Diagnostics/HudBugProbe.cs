#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// HUD 专项诊断 — 异常加载、重复 Bind、Handle/Widget 数量失配、CD 计时器失效等。
/// 启用：GameMain → Debug → Log Settings → HUD Bug Log。
/// Console 过滤：<c>[HudBug]</c>
/// </summary>
public static class HudBugProbe
{
    public const string Prefix = "[HudBug]";

    public static bool IsEnabled => GameMainDebugSettings.HudBugLog;

    public static void SetEnabled(bool enabled)
    {
#if UNITY_EDITOR
        GameMainDebugSettings.HudBugLog = enabled;
        GameMainDebugSettings.SaveToEditorPrefs();
        OnSettingsChanged();
        Debug.Log($"{Prefix} probe {(enabled ? "ENABLED" : "DISABLED")} — filter Console: {Prefix}");
#endif
    }

    public static void OnSettingsChanged()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            if (IsEnabled)
            {
                HudBugRuntimeMonitor.EnsureExists();
            }
            else
            {
                HudBugRuntimeMonitor.DestroyIfExists();
            }
        }
#endif
    }

    static readonly Dictionary<int, WidgetCdTrack> s_widgetCd = new Dictionary<int, WidgetCdTrack>(16);
    static readonly Dictionary<int, int> s_presenterBindGen = new Dictionary<int, int>(4);
    static readonly Dictionary<string, float> s_dedupUntil = new Dictionary<string, float>(32);

    struct WidgetCdTrack
    {
        public float LastRemaining;
        public int LastTenths;
        public int StaleFrames;
        public int HiddenWhileCdFrames;
        public int VisibleWhileReadyFrames;
    }

    public static void LogBootstrapBind(Player player, string presenterType, bool presenterNull)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (presenterNull)
        {
            WarnOnce(
                "LOAD_MISSING_PRESENTER",
                $"{Prefix} LOAD_MISSING_PRESENTER player={(player != null ? player.name : "null")} " +
                "→ skillBarRoutePresenter 未指派，RouteWidget 不会生成");
            return;
        }

        Debug.Log(
            $"{Prefix} BOOTSTRAP_BIND player={(player != null ? player.name : "null")} presenter={presenterType}");
    }

    public static void LogPresenterBind(
        Object presenter,
        Player player,
        int visibleHandleCount,
        int spawnedWidgetCount,
        string presenterKind)
    {
        if (!IsEnabled)
        {
            return;
        }

        var id = presenter != null ? presenter.GetInstanceID() : 0;
        if (id != 0)
        {
            s_presenterBindGen.TryGetValue(id, out var gen);
            gen++;
            s_presenterBindGen[id] = gen;

            if (gen > 1)
            {
                WarnOnce(
                    $"LOAD_DUP_BIND_{id}",
                    $"{Prefix} LOAD_DUP_BIND gen={gen} kind={presenterKind} player={(player != null ? player.name : "null")} " +
                    "→ 同一 Presenter 多次 Bind（若未 Unbind 可能重复实例化 Widget）");
            }
        }

        Debug.Log(
            $"{Prefix} BIND_OK kind={presenterKind} player={(player != null ? player.name : "null")} " +
            $"handles={visibleHandleCount} widgets={spawnedWidgetCount}");

        LogUnitRegistry(player, "BIND");

        if (visibleHandleCount > 0 && spawnedWidgetCount == 0)
        {
            WarnOnce(
                "LOAD_ZERO_WIDGETS",
                $"{Prefix} LOAD_ABORT handles={visibleHandleCount} widgets=0 kind={presenterKind} " +
                "→ 有 HudHandle 但未生成 Widget（查 prefab / widgetsRoot）");
        }

        if (visibleHandleCount == 0 && player?.SkillEntries != null)
        {
            var total = player.SkillEntries.HudHandles?.Count ?? 0;
            if (total > 0)
            {
                WarnOnce(
                    "LOAD_ALL_HIDDEN",
                    $"{Prefix} LOAD_ALL_HIDDEN serviceHandles={total} visible=0 " +
                    "→ 全部 ShowOnHud=false 或 Group 吞并");
            }
        }
    }

    public static void LogWidgetSpawned(RouteWidget widget, IRouteRuntimeHandle handle)
    {
        if (!IsEnabled || widget == null || handle == null)
        {
            return;
        }

        Debug.Log(
            $"{Prefix} WIDGET_BIND {FormatUnitTag(handle)} " +
            $"widget={widget.name}#{widget.GetInstanceID()} show={handle.ShowOnHud}");
    }

    public static void LogUnitRegistry(Player player, string phase)
    {
        if (!IsEnabled || player?.SkillEntries == null)
        {
            return;
        }

        var handles = player.SkillEntries.HudHandles;
        if (handles == null || handles.Count == 0)
        {
            Debug.Log($"{Prefix} UNIT_REGISTRY phase={phase} count=0");
            return;
        }

        var sb = new StringBuilder(512);
        var visibleIndex = 0;
        sb.Append(Prefix).Append(" UNIT_REGISTRY phase=").Append(phase).Append(" total=").Append(handles.Count)
            .AppendLine();

        for (var i = 0; i < handles.Count; i++)
        {
            var h = handles[i];
            if (h == null)
            {
                sb.Append("  #").Append(i).Append(" (null handle)").AppendLine();
                continue;
            }

            var marker = h.ShowOnHud ? "SHOW" : "HIDE";
            sb.Append("  [").Append(marker).Append("] ").Append(FormatUnitLine(i, h)).AppendLine();
            if (h.ShowOnHud)
            {
                visibleIndex++;
            }
        }

        sb.Append(Prefix).Append(" UNIT_REGISTRY phase=").Append(phase)
            .Append(" visible=").Append(visibleIndex);
        Debug.Log(sb.ToString());
    }

    public static void LogWidgetRegistry(Player player)
    {
        if (!IsEnabled)
        {
            return;
        }

        var widgets = Object.FindObjectsByType<RouteWidget>(FindObjectsSortMode.None);
        var sb = new StringBuilder(256);
        var active = 0;
        sb.Append(Prefix).Append(" WIDGET_REGISTRY count=").Append(widgets.Length).AppendLine();

        for (var i = 0; i < widgets.Length; i++)
        {
            var w = widgets[i];
            if (w == null || !w.gameObject.activeInHierarchy)
            {
                continue;
            }

            active++;
            if (w.TryGetHudBugSnapshot(out var snap))
            {
                sb.Append("  #").Append(active - 1).Append(' ')
                    .Append(FormatSnapshotLine(snap, w.GetInstanceID())).AppendLine();
            }
            else
            {
                sb.Append("  #").Append(active - 1).Append(" ORPHAN widget=")
                    .Append(w.name).Append('#').Append(w.GetInstanceID()).AppendLine();
            }
        }

        sb.Append(Prefix).Append(" WIDGET_REGISTRY active=").Append(active);
        Debug.Log(sb.ToString());
    }

    static string FormatUnitTag(IRouteRuntimeHandle h)
    {
        if (h == null)
        {
            return "unit=?";
        }

        return $"kind={h.HudUnitKind} slot={h.EntrySlot} unitId={h.UnitId} " +
               $"hudIdentity={h.HudIdentity} folder={h.AssetFolder}";
    }

    static string FormatUnitLine(int index, IRouteRuntimeHandle h)
    {
        var presentation = string.IsNullOrEmpty(h.PresentationId) ? "-" : h.PresentationId;
        return $"#{index} kind={h.HudUnitKind} slot={h.EntrySlot} unitId={h.UnitId} " +
               $"presentationId={presentation} hudIdentity={h.HudIdentity} key={h.KeyLabel} " +
               $"display={h.DisplayName} folder={h.AssetFolder} path={h.AssetPath}";
    }

    static string FormatSnapshotLine(WidgetSnapshot snap, int widgetInstanceId)
    {
        var presentation = string.IsNullOrEmpty(snap.PresentationId) ? "-" : snap.PresentationId;
        return $"kind={snap.HudUnitKind} slot={snap.Slot} unitId={snap.UnitId} " +
               $"presentationId={presentation} hudIdentity={snap.HudIdentity} key={snap.KeyLabel} " +
               $"display={snap.DisplayName} folder={snap.AssetFolder} path={snap.AssetPath} " +
               $"widgetId={widgetInstanceId}";
    }

    public static void LogPresenterBindAbort(string presenterKind, string reason)
    {
        if (!IsEnabled)
        {
            return;
        }

        WarnOnce(
            $"LOAD_ABORT_{presenterKind}_{reason}",
            $"{Prefix} LOAD_ABORT kind={presenterKind} reason={reason}");
    }

    public static void LogLegacyPresenterActive(Object legacyPresenter, Player player)
    {
        if (!IsEnabled || legacyPresenter == null)
        {
            return;
        }

        WarnOnce(
            "LOAD_LEGACY_PRESENTER",
            $"{Prefix} LOAD_LEGACY_PRESENTER {legacyPresenter.name} still active on player={(player != null ? player.name : "null")} " +
            "→ 请换 SkillBarRoutePresenter（211.3 D1）");
    }

    public static void TickWidgetCd(
        int widgetInstanceId,
        IRouteRuntimeHandle handle,
        int lastDisplayTenths,
        bool timerVisible,
        float cooldownMaskFill)
    {
        if (!IsEnabled || handle == null)
        {
            return;
        }

        if (!s_widgetCd.TryGetValue(widgetInstanceId, out var track))
        {
            track = default;
        }

        var remaining = handle.CdRemainingSeconds;
        var onCd = handle.IsOnCooldown;
        var expectedMask = 1f - handle.CdProgress01;

        if (onCd && remaining > 0.05f)
        {
            if (!timerVisible)
            {
                track.HiddenWhileCdFrames++;
                if (track.HiddenWhileCdFrames == 30)
                {
                    WarnOnce(
                        $"CD_TIMER_HIDDEN_{widgetInstanceId}",
                        $"{Prefix} CD_TIMER_HIDDEN {FormatUnitTag(handle)} " +
                        $"remain={remaining:F2}s tenths={lastDisplayTenths} " +
                        "→ IsOnCooldown 但 cooldownTimerText 未显示（查 SerializeField / SetActive）");
                }
            }
            else
            {
                track.HiddenWhileCdFrames = 0;
            }

            if (Mathf.Abs(remaining - track.LastRemaining) > 0.02f
                && lastDisplayTenths == track.LastTenths
                && track.LastTenths > 0)
            {
                track.StaleFrames++;
                if (track.StaleFrames >= 20)
                {
                    WarnOnce(
                        $"CD_TIMER_STALE_{widgetInstanceId}",
                        $"{Prefix} CD_TIMER_STALE {FormatUnitTag(handle)} " +
                        $"remain={remaining:F2}s displayTenths={lastDisplayTenths} (frozen) " +
                        "→ CD 在减但计时器数字不更新");
                    track.StaleFrames = 0;
                }
            }
            else if (lastDisplayTenths != track.LastTenths)
            {
                track.StaleFrames = 0;
            }
        }
        else
        {
            track.StaleFrames = 0;
            track.HiddenWhileCdFrames = 0;

            if (!onCd && timerVisible)
            {
                track.VisibleWhileReadyFrames++;
                if (track.VisibleWhileReadyFrames == 15)
                {
                    WarnOnce(
                        $"CD_TIMER_STUCK_{widgetInstanceId}",
                        $"{Prefix} CD_TIMER_STUCK {FormatUnitTag(handle)} " +
                        "→ CD 已结束但计时器仍显示");
                }
            }
            else
            {
                track.VisibleWhileReadyFrames = 0;
            }
        }

        if (onCd && cooldownMaskFill >= 0f && Mathf.Abs(cooldownMaskFill - expectedMask) > 0.15f)
        {
            WarnOnce(
                $"CD_MASK_DRIFT_{widgetInstanceId}",
                $"{Prefix} CD_MASK_DRIFT {FormatUnitTag(handle)} mask={cooldownMaskFill:F2} " +
                $"expected={expectedMask:F2} progress={handle.CdProgress01:F2}");
        }

        track.LastRemaining = remaining;
        track.LastTenths = lastDisplayTenths;
        s_widgetCd[widgetInstanceId] = track;
    }

    public static void NotifyWidgetUnbind(int widgetInstanceId)
    {
        s_widgetCd.Remove(widgetInstanceId);
    }

    public static void ScanScene(Player player)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (player == null)
        {
            Debug.LogWarning($"{Prefix} SCAN skip — no active Player");
            return;
        }

        var service = player.SkillEntries;
        var handles = service?.HudHandles;
        var visibleCount = 0;
        if (handles != null)
        {
            for (var i = 0; i < handles.Count; i++)
            {
                if (handles[i] != null && handles[i].ShowOnHud)
                {
                    visibleCount++;
                }
            }
        }

        var widgets = Object.FindObjectsByType<RouteWidget>(FindObjectsSortMode.None);
        var activeWidgets = 0;
        var orphanWidgets = 0;
        var identityCounts = new Dictionary<string, int>(16);

        for (var i = 0; i < widgets.Length; i++)
        {
            var w = widgets[i];
            if (w == null || !w.gameObject.activeInHierarchy)
            {
                continue;
            }

            activeWidgets++;
            if (!w.TryGetHudBugSnapshot(out var snap))
            {
                orphanWidgets++;
                continue;
            }

            if (string.IsNullOrEmpty(snap.HudIdentity))
            {
                continue;
            }

            identityCounts.TryGetValue(snap.HudIdentity, out var c);
            identityCounts[snap.HudIdentity] = c + 1;
        }

        var routePresenters = Object.FindObjectsByType<SkillBarRoutePresenter>(FindObjectsSortMode.None);
        var legacyPresenters = Object.FindObjectsByType<SkillEntryBarPresenter>(FindObjectsSortMode.None);

        Debug.Log(
            $"{Prefix} SCAN player={player.name} serviceHandles={handles?.Count ?? 0} visible={visibleCount} " +
            $"routeWidgets={activeWidgets} orphanWidgets={orphanWidgets} " +
            $"SkillBarRoutePresenter={routePresenters.Length} legacyEntryBar={legacyPresenters.Length}");

        LogUnitRegistry(player, "SCAN");
        LogWidgetRegistry(player);

        if (visibleCount > 0 && activeWidgets == 0)
        {
            WarnOnce(
                "SCAN_NO_WIDGETS",
                $"{Prefix} LOAD_HANDLE_WIDGET_MISMATCH visibleHandles={visibleCount} activeWidgets=0");
        }

        if (Mathf.Abs(visibleCount - activeWidgets) > 0 && orphanWidgets == 0 && activeWidgets > 0)
        {
            WarnOnce(
                "SCAN_COUNT_MISMATCH",
                $"{Prefix} LOAD_COUNT_MISMATCH visibleHandles={visibleCount} activeWidgets={activeWidgets} " +
                "→ 数量不一致（可能双 Presenter 或 Group 并排多 Widget 属正常，请对照 HudIdentity）");
        }

        foreach (var kv in identityCounts)
        {
            if (kv.Value <= 1)
            {
                continue;
            }

            WarnOnce(
                $"LOAD_DUP_IDENTITY_{kv.Key}",
                $"{Prefix} LOAD_DUP_IDENTITY hudIdentity={kv.Key} count={kv.Value} " +
                "→ 同 HudIdentity 多个活跃 Widget（重复 Bind / 未 Unbind）");
        }

        if (orphanWidgets > 0)
        {
            WarnOnce(
                "LOAD_ORPHAN_WIDGET",
                $"{Prefix} LOAD_ORPHAN_WIDGET count={orphanWidgets} → RouteWidget 无 Handle 仍激活");
        }

        if (legacyPresenters.Length > 0 && routePresenters.Length > 0)
        {
            WarnOnce(
                "LOAD_DUAL_PRESENTER",
                $"{Prefix} LOAD_DUAL_PRESENTER legacy={legacyPresenters.Length} route={routePresenters.Length} " +
                "→ 场景中同时存在 SkillEntryBarPresenter 与 SkillBarRoutePresenter");
        }

        for (var i = 0; i < legacyPresenters.Length; i++)
        {
            LogLegacyPresenterActive(legacyPresenters[i], player);
        }
    }

    static void WarnOnce(string key, string message)
    {
        var now = Time.unscaledTime;
        if (s_dedupUntil.TryGetValue(key, out var until) && now < until)
        {
            return;
        }

        s_dedupUntil[key] = now + 2f;
        Debug.LogWarning(message);
    }

    public readonly struct WidgetSnapshot
    {
        public readonly string HudUnitKind;
        public readonly string UnitId;
        public readonly string PresentationId;
        public readonly string HudIdentity;
        public readonly SkillEntrySlot Slot;
        public readonly string KeyLabel;
        public readonly string DisplayName;
        public readonly string AssetFolder;
        public readonly string AssetPath;
        public readonly bool HasHandle;

        public WidgetSnapshot(
            string hudUnitKind,
            string unitId,
            string presentationId,
            string hudIdentity,
            SkillEntrySlot slot,
            string keyLabel,
            string displayName,
            string assetFolder,
            string assetPath,
            bool hasHandle)
        {
            HudUnitKind = hudUnitKind ?? string.Empty;
            UnitId = unitId ?? string.Empty;
            PresentationId = presentationId ?? string.Empty;
            HudIdentity = hudIdentity ?? string.Empty;
            Slot = slot;
            KeyLabel = keyLabel ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            AssetFolder = assetFolder ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            HasHandle = hasHandle;
        }
    }
}

/// <summary>Play 模式周期性 HUD 扫描（仅 Editor Play + Probe 开启时存在）。</summary>
public sealed class HudBugRuntimeMonitor : MonoBehaviour
{
    const float ScanInterval = 0.75f;

    float _scanTimer;

#if UNITY_EDITOR
    static HudBugRuntimeMonitor s_instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoBootstrap()
    {
        if (GameMainDebugSettings.HudBugLog)
        {
            EnsureExists();
        }
    }

    public static void EnsureExists()
    {
        if (s_instance != null)
        {
            return;
        }

        var go = new GameObject("[HudBugMonitor]");
        go.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(go);
        s_instance = go.AddComponent<HudBugRuntimeMonitor>();
    }

    public static void DestroyIfExists()
    {
        if (s_instance == null)
        {
            return;
        }

        var go = s_instance.gameObject;
        s_instance = null;
        if (go != null)
        {
            Destroy(go);
        }
    }

    void OnDestroy()
    {
        if (s_instance == this)
        {
            s_instance = null;
        }
    }
#else
    public static void EnsureExists() { }
    public static void DestroyIfExists() { }
#endif

    void LateUpdate()
    {
        if (!HudBugProbe.IsEnabled)
        {
            return;
        }

        _scanTimer += Time.unscaledDeltaTime;
        if (_scanTimer < ScanInterval)
        {
            return;
        }

        _scanTimer = 0f;
        var player = ResolveActivePlayer();
        if (player != null)
        {
            HudBugProbe.ScanScene(player);
        }
    }

    static Player ResolveActivePlayer()
    {
        var managers = Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
        for (var i = 0; i < managers.Length; i++)
        {
            var p = managers[i]?.ActivePlayer;
            if (p != null)
            {
                return p;
            }
        }

        return Object.FindFirstObjectByType<Player>();
    }
}
