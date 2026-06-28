using UnityEngine;

[CreateAssetMenu(menuName = "GameMain/Skill/EdgeCondition/EventWindow", fileName = "EdgeCondition_EventWindow_")]
public sealed class EventWindowConditionSO : EdgeConditionSO
{
    public EventWindowTag Tag;

    [Tooltip("true = 窗口未激活时通过（必须没有此窗口）。")]
    public bool RequireInactive;

    public override bool Evaluate(in EdgeContext ctx)
    {
        var active = ctx.Windows != null && ctx.Windows.IsActive(Tag, ctx.TimeNow);
        return RequireInactive ? !active : active;
    }
}
