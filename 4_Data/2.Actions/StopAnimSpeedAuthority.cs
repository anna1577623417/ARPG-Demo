/// <summary>238.1 — Stop Clip 基准播放倍率的权威来源。</summary>
public enum StopAnimSpeedAuthority : byte
{
    /// <summary>兼容模式下跟随 Action；LegacyLease 仍保留旧尾段/墙钟解析。</summary>
    InheritAction = 0,

    /// <summary>用已确定的 Stop 有效时长反算 Clip 窗口倍率。</summary>
    AutoFitEffectiveDuration = 1,

    /// <summary>使用 Stop 子配置中的固定倍率。</summary>
    FixedOverride = 2,
}
