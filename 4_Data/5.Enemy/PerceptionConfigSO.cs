using UnityEngine;

[CreateAssetMenu(
    menuName = "GameMain/AI/Perception Config",
    fileName = "PerceptionConfig_")]
public sealed class PerceptionConfigSO : ScriptableObject
{
    [Header("Vision")]
    [SerializeField, Min(0f)] float detectionRadius = 8f;
    [SerializeField, Min(0.05f)] float scanInterval = 0.25f;
    [SerializeField, Range(0f, 360f)] float fieldOfView = 360f;
    [SerializeField] LayerMask targetLayers = ~0;

    [Header("Line Of Sight")]
    [SerializeField] bool requireLineOfSight;
    [SerializeField] LayerMask obstructionLayers = ~0;

    public float DetectionRadius => detectionRadius;
    public float ScanInterval => scanInterval;
    public float FieldOfView => fieldOfView;
    public LayerMask TargetLayers => targetLayers;
    public bool RequireLineOfSight => requireLineOfSight;
    public LayerMask ObstructionLayers => obstructionLayers;
}
