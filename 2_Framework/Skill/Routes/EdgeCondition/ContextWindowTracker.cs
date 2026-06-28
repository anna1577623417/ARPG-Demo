using System.Collections.Generic;

/// <summary>185.2 — 最近事件窗口有效期追踪（运行期可变）。</summary>
public sealed class ContextWindowTracker
{
    readonly Dictionary<EventWindowTag, float> m_expires = new Dictionary<EventWindowTag, float>(8);

    public void Open(EventWindowTag tag, float durationSec, float now)
    {
        if (tag == EventWindowTag.None || durationSec <= 0f)
        {
            return;
        }

        m_expires[tag] = now + durationSec;
    }

    public bool IsActive(EventWindowTag tag, float now) =>
        tag != EventWindowTag.None
        && m_expires.TryGetValue(tag, out var expire)
        && now <= expire;

    public void Clear(EventWindowTag tag) => m_expires.Remove(tag);

    public void ClearAll() => m_expires.Clear();
}
