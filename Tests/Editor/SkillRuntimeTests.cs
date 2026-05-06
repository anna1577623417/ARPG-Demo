#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

public sealed class SkillRuntimeTests
{
    [Test]
    public void SkillRuntime_ChargesSpendsAndRefills_Stepwise()
    {
        var skill = ScriptableObject.CreateInstance<SkillDataSO>();
        skill.maxCharges = 2;
        skill.rechargeTime = 10f;

        var rt = new SkillRuntime(skill);
        Assert.AreEqual(2, rt.CurrentCharges);

        rt.StartCooldown(null);
        Assert.AreEqual(1, rt.CurrentCharges);
        Assert.Greater(rt.RechargeTimer, 0f);

        rt.RechargeTimer = 0f;
        rt.TickCooldown(11f, null);
        Assert.AreEqual(2, rt.CurrentCharges);
    }

    [Test]
    public void ModifyCooldown_ResetClearsRemainderForSingleCharge()
    {
        var skill = ScriptableObject.CreateInstance<SkillDataSO>();
        skill.baseCooldown = 5f;

        var rt = new SkillRuntime(skill);
        rt.CdRemaining = 4f;
        rt.ModifyCooldown(CooldownOp.Reset, 0f);
        Assert.AreEqual(0f, rt.CdRemaining, 1e-4f);
    }

    [Test]
    public void TaskExecutor_AllTasksCompleted_AfterMarkedComplete()
    {
        var so = ScriptableObject.CreateInstance<InstantCompleteTaskStubSO>();
        var ctx = new SkillContext();

        var ex = new TaskExecutor();
        ex.StartAll(new[] { so }, ctx);
        ex.UpdateAll(ctx, 1f / 60f);
        Assert.IsTrue(ex.AllCompleted());

        ex.ExitAll(ctx);
    }

    sealed class InstantCompleteTaskStubSO : AbilityTaskSO
    {
        public override AbilityTask CreateInstance() => new StubTask(this);
    }

    sealed class StubTask : AbilityTask
    {
        readonly InstantCompleteTaskStubSO _unused;

        public StubTask(InstantCompleteTaskStubSO unused)
        {
            _unused = unused;
        }

        public override void OnEnter(SkillContext ctx)
        {
            Complete();
        }
    }
}
#endif
