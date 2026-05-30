> 最后更新：2026-05-19 15:30
> 产出时间：2026-05-19 14:00

# Combo1 + Combo2 虚拟链（A1-B1-A2-B2-C2）

## 设计（与你的配表意图一致）

| 概念 | 资产 | 链 |
|------|------|-----|
| **Combo1** | `Route_Combo_LM` | A1 → B1（无容器 CD） |
| **Combo2** | `Route_Combo_Extended_LM` | A2 → B2 → C2（独立 NormalRoute SO，可与 Combo1 **同动作**） |
| **虚拟链** | `Entry_LM` 上两容器 | **A1 → B1 → A2 → B2 → C2**（5 段） |

- 新开一轮：**永远**从 Combo1 起手（`always_primary`）。
- B1 **自然结束**后：若 Combo2 **CD 已好**，武装 `COMBO2 CONTINUATION ARMED`；窗内按 LM 进入 **整段 Combo2**（不是只打 C）。
- Combo2 未好 / 窗外 / Combo1 中断：**不**接 Combo2，下轮仍 A1-B1。

## 配表要点

1. **Combo1** `comboChain`：`Route_Normal_Combo_A`、`Route_Normal_Combo_B`（或你的 A1/B1 SO）。
2. **Combo2** `comboChain`：**三条**子 Route（A2/B2/C2），勿只填 [C]。
3. **Entry_LM**：`comboRoute` + `extendedComboRoute`；双链衔接见下节。
4. 同动作不同 Route：复制 NormalRoute + Stage + Action，挂到对应容器即可。

## 双链衔接窗口（Entry_LM Inspector）

在 **Skill Entry Definition** 上 **Combo1 末段(B1) → Combo2 起手(A2) 双链衔接** 分组：

| 字段 | 作用 | 0 的含义 |
|------|------|----------|
| **Extended Handoff Window Seconds** | B1 自然结束后，**总共**还能等多久按 LM 接 Combo2 | 用 `Route_Combo_LM` 的 **Combo Session Reset Time** |
| **Extended Handoff Min Gap** | B1 结束后，**最早**何时按键才接 A2（防连点/过早） | 不限制 |
| **Extended Handoff Max Gap** | B1 结束后，**最晚**何时必须按下才接 A2 | 与 **衔接总窗口** 相同 |

**与 Combo1 内 A1→B1 的区别**

- A1→B1：只改 `Route_Combo_LM` 的 **Combo Session Reset Time** + **Combo Transitions**。
- B1→A2：只改 **Entry** 上表三字段（不必和 Combo1 Session 窗绑死）。

**推荐起步值（`Entry_LM` 已写入）**

- Window = `1.2`（可与 Combo1 Session 一致，也可单独加长，如 `1.8` 给玩家更多接 Combo2 时间）
- Min Gap = `0`
- Max Gap = `0`（跟随 Window）

**Combo2 内部（A2→B2→C2）**

- 仍用 `Route_Combo_Extended_LM` 的 **Combo Transitions**（与单链 Combo 相同）。

**验收 Log**

- B1 结束：`COMBO2 CONTINUATION ARMED … handoffWin=1.20s min=0.00s max=1.20s remain=…`
- 超时：`window expired (1.20s after last segment end)`（数字应等于你设的 Window，而非误用 Combo2 CD）

## 运行时映射

- Resolver / Intent 使用 **virtualIdx**（0…4）。
- 进入 Combo2 容器时 **pickIdx = virtualIdx - Combo1.ChainLength**（例：virtual 2 → pick 0 = A2）。
- Session 内 `CommitAdvance` 用容器内 pickIdx；序号校验用 virtualIdx。

## 验收 Log（`Player.DebugSkillRoute`）

1. 空闲 Tap：`PICK … reason=new_chain_primary` → A1。
2. 窗内：`virtualIdx=1` → B1。
3. B1 结束：`COMBO2 CONTINUATION ARMED … (virtual+3)`；`ConfigureSlot … chain=5`。
4. 窗内：`virtualIdx=2` → `pickIdx=0` → A2；`HANDOFF Combo1→Combo2 … (A2)`；`SESSION START … pick=0 virtual=2`。
5. 继续：`virtualIdx=3,4` → B2、C2；`OnLastSubRouteEnd` 仅 **Combo2 容器** CD。
6. Extended CD 中：B1 后 `EXT HANDOFF skip`；循环仅 A1-B1。

## 与旧日志的差异（修前）

- 错误：`pickIdx=2 virtualIdx=2` 直接打 C（Extended 只配 1 段或 `ResolveExtendedPickIndex` 用错 virtualIdx）。
- 正确：`pickIdx=0 virtualIdx=2` 从 A2 起打满 Combo2。
