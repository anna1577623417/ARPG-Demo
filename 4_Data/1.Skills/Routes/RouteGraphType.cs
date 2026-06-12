/// <summary>
/// Combat Graph 可配置的 Route 类别（153.2）。
/// 与 <see cref="RouteKind"/> 解耦：仅表达「能否进图 + Flow/Interrupt 资格」。
/// </summary>
public enum RouteGraphType : byte
{
    /// <summary>Combo / Charge 等不可进图。</summary>
    Unsupported = 0,

    /// <summary>NormalRoute 单 Stage → Flow 边目标。</summary>
    SingleAction = 1,

    /// <summary>MultiStageRoute → 仅 Interrupt(IN→End)。</summary>
    MultiStage = 2,

    /// <summary>DerivativeRoute → 解析唯一入口 Action，可做 Flow 目标。</summary>
    Derived = 3,
}
