using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 241：YMotion / Gravity / GroundConstraint 三权分立低噪声运行时探针。
/// 只记录 Player 的 ActionDataSO → MotionProfileSO → MotionExecutor → Player KCC 链路，
/// 不参与任何位移、重力或落地裁决。
/// </summary>
public static class YAxis241Probe
{
    sealed class Trace
    {
        public int SessionId;
        public int PlayerId;
        public Player Owner;
        public string ActionRef;
        public string ProfileRef;
        public string ActionName;
        public string ProfileName;
        public bool V2Configured;
        public byte LegacyPolicyRaw;
        public MotionYAxisConfig VisibleConfig;
        public MotionYAxisConfig RuntimeConfig;
        public bool SourceMismatch;
        public bool ConfigLost;
        public string FirstMismatchStage;
        public bool Overridden;
        public bool PostMutated;
        public bool HasMotorResult;
        public bool HasExecutorSample;
        public bool HasAnySample;
        public bool HasMidSample;
        public bool HasEndSample;
        public bool SamplePending;
        public float LastT;
        public Vector3 LastWorldDelta;
        public MotionYAxisConfig LastExecutorConfig;
        public MotionYAxisConfig LastAdapterConfig;
        public MotionYAxisConfig LastMotorInConfig;
        public MotionYAxisConfig LastComposerConfig;
        public MotionYAxisConfig LastConstraintConfig;
        public bool HasExecutorConfig;
        public bool HasAdapterConfig;
        public bool HasMotorInConfig;
        public bool HasComposerConfig;
        public bool HasConstraintConfig;
    }

    static readonly Dictionary<int, Trace> s_traces = new Dictionary<int, Trace>();
    static int s_nextSessionId;

    static bool Enabled => GameMainDebugSettings.YAxis241Log;

    public static void Begin(Player owner, ActionDataSO action, MotionProfileSO profile, float startT)
    {
        if (!Enabled || owner == null || profile == null)
        {
            return;
        }

        var playerId = owner.GetInstanceID();
        if (s_traces.ContainsKey(playerId))
        {
            End(owner);
        }

        var trace = new Trace
        {
            SessionId = ++s_nextSessionId,
            PlayerId = playerId,
            Owner = owner,
            ActionRef = GetAssetRef(action),
            ProfileRef = GetAssetRef(profile),
            ActionName = action != null ? action.name : "(none)",
            ProfileName = profile.name,
            V2Configured = profile.IsYAxisV2Configured,
            LegacyPolicyRaw = profile.LegacyYAxisPolicyRaw,
            VisibleConfig = profile.GetVisibleYAxisConfig(),
            RuntimeConfig = profile.GetYAxisConfig(),
            LastT = Mathf.Clamp01(startT),
        };
        trace.SourceMismatch = !trace.V2Configured
            || !ConfigEquals(trace.VisibleConfig, trace.RuntimeConfig);
        trace.FirstMismatchStage = trace.SourceMismatch ? "ProfileRead" : null;
        s_traces[playerId] = trace;

        Write(owner,
            "[Y241] e=241_CFG stage=ProfileRead sid={0} pid={1} ad={2} mp={3} t={4:F3} v2Configured={5} legacyRaw={6} visibleCfg={7} runtimeCfg={8} cfgSource={9}",
            trace.SessionId,
            trace.PlayerId,
            trace.ActionRef,
            trace.ProfileRef,
            trace.LastT,
            trace.V2Configured ? 1 : 0,
            trace.LegacyPolicyRaw,
            FormatConfig(trace.VisibleConfig),
            FormatConfig(trace.RuntimeConfig),
            trace.V2Configured ? "V2" : "LegacyMapping");
    }

    public static void ObserveExecutor(
        Player owner,
        float normalizedTime,
        Vector3 localDelta,
        Vector3 worldDelta,
        MotionYAxisConfig config)
    {
        if (!TryGet(owner, out var trace))
        {
            return;
        }

        trace.LastT = Mathf.Clamp01(normalizedTime);
        trace.LastWorldDelta = worldDelta;
        trace.HasExecutorSample = true;
        ObserveWire(trace, "ExecutorOut", config, ref trace.HasExecutorConfig, ref trace.LastExecutorConfig);

        trace.SamplePending = ShouldSample(trace, trace.LastT);
        if (trace.SamplePending || !ConfigEquals(config, trace.RuntimeConfig))
        {
            Write(trace.Owner,
                "[Y241] e=241_Y stage=ExecutorOut sid={0} pid={1} ad={2} mp={3} t={4:F3} cfg={5} localDY={6:F4} worldDY={7:F4}",
                trace.SessionId,
                trace.PlayerId,
                trace.ActionRef,
                trace.ProfileRef,
                trace.LastT,
                FormatConfig(config),
                localDelta.y,
                worldDelta.y);
        }
    }

