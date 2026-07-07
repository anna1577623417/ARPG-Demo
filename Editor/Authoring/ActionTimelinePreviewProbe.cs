#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Action 时间轴预览诊断通道（独立 prefix = [ActTimelineDiag]）。
///
/// 用途：对账 Timeline 绿线轨迹 vs 场景角色模型实际位置。
///
/// 须在 <see cref="ActionTimelinePreviewController.SamplePose"/> 之后调用（传 poseSampled=true），
/// 否则只输出 Profile 理论值。Post-Sample 会读 Hips 骨骼世界坐标（与 ClipMotionExtractor 采骨口径一致）。
///
/// 启用：GameMain → Debug → Log Settings → Action Timeline Preview
/// 节流：0.25s 一条
/// </summary>
internal static class ActionTimelinePreviewProbe
{
    const string Prefix = "[ActTimelineDiag]";
    const double LogIntervalSec = 0.25;

    static double s_nextLogTime;
    static float s_lastNt = -1f;

    static ActionTimelinePreviewProbe()
    {
    }

    internal static bool IsEnabled => GameMainDebugSettings.ActionTimelinePreviewLog;

    /// <summary>SamplePose 之后的状态；未采样时传 default。</summary>
    internal readonly struct PoseSampleState
    {
        public readonly bool PoseSampled;
        public readonly bool MotionDisplacementApplied;

        public PoseSampleState(bool poseSampled, bool motionDisplacementApplied)
        {
            PoseSampled = poseSampled;
            MotionDisplacementApplied = motionDisplacementApplied;
        }
    }

