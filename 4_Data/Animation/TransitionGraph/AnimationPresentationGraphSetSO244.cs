using UnityEngine;

/// <summary>244.8 L3 — Explicit authoring boundary tying a graph, domain and default policy profile together.</summary>
[CreateAssetMenu(menuName = "GameMain/Animation/Presentation Graph Set", fileName = "AnimationPresentationGraphSet_")]
public sealed class AnimationPresentationGraphSetSO244 : ScriptableObject
{
    [SerializeField] string setId;
    [SerializeField] AnimTransitionGraphDomain domain = AnimTransitionGraphDomain.Any;
    [SerializeField] AnimTransitionAuthoringGraph graph;
    [SerializeField] AnimationTransitionPolicyProfileSO244 defaultProfile;

    public string SetId => setId ?? string.Empty;
    public AnimTransitionGraphDomain Domain => domain;
    public AnimTransitionAuthoringGraph Graph => graph;
    public AnimationTransitionPolicyProfileSO244 DefaultProfile => defaultProfile;
    public bool IsValid => !string.IsNullOrEmpty(SetId) && graph != null;

    void OnValidate()
    {
        if (setId == null) setId = string.Empty;
    }
}