    public static void ObserveAdapterCache(Player owner, MotionYAxisConfig config)
    {
        if (!TryGet(owner, out var trace))
        {
            return;
        }

        ObserveWire(trace, "AdapterCache", config, ref trace.HasAdapterConfig, ref trace.LastAdapterConfig);
    }

    public static void ObserveMotorIn(Player owner, MotionYAxisConfig config, float gravityVy, bool grounded)
    {
        if (!TryGet(owner, out var trace))
        {
            return;
        }

        ObserveWire(trace, "MotorIn", config, ref trace.HasMotorInConfig, ref trace.LastMotorInConfig);
        if (ConfigEquals(config, trace.RuntimeConfig) && trace.SourceMismatch)
        {
            return;
        }

        _ = gravityVy;
        _ = grounded;
    }

    public static void ObserveComposer(Player owner, MotionYAxisConfig config, float composedVy)
    {
        if (!TryGet(owner, out var trace))
        {
            return;
        }

        ObserveWire(trace, "ComposerIn", config, ref trace.HasComposerConfig, ref trace.LastComposerConfig);
        _ = composedVy;
    }

    public static void ObserveConstraint(Player owner, MotionYAxisConfig config, float constrainedVy)
    {
        if (!TryGet(owner, out var trace))
        {
            return;
        }

        ObserveWire(trace, "ConstraintIn", config, ref trace.HasConstraintConfig, ref trace.LastConstraintConfig);
        _ = constrainedVy;
    }

    public static void ObserveMotorResult(
        Player owner,
        MotionYAxisConfig config,
        float gravityVy,
        float composedVy,
        float constrainedVy,
        Vector3 plannedDelta,
        Vector3 solvedDelta,
        Vector3 actualDelta,
        bool groundedBefore,
        bool groundedAfter,
        bool gameplayDrivenVy)
    {
        if (!TryGet(owner, out var trace))
        {
            return;
        }

        trace.HasMotorResult = true;
        if (Mathf.Abs(solvedDelta.y - plannedDelta.y) > 0.002f
            || Mathf.Abs(actualDelta.y - solvedDelta.y) > 0.002f)
        {
            trace.PostMutated = true;
        }

        if (config.YMotion == YMotionMode.None && Mathf.Abs(composedVy) > 0.002f)
        {
            trace.Overridden = true;
        }

        if (!trace.SamplePending
            && !trace.PostMutated
            && ConfigEquals(config, trace.RuntimeConfig))
        {
            return;
        }

        Write(trace.Owner,
            "[Y241] e=241_Y stage=MotorResult sid={0} pid={1} ad={2} mp={3} t={4:F3} cfg={5} gravityVy={6:F3} composedVy={7:F3} constrainedVy={8:F3} plannedDY={9:F4} solvedDY={10:F4} actualDY={11:F4} grounded={12}->{13} yDriven={14}",
            trace.SessionId,
            trace.PlayerId,
            trace.ActionRef,
            trace.ProfileRef,
            trace.LastT,
            FormatConfig(config),
            gravityVy,
            composedVy,
            constrainedVy,
            plannedDelta.y,
            solvedDelta.y,
            actualDelta.y,
            groundedBefore ? 1 : 0,
            groundedAfter ? 1 : 0,
            gameplayDrivenVy ? 1 : 0);
        trace.SamplePending = false;
    }