  /// <summary>由 OnSceneGUI（SamplePose 之后）或 SceneBridge 调用。</summary>
    internal static void Tick(in ActionTimelinePreviewContext ctx, in PoseSampleState pose = default)
    {
        if (!IsEnabled || ctx.Action == null) return;

        var now = EditorApplication.timeSinceStartup;
        var ntChanged = Mathf.Abs(ctx.NormalizedTime - s_lastNt) > 0.001f;
        if (now < s_nextLogTime && !ntChanged) return;
        s_nextLogTime = now + LogIntervalSec;
        s_lastNt = ctx.NormalizedTime;

        var action = ctx.Action;
        var anchorName = ctx.Anchor != null ? ctx.Anchor.name : "(none)";
        var profileName = action.MotionProfile != null ? action.MotionProfile.name : "(none)";
        var forward = PlanarForward(ctx.PlanarForward);

        // 绿线轨迹播放头（理论 Motion 世界位移）
        var trajWorld = ctx.MotionWorldPosition;
        var captureOrigin = ctx.HasCaptureOrigin ? ctx.CaptureOriginPos : ctx.AnchorOrigin;

        // Clip 时间轴（与 SamplePose 一致）
        var clip = action.MainClip;
        var clipSec = ActionAnimSpeedAuthority.ResolvePreviewClipSeconds(action, ctx.NormalizedTime);
        var clipLen = clip != null ? clip.length : 0f;
        var clipNorm = clipLen > 1e-5f ? clipSec / clipLen : 0f;
        var clipDoneNt = ActionAnimSpeedAuthority.ResolveClipDoneNormalizedTime(action);
        var combinedSpeed = ActionAnimSpeedAuthority.ResolveCombinedAnimSpeed(action, ctx.NormalizedTime);

        // Clip RM 路径在 nt 的世界点（与 MotionDriven 绿线无关，用于对账「动画烘焙位移」）
        var clipRmWorld = captureOrigin;
        if (ctx.HasAnchor && clip != null)
        {
            clipRmWorld = ActionTimelineRootMotionSampler.EvaluateWorldPosition(
                action, ctx.Anchor, ctx.NormalizedTime, captureOrigin);
        }

        var clipRmAlong = AlongForward(clipRmWorld - trajWorld, forward);
        var clipRmDist = Vector3.Distance(clipRmWorld, trajWorld);

        // MotionDriven 下 rootWorld 固定为 CaptureOrigin，不等于轨迹播放头 — 仅作参考
        var rootWorld = ctx.RootMotionWorldPosition;
        var motionVsRootDist = Vector3.Distance(trajWorld, rootWorld);

        // —— Post-Sample：读场景真实 Transform ——
        var anchorWorld = captureOrigin;
        var modelWorld = captureOrigin;
        var anchorVsTrajAlong = 0f;
        var anchorVsTrajDist = 0f;
        var modelVsTrajAlong = 0f;
        var modelVsTrajDist = 0f;
        var clipBoneOffset = Vector3.zero;
        var clipBoneAlong = 0f;
        var clipBoneDist = 0f;
        var sampleRootName = "(none)";
        var hasHips = false;
        var hipsBoneName = "(none)";
        var hipsWorld = captureOrigin;
        var hipsVsTrajAlong = 0f;
        var hipsVsTrajDist = 0f;
        var hipsOnAnchorAlong = 0f;
        var hipsOnAnchorDist = 0f;
        var hipsLocalOnAnchor = Vector3.zero;

        if (pose.PoseSampled && ctx.HasAnchor)
        {
            anchorWorld = ctx.Anchor.position;
            var sampleRoot = ResolveSampleRoot(ctx.Anchor);
            if (sampleRoot != null)
            {
                modelWorld = sampleRoot.position;
                sampleRootName = sampleRoot.name;
            }
            else
            {
                modelWorld = anchorWorld;
            }

            var anchorDelta = anchorWorld - trajWorld;
            var modelDelta = modelWorld - trajWorld;
            anchorVsTrajAlong = AlongForward(anchorDelta, forward);
            anchorVsTrajDist = PlanarDistance(anchorWorld, trajWorld);
            modelVsTrajAlong = AlongForward(modelDelta, forward);
            modelVsTrajDist = PlanarDistance(modelWorld, trajWorld);

            clipBoneOffset = modelWorld - anchorWorld;
            clipBoneAlong = AlongForward(clipBoneOffset, forward);
            clipBoneDist = PlanarDistance(modelWorld, anchorWorld);

            if (MotionProfileInPlaceBoneCompensator.TryResolveHipsBone(ctx.Anchor, out var hipsBone))
            {
                hasHips = true;
                hipsBoneName = hipsBone.name;
                hipsWorld = hipsBone.position;
                hipsLocalOnAnchor = ctx.Anchor.InverseTransformPoint(hipsWorld);

                var hipsDelta = hipsWorld - trajWorld;
                hipsVsTrajAlong = AlongForward(hipsDelta, forward);
                hipsVsTrajDist = PlanarDistance(hipsWorld, trajWorld);

                var hipsOnAnchor = hipsWorld - anchorWorld;
                hipsOnAnchorAlong = AlongForward(hipsOnAnchor, forward);
                hipsOnAnchorDist = PlanarDistance(hipsWorld, anchorWorld);
            }
        }

        var verdict = BuildVerdict(
            pose,
            anchorVsTrajAlong,
            modelVsTrajAlong,
            clipBoneAlong,
            hasHips,
            hipsVsTrajAlong,
            hipsOnAnchorAlong);

        Debug.Log(
            $"{Prefix} action={action.name} nt={ctx.NormalizedTime:F3} wallSec={ctx.WallTimeSeconds:F3} " +
            $"mode={ctx.MotionMode} pose={pose.PoseSampled} motionDisp={pose.MotionDisplacementApplied}\n" +
            $"  profile={profileName} hasMP={ctx.HasMotionProfile} useClipRM={action.UseClipRootMotion}\n" +
            $"  clipSec={clipSec:F3} clipNorm={clipNorm:F3} clipLen={clipLen:F3} " +
            $"clipDoneNt={clipDoneNt:F3} combinedSpeed={combinedSpeed:F2} clipAnimMode={action.ClipAnimSpeedMode}\n" +
            $"  anchor={anchorName} sampleRoot={sampleRootName} hasCapture={ctx.HasCaptureOrigin}\n" +
            $"  captureOrigin=({captureOrigin.x:F2},{captureOrigin.y:F2},{captureOrigin.z:F2}) " +
            $"forward=({forward.x:F2},{forward.z:F2})\n" +
            $"  motionLocal=({ctx.MotionLocalPosition.x:F2},{ctx.MotionLocalPosition.y:F2},{ctx.MotionLocalPosition.z:F2})\n" +
            (ctx.UsesActionYawPreview
                ? $"  actionYaw=({ctx.ActionYawForward.x:F2},{ctx.ActionYawForward.z:F2}) deg={ctx.ActionYawDegrees:F0}\n"
                : string.Empty) +
            $"  trajWorld=({trajWorld.x:F2},{trajWorld.y:F2},{trajWorld.z:F2}) " +
            $"displayPos=({ctx.Position.x:F2},{ctx.Position.y:F2},{ctx.Position.z:F2})\n" +
            $"  rootWorld(ref)=({rootWorld.x:F2},{rootWorld.y:F2},{rootWorld.z:F2}) motionVsRootDist={motionVsRootDist:F3}m\n" +
            $"  clipRmWorld=({clipRmWorld.x:F2},{clipRmWorld.y:F2},{clipRmWorld.z:F2}) " +
            $"clipRmVsTraj along={clipRmAlong:F3}m dist={clipRmDist:F3}m\n" +
            (pose.PoseSampled
                ? $"  anchorWorld=({anchorWorld.x:F2},{anchorWorld.y:F2},{anchorWorld.z:F2}) " +
                  $"modelWorld=({modelWorld.x:F2},{modelWorld.y:F2},{modelWorld.z:F2})\n" +
                  $"  anchorVsTraj along={anchorVsTrajAlong:F3}m dist={anchorVsTrajDist:F3}m " +
                  $"modelVsTraj along={modelVsTrajAlong:F3}m dist={modelVsTrajDist:F3}m\n" +
                  $"  clipBoneOffset=({clipBoneOffset.x:F2},{clipBoneOffset.y:F2},{clipBoneOffset.z:F2}) " +
                  $"clipBone along={clipBoneAlong:F3}m dist={clipBoneDist:F3}m\n" +
                  (hasHips
                      ? $"  hipsBone={hipsBoneName} hipsWorld=({hipsWorld.x:F2},{hipsWorld.y:F2},{hipsWorld.z:F2})\n" +
                        $"  hipsLocalOnAnchor=({hipsLocalOnAnchor.x:F2},{hipsLocalOnAnchor.y:F2},{hipsLocalOnAnchor.z:F2})\n" +
                        $"  hipsVsTraj along={hipsVsTrajAlong:F3}m dist={hipsVsTrajDist:F3}m " +
                        $"hipsOnAnchor along={hipsOnAnchorAlong:F3}m dist={hipsOnAnchorDist:F3}m\n"
                      : $"  hipsBone=(not found — non-human rig or missing Hips)\n") +
                  $"  {verdict}"
                : $"  (pose not sampled — enable PreviewVisibility Pose to log model vs trajectory)"));
    }

