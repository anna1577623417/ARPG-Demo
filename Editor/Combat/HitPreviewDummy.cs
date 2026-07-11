#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 216.3 M6 L2 — 命中预览假人变色（灰/蓝/绿/红）。
/// <para>Editor 侧几何 Overlap 模拟，纯 Handles，不改运行时逻辑。</para>
/// </summary>
public static class HitPreviewDummy
{
    public enum State : byte
    {
        /// <summary>未命中 / 无 Active。</summary>
        Idle = 0,

        /// <summary>Active 开着但未碰到（观察中）。</summary>
        Armed = 1,

        /// <summary>当前 Shape 与假人相交（命中）。</summary>
        Hit = 2,

        /// <summary>假人中心深入 Shape（强接触）。</summary>
        Contact = 3,
    }

    static readonly Color ColIdle = new Color(0.55f, 0.55f, 0.55f, 0.85f);
    static readonly Color ColArmed = new Color(0.35f, 0.65f, 1f, 0.95f);
    static readonly Color ColHit = new Color(0.25f, 0.95f, 0.4f, 0.95f);
    static readonly Color ColContact = new Color(1f, 0.25f, 0.25f, 1f);

    static HitPreviewDummyHost[] s_hosts;
    static double s_lastFindTime;

    /// <summary>对场景内所有 <see cref="HitPreviewDummyHost"/> 评估并画 Handles。</summary>
    public static void EvaluateAndDraw(
        ActionDataSO action,
        Transform anchor,
        Vector3 planarForward,
        Vector3 anchorOrigin,
        float playheadNt)
    {
        var hosts = FindHosts();
        if (hosts == null || hosts.Length == 0)
        {
            return;
        }

        for (var i = 0; i < hosts.Length; i++)
        {
            var host = hosts[i];
            if (host == null)
            {
                continue;
            }

            var state = EvaluateHost(host, action, anchor, planarForward, anchorOrigin, playheadNt);
            DrawHost(host, state);
        }
    }

    static HitPreviewDummyHost[] FindHosts()
    {
        // SceneView 每帧调用：节流 FindObjects，避免卡顿。
        if (s_hosts == null || EditorApplication.timeSinceStartup - s_lastFindTime > 0.5)
        {
            s_hosts = Object.FindObjectsByType<HitPreviewDummyHost>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            s_lastFindTime = EditorApplication.timeSinceStartup;
        }

        return s_hosts;
    }

    static State EvaluateHost(
        HitPreviewDummyHost host,
        ActionDataSO action,
        Transform anchor,
        Vector3 planarForward,
        Vector3 anchorOrigin,
        float playheadNt)
    {
        if (action?.AttackClips == null || anchor == null)
        {
            return State.Idle;
        }

        var dummyCenter = host.WorldCenter;
        var dummyRadius = Mathf.Max(0.05f, host.Radius);
        var anyArmed = false;
        var best = State.Idle;

        var clips = action.AttackClips;
        for (var i = 0; i < clips.Count; i++)
        {
            var clip = clips[i];
            var start = Mathf.Min(clip.ActiveStart, clip.ActiveEnd);
            var end = Mathf.Max(clip.ActiveStart, clip.ActiveEnd);
            if (playheadNt < start || playheadNt > end)
            {
                continue;
            }

            anyArmed = true;

            if (!CombatHitPreviewResolver.TryResolveHitClipWorldPose(
                    action, in clip, anchor, planarForward, anchorOrigin, playheadNt,
                    out var pos, out var rot))
            {
                continue;
            }

            var overlap = TestOverlap(in clip, pos, rot, action, anchor, planarForward, anchorOrigin, playheadNt,
                dummyCenter, dummyRadius, out var deep);
            if (!overlap)
            {
                continue;
            }

            var next = deep ? State.Contact : State.Hit;
            if (next > best)
            {
                best = next;
            }
        }

        if (best != State.Idle)
        {
            return best;
        }

        return anyArmed ? State.Armed : State.Idle;
    }

