using System.Text;
using UnityEngine;

/// <summary>
/// 216.3 M4 — 武器轨迹 Provider：Socket 采样 → 多点 Sweep →（调用方）产 HitResult。
/// <para>L1：采样 Log。L2：每 Socket 上一帧→当前帧 SphereCast（零位移补 OverlapSphere）。</para>
/// <para><b>不结算伤害</b>——只报告扫到的 Collider，交 AttackInstance → Resolver。</para>
/// </summary>
public sealed class WeaponTraceProvider
{
    public readonly struct SocketSample
    {
        public readonly string Name;
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly bool Valid;

        public SocketSample(string name, Vector3 position, float radius, bool valid)
        {
            Name = name;
            Position = position;
            Radius = radius;
            Valid = valid;
        }
    }

    public delegate void SocketHitHandler(Collider col, Vector3 point, Vector3 normal);

    readonly SocketSample[] _scratch;
    readonly Vector3[] _prevPos;
    readonly bool[] _prevValid;
    readonly StringBuilder _logBuilder = new StringBuilder(128);

    bool _hasPrevFrame;

    public WeaponTraceProvider(int maxSockets = 32)
    {
        var cap = Mathf.Max(4, maxSockets);
        _scratch = new SocketSample[cap];
        _prevPos = new Vector3[cap];
        _prevValid = new bool[cap];
    }

    public SocketSample[] Scratch => _scratch;

    /// <summary>开判时清空上一帧，避免跨 Active 幽灵轨迹。</summary>
    public void ResetHistory()
    {
        _hasPrevFrame = false;
    }

    /// <summary>
    /// 从 Source Animator 解析各 Socket 世界坐标，写入 <paramref name="results"/>。
    /// </summary>
    public int SampleSockets(Entity source, WeaponSocketSetSO set, SocketSample[] results)
    {
        if (source == null || set == null || set.Sockets == null || results == null)
        {
            return 0;
        }

        var anim = source.Animator;
        var written = 0;
        var max = Mathf.Min(set.Sockets.Length, results.Length);

        for (var i = 0; i < max; i++)
        {
            var def = set.Sockets[i];
            var name = string.IsNullOrEmpty(def.DebugName) ? $"s{i}" : def.DebugName;
            var radius = def.Radius > 0.01f ? def.Radius : 0.05f;

            if (TryResolveSocketWorld(anim, source.transform, in def, out var pos))
            {
                results[written++] = new SocketSample(name, pos, radius, true);
            }
            else
            {
                results[written++] = new SocketSample(name, default, radius, false);
            }
        }

        return written;
    }

    /// <summary>采样并打 <c>[Trace] SOCKET tip=(..) mid=(..)</c>。</summary>
    public int SampleSocketsAndLog(Entity source, WeaponSocketSetSO set)
    {
        var n = SampleSockets(source, set, _scratch);
        LogSocketSamples(n);
        return n;
    }

    /// <summary>仅采样并写入上一帧（Interval 未开窗时推进轨迹，不做 Cast）。</summary>
    public void AdvanceHistory(Entity source, WeaponSocketSetSO set)
    {
        if (source == null || set == null)
        {
            return;
        }

        var n = SampleSockets(source, set, _scratch);
        StorePrev(n);
        _hasPrevFrame = true;
    }

