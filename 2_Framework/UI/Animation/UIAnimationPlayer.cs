using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// UIScreenBase 可选入场表现；不写 Animator，避免 Canvas Rebuild。
/// </summary>
public static class UIAnimationPlayer
{
    public static IEnumerator PlayEnter(
        UITransitionPresetSO preset,
        RectTransform root,
        CanvasGroup canvasGroup,
        Action onComplete)
    {
        return PlayCore(preset, root, canvasGroup, false, onComplete);
    }

    public static IEnumerator PlayExit(
        UITransitionPresetSO preset,
        RectTransform root,
        CanvasGroup canvasGroup,
        Action onComplete)
    {
        return PlayCore(preset, root, canvasGroup, true, onComplete);
    }

    static IEnumerator PlayCore(
        UITransitionPresetSO preset,
        RectTransform root,
        CanvasGroup canvasGroup,
        bool reverse,
        Action onComplete)
    {
        if (preset == null || preset.kind == UITransitionKind.None || preset.durationSeconds <= 0f || root == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        var dur = preset.durationSeconds;
        var cg = canvasGroup;

        var createdCanvasGroup = false;
        if (cg == null && preset.kind == UITransitionKind.Fade)
        {
            cg = root.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = root.gameObject.AddComponent<CanvasGroup>();
                createdCanvasGroup = true;
            }
        }

        var t = 0f;
        Vector3 scaleEnd = root.localScale;
        Vector2 anchoredEnd = root.anchoredPosition;
        var slideOffset = ResolveSlideOffset(preset);

        switch (preset.kind)
        {
            case UITransitionKind.Fade:
                if (cg != null)
                {
                    cg.alpha = reverse ? 1f : preset.fadeFromAlpha;
                }

                break;
            case UITransitionKind.ScaleIn:
                root.localScale = reverse ? scaleEnd : scaleEnd * preset.scaleFrom;
                break;
            case UITransitionKind.Slide:
            case UITransitionKind.SlideLeft:
            case UITransitionKind.SlideRight:
            case UITransitionKind.SlideUp:
            case UITransitionKind.SlideDown:
                root.anchoredPosition = reverse ? anchoredEnd : anchoredEnd + slideOffset;
                break;
        }

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            var u = Mathf.Clamp01(t / dur);
            var k = preset.easing != null ? preset.easing.Evaluate(u) : u;

            switch (preset.kind)
            {
                case UITransitionKind.Fade:
                    if (cg != null)
                    {
                        cg.alpha = reverse
                            ? Mathf.Lerp(1f, preset.fadeFromAlpha, k)
                            : Mathf.Lerp(preset.fadeFromAlpha, 1f, k);
                    }

                    break;
                case UITransitionKind.ScaleIn:
                    root.localScale = reverse
                        ? Vector3.Lerp(scaleEnd, scaleEnd * preset.scaleFrom, k)
                        : Vector3.Lerp(scaleEnd * preset.scaleFrom, scaleEnd, k);
                    break;
                case UITransitionKind.Slide:
                case UITransitionKind.SlideLeft:
                case UITransitionKind.SlideRight:
                case UITransitionKind.SlideUp:
                case UITransitionKind.SlideDown:
                    root.anchoredPosition = reverse
                        ? Vector2.Lerp(anchoredEnd, anchoredEnd + slideOffset, k)
                        : Vector2.Lerp(anchoredEnd + slideOffset, anchoredEnd, k);
                    break;
            }

            yield return null;
        }

        if (cg != null)
        {
            cg.alpha = reverse ? preset.fadeFromAlpha : 1f;
        }

        root.localScale = reverse ? scaleEnd * preset.scaleFrom : scaleEnd;
        root.anchoredPosition = reverse ? anchoredEnd + slideOffset : anchoredEnd;
        onComplete?.Invoke();

        if (createdCanvasGroup && cg != null)
        {
            UnityEngine.Object.Destroy(cg);
        }
    }

    static Vector2 ResolveSlideOffset(UITransitionPresetSO preset)
    {
        if (preset == null)
        {
            return Vector2.zero;
        }

        var x = Mathf.Abs(preset.slideOffset.x);
        var y = Mathf.Abs(preset.slideOffset.y);
        switch (preset.kind)
        {
            case UITransitionKind.SlideLeft:
                return new Vector2(-Mathf.Max(1f, x), 0f);
            case UITransitionKind.SlideRight:
                return new Vector2(Mathf.Max(1f, x), 0f);
            case UITransitionKind.SlideUp:
                return new Vector2(0f, Mathf.Max(1f, y <= 0f ? x : y));
            case UITransitionKind.SlideDown:
                return new Vector2(0f, -Mathf.Max(1f, y <= 0f ? x : y));
            default:
                return preset.slideOffset;
        }
    }
}
