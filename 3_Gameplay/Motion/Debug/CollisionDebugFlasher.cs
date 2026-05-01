using System.Collections;
using UnityEngine;

/// <summary>
/// 碰撞反馈高亮（MaterialPropertyBlock，不 Instantiate 材质）。
/// Why: Scene / Game 视图快速确认 CapsuleSweep 碰到了哪块几何；挂载在障碍物或地面预制体上。
/// </summary>
[DisallowMultipleComponent]
public sealed class CollisionDebugFlasher : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Renderer[] m_renderers;
    private MaterialPropertyBlock m_pb;
    private Coroutine m_flashRoutine;
    private bool m_haveStoredTint;
    private Color m_originalTint = Color.white;
    private bool m_useBaseColor;

    private void Awake()
    {
        CacheRenderers();
    }

    private void CacheRenderers()
    {
        m_renderers = GetComponentsInChildren<Renderer>(true);
        m_pb ??= new MaterialPropertyBlock();
    }

    /// <summary>瞬时变色再插值回到记录色。</summary>
    public void Flash(Color flashTint, float holdSeconds = 0.12f, float fadeSeconds = 0.18f)
    {
        CacheRenderers();
        if (m_renderers == null || m_renderers.Length == 0)
        {
            return;
        }

        if (m_flashRoutine != null)
        {
            StopCoroutine(m_flashRoutine);
        }

        CaptureOriginalTintOnce();
        m_flashRoutine = StartCoroutine(FlashRoutine(flashTint, holdSeconds, fadeSeconds));
    }

    private void CaptureOriginalTintOnce()
    {
        if (m_haveStoredTint)
        {
            return;
        }

        foreach (var r in m_renderers)
        {
            if (r == null || r.sharedMaterial == null)
            {
                continue;
            }

            if (r.sharedMaterial.HasProperty(BaseColorId))
            {
                m_useBaseColor = true;
                m_originalTint = r.sharedMaterial.GetColor(BaseColorId);
                break;
            }

            if (r.sharedMaterial.HasProperty(ColorId))
            {
                m_originalTint = r.sharedMaterial.GetColor(ColorId);
                break;
            }
        }

        m_haveStoredTint = true;
    }

    private IEnumerator FlashRoutine(Color flashTint, float hold, float fade)
    {
        ApplyTintAll(flashTint);
        yield return new WaitForSeconds(Mathf.Max(0f, hold));

        var t = 0f;
        var denom = Mathf.Max(1e-4f, fade);
        while (t < 1f)
        {
            t += Time.deltaTime / denom;
            ApplyTintAll(Color.Lerp(flashTint, m_originalTint, Mathf.Clamp01(t)));
            yield return null;
        }

        ApplyTintAll(m_originalTint);
        m_flashRoutine = null;
    }

    private void ApplyTintAll(Color c)
    {
        foreach (var r in m_renderers)
        {
            if (r == null)
            {
                continue;
            }

            r.GetPropertyBlock(m_pb);
            if (m_useBaseColor || (r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId)))
            {
                m_pb.SetColor(BaseColorId, c);
            }
            else
            {
                m_pb.SetColor(ColorId, c);
            }

            r.SetPropertyBlock(m_pb);
        }
    }

    /// <summary>无障碍物上挂脚本时按需添加（仅在调试开关启用时调用）。</summary>
    public static CollisionDebugFlasher GetOrCreate(Collider c)
    {
        if (c == null)
        {
            return null;
        }

        var f = c.GetComponent<CollisionDebugFlasher>();
        if (f != null)
        {
            return f;
        }

        return c.gameObject.AddComponent<CollisionDebugFlasher>();
    }
}
