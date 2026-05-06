using UnityEngine;

/// <summary>UIScreenBase 可选入场过渡（零长度或 None = 瞬时）。</summary>
[CreateAssetMenu(menuName = "GameMain/UI/Transition Preset", fileName = "UITransitionPreset")]
public class UITransitionPresetSO : ScriptableObject
{
    public UITransitionKind kind = UITransitionKind.None;

    [Min(0f)] public float durationSeconds = 0.25f;

    public AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Fade 起始 Alpha（目标为 1）。")]
    [Range(0f, 1f)] public float fadeFromAlpha = 0f;

    [Tooltip("Scale 起始乘数（目标为 1）。")]
    [Min(0.01f)] public float scaleFrom = 0.92f;

    [Tooltip("Slide 起始位移（Enter）或结束位移（Exit）。")]
    public Vector2 slideOffset = new Vector2(240f, 0f);
}

public enum UITransitionKind
{
    None = 0,
    Fade = 1,
    ScaleIn = 2,
    Slide = 3,
    SlideLeft = 4,
    SlideRight = 5,
    SlideUp = 6,
    SlideDown = 7
}
