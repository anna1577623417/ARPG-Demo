/// <summary>
/// 技能入口槽位（入口语义层 — 仅描述「玩家从哪一把钥匙开门」）。
///
/// ═══ 设计原则 ═══
///   · 与按键解耦：键位映射在 .inputactions + <see cref="InputReader"/> 完成。
///   · 与技能解耦：门由 <see cref="SkillEntryDefinition"/> 决定。
///   · 显式整型：下标 2 保留为空；SoA 表长度见 <see cref="SkillEntrySlots.TableSize"/>。
/// </summary>
public enum SkillEntrySlot : byte
{
    LM = 0,

    /// <summary>数字键 0 — InputAction <c>SlotAbility_18</c>。</summary>
    Key0 = 1,

    RM = 3,
    R = 4,
    Q = 5,
    Shift = 6,
    Space = 7,
    Key1 = 8,
    Key2 = 9,
    Key3 = 10,
    Key4 = 11,
    Key5 = 12,
    Key6 = 13,
    Key7 = 14,
    Key8 = 15,
    Key9 = 16,
}

/// <summary>槽位 SoA 表尺寸（非可配置槽位；勿写入 Loadout）。</summary>
public static class SkillEntrySlots
{
    /// <summary>InputReader / GameplayIntent 用数组长度（最大槽位整型 + 1）。</summary>
    public const int TableSize = 17;
}
