using UnityEngine;

[CreateAssetMenu(
    menuName = "GameMain/AI/Monster Personality",
    fileName = "MonsterPersonality_")]
public sealed class MonsterPersonalitySO : ScriptableObject
{
    [Header("Combat Distance")]
    [SerializeField, Min(0f)] float preferredDistance = 1.1f;
    [SerializeField, Min(0f)] float meleeRange = 1.5f;

    [Header("Decision")]
    [SerializeField] bool aggressive = true;

    [Header("Reserved Behavior Weights")]
    [SerializeField, Range(0f, 1f)] float retreatThreshold;
    [SerializeField, Range(0f, 1f)] float comboProbability;
    [SerializeField, Range(0f, 1f)] float counterAttackWeight;
    [SerializeField, Range(0f, 1f)] float randomness;

    public float PreferredDistance => preferredDistance;
    public float MeleeRange => meleeRange;
    public bool Aggressive => aggressive;
    public float RetreatThreshold => retreatThreshold;
    public float ComboProbability => comboProbability;
    public float CounterAttackWeight => counterAttackWeight;
    public float Randomness => randomness;
}