    public static void End(Player owner)
    {
        if (owner == null)
        {
            return;
        }

        var playerId = owner.GetInstanceID();
        if (!Enabled)
        {
            s_traces.Remove(playerId);
            return;
        }

        if (!s_traces.TryGetValue(playerId, out var trace))
        {
            return;
        }

        var result = ResolveResult(trace);
        Write(trace.Owner,
            "[Y241] e=241_RESULT stage=ActionEnd sid={0} pid={1} ad={2} mp={3} t={4:F3} result={5} cfgSource={6} v2Configured={7} legacyRaw={8} visibleCfg={9} runtimeCfg={10} firstMismatch={11} wire={12}/{13}/{14}/{15}/{16} samples={17}",
            trace.SessionId,
            trace.PlayerId,
            trace.ActionRef,
            trace.ProfileRef,
            trace.LastT,
            result,
            trace.V2Configured ? "V2" : "LegacyMapping",
            trace.V2Configured ? 1 : 0,
            trace.LegacyPolicyRaw,
            FormatConfig(trace.VisibleConfig),
            FormatConfig(trace.RuntimeConfig),
            string.IsNullOrEmpty(trace.FirstMismatchStage) ? "none" : trace.FirstMismatchStage,
            trace.HasExecutorConfig ? FormatConfig(trace.LastExecutorConfig) : "missing",
            trace.HasAdapterConfig ? FormatConfig(trace.LastAdapterConfig) : "missing",
            trace.HasMotorInConfig ? FormatConfig(trace.LastMotorInConfig) : "missing",
            trace.HasComposerConfig ? FormatConfig(trace.LastComposerConfig) : "missing",
            trace.HasConstraintConfig ? FormatConfig(trace.LastConstraintConfig) : "missing",
            trace.HasAnySample ? (trace.HasMidSample ? (trace.HasEndSample ? "first,mid,end" : "first,mid") : "first") : "none");
        s_traces.Remove(playerId);
    }

    static string ResolveResult(Trace trace)
    {
        if (!trace.HasMotorResult)
        {
            return "SAMPLE_MISSING";
        }

        if (trace.SourceMismatch)
        {
            return "CFG_SOURCE_MISMATCH";
        }

        if (trace.ConfigLost)
        {
            return "CFG_LOST";
        }

        if (trace.Overridden)
        {
            return "OVERRIDDEN";
        }

        if (trace.PostMutated)
        {
            return "POST_MUTATED";
        }

        return "APPLIED";
    }

    static void ObserveWire(
        Trace trace,
        string stage,
        MotionYAxisConfig config,
        ref bool hasLast,
        ref MotionYAxisConfig lastConfig)
    {
        var mismatch = !ConfigEquals(config, trace.RuntimeConfig);
        if (mismatch)
        {
            trace.ConfigLost = true;
            if (string.IsNullOrEmpty(trace.FirstMismatchStage))
            {
                trace.FirstMismatchStage = stage;
            }
        }

        if (!hasLast || !ConfigEquals(lastConfig, config))
        {
            Write(trace.Owner,
                "[Y241] e=241_WIRE stage={0} sid={1} pid={2} ad={3} mp={4} t={5:F3} cfg={6} expected={7} result={8}",
                stage,
                trace.SessionId,
                trace.PlayerId,
                trace.ActionRef,
                trace.ProfileRef,
                trace.LastT,
                FormatConfig(config),
                FormatConfig(trace.RuntimeConfig),
                mismatch ? "CFG_LOST" : "MATCH");
            lastConfig = config;
            hasLast = true;
        }
    }

    static bool ShouldSample(Trace trace, float normalizedTime)
    {
        if (!trace.HasAnySample)
        {
            trace.HasAnySample = true;
            return true;
        }

        if (!trace.HasMidSample && normalizedTime >= 0.5f)
        {
            trace.HasMidSample = true;
            return true;
        }

        if (!trace.HasEndSample && normalizedTime >= 0.99f)
        {
            trace.HasEndSample = true;
            return true;
        }

        return false;
    }

    static bool TryGet(Player owner, out Trace trace)
    {
        trace = null;
        if (!Enabled || owner == null)
        {
            return false;
        }

        return s_traces.TryGetValue(owner.GetInstanceID(), out trace);
    }

    static bool ConfigEquals(MotionYAxisConfig a, MotionYAxisConfig b) =>
        a.YMotion == b.YMotion
        && a.Gravity == b.Gravity
        && a.GroundConstraint == b.GroundConstraint;

    static string FormatConfig(MotionYAxisConfig config) =>
        string.Format("{0}/{1}/{2}", config.YMotion, config.Gravity, config.GroundConstraint);

    static void Write(Object context, string format, params object[] args)
    {
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, context, format, args);
    }

    static string GetAssetRef(Object asset)
    {
        if (asset == null)
        {
            return "(none)";
        }

#if UNITY_EDITOR
        var path = AssetDatabase.GetAssetPath(asset);
        var guid = string.IsNullOrEmpty(path) ? "(noguid)" : AssetDatabase.AssetPathToGUID(path);
        return string.Format("{0}#{1}@{2}", asset.name, asset.GetInstanceID(), guid);
#else
        return string.Format("{0}#{1}", asset.name, asset.GetInstanceID());
#endif
    }
}
