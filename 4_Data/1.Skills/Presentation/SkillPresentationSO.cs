using UnityEngine;

/// <summary>
/// 可选展示资产包 — Route 可引用以覆盖 Icon/Name/Description（214 设施；未强制迁移）。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Skills/Skill Presentation", fileName = "Presentation_")]
public sealed class SkillPresentationSO : ScriptableObject
{
    [SerializeField] Sprite icon;
    [SerializeField] string displayName;
    [SerializeField] SkillDescriptionSO description;

    public Sprite Icon => icon;
    public string DisplayName => displayName ?? string.Empty;
    public SkillDescriptionSO Description => description;
}
