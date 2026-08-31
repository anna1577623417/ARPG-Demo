using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class AnimationPresentationFirewall243Tests
{
    [Test]
    public void NewTransitionContractsContainNoGameplayOrTransformWriterCall()
    {
        var scriptsRoot = Application.dataPath + "/GameMain/Scripts";
        var files = new[]
        {
            "2_Framework/Animation/Runtime/AnimationObservation.cs",
            "2_Framework/Animation/Runtime/IAnimationObservationSource.cs",
            "2_Framework/Animation/Runtime/AnimationPlayRequest.cs",
            "2_Framework/Animation/Runtime/AnimationArbitrationDecision.cs",
            "2_Framework/Animation/Runtime/AnimationArbitrationState.cs",
            "2_Framework/Animation/Runtime/AnimationRequestArbiter.cs",
            "2_Framework/Animation/Runtime/AnimationPipelineMode.cs",
            "2_Framework/Animation/Runtime/AnimationPipelineGate243.cs",
            "2_Framework/Animation/Transition/SpatialHandoffMode.cs",
            "2_Framework/Animation/Transition/RootYawChannelMode.cs",
            "2_Framework/Animation/Transition/PoseChannelMode.cs",
            "2_Framework/Animation/Transition/TransitionContext.cs",
            "2_Framework/Animation/Transition/TransitionPlan.cs",
            "2_Framework/Animation/Transition/AnimationTransitionSafetyResolver.cs",
            "5_Presentation/Animation/Runtime/AnimationTransitionGraphShadowEvaluator243.cs",
            "5_Presentation/Animation/Runtime/TurnPresentationRequestProducer243.cs",
            "5_Presentation/Animation/Runtime/LocomotionPresentationRequestProducer243.cs",
            "5_Presentation/Animation/Runtime/AirborneActionPresentationRequestProducer243.cs",
        };
        var forbidden = new[]
        {
            "SetLogicForward(",
            "SetPlanarVelocity(",
            "MoveByLocomotionIntent(",
            "RequestFacing(",
            "ApplyMotor(",
            "ChangeState(",
            "EntityAnimController",
            "PlayerAnimController",
            ".transform.",
        };
        var violations = new List<string>();

        foreach (var relativePath in files)
        {
            var path = Path.Combine(scriptsRoot, relativePath);
            var source = File.ReadAllText(path);
            for (var i = 0; i < forbidden.Length; i++)
            {
                if (source.Contains(forbidden[i]))
                {
                    violations.Add(path.Replace('\\', '/') + " :: " + forbidden[i]);
                }
            }
        }

        Assert.That(violations, Is.Empty, "Presentation contract firewall violations:\n" + string.Join("\n", violations));
    }

    [Test]
    public void NewTraceUsesExplicitNoStacktraceAndNoBareDebugLog()
    {
        var path = Path.Combine(Application.dataPath, "GameMain/Scripts/2_Framework/Animation/Diagnostics/AnimationTransitionGraphTrace243.cs");
        var source = File.ReadAllText(path);

        Assert.That(source, Does.Contain("Debug.LogFormat("));
        Assert.That(source, Does.Contain("LogOption.NoStacktrace"));
        Assert.That(source, Does.Not.Contain("Debug.Log("));
        Assert.That(source, Does.Not.Contain("Debug.LogWarning("));
        Assert.That(source, Does.Not.Contain("Debug.LogError("));
    }
}
