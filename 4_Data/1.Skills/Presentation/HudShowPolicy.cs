/// <summary>
/// HUD 显隐策略 — 214 起与 legacy <c>showOnHud</c> 并存；运行时以本枚举为准（Auto 回落 bool）。
/// </summary>
public enum HudShowPolicy : byte
{
    Auto = 0,
    ForceVisible = 1,
    ForceHidden = 2,
    DebugOnly = 3,
}
