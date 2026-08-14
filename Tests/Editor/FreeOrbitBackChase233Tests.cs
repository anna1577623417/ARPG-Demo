using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 233 P0 — FreeOrbit 相机权威边界的静态防回归门禁。
/// 只禁止 Root/Logic 驱动的后台追背，不限制未来显式 TargetLockSnapshot 驱动的 LockOn 策略。
/// </summary>
public sealed class FreeOrbitBackChase233Tests
{
    private static string LoadActionCameraControllerSource()
    {
        var path = Path.Combine(
            Application.dataPath,
            "GameMain/Scripts/2_Framework/Camera/Controllers/ActionCameraController.cs");
        Assert.That(File.Exists(path), Is.True, $"ActionCameraController source not found: {path}");
        return File.ReadAllText(path);
    }

    [Test]
    public void FreeOrbitRuntimeHasNoLegacyChaseAssistWriter()
    {
        var source = LoadActionCameraControllerSource();

        Assert.That(source, Does.Not.Contain("ApplyChaseAssist"));
        Assert.That(source, Does.Not.Contain("enableChaseAssist"));
        Assert.That(source, Does.Not.Contain("chaseRecenterDelay"));
        Assert.That(source, Does.Not.Contain("chaseRecenterAngularSpeed"));
        Assert.That(source, Does.Not.Contain("chaseYawDeadzone"));
    }

    [Test]
    public void FreeOrbitRuntimeDoesNotReadPlayerRootForwardForHeading()
    {
        var source = LoadActionCameraControllerSource();

        Assert.That(source, Does.Not.Contain("followTarget.parent.forward"));
        Assert.That(source, Does.Not.Contain("followTarget.parent.rotation"));
    }

    [Test]
    public void MovementReferenceRemainsBoundToPersistentOrbitYaw()
    {
        var source = LoadActionCameraControllerSource();

        StringAssert.Contains(
            "MovementReferenceRotation => Quaternion.Euler(0f, _yaw, 0f)",
            source);
        StringAssert.Contains(
            "followTarget.rotation = Quaternion.Euler(_pitch + _pushPitchOffset, _yaw, 0f)",
            source);
    }
}
