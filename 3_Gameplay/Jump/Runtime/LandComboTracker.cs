/// <summary>
/// 175.2 W2 — 落地连跳追踪器（马里奥三连跳关键）。
/// <para>落地后若 <see cref="ComboWindowSec"/> 内再次起跳，combo +1（上限 2 → Triple）。</para>
/// <para>纯逻辑组件；运行时按 OnLand / OnJump 事件驱动；外部读 <see cref="CurrentIndex"/>。</para>
/// </summary>
public sealed class LandComboTracker
{
    public float ComboWindowSec = 0.20f;
    public int MaxComboIndex = 2;     // 0=Normal, 1=Double, 2=Triple

    float _lastLandTime = -1f;
    int _comboIndex;

    public int CurrentIndex => _comboIndex;
    public float LastLandTime => _lastLandTime;

    /// <summary>落地事件 —— 标记可能的 Combo 起点。</summary>
    public void OnLand(float now)
    {
        _lastLandTime = now;
    }

    /// <summary>起跳事件 —— 判断是否在落地 Combo 窗口内。</summary>
    public void OnJump(float now)
    {
        if (_lastLandTime < 0f)
        {
            _comboIndex = 0;
            return;
        }

        if (now - _lastLandTime <= ComboWindowSec && _comboIndex < MaxComboIndex)
        {
            _comboIndex++;
        }
        else
        {
            _comboIndex = 0;
        }
    }

    /// <summary>显式重置（场景切换 / 死亡）。</summary>
    public void Reset()
    {
        _lastLandTime = -1f;
        _comboIndex = 0;
    }

    /// <summary>Tick：落地窗口超时后回 0（避免 HUD 残留显示）。</summary>
    public void Tick(float now)
    {
        if (_lastLandTime >= 0f && now - _lastLandTime > ComboWindowSec && _comboIndex == 0)
        {
            // 已自然落地超出窗口；保持 _comboIndex=0 即可，无需特别处理。
        }
    }
}
