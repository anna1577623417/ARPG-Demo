using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class AnimatorRootMotionAssetScan232Tests
{
    [Test]
    public void DirectClipRootMotionIsOnlyTheKnownBypassedRunStartAsset()
    {
        var dataRoot = Application.dataPath + "/GameMain/Scripts/4_Data";
        var directAssets = Directory.GetFiles(dataRoot, "*.asset", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("MotionDriverMode: 3"))
            .Select(path => path.Replace('\\', '/'))
            .ToArray();

        Assert.AreEqual(1, directAssets.Length,
            "Direct ClipRootMotion asset set changed:\n" + string.Join("\n", directAssets));
        StringAssert.EndsWith(
            "/GhostSamurai_Common_Run_Start_ActionData.asset",
            directAssets[0]);
    }

    [Test]
    public void LegacyUseClipRootMotionFlagHasNoEnabledAssets()
    {
        var dataRoot = Application.dataPath + "/GameMain/Scripts/4_Data";
        var enabled = Directory.GetFiles(dataRoot, "*.asset", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("UseClipRootMotion: 1"))
            .Select(path => path.Replace('\\', '/'))
            .ToArray();

        Assert.That(enabled, Is.Empty,
            "Legacy UseClipRootMotion assets:\n" + string.Join("\n", enabled));
    }
}
