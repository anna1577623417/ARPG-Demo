using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 技能栏 Presenter — 从 Player.SkillEntries 拉取 RouteRuntimeHandle 列表，
/// 实例化 / 复用 RouteWidget 渲染。
///
/// ═══ 设计契约 ═══
///   · 一个 ShowOnHud Route → 池内一个 RouteWidget；显隐仅由本类 SetActive 控制。
///   · widgetRoot 下场景样板（Template_*）须保持关闭，不参与 GridLayout；Bind 时自动关。
///   · 0-GC：LateUpdate 在 RouteWidget 内拉句柄，本类不每帧查 SkillEntryService。
///   · 未走 PlayerHudBootstrap 时，可勾选 autoBindFromPlayerManager 自行订阅在场 Player。
/// </summary>
[DefaultExecutionOrder(-40)]
public sealed class SkillEntryBarPresenter : MonoBehaviour
{
    const float DefaultWidgetCellSize = 100f;

    [Header("Layout")]
    [Tooltip("RouteWidget 的父挂载点；Presenter 会按 HudHandles 顺序实例化子项。")]
    [SerializeField] RectTransform widgetRoot;

    [Tooltip("RouteWidget 预制体；必填。建议拖 Project 内 Prefab，勿拖场景子物体（会告警）。")]
    [SerializeField] RouteWidget widgetPrefab;

    [Tooltip("最大可同时显示的 Route 数（池容量）。")]
    [SerializeField, Min(1)] int maxWidgets = 16;

    [Header("Auto Bind")]
    [Tooltip("未由 PlayerHudBootstrap 调用 Bind 时，订阅 PlayerManager.ActivePlayerChanged。")]
    [SerializeField] bool autoBindFromPlayerManager = true;

    [SerializeField] PlayerManager playerManager;

    readonly List<RouteWidget> _pool = new List<RouteWidget>(16);
    Player _player;
    bool _subscribedPlayerManager;

    public void Bind(Player player)
    {
        if (_player == player)
        {
            RefreshFromPlayer();
            return;
        }

        Unbind();
        _player = player;
        SuppressSceneChildrenUnderRoot();
        EnsurePool();
        RefreshFromPlayer();

        if (player != null && GameMainDebugSettings.SkillRouteGraph)
        {
            LogHudSetup(player, "Bind");
        }
    }

    public void Unbind()
    {
        SetPoolActiveRange(0, false);
        _player = null;
        SuppressSceneChildrenUnderRoot();
    }

    void Awake()
    {
        SuppressSceneChildrenUnderRoot();
    }

    void OnEnable()
    {
        if (autoBindFromPlayerManager)
        {
            SubscribePlayerManager();
            var active = ResolvePlayerManager()?.ActivePlayer;
            if (active != null)
            {
                Bind(active);
            }
        }
    }

    void OnDisable()
    {
        UnsubscribePlayerManager();
        Unbind();
    }

    void SubscribePlayerManager()
    {
        if (_subscribedPlayerManager)
        {
            return;
        }

        var mgr = ResolvePlayerManager();
        if (mgr == null)
        {
            return;
        }

        mgr.ActivePlayerChanged += HandleActivePlayerChanged;
        _subscribedPlayerManager = true;
    }

    void UnsubscribePlayerManager()
    {
        if (!_subscribedPlayerManager)
        {
            return;
        }

        var mgr = ResolvePlayerManager();
        if (mgr != null)
        {
            mgr.ActivePlayerChanged -= HandleActivePlayerChanged;
        }

        _subscribedPlayerManager = false;
    }

    PlayerManager ResolvePlayerManager()
    {
        if (playerManager != null)
        {
            return playerManager;
        }

        return FindFirstObjectByType<PlayerManager>();
    }

    void HandleActivePlayerChanged(Player player) => Bind(player);

