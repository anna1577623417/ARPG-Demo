using NUnit.Framework;

public sealed class AnimationPipelineGate243Tests
{
    [Test]
    public void CanaryRequiresShadowParityAndOneWriter()
    {
        var gate = new AnimationPipelineGate243();

        Assert.IsTrue(gate.TrySetMode(
            AnimationRequestDomain.Turn, AnimationPipelineMode.Shadow, "shadow", AnimationObservation.CurrentSchemaVersion, "hash", false, true));
        Assert.IsFalse(gate.TrySetMode(
            AnimationRequestDomain.Turn, AnimationPipelineMode.Canary, "missing-parity", AnimationObservation.CurrentSchemaVersion, "hash", false, true));
        Assert.IsFalse(gate.TrySetMode(
            AnimationRequestDomain.Turn, AnimationPipelineMode.Canary, "two-writers", AnimationObservation.CurrentSchemaVersion, "hash", true, false));
        Assert.IsTrue(gate.TrySetMode(
            AnimationRequestDomain.Turn, AnimationPipelineMode.Canary, "ready", AnimationObservation.CurrentSchemaVersion, "hash", true, true));
    }

    [Test]
    public void InvalidSchemaCannotOpenAnyDomain()
    {
        var gate = new AnimationPipelineGate243();

        Assert.IsFalse(gate.TrySetMode(
            AnimationRequestDomain.Locomotion, AnimationPipelineMode.Shadow, "bad-schema",
            AnimationObservation.CurrentSchemaVersion + 1, "hash", false, true));
        Assert.AreEqual(AnimationPipelineMode.Disabled, gate.ResolveMode(AnimationRequestDomain.Locomotion));
    }

    [Test]
    public void DisableIsAlwaysRecoverableKillSwitch()
    {
        var gate = new AnimationPipelineGate243();
        gate.Disable(AnimationRequestDomain.Action, "manual-kill");

        Assert.AreEqual(AnimationPipelineMode.Disabled, gate.ResolveMode(AnimationRequestDomain.Action));
        Assert.AreEqual("manual-kill", gate.GetState(AnimationRequestDomain.Action).Reason);
    }
}
