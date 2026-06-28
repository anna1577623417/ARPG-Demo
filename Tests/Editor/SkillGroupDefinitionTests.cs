using NUnit.Framework;
using UnityEngine;

public sealed class SkillGroupDefinitionTests
{
    [Test]
    public void SelectByDirection_DiagonalMissing_FallsToForwardAxis()
    {
        var fwd = ScriptableObject.CreateInstance<NormalRouteDefinition>();
        var group = ScriptableObject.CreateInstance<SkillGroupDefinition>();
        SetField(group, "forward", fwd);

        Assert.AreEqual(fwd, group.SelectByDirection(DirectionalRouteType.ForwardLeft));
        Assert.AreEqual(fwd, group.SelectByDirection(DirectionalRouteType.ForwardRight));
    }

    [Test]
    public void SelectByDirection_DiagonalMissing_FallsToBackwardAxis()
    {
        var back = ScriptableObject.CreateInstance<NormalRouteDefinition>();
        var group = ScriptableObject.CreateInstance<SkillGroupDefinition>();
        SetField(group, "backward", back);

        Assert.AreEqual(back, group.SelectByDirection(DirectionalRouteType.BackwardLeft));
        Assert.AreEqual(back, group.SelectByDirection(DirectionalRouteType.BackwardRight));
    }

    [Test]
    public void SelectByDirection_CardinalMissing_ReturnsNull()
    {
        var group = ScriptableObject.CreateInstance<SkillGroupDefinition>();
        Assert.IsNull(group.SelectByDirection(DirectionalRouteType.Left));
    }

    static void SetField<T>(object obj, string name, T value)
    {
        var t = obj.GetType();
        while (t != null)
        {
            var f = t.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (f != null)
            {
                f.SetValue(obj, value);
                return;
            }

            t = t.BaseType;
        }

        Assert.Fail($"Field not found: {name}");
    }
}
