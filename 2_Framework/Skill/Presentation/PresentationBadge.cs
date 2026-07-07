/// <summary>
/// HUD 角标数据 — Widget 只读渲染，不含战斗语义。
/// </summary>
public readonly struct PresentationBadge
{
    public static readonly PresentationBadge Hidden = default;

    public string Text { get; }
    public int Number { get; }

    public bool IsVisible => !string.IsNullOrEmpty(Text) || Number >= 0;

    public PresentationBadge(string text, int number = -1)
    {
        Text = text ?? string.Empty;
        Number = number;
    }
}
