/// <summary>
/// 重力参与方式（环境力层）。
/// </summary>
public enum GravityMode : byte
{
    /// <summary>有 Y 曲线时与曲线叠加；无曲线时垂直仅重力 Vy。</summary>
    UseGravity = 0,

    /// <summary>挂起重力，垂直仅 Motion。</summary>
    SuspendGravity = 1,

    /// <summary>Motion.y + 重力 Vy。</summary>
    AdditiveGravity = 2,
}
