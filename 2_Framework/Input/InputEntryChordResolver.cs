using UnityEngine;

/// <summary>
/// 入口组合键解析（Shift+LM 等）— 在 <see cref="InputSemanticResolver"/> 松开主键时检测修饰槽位。
/// 修饰键列表可扩展；当前支持 Shift 作为 LM/RM 的 Chord 修饰。
/// </summary>
public static class InputEntryChordResolver
{
    /// <summary>
    /// 主槽位触发瞬间是否有修饰键按住。
    /// modifier 为具体修饰槽位；无修饰时返回 false（modifier 置 <see cref="SkillEntrySlot.Any"/>）。
    /// </summary>
    public static bool TryResolveModifierAtTrigger(Player player, SkillEntrySlot primarySlot, out SkillEntrySlot modifier)
    {
        modifier = SkillEntrySlot.Any;
        if (player?.InputReader == null)
        {
            return false;
        }

        if (primarySlot != SkillEntrySlot.LM && primarySlot != SkillEntrySlot.RM)
        {
            return false;
        }

        if (player.InputReader.IsSkillEntryHeld(SkillEntrySlot.Shift))
        {
            modifier = SkillEntrySlot.Shift;
            return true;
        }

        return false;
    }
}