    static string BuildVerdict(
        in PoseSampleState pose,
        float anchorVsTrajAlong,
        float modelVsTrajAlong,
        float clipBoneAlong,
        bool hasHips,
        float hipsVsTrajAlong,
        float hipsOnAnchorAlong)
    {
        if (!pose.PoseSampled)
            return string.Empty;

        const float eps = 0.02f;

        // Hips 超前 + Anchor 贴轨迹 → MotionProfile 位移 + Clip 骨骼平移叠加（197.2 主嫌疑）
        if (hasHips && hipsVsTrajAlong > eps && Mathf.Abs(anchorVsTrajAlong) < eps)
        {
            return $"VERDICT=SUSPECT_HIPS_ON_MOTOR_DOUBLE_DISP " +
                   $"hipsAhead={hipsVsTrajAlong:F3}m hipsOnAnchor along={hipsOnAnchorAlong:F3}m " +
                   $"(anchor≈traj; visual mesh follows Hips bone offset from clip)";
        }

        if (modelVsTrajAlong > eps && Mathf.Abs(anchorVsTrajAlong) < eps && clipBoneAlong > eps)
        {
            return $"VERDICT=SUSPECT_CLIP_BONE_ON_MOTION " +
                   $"modelAhead={modelVsTrajAlong:F3}m clipBoneAlong={clipBoneAlong:F3}m " +
                   $"(anchor tracks traj; sampleRoot extra ≈ clip bone offset)";
        }

        if (hasHips && hipsVsTrajAlong > eps && anchorVsTrajAlong > eps)
        {
            return $"VERDICT=SUSPECT_DOUBLE_ANCHOR_AND_HIPS " +
                   $"hipsAhead={hipsVsTrajAlong:F3}m anchorAhead={anchorVsTrajAlong:F3}m " +
                   $"hipsOnAnchor={hipsOnAnchorAlong:F3}m";
        }

        if (modelVsTrajAlong > eps && anchorVsTrajAlong > eps)
        {
            return $"VERDICT=SUSPECT_DOUBLE_ANCHOR_MOTION " +
                   $"modelAhead={modelVsTrajAlong:F3}m anchorAhead={anchorVsTrajAlong:F3}m " +
                   $"clipBoneAlong={clipBoneAlong:F3}m";
        }

        if (hasHips && hipsVsTrajAlong > eps)
        {
            return $"VERDICT=HIPS_AHEAD_OF_TRAJ ahead={hipsVsTrajAlong:F3}m " +
                   $"anchorSlip={anchorVsTrajAlong:F3}m hipsOnAnchor={hipsOnAnchorAlong:F3}m";
        }

        if (modelVsTrajAlong > eps)
        {
            return $"VERDICT=MODEL_AHEAD_OF_TRAJ ahead={modelVsTrajAlong:F3}m " +
                   $"anchorSlip={anchorVsTrajAlong:F3}m clipBone={clipBoneAlong:F3}m";
        }

        if (Mathf.Abs(anchorVsTrajAlong) > eps)
        {
            return $"VERDICT=ANCHOR_DRIFT vsTraj={anchorVsTrajAlong:F3}m " +
                   $"hipsVsTraj={hipsVsTrajAlong:F3}m modelVsTraj={modelVsTrajAlong:F3}m";
        }

        if (hasHips && Mathf.Abs(hipsVsTrajAlong) > eps)
        {
            return $"VERDICT=HIPS_DRIFT vsTraj={hipsVsTrajAlong:F3}m (anchor/model aligned with traj)";
        }

        return hasHips
            ? "VERDICT=ALIGNED (anchor + hips ≈ traj within 2cm)"
            : "VERDICT=ALIGNED (anchor ≈ traj within 2cm; hips not resolved)";
    }

    static Transform ResolveSampleRoot(Transform anchor)
    {
        if (anchor == null) return null;
        var animator = anchor.GetComponentInChildren<Animator>();
        return animator != null ? animator.transform : anchor;
    }

    static Vector3 PlanarForward(Vector3 forward)
    {
        forward.y = 0f;
        return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
    }

    static float AlongForward(Vector3 delta, Vector3 forward)
    {
        return Vector3.Dot(new Vector3(delta.x, 0f, delta.z), forward);
    }

    static float PlanarDistance(Vector3 a, Vector3 b)
    {
        var d = a - b;
        d.y = 0f;
        return d.magnitude;
    }
}
#endif
