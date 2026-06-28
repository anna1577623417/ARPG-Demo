using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>188.3 — CombatObject 数据层 + Runtime 验收测试。</summary>
public sealed class CombatObjectTests
{
    // ─── 数据层：CombatObjectDefinitionSO 验证 ─────────────

    [Test]
    public void Definition_DefaultValues_AreValid()
    {
        var def = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        try
        {
            def.Shape = ScriptableObject.CreateInstance<SphereShapeSO>();
            def.Damage = ScriptableObject.CreateInstance<DamageDefinitionSO>();
            def.Lifecycle = LifecycleParams.DefaultMeleeOneShot;
            Assert.IsTrue(def.IsValid(out var _));
        }
        finally
        {
            if (def.Shape != null) Object.DestroyImmediate(def.Shape);
            if (def.Damage != null) Object.DestroyImmediate(def.Damage);
            Object.DestroyImmediate(def);
        }
    }

    [Test]
    public void Definition_MissingShape_IsInvalid()
    {
        var def = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        try
        {
            def.Damage = ScriptableObject.CreateInstance<DamageDefinitionSO>();
            def.Lifecycle = LifecycleParams.DefaultMeleeOneShot;
            Assert.IsFalse(def.IsValid(out var reason));
            Assert.AreEqual("Shape is null", reason);
        }
        finally
        {
            if (def.Damage != null) Object.DestroyImmediate(def.Damage);
            Object.DestroyImmediate(def);
        }
    }

    // ─── Lifecycle 默认值 ───────────────────────────────

    [Test]
    public void Lifecycle_DefaultMelee_HasExpectedDefaults()
    {
        var l = LifecycleParams.DefaultMeleeOneShot;
        Assert.AreEqual(0.1f, l.Duration);
        Assert.AreEqual(0f, l.TickInterval);
        Assert.AreEqual(1, l.MaxHitsPerTarget);
    }

    [Test]
    public void Lifecycle_DefaultDot_HasExpectedDefaults()
    {
        var l = LifecycleParams.DefaultDot(duration: 5f, tick: 0.5f);
        Assert.AreEqual(5f, l.Duration);
        Assert.AreEqual(0.5f, l.TickInterval);
        Assert.AreEqual(999, l.MaxHitsPerTarget);
    }

    // ─── Movement 默认值 ────────────────────────────────

    [Test]
    public void Movement_DefaultStatic_HasKindStatic()
    {
        var m = MovementParams.DefaultStatic;
        Assert.AreEqual(MovementKind.Static, m.Kind);
    }

    [Test]
    public void Movement_DefaultLinear_SpeedAndDistance()
    {
        var m = MovementParams.DefaultLinear(speed: 25f, maxDist: 20f);
        Assert.AreEqual(MovementKind.Linear, m.Kind);
        Assert.AreEqual(25f, m.Speed);
        Assert.AreEqual(20f, m.MaxDistance);
    }

    // ─── Spawner: 基本 Spawn / Tick / Expire ─────────────

    [Test]
    public void Spawner_NullDefinition_ReturnsNull()
    {
        var sp = new CombatObjectSpawner();
        var co = sp.Spawn(null, null, Vector3.zero, Quaternion.identity);
        Assert.IsNull(co);
        Assert.AreEqual(0, sp.ActiveCount);
    }

    [Test]
    public void Spawner_InvalidDefinition_Rejected()
    {
        var def = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        var sp = new CombatObjectSpawner();
        try
        {
            // 缺 Shape / Damage
            LogAssert.ignoreFailingMessages = true;
            var co = sp.Spawn(def, null, Vector3.zero, Quaternion.identity);
            LogAssert.ignoreFailingMessages = false;
            Assert.IsNull(co);
        }
        finally { Object.DestroyImmediate(def); }
    }

