using UnityEngine;

/// <summary>
/// 178.1 W1+W2+W3 — 四层降级过渡解析（唯一 Resolve 入口）。
/// <para>① Override（5%）→ ② Clip DB（4%）→ ③ Tag Matrix（90%）→ ④ Default（兜底）</para>
/// </summary>
public sealed class AnimationTransitionService
{
    readonly TransitionTagMatrixSO _matrix;
    readonly TransitionDatabaseSO _clipDb;
    readonly TransitionConfigSO _config;

    public AnimationTransitionService(
        TransitionTagMatrixSO matrix,
        TransitionDatabaseSO clipDb,
        TransitionConfigSO config)
    {
        _matrix = matrix;
        _clipDb = clipDb;
        _config = config;
    }

    public TransitionTagMatrixSO Matrix => _matrix;
    public TransitionDatabaseSO ClipDb => _clipDb;
    public TransitionConfigSO Config => _config;

    /// <summary>
    /// 178.1 §3 — 四层降级解析。永不返回无效结果（兜底 Default）。
    /// </summary>
    public TransitionParams Resolve(ActionDataSO from, ActionDataSO to)
    {
        if (to == null)
        {
            return FallbackDefault();
        }

        // ① Override（最高优先）
        if (to.OverrideIncomingTransition)
        {
            return new TransitionParams(
                to.OverrideBlendDuration,
                to.OverrideTransitionMode,
                to.OverrideEaseCurve,
                TransitionResolveLayer.Override);
        }

        // ② Clip DB（仅当烘焙存在条目）
        var fromClip = from != null ? from.MainClip : null;
        var toClip = to.MainClip;
        if (fromClip != null && toClip != null
            && _clipDb != null
            && _clipDb.TryGet(fromClip, toClip, out var dbEntry))
        {
            var mode = dbEntry.PhaseMatchSafe ? TransitionMode.PhaseMatch : TransitionMode.CrossFade;
            return new TransitionParams(
                dbEntry.RecommendedBlend,
                mode,
                null,
                TransitionResolveLayer.ClipDb);
        }

        // ③ Tag Matrix（主力）
        if (_matrix != null)
        {
            var fromTag = from != null ? from.TransitionTag : TransitionTag.Idle;
            var toTag = to.TransitionTag;
            var entry = _matrix.GetEntry(fromTag, toTag);

            // 同 Tag 自动启用 PhaseMatch（Locomotion / Crouch）
            var mode = entry.Mode;
            if (mode == TransitionMode.CrossFade
                && TransitionTagMatrixSO.IsPhaseMatchPair(fromTag, toTag))
            {
                mode = TransitionMode.PhaseMatch;
            }

            // Hit 行全 0 → Snap
            if (entry.BlendDuration <= 0.0001f && fromTag == TransitionTag.Hit)
            {
                mode = TransitionMode.Snap;
            }

            // 非零 blend 或 Snap 都视为命中矩阵
            if (entry.BlendDuration > 0f || mode == TransitionMode.Snap)
            {
                return new TransitionParams(
                    entry.BlendDuration,
                    mode,
                    entry.EaseCurve,
                    TransitionResolveLayer.TagMatrix);
            }
        }

        // ④ Default 兜底
        return FallbackDefault();
    }

    TransitionParams FallbackDefault()
    {
        if (_config != null)
        {
            return new TransitionParams(
                _config.DefaultBlendDuration,
                _config.DefaultMode,
                null,
                TransitionResolveLayer.Default);
        }
        return TransitionParams.DefaultFallback(0.10f);
    }
}
