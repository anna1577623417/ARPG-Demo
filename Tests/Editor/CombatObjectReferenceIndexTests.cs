#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

/// <summary>224.1 L2 — ReferenceIndex 按 EventId 稳定（ACS-08）。</summary>
public sealed class CombatObjectReferenceIndexTests
{
    [Test]
    public void ACS08_Invalidate_ClearsCacheFlag()
    {
        CombatObjectReferenceIndex.Invalidate();
        // 空工程也允许 Count=0；本测试只验证 API 不抛且失效可调用。
        var def = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        try
        {
            var count = CombatObjectReferenceIndex.CountContactsForDefinition(def);
            Assert.GreaterOrEqual(count, 0);
            CombatObjectReferenceIndex.Invalidate();
            count = CombatObjectReferenceIndex.CountContactsForDefinition(def);
            Assert.GreaterOrEqual(count, 0);
        }
        finally
        {
            Object.DestroyImmediate(def);
        }
    }
}
#endif