    /// <summary>
    /// 216.3 M4 L2 — 采样当前帧，对每 Socket 做 prev→curr SphereCast（零位移 OverlapSphere）。
    /// 命中经 <paramref name="onHit"/> 回调；返回物理命中次数（Policy 过滤前）。
    /// </summary>
    public int SweepSockets(
        Entity source,
        WeaponSocketSetSO set,
        int layerMask,
        RaycastHit[] hitScratch,
        Collider[] overlapScratch,
        SocketHitHandler onHit)
    {
        if (source == null || set == null || onHit == null)
        {
            return 0;
        }

        var n = SampleSockets(source, set, _scratch);
        LogSocketSamples(n);

        var physicsHits = 0;

        if (!_hasPrevFrame)
        {
            // 首帧：无轨迹，仅 Overlap 当前位置（贴身开判）。
            physicsHits += OverlapCurrentSockets(n, layerMask, overlapScratch, onHit);
            StorePrev(n);
            _hasPrevFrame = true;
            if (GameMainDebugSettings.CombatHit)
            {
                Debug.Log($"[Trace] SWEEP sockets={CountValidSockets(n)} hit={physicsHits} (first-frame overlap)");
            }

            return physicsHits;
        }

        var maxCast = hitScratch != null ? hitScratch.Length : 0;
        for (var i = 0; i < n; i++)
        {
            var cur = _scratch[i];
            if (!cur.Valid || !_prevValid[i])
            {
                continue;
            }

            var from = _prevPos[i];
            var to = cur.Position;
            var radius = cur.Radius;
            var delta = to - from;
            var dist = delta.magnitude;

            if (dist < 1e-4f)
            {
                if (overlapScratch == null || overlapScratch.Length == 0)
                {
                    continue;
                }

                var overlapN = Physics.OverlapSphereNonAlloc(
                    to,
                    radius,
                    overlapScratch,
                    layerMask,
                    QueryTriggerInteraction.Collide);
                for (var h = 0; h < overlapN; h++)
                {
                    var col = overlapScratch[h];
                    if (col == null)
                    {
                        continue;
                    }

                    var point = col.ClosestPoint(to);
                    var toOrigin = to - point;
                    var normal = toOrigin.sqrMagnitude > 1e-6f ? toOrigin.normalized : Vector3.up;
                    onHit(col, point, normal);
                    physicsHits++;
                }

                continue;
            }

            if (maxCast <= 0)
            {
                continue;
            }

            var dir = delta / dist;
            var castN = Physics.SphereCastNonAlloc(
                from,
                radius,
                dir,
                hitScratch,
                dist,
                layerMask,
                QueryTriggerInteraction.Collide);

            for (var h = 0; h < castN; h++)
            {
                ref var hit = ref hitScratch[h];
                if (hit.collider == null)
                {
                    continue;
                }

                onHit(hit.collider, hit.point, hit.normal);
                physicsHits++;
            }
        }

        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log($"[Trace] SWEEP sockets={CountValidSockets(n)} hit={physicsHits}");
        }

        StorePrev(n);
        return physicsHits;
    }

    void LogSocketSamples(int n)
    {
        if (n <= 0 || !GameMainDebugSettings.CombatHit)
        {
            return;
        }

        _logBuilder.Clear();
        _logBuilder.Append("[Trace] SOCKET");
        for (var i = 0; i < n; i++)
        {
            var s = _scratch[i];
            _logBuilder.Append(' ');
            _logBuilder.Append(s.Name);
            _logBuilder.Append('=');
            if (s.Valid)
            {
                _logBuilder.Append('(');
                _logBuilder.Append(s.Position.x.ToString("F2"));
                _logBuilder.Append(',');
                _logBuilder.Append(s.Position.y.ToString("F2"));
                _logBuilder.Append(',');
                _logBuilder.Append(s.Position.z.ToString("F2"));
                _logBuilder.Append(')');
            }
            else
            {
                _logBuilder.Append("INVALID");
            }
        }

        Debug.Log(_logBuilder.ToString());
    }

    int OverlapCurrentSockets(
        int n,
        int layerMask,
        Collider[] overlapScratch,
        SocketHitHandler onHit)
    {
        if (overlapScratch == null || overlapScratch.Length == 0)
        {
            return 0;
        }

        var physicsHits = 0;
        for (var i = 0; i < n; i++)
        {
            var cur = _scratch[i];
            if (!cur.Valid)
            {
                continue;
            }

            var overlapN = Physics.OverlapSphereNonAlloc(
                cur.Position,
                cur.Radius,
                overlapScratch,
                layerMask,
                QueryTriggerInteraction.Collide);
            for (var h = 0; h < overlapN; h++)
            {
                var col = overlapScratch[h];
                if (col == null)
                {
                    continue;
                }

                var point = col.ClosestPoint(cur.Position);
                var toOrigin = cur.Position - point;
                var normal = toOrigin.sqrMagnitude > 1e-6f ? toOrigin.normalized : Vector3.up;
                onHit(col, point, normal);
                physicsHits++;
            }
        }

        return physicsHits;
    }

    void StorePrev(int n)
    {
        for (var i = 0; i < n; i++)
        {
            _prevPos[i] = _scratch[i].Position;
            _prevValid[i] = _scratch[i].Valid;
        }
    }

    int CountValidSockets(int n)
    {
        var c = 0;
        for (var i = 0; i < n; i++)
        {
            if (_scratch[i].Valid)
            {
                c++;
            }
        }

        return c;
    }

    static bool TryResolveSocketWorld(
        Animator anim,
        Transform fallbackRoot,
        in WeaponSocketDef def,
        out Vector3 worldPos)
    {
        worldPos = default;

        if (anim != null && anim.isHuman)
        {
            var bone = anim.GetBoneTransform(def.Bone);
            if (bone != null)
            {
                worldPos = bone.position + bone.rotation * def.LocalOffset;
                return true;
            }
        }

        if (fallbackRoot != null)
        {
            worldPos = fallbackRoot.position + fallbackRoot.rotation * def.LocalOffset;
            return true;
        }

        return false;
    }
}
