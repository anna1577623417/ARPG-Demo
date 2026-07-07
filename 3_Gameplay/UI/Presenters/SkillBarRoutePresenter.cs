using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ver4.6 单轨技能栏 — 唯一数据源 = <see cref="SkillEntryService.HudHandles"/>。
///
/// ═══ 设计契约（与蓝图 C4 / 211.3 D1 对齐）═══
///   · 不持有 SkillSlotType / SkillDataSO；只读 IRouteRuntimeHandle。
///   · 同 EntrySlot 多 Route 自动横向并排（D2：蓄力 + 普攻同时可见）。
///   · Bind 一次后 Widget 自更新（LateUpdate 拉值）；HUD 不做事件订阅、零 GC。
/// </summary>
[AddComponentMenu("GameMain/UI/Skill Bar Route Presenter")]
public sealed class SkillBarRoutePresenter : MonoBehaviour
{
    [Tooltip("RouteWidget 实例化的根节点（须挂 Grid Layout Group 等场景布局；Presenter 不改 Root 布局）。")]
    [SerializeField] Transform widgetsRoot;

    [Tooltip("须挂 RouteWidget 组件的预制体。")]
    [SerializeField] RouteWidget routeWidgetPrefab;

    [Header("Layout (optional — 默认交给场景 Grid)")]
    [SerializeField, Tooltip(
        "勾选：Presenter 为同槽多 Route 创建 Entry 横向组（HorizontalLayoutGroup），会覆盖/干扰 Widgets Root 上的 Grid Layout Group。\n" +
        "不勾选（默认）：Widget 直接挂到 Widgets Root，槽位排布完全由场景 Grid Layout Group 负责。")]
    bool groupWidgetsByEntry = false;

    [SerializeField, Tooltip("仅 groupWidgetsByEntry 勾选时生效 — Entry 组内横向间距。")]
    float entryGroupSpacing = 6f;

    readonly List<GameObject> m_spawned = new List<GameObject>(24);
    readonly Dictionary<SkillEntrySlot, Transform> m_groupBySlot = new Dictionary<SkillEntrySlot, Transform>(8);
    static readonly List<IRouteRuntimeHandle> s_orderBuffer = new List<IRouteRuntimeHandle>(24);
    static readonly Dictionary<SkillEntrySlot, List<IRouteRuntimeHandle>> s_slotBuckets
        = new Dictionary<SkillEntrySlot, List<IRouteRuntimeHandle>>(8);
    static readonly List<SkillEntryLoadoutSO.HudLayoutEntry> s_layoutScratch
        = new List<SkillEntryLoadoutSO.HudLayoutEntry>(16);

    Player m_player;
    bool m_bound;

    public void Bind(Player player)
    {
        if (player == null)
        {
            HudBugProbe.LogPresenterBindAbort(nameof(SkillBarRoutePresenter), "player=null");
            return;
        }

        if (widgetsRoot == null)
        {
            HudBugProbe.LogPresenterBindAbort(nameof(SkillBarRoutePresenter), "widgetsRoot=null");
            return;
        }

        if (routeWidgetPrefab == null)
        {
            HudBugProbe.LogPresenterBindAbort(nameof(SkillBarRoutePresenter), "routeWidgetPrefab=null");
            return;
        }

        if (m_bound) Unbind();

        m_player = player;
        m_bound = true;
        SuppressSceneChildrenUnderRoot();

        var handles = player.SkillEntries?.HudHandles;
        if (handles == null)
        {
            HudBugProbe.LogPresenterBindAbort(nameof(SkillBarRoutePresenter), "HudHandles=null");
            return;
        }

        CollectVisibleHandles(handles, s_orderBuffer);
        OrderHandlesByLayout(s_orderBuffer, player.SkillEntryLoadout);

        for (var i = 0; i < s_orderBuffer.Count; i++)
        {
            var h = s_orderBuffer[i];
            var parent = groupWidgetsByEntry ? GetOrCreateEntryGroup(h.EntrySlot) : widgetsRoot;
            SpawnWidget(h, parent);
        }

        var widgetCount = 0;
        for (var i = 0; i < m_spawned.Count; i++)
        {
            if (m_spawned[i] != null && m_spawned[i].GetComponent<RouteWidget>() != null)
            {
                widgetCount++;
            }
        }

        HudBugProbe.LogPresenterBind(
            this,
            player,
            s_orderBuffer.Count,
            widgetCount,
            nameof(SkillBarRoutePresenter));
    }

    public void Unbind()
    {
        ClearSpawned();
        m_groupBySlot.Clear();
        m_player = null;
        m_bound = false;
    }