    void EnsurePool()
    {
        if (widgetRoot == null || widgetPrefab == null) return;

        if (IsSceneInstanceUnderRoot(widgetPrefab, widgetRoot))
        {
            SkillRouteDebug.LogWarn(
                null,
                SkillRouteDebug.CatHud,
                "widgetPrefab 是场景内子物体，建议改为 Project Prefab；当前仍会 Instantiate 副本。",
                widgetPrefab);
        }

        while (_pool.Count < maxWidgets)
        {
            var w = Instantiate(widgetPrefab, widgetRoot);
            NormalizeWidgetRect(w);
            w.gameObject.SetActive(false);
            _pool.Add(w);
        }
    }

    static void NormalizeWidgetRect(RouteWidget widget)
    {
        if (widget == null) return;
        var rect = widget.transform as RectTransform;
        if (rect == null) return;
        if (rect.sizeDelta.x >= 1f && rect.sizeDelta.y >= 1f)
        {
            return;
        }

        rect.sizeDelta = new Vector2(DefaultWidgetCellSize, DefaultWidgetCellSize);
    }

    static bool IsSceneInstanceUnderRoot(RouteWidget prefab, RectTransform root)
    {
        if (prefab == null || root == null) return false;
        return prefab.transform.parent == root;
    }

    void RefreshFromPlayer()
    {
        if (_player == null || _player.SkillEntries == null)
        {
            SetPoolActiveRange(0, false);
            if (_player != null && GameMainDebugSettings.SkillRouteGraph)
            {
                SkillRouteDebug.LogWarn(_player, SkillRouteDebug.CatHud, "Refresh: SkillEntries 为空");
            }

            return;
        }

        SuppressSceneChildrenUnderRoot();

        var handles = _player.SkillEntries.HudHandles;
        var n = Mathf.Min(handles.Count, _pool.Count);

        for (var i = 0; i < n; i++)
        {
            _pool[i].Bind(handles[i]);
            var show = handles[i] != null && handles[i].ShowOnHud;
            SetWidgetActive(_pool[i], show);
            if (GameMainDebugSettings.SkillRouteGraph && handles[i] != null)
            {
                SkillRouteDebug.Log(
                    _player,
                    SkillRouteDebug.CatHud,
                    $"Widget[{i}] show={show} name={handles[i].DisplayName} slot={handles[i].EntrySlot}");
            }
        }

        SetPoolActiveRange(n, false);

        if (GameMainDebugSettings.SkillRouteGraph)
        {
            LogHudSetup(_player, $"Refresh handles={handles.Count} shown={n} pool={_pool.Count}");
        }
    }

    void LogHudSetup(Player player, string phase)
    {
        if (widgetRoot == null)
        {
            SkillRouteDebug.LogWarn(player, SkillRouteDebug.CatHud, $"{phase}: widgetRoot 未指派 → 不会实例化 RouteWidget");
        }

        if (widgetPrefab == null)
        {
            SkillRouteDebug.LogWarn(player, SkillRouteDebug.CatHud, $"{phase}: widgetPrefab 未指派 → EnsurePool 跳过");
        }
    }

    /// <summary>关闭 widgetRoot 下非池内子物体（场景 Template 等），避免占 GridLayout 格位。</summary>
    void SuppressSceneChildrenUnderRoot()
    {
        if (widgetRoot == null) return;

        for (var i = 0; i < widgetRoot.childCount; i++)
        {
            var child = widgetRoot.GetChild(i);
            if (child == null) continue;
            if (IsPooledWidget(child)) continue;
            if (child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    bool IsPooledWidget(Transform child)
    {
        var w = child.GetComponent<RouteWidget>();
        if (w == null) return false;

        for (var i = 0; i < _pool.Count; i++)
        {
            if (_pool[i] == w) return true;
        }

        return false;
    }

    void SetPoolActiveRange(int fromInclusive, bool active)
    {
        for (var i = fromInclusive; i < _pool.Count; i++)
        {
            if (_pool[i] == null) continue;
            if (!active)
            {
                _pool[i].Unbind();
            }

            SetWidgetActive(_pool[i], active);
        }
    }

    static void SetWidgetActive(RouteWidget widget, bool active)
    {
        if (widget == null) return;
        var go = widget.gameObject;
        if (go.activeSelf != active)
        {
            go.SetActive(active);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying) return;
        SuppressSceneChildrenUnderRoot();
    }
#endif
}
