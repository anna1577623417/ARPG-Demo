using UnityEngine;

[CreateAssetMenu(menuName = "GameMain/UI/Damage Text Settings", fileName = "DamageTextSettings")]
public class DamageTextSettingsSO : ScriptableObject
{
    [Header("CPU")]
    [Min(1)] public int cpuPreloadCount = 32;

    [Header("GPU (optional)")]
    public bool enableGpuPath;
    [Min(1)] public int gpuMaxInstances = 1024;

    [Header("Space")]
    public DamageTextSpaceMode spaceMode = DamageTextSpaceMode.ScreenSpace;

    [Tooltip("ScreenSpace：以屏幕像素为单位。WorldSpace：以世界坐标为单位。")]
    [Min(0.01f)] public float glyphSize = 22f;
    [Min(0f)] public float glyphSpacing = 14f;

    [Header("Digits Atlas (GPU path)")]
    public Texture2D digitsAtlas;
    [Min(1)] public int atlasColumns = 10;
    [Min(1)] public int atlasRows = 1;

    [Header("Crit Style")]
    [Min(0.1f)] public float critSizeMultiplier = 1.2f;
    [Min(0f)] public float critRiseMultiplier = 1.3f;
    public Color critTint = new Color(1f, 0.95f, 0.45f, 1f);

    [Header("Lifetime")]
    [Min(0.05f)] public float defaultLifeSeconds = 0.85f;

    [Header("Auto Switch")]
    [Min(1)] public int switchUpThreshold = 220;
    [Min(1)] public int switchDownThreshold = 110;
}

public enum DamageTextSpaceMode
{
    ScreenSpace = 0,
    WorldSpace = 1
}
