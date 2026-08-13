using System.Collections.Generic;
using UnityEngine;

public partial class ActionDataSO
{
    [Header("Action Contact Track (223.4)")]
    [Tooltip("Action Window 驱动的唯一 Hitbox 作者入口。")]
    public List<ContactEvent> ContactEvents = new List<ContactEvent>();

    [Header("Window Authoring (224.1)")]
    [Tooltip("WindowIdBoundContactV1：Contact 时间只经 WindowId 解析。旧资产保持 LegacyRangeOnContact。")]
    public ActionWindowAuthoringVersion WindowAuthoringVersion = ActionWindowAuthoringVersion.LegacyRangeOnContact;
}
