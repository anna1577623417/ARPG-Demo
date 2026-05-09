using System;
using UnityEngine;

/// <summary>
/// 可选覆盖：GameplayIntentKind -> SkillSlotType。
/// 未命中时回退到 SkillSystem 内置默认映射。
/// </summary>
[CreateAssetMenu(menuName = "Skill/Intent Slot Map", fileName = "IntentSkillSlotMap")]
public class IntentSkillSlotMapSO : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public GameplayIntentKind intent;
        public SkillSlotType slot;
    }

    public Entry[] entries;

    public bool TryResolve(GameplayIntentKind intent, out SkillSlotType slot)
    {
        if (entries != null)
        {
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].intent == intent)
                {
                    slot = entries[i].slot;
                    return true;
                }
            }
        }

        slot = default;
        return false;
    }
}
