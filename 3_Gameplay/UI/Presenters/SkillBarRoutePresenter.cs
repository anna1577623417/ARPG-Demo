using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ver4.6 单轨技能栏 — 唯一数据源 = <see cref="SkillEntryService.HudHandles"/>。
///
/// ═══ 设计契约（与蓝图 C4 对齐）═══
///   · 不持有 SkillSlotType / SkillDataSO / SkillEntryLoadoutSO；只读 IRouteRuntimeHandle。
///   · 同 EntrySlot 多 Route 自动横向并排（一槽多 Widget = 蓄力 + 普攻 + 派生 同时可见）。
///   · Bind 一次后 Widget 自更新（LateUpdate 拉值）；HUD 不做事件订阅、零 GC。
/// </summary>
[AddComponentMenu("GameMain/UI/Skill Bar Route Presenter")]
public sealed class SkillBarRoutePresenter : MonoBehaviour
{
    [Tooltip("RouteWidget 实例化的根节点（每个 EntrySlot 创建一个横向组）。")]
    [SerializeField] Transform widgetsRoot;

    [Tooltip("须挂 RouteWidget 组件的预制体。")]
    [SerializeField] RouteWidget routeWidgetPrefab;

    [Tooltip("同 Entry 的 Route 横向并排；关闭则扁平列表。")]
    [SerializeField] bool groupWidgetsByEntry = true;

    [Tooltip("Entry 组横向间距（仅运行时创建组时生效）。")]
    [SerializeField] float entryGroupSpacing = 6f;

    readonly List<GameObject> m_spawned = new List<GameObject>(24);
    readonly Dictionary<SkillEntrySlot, Transform> m_groupBySlot = new Dictionary<SkillEntrySlot, Transform>(8);

    Player m_player;
    bool m_bound;

    public void Bind(Player player)
    {
        if (player == null || widgetsRoot == null || routeWidgetPrefab == null)
        {
            return;
        }

        if (m_bound) Unbind();

        m_player = player;
        m_bound = true;
        SuppressSceneChildrenUnderRoot();

        var handles = player.SkillEntries?.HudHandles;
        if (handles == null) return;

        for (var i = 0; i < handles.Count; i++)
        {
            var h = handles[i];
            if (h == null || !h.ShowOnHud) continue;

            var parent = groupWidgetsByEntry ? GetOrCreateEntryGroup(h.EntrySlot) : widgetsRoot;
            SpawnWidget(h, parent);
        }
    }

    public void Unbind()
    {
        ClearSpawned();
        m_groupBySlot.Clear();
        m_player = null;
        m_bound = false;
    }

    void SpawnWidget(IRouteRuntimeHandle handle, Transform parent)
    {
        var inst = Instantiate(routeWidgetPrefab, parent);
        if (!inst.gameObject.activeSelf) inst.gameObject.SetActive(true);
        inst.Bind(handle);
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
        if (widgetsRoot == null) return;

        for (var i = 0; i < widgetsRoot.childCount; i++)
        {
            var child = widgetsRoot.GetChild(i);
            if (child == null) continue;
            if (m_spawned.Contains(child.gameObject)) continue;
            if (child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    void OnDisable() => Unbind();
    void OnDestroy() => Unbind();
}
