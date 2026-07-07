using UnityEngine;

/// <summary>
/// 技能描述资产（D5）— 与 Route/Group 展示字段解耦；Tooltip 入口 214 OPEN。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Skills/Skill Description", fileName = "Desc_")]
public sealed class SkillDescriptionSO : ScriptableObject
{
    [TextArea(3, 8)]
    [SerializeField] string shortDescription;

    [TextArea(5, 20)]
    [SerializeField] string fullDescription;

    public string ShortDescription => shortDescription ?? string.Empty;
    public string FullDescription => fullDescription ?? string.Empty;
}
