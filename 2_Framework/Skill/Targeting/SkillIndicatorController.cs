using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 技能范围指示与落点采样（桌面：鼠标俯视射线与地面/高度平面）。
/// Presentation 替换为预制体缩放 / Decal 可与本组件并存。
/// </summary>
public sealed class SkillIndicatorController : MonoBehaviour
{
    [SerializeField] GameObject areaDiscPrefab;

    Transform m_discInst;
    bool m_hasExplicitPoint;

    [SerializeField] float aimYawOffsetDegrees;

    Vector3 _explicitWorld;

    public bool HasManualPlacement => m_hasExplicitPoint;

    /// <summary>由外部输入（确认键）写入落点。</summary>
    public void SetExplicitWorldPlacement(Vector3 world)
    {
        _explicitWorld = world;
        m_hasExplicitPoint = true;
    }

    public void ClearManualPlacement()
    {
        m_hasExplicitPoint = false;
    }

    public bool TrySampleAreaPlacement(Player caster, float maxRange, out Vector3 worldPoint)
    {
        worldPoint = default;

        if (m_hasExplicitPoint)
        {
            worldPoint = _explicitWorld;
            return true;
        }

        if (Camera.main == null || caster == null)
        {
            return false;
        }

        Vector2 screen = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : (Vector2)UnityEngine.Input.mousePosition;

        var ray = Camera.main.ScreenPointToRay(screen);

        var plane = new Plane(Vector3.up, new Vector3(0f, caster.Position.y, 0f));
        if (!plane.Raycast(ray, out var enter))
        {
            return false;
        }

        var p = ray.GetPoint(enter);
        var to = Vector3.ProjectOnPlane(p - caster.Position, Vector3.up);
        if (to.sqrMagnitude > maxRange * maxRange && maxRange > 0.01f)
        {
            to = to.normalized * maxRange;
        }

        var yawRot = Quaternion.AngleAxis(aimYawOffsetDegrees, Vector3.up);
        worldPoint = caster.Position + yawRot * to;
        return true;
    }

    /// <summary>占位：运行时激活圆盘缩放随 <paramref name="radius"/>。</summary>
    public void PresentAreaPreview(float radius, Vector3 groundPoint)
    {
        if (areaDiscPrefab == null)
        {
            return;
        }

        if (m_discInst == null)
        {
            m_discInst = Instantiate(areaDiscPrefab, transform).transform;
        }

        m_discInst.gameObject.SetActive(true);
        m_discInst.position = groundPoint;
        var s = Mathf.Max(0.1f, radius * 2f);
        m_discInst.localScale = new Vector3(s, 1f, s);
    }

    public void HidePreview()
    {
        if (m_discInst != null)
        {
            m_discInst.gameObject.SetActive(false);
        }
    }
}