    static bool TestOverlap(
        in HitClip clip,
        Vector3 shapePos,
        Quaternion shapeRot,
        ActionDataSO action,
        Transform anchor,
        Vector3 planarForward,
        Vector3 anchorOrigin,
        float nt,
        Vector3 dummyCenter,
        float dummyRadius,
        out bool deepContact)
    {
        deepContact = false;

        if (clip.ShapeMode == HitShapeMode.WeaponTrace)
        {
            if (!CoverageDrawer.TrySampleSockets(
                    action, in clip, anchor, planarForward, anchorOrigin, nt,
                    out var tip, out var tipRadius))
            {
                return false;
            }

            var limit = tipRadius + dummyRadius;
            var distSq = (tip - dummyCenter).sqrMagnitude;
            if (distSq > limit * limit)
            {
                return false;
            }

            deepContact = distSq <= tipRadius * tipRadius;
            return true;
        }

        if (clip.Shape == null)
        {
            return false;
        }

        return TestShapeVsSphere(clip.Shape, shapePos, shapeRot, dummyCenter, dummyRadius, out deepContact);
    }

    static bool TestShapeVsSphere(
        HitShapeSO shape,
        Vector3 origin,
        Quaternion rotation,
        Vector3 sphereCenter,
        float sphereRadius,
        out bool deepContact)
    {
        deepContact = false;

        switch (shape)
        {
            case SphereShapeSO sph:
            {
                var r = Mathf.Max(0f, sph.radius);
                var dist = Vector3.Distance(origin, sphereCenter);
                if (dist > r + sphereRadius)
                {
                    return false;
                }

                deepContact = dist + sphereRadius * 0.25f <= r;
                return true;
            }

            case BoxShapeSO box:
            {
                var center = origin + rotation * box.offset;
                var local = Quaternion.Inverse(rotation) * (sphereCenter - center);
                var he = box.halfExtents;
                var closest = new Vector3(
                    Mathf.Clamp(local.x, -he.x, he.x),
                    Mathf.Clamp(local.y, -he.y, he.y),
                    Mathf.Clamp(local.z, -he.z, he.z));
                var delta = local - closest;
                if (delta.sqrMagnitude > sphereRadius * sphereRadius)
                {
                    return false;
                }

                deepContact = Mathf.Abs(local.x) <= he.x
                              && Mathf.Abs(local.y) <= he.y
                              && Mathf.Abs(local.z) <= he.z;
                return true;
            }

            case CapsuleShapeSO cap:
            {
                var axis = rotation * Vector3.up;
                var half = Mathf.Max(cap.height * 0.5f - cap.radius, 0f);
                var center = origin + rotation * cap.offset;
                var p0 = center + axis * half;
                var p1 = center - axis * half;
                var closest = ClosestPointOnSegment(sphereCenter, p0, p1);
                var dist = Vector3.Distance(sphereCenter, closest);
                var r = cap.radius;
                if (dist > r + sphereRadius)
                {
                    return false;
                }

                deepContact = dist + sphereRadius * 0.25f <= r;
                return true;
            }

            default:
            {
                // 未知 Shape：用 Reach 或默认 0.5m 球近似。
                var r = 0.5f;
                var dist = Vector3.Distance(origin, sphereCenter);
                return dist <= r + sphereRadius;
            }
        }
    }

    static Vector3 ClosestPointOnSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        var lenSq = ab.sqrMagnitude;
        if (lenSq < 1e-8f)
        {
            return a;
        }

        var t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lenSq);
        return a + ab * t;
    }

    static void DrawHost(HitPreviewDummyHost host, State state)
    {
        var color = state switch
        {
            State.Armed => ColArmed,
            State.Hit => ColHit,
            State.Contact => ColContact,
            _ => ColIdle,
        };

        var center = host.WorldCenter;
        var radius = Mathf.Max(0.05f, host.Radius);

        Handles.color = color;
        Handles.DrawWireDisc(center, Vector3.up, radius);
        Handles.DrawWireDisc(center, Vector3.right, radius);
        Handles.DrawWireDisc(center, Vector3.forward, radius);

        // 实心盘增强「变色」可读性（仍是 Handles）。
        Handles.color = new Color(color.r, color.g, color.b, 0.18f);
        Handles.DrawSolidDisc(center, Vector3.up, radius);

        Handles.color = color;
        var label = state switch
        {
            State.Armed => "Dummy · Armed",
            State.Hit => "Dummy · HIT",
            State.Contact => "Dummy · CONTACT",
            _ => "Dummy · Idle",
        };
        Handles.Label(center + Vector3.up * (radius + 0.15f), label);
    }
}
#endif
