using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class GameplayPresentationDependency232Tests
{
    [Test]
    public void GameplayRuntimeHasNoConcreteAnimationControllerOrRootMotionWrites()
    {
        var scriptsRoot = Application.dataPath + "/GameMain/Scripts";
        var gameplayRoot = Path.Combine(scriptsRoot, "3_Gameplay");
        var violations = new System.Collections.Generic.List<string>();

        foreach (var path in Directory.GetFiles(gameplayRoot, "*.cs", SearchOption.AllDirectories))
        {
            var normalized = path.Replace('\\', '/');
            if (normalized.EndsWith("/Characters/Player/Presentation/VisualFacingDriver.cs"))
            {
                continue;
            }

            var source = File.ReadAllText(path);
            if (source.Contains("GetComponent<PlayerAnimController>")
                || source.Contains("GetComponent<EntityAnimController>")
                || source.Contains("SetClipRootMotionEnabled(")
                || source.Contains(".applyRootMotion ="))
            {
                violations.Add(normalized);
            }
        }

        Assert.That(violations, Is.Empty,
            "Gameplay→Presentation concrete dependencies:\n" + string.Join("\n", violations));
    }
}