    static void CollectVisibleHandles(IReadOnlyList<IRouteRuntimeHandle> source, List<IRouteRuntimeHandle> dest)
    {
        dest.Clear();
        if (source == null)
        {
            return;
        }

        for (var i = 0; i < source.Count; i++)
        {
            var h = source[i];
            if (h != null && h.ShowOnHud)
            {
                dest.Add(h);
            }
        }
    }

    static void OrderHandlesByLayout(List<IRouteRuntimeHandle> handles, SkillEntryLoadoutSO loadout)
    {
        var layout = loadout?.HudLayout;
        if (layout == null || layout.Length == 0 || handles.Count <= 1)
        {
            return;
        }

        foreach (var kv in s_slotBuckets)
        {
            kv.Value.Clear();
        }

        for (var i = 0; i < handles.Count; i++)
        {
            var h = handles[i];
            if (!s_slotBuckets.TryGetValue(h.EntrySlot, out var list))
            {
                list = new List<IRouteRuntimeHandle>(4);
                s_slotBuckets[h.EntrySlot] = list;
            }

            list.Add(h);
        }

        s_layoutScratch.Clear();
        for (var i = 0; i < layout.Length; i++)
        {
            s_layoutScratch.Add(layout[i]);
        }

        s_layoutScratch.Sort(CompareLayoutEntries);

        handles.Clear();
        var usedSlots = new HashSet<SkillEntrySlot>();

        for (var i = 0; i < s_layoutScratch.Count; i++)
        {
            var slot = s_layoutScratch[i].Slot;
            if (!s_slotBuckets.TryGetValue(slot, out var bucket) || bucket.Count == 0)
            {
                continue;
            }

            handles.AddRange(bucket);
            usedSlots.Add(slot);
        }

        foreach (var kv in s_slotBuckets)
        {
            if (usedSlots.Contains(kv.Key) || kv.Value.Count == 0)
            {
                continue;
            }

            handles.AddRange(kv.Value);
        }
    }

    static int CompareLayoutEntries(SkillEntryLoadoutSO.HudLayoutEntry a, SkillEntryLoadoutSO.HudLayoutEntry b)
    {
        var c = ((int)a.Category).CompareTo((int)b.Category);
        if (c != 0) return c;
        c = a.SortOrder.CompareTo(b.SortOrder);
        if (c != 0) return c;
        return ((int)a.Slot).CompareTo((int)b.Slot);
    }

    void SpawnWidget(IRouteRuntimeHandle handle, Transform parent)
    {
        var inst = Instantiate(routeWidgetPrefab, parent);
        if (!inst.gameObject.activeSelf) inst.gameObject.SetActive(true);
        inst.Bind(handle);
        HudBugProbe.LogWidgetSpawned(inst, handle);
        m_spawned.Add(inst.gameObject);
    }

    Transform GetOrCreateEntryGroup(SkillEntrySlot slot)
    {
        if (m_groupBySlot.TryGetValue(slot, out var existing) && existing != null)
        {
            return existing;
        }

        var go = new GameObject($"Entry_{slot}", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(widgetsRoot, false);
        rt.localScale = Vector3.one;

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = entryGroupSpacing;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        m_groupBySlot[slot] = rt;
        m_spawned.Add(go);
        return rt;
    }

    void ClearSpawned()
    {
        for (var i = 0; i < m_spawned.Count; i++)
        {
            var go = m_spawned[i];
            if (go != null) Destroy(go);
        }
        m_spawned.Clear();
    }

    void SuppressSceneChildrenUnderRoot()
    {
        if (widgetsRoot == null)
        {
            return;
        }

        var spawnedWidgets = new HashSet<RouteWidget>(m_spawned.Count);
        for (var i = 0; i < m_spawned.Count; i++)
        {
            var go = m_spawned[i];
            if (go == null)
            {
                continue;
            }

            var widgets = go.GetComponentsInChildren<RouteWidget>(true);
            for (var w = 0; w < widgets.Length; w++)
            {
                spawnedWidgets.Add(widgets[w]);
            }
        }

        var sceneWidgets = widgetsRoot.GetComponentsInChildren<RouteWidget>(true);
        for (var i = 0; i < sceneWidgets.Length; i++)
        {
            var widget = sceneWidgets[i];
            if (widget == null || spawnedWidgets.Contains(widget))
            {
                continue;
            }

            if (widget.gameObject.activeSelf)
            {
                widget.gameObject.SetActive(false);
            }
        }

        for (var i = 0; i < widgetsRoot.childCount; i++)
        {
            var child = widgetsRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (m_spawned.Contains(child.gameObject))
            {
                continue;
            }

            if (child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    void OnDisable() => Unbind();
    void OnDestroy() => Unbind();
}