    [Test]
    public void Spawner_ValidDef_CreatesActiveObject()
    {
        var (def, shape, dmg) = MakeBasicDefinition();
        var sp = new CombatObjectSpawner();
        try
        {
            var co = sp.Spawn(def, null, Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(co);
            Assert.AreEqual(1, sp.ActiveCount);
            Assert.IsFalse(co.IsExpired);
        }
        finally { CleanupDef(def, shape, dmg); }
    }

    [Test]
    public void Spawner_Tick_AdvancesElapsed()
    {
        var (def, shape, dmg) = MakeBasicDefinition();
        var sp = new CombatObjectSpawner();
        try
        {
            var co = sp.Spawn(def, null, Vector3.zero, Quaternion.identity);
            sp.Tick(0.05f);
            Assert.AreEqual(0.05f, co.ElapsedSec, 0.001f);
        }
        finally { CleanupDef(def, shape, dmg); }
    }

    [Test]
    public void Spawner_DurationExpired_RemovesFromActive()
    {
        var (def, shape, dmg) = MakeBasicDefinition();
        var sp = new CombatObjectSpawner();
        try
        {
            // Duration=0.1s
            sp.Spawn(def, null, Vector3.zero, Quaternion.identity);
            Assert.AreEqual(1, sp.ActiveCount);
            sp.Tick(0.2f);  // 超出 duration
            Assert.AreEqual(0, sp.ActiveCount, "Active 应清空（过期）");
        }
        finally { CleanupDef(def, shape, dmg); }
    }

    [Test]
    public void Spawner_Clear_RemovesAllAndPoolsObjects()
    {
        var (def, shape, dmg) = MakeBasicDefinition();
        var sp = new CombatObjectSpawner();
        try
        {
            sp.Spawn(def, null, Vector3.zero, Quaternion.identity);
            sp.Spawn(def, null, Vector3.zero, Quaternion.identity);
            Assert.AreEqual(2, sp.ActiveCount);
            sp.Clear();
            Assert.AreEqual(0, sp.ActiveCount);
        }
        finally { CleanupDef(def, shape, dmg); }
    }

    // ─── Movement: Linear 位置推进 ───────────────────────

    [Test]
    public void Runtime_LinearMovement_PositionAdvances()
    {
        var (def, shape, dmg) = MakeBasicDefinition();
        def.Movement = MovementParams.DefaultLinear(speed: 10f, maxDist: 100f);
        def.Lifecycle.Duration = 5f;
        var sp = new CombatObjectSpawner();
        try
        {
            var co = sp.Spawn(def, null, Vector3.zero, Quaternion.identity);
            sp.Tick(0.5f);
            // forward = (0,0,1) → t=0.5s × 10m/s = 5m forward
            Assert.AreEqual(5f, co.CurrentWorldPos.z, 0.01f);
        }
        finally { CleanupDef(def, shape, dmg); }
    }

    [Test]
    public void Runtime_LinearMovement_MaxDistanceClamps()
    {
        var (def, shape, dmg) = MakeBasicDefinition();
        def.Movement = MovementParams.DefaultLinear(speed: 10f, maxDist: 3f);
        def.Lifecycle.Duration = 5f;
        var sp = new CombatObjectSpawner();
        try
        {
            var co = sp.Spawn(def, null, Vector3.zero, Quaternion.identity);
            sp.Tick(1f);  // 10m/s × 1s = 10m，被 MaxDistance=3 钳制
            Assert.AreEqual(3f, co.CurrentWorldPos.z, 0.01f);
        }
        finally { CleanupDef(def, shape, dmg); }
    }

    // ─── OnExpire 嵌套链深限制 ───────────────────────────

    [Test]
    public void Spawner_OnExpireRecursion_StopsAtDepth3()
    {
        var (def, shape, dmg) = MakeBasicDefinition();
        // 自指引用：OnExpire 又生成自己 → 无限递归（应被 MaxDepth=3 切断）
        def.Lifecycle.OnExpireSpawn = def;
        var sp = new CombatObjectSpawner();
        try
        {
            LogAssert.ignoreFailingMessages = true;
            sp.Spawn(def, null, Vector3.zero, Quaternion.identity);
            // 多次 Tick 推进 expire
            for (var i = 0; i < 5; i++) sp.Tick(0.2f);
            LogAssert.ignoreFailingMessages = false;
            Assert.LessOrEqual(sp.ActiveCount, 1, "嵌套链深 ≤ 3 应阻止无限增长");
        }
        finally { CleanupDef(def, shape, dmg); }
    }

    // ─── W11 Curve Movement ──────────────────────────────

    [Test]
    public void Runtime_CurveMovement_UsesAnimationCurves()
    {
        var (def, shape, dmg) = MakeBasicDefinition();
        def.Movement = new MovementParams
        {
            Kind = MovementKind.Curve,
            LocalOffsetZOverTime = AnimationCurve.Linear(0f, 0f, 1f, 5f),
        };
        def.Lifecycle.Duration = 1f;
        var sp = new CombatObjectSpawner();
        try
        {
            var co = sp.Spawn(def, null, Vector3.zero, Quaternion.identity);
            sp.Tick(0.5f);
            // t=0.5 时 LocalOffsetZ = 2.5
            Assert.AreEqual(2.5f, co.CurrentWorldPos.z, 0.01f);
        }
        finally { CleanupDef(def, shape, dmg); }
    }

    // ─── W12 Expand Movement ────────────────────────────

    [Test]
    public void Runtime_ExpandMovement_RadiusInterpolates()
    {
        var (def, shape, dmg) = MakeBasicDefinition();
        def.Movement = new MovementParams
        {
            Kind = MovementKind.Expand,
            StartRadius = 0f,
            EndRadius = 10f,
        };
        def.Lifecycle.Duration = 1f;
        var sp = new CombatObjectSpawner();
        try
        {
            var co = sp.Spawn(def, null, Vector3.zero, Quaternion.identity);
            sp.Tick(0.5f);
            // t=0.5 → 半径 5
            Assert.AreEqual(5f, co.CurrentExpandedRadius, 0.1f);
        }
        finally { CleanupDef(def, shape, dmg); }
    }

    // ─── Ring / Beam Shape ──────────────────────────────

    [Test]
    public void RingShape_DrawGizmo_DoesNotThrow()
    {
        var ring = ScriptableObject.CreateInstance<RingShapeSO>();
        ring.innerRadius = 1f;
        ring.outerRadius = 3f;
        try
        {
            // 实际 Gizmo 调用需 OnDrawGizmos 上下文，仅校验不抛
            Assert.DoesNotThrow(() => ring.DrawGizmo(Vector3.zero, Quaternion.identity, Color.red));
        }
        finally { Object.DestroyImmediate(ring); }
    }

    [Test]
    public void BeamShape_DrawGizmo_DoesNotThrow()
    {
        var beam = ScriptableObject.CreateInstance<BeamShapeSO>();
        beam.length = 10f;
        beam.width = 0.5f;
        try
        {
            Assert.DoesNotThrow(() => beam.DrawGizmo(Vector3.zero, Quaternion.identity, Color.red));
        }
        finally { Object.DestroyImmediate(beam); }
    }

    // ─── Utility ──────────────────────────────────────────

    static (CombatObjectDefinitionSO, SphereShapeSO, DamageDefinitionSO) MakeBasicDefinition()
    {
        var shape = ScriptableObject.CreateInstance<SphereShapeSO>();
        shape.radius = 1f;
        var dmg = ScriptableObject.CreateInstance<DamageDefinitionSO>();
        dmg.Amount = 10f;
        var def = ScriptableObject.CreateInstance<CombatObjectDefinitionSO>();
        def.Shape = shape;
        def.Damage = dmg;
        def.Movement = MovementParams.DefaultStatic;
        def.Lifecycle = LifecycleParams.DefaultMeleeOneShot;
        def.QueryLayerMask = ~0;
        return (def, shape, dmg);
    }

    static void CleanupDef(CombatObjectDefinitionSO def, SphereShapeSO shape, DamageDefinitionSO dmg)
    {
        Object.DestroyImmediate(def);
        if (shape != null) Object.DestroyImmediate(shape);
        if (dmg != null) Object.DestroyImmediate(dmg);
    }
}
