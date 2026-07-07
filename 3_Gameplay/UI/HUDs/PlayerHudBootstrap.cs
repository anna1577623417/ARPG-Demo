using UnityEngine;



/// <summary>

/// 玩家 HUD 装配桥（v4.3）：把 <see cref="PlayerManager"/> 的"当前在场角色"推送给场景内的 Presenter，

/// 替代未启用的 <see cref="PlayerStatusHud"/> + UIRoot HUD Registry 路径。

///

/// ═══ 用法 ═══

///

/// 把本组件挂在 HUD 根节点（如 1_TopLeftHUD 或 ResourceGroup），Inspector 拖入：

///   • <see cref="playerManager"/> = 场景里负责实例化角色的 PlayerManager

///   • <see cref="resourcePresenter"/> / <see cref="skillBarRoutePresenter"/> / <see cref="buffStripPresenter"/>

///     （任意为空都允许，仅绑定非空者）

///

/// ═══ 行为 ═══

///

/// · OnEnable 订阅 <see cref="PlayerManager.ActivePlayerChanged"/>，并立即对当前在场角色 Bind 一次

///   （避免 PlayerManager 已经在 Awake/Start 阶段完成生成、HUD 错过事件）。

/// · 队伍切换时自动 Unbind 旧 Player → Bind 新 Player。

/// · OnDisable 解订阅 + Unbind 全部 Presenter（铁规：禁止内存泄漏）。

/// </summary>

[AddComponentMenu("GameMain/UI/Player HUD Bootstrap")]

[DefaultExecutionOrder(-50)]

public sealed class PlayerHudBootstrap : MonoBehaviour

{

    [Header("Source")]

    [Tooltip("场景里负责实例化在场角色的 PlayerManager。运行时通过其 ActivePlayerChanged 事件感知队伍切换。")]

    [SerializeField] PlayerManager playerManager;



    [Header("Presenters (任意可空)")]

    [Tooltip("HP / Stamina / MP 等资源条 Presenter。")]

    [SerializeField] PlayerHUDPresenter resourcePresenter;



    [Tooltip("211.3 D1 — Ver4.6+ Route 技能栏：绑定 IRouteRuntimeHandle。")]

    [SerializeField] SkillBarRoutePresenter skillBarRoutePresenter;



    [Tooltip("Buff 条 Presenter。")]

    [SerializeField] BuffStripPresenter buffStripPresenter;



    [Tooltip("可选：Route 运行时叠层（需 Tools/GameMain/Debug Settings → Skill Route Graph）。")]

    [SerializeField] RouteHudDebugOverlay routeDebugOverlay;



    Player m_currentPlayer;



    void OnEnable()

    {

        if (skillBarRoutePresenter == null)

        {

            skillBarRoutePresenter = GetComponentInChildren<SkillBarRoutePresenter>(true);

        }



        if (skillBarRoutePresenter == null)

        {

#pragma warning disable CS0618

            var legacy = GetComponentInChildren<SkillEntryBarPresenter>(true);

#pragma warning restore CS0618

            if (legacy != null)

            {

                Debug.LogWarning(

                    "[PlayerHudBootstrap] 检测到 legacy SkillEntryBarPresenter；请换为 SkillBarRoutePresenter（211.3 D1）。",

                    legacy);

            }

        }



        if (playerManager == null)

        {

            Debug.LogWarning(

                "[PlayerHudBootstrap] 未指派 PlayerManager；HUD 将不会绑定到任何 Player。",

                this);

            return;

        }



        playerManager.ActivePlayerChanged += HandleActivePlayerChanged;



        if (playerManager.ActivePlayer != null)

        {

            HandleActivePlayerChanged(playerManager.ActivePlayer);

        }

    }



    void OnDisable()

    {

        if (playerManager != null)

        {

            playerManager.ActivePlayerChanged -= HandleActivePlayerChanged;

        }



        UnbindAll();

        m_currentPlayer = null;

    }



    void HandleActivePlayerChanged(Player player)

    {

        if (m_currentPlayer == player)

        {

            return;

        }



        UnbindAll();

        m_currentPlayer = player;



        if (player == null)

        {

            return;

        }



        if (skillBarRoutePresenter == null && GameMainDebugSettings.SkillRouteGraph)

        {

            Debug.LogWarning(

                "[PlayerHudBootstrap] skillBarRoutePresenter 未指派 → RouteWidget 不会在运行时生成。",

                this);

        }



        if (resourcePresenter != null) resourcePresenter.Bind(player);

        HudBugProbe.LogBootstrapBind(
            player,
            skillBarRoutePresenter != null ? nameof(SkillBarRoutePresenter) : null,
            skillBarRoutePresenter == null);
        if (skillBarRoutePresenter != null) skillBarRoutePresenter.Bind(player);

        if (buffStripPresenter != null) buffStripPresenter.Bind(player);

        if (routeDebugOverlay != null) routeDebugOverlay.Bind(player);

    }



    void UnbindAll()

    {

        if (resourcePresenter != null) resourcePresenter.Unbind();

        if (skillBarRoutePresenter != null) skillBarRoutePresenter.Unbind();

        if (buffStripPresenter != null) buffStripPresenter.Unbind();

        if (routeDebugOverlay != null) routeDebugOverlay.Unbind();

    }

}

