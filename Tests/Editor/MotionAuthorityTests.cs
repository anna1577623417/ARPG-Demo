using NUnit.Framework;
using UnityEngine;

/// <summary>187.3 — Motion Authority 仲裁与优先级 EditMode 测试。</summary>
public sealed class MotionAuthorityTests
{
    // ─── Arbiter 基础 ─────────────────────────────────────

    [Test]
    public void Initial_HoldsLocomotion_Both()
    {
        var a = new MotionAuthorityArbiter();
        Assert.AreEqual(AuthorityHolder.Locomotion, a.MoveHolder);
        Assert.AreEqual(AuthorityHolder.Locomotion, a.RotateHolder);
        Assert.AreEqual(100, a.MovePriority);
        Assert.AreEqual(100, a.RotatePriority);
    }

    [Test]
    public void TryGrant_HigherPriority_Succeeds()
    {
        var a = new MotionAuthorityArbiter();
        var ok = a.TryGrant(AuthorityChannel.Move, new AuthorityRequest
        {
            Holder = AuthorityHolder.Action,
            Priority = AuthorityPriorityTable.ActionUnlocked,
            DebugReason = "test",
        });
        Assert.IsTrue(ok);
        Assert.AreEqual(AuthorityHolder.Action, a.MoveHolder);
        Assert.AreEqual(20, a.MovePriority);
    }

    [Test]
    public void TryGrant_LowerPriority_Rejected()
    {
        var a = new MotionAuthorityArbiter();
        a.TryGrant(AuthorityChannel.Move, new AuthorityRequest
        {
            Holder = AuthorityHolder.Action,
            Priority = AuthorityPriorityTable.ActionLocked,  // 10
        });

        // 试图用 Priority=40 抢占已持有的 10 → 拒绝
        var ok = a.TryGrant(AuthorityChannel.Move, new AuthorityRequest
        {
            Holder = AuthorityHolder.Recovery,
            Priority = AuthorityPriorityTable.Recovery,
        });
        Assert.IsFalse(ok);
        Assert.AreEqual(AuthorityHolder.Action, a.MoveHolder);
    }

    [Test]
    public void TryGrant_SameSource_NoEvent()
    {
        var a = new MotionAuthorityArbiter();
        a.TryGrant(AuthorityChannel.Move, new AuthorityRequest
        {
            Holder = AuthorityHolder.Action,
            Priority = 20,
        });
        var events = 0;
        a.AuthorityChanged += _ => events++;

        // 再次请求同源同优先级 → 不应触发事件
        var ok = a.TryGrant(AuthorityChannel.Move, new AuthorityRequest
        {
            Holder = AuthorityHolder.Action,
            Priority = 20,
        });
        Assert.IsTrue(ok);
        Assert.AreEqual(0, events);
    }

    // ─── Recovery 被抢占（核心：BUG ① 收敛）─────────────────

    [Test]
    public void Recovery_IsPreemptedBy_AnyProactiveIntent()
    {
        var a = new MotionAuthorityArbiter();
        // 先持 Recovery（WalkEnd 期间）
        a.TryGrant(AuthorityChannel.Move, new AuthorityRequest
        {
            Holder = AuthorityHolder.Recovery,
            Priority = AuthorityPriorityTable.Recovery,
        });

        // 翻滚 Intent 申请抢占
        var ok = a.TryGrant(AuthorityChannel.Move, new AuthorityRequest
        {
            Holder = AuthorityHolder.Action,
            Priority = AuthorityPriorityTable.ActionUnlocked,  // 20 < 40
        });
        Assert.IsTrue(ok, "Recovery 必须被 ActionUnlocked 抢占");
        Assert.AreEqual(AuthorityHolder.Action, a.MoveHolder);
    }

    [Test]
    public void Cinematic_CannotBePreempted()
    {
        var a = new MotionAuthorityArbiter();
        a.TryGrant(AuthorityChannel.Move, new AuthorityRequest
        {
            Holder = AuthorityHolder.Cinematic,
            Priority = AuthorityPriorityTable.Cinematic,  // 0
        });
        var ok = a.TryGrant(AuthorityChannel.Move, new AuthorityRequest
        {
            Holder = AuthorityHolder.Action,
            Priority = AuthorityPriorityTable.ActionLocked,  // 10
        });
        Assert.IsFalse(ok);
        Assert.AreEqual(AuthorityHolder.Cinematic, a.MoveHolder);
    }

    // ─── Release ─────────────────────────────────────────

    [Test]
    public void Release_FallsBackToLocomotion()
    {
        var a = new MotionAuthorityArbiter();
        a.TryGrant(AuthorityChannel.Move, new AuthorityRequest
        {
            Holder = AuthorityHolder.Action,
            Priority = 20,
        });
        var ok = a.Release(AuthorityChannel.Move, AuthorityHolder.Action, "OnExit");
        Assert.IsTrue(ok);
        Assert.AreEqual(AuthorityHolder.Locomotion, a.MoveHolder);
        Assert.AreEqual(100, a.MovePriority);
    }

    [Test]
    public void Release_WrongExpected_NoOp()
    {
        var a = new MotionAuthorityArbiter();
        a.TryGrant(AuthorityChannel.Move, new AuthorityRequest
        {
            Holder = AuthorityHolder.Action,
            Priority = 20,
        });
        // 另一个系统试图释放 Recovery 但当前是 Action → 不应改变
        var ok = a.Release(AuthorityChannel.Move, AuthorityHolder.Recovery, "wrong");
        Assert.IsFalse(ok);
        Assert.AreEqual(AuthorityHolder.Action, a.MoveHolder);
    }

    // ─── 双通道独立 ───────────────────────────────────────

    [Test]
    public void Move_And_Rotate_AreIndependent()
    {
        var a = new MotionAuthorityArbiter();
        // 跳劈：Move=Action 锁落点 / Rotate=Locomotion 玩家空中修正
        a.TryGrant(AuthorityChannel.Move, new AuthorityRequest
        {
            Holder = AuthorityHolder.Action,
            Priority = 10,
        });
        // Rotate 不变（仍 Locomotion 100）
        Assert.AreEqual(AuthorityHolder.Action, a.MoveHolder);
        Assert.AreEqual(AuthorityHolder.Locomotion, a.RotateHolder);
    }

    // ─── Event 广播 ──────────────────────────────────────

    [Test]
    public void AuthorityChanged_FiresWithCorrectFields()
    {
        var a = new MotionAuthorityArbiter();
        AuthorityChangedEvent captured = default;
        a.AuthorityChanged += e => captured = e;

        a.TryGrant(AuthorityChannel.Rotate, new AuthorityRequest
        {
            Holder = AuthorityHolder.Action,
            Priority = 15,
            DebugReason = "Attack_A1",
        }, now: 1.234f);

        Assert.AreEqual(AuthorityChannel.Rotate, captured.Channel);
        Assert.AreEqual(AuthorityHolder.Locomotion, captured.Previous);
        Assert.AreEqual(AuthorityHolder.Action, captured.Current);
        Assert.AreEqual(100, captured.PreviousPriority);
        Assert.AreEqual(15, captured.CurrentPriority);
        Assert.AreEqual("Attack_A1", captured.Reason);
        Assert.AreEqual(1.234f, captured.TimeStamp, 0.001f);
    }

    [Test]
    public void RecentChanges_KeepsLast8()
    {
        var a = new MotionAuthorityArbiter();
        for (var i = 0; i < 20; i++)
        {
            a.TryGrant(AuthorityChannel.Move, new AuthorityRequest
            {
                Holder = (i % 2 == 0) ? AuthorityHolder.Action : AuthorityHolder.Locomotion,
                Priority = (i % 2 == 0) ? 20 : 100,
            });
        }
        Assert.LessOrEqual(a.RecentChanges.Count, 8);
    }

    // ─── CanPreempt 查询 ─────────────────────────────────

    [Test]
    public void CanPreempt_ReturnsCorrectBoolean()
    {
        var a = new MotionAuthorityArbiter();
        a.TryGrant(AuthorityChannel.Move, new AuthorityRequest
        {
            Holder = AuthorityHolder.Action,
            Priority = 20,
        });
        Assert.IsTrue(a.CanPreempt(AuthorityChannel.Move, 10), "10 < 20 可抢");
        Assert.IsTrue(a.CanPreempt(AuthorityChannel.Move, 20), "20 == 20 可（同源刷新）");
        Assert.IsFalse(a.CanPreempt(AuthorityChannel.Move, 40), "40 > 20 不可抢");
    }

    [Test]
    public void HardReset_RestoresInitial()
    {
        var a = new MotionAuthorityArbiter();
        a.TryGrant(AuthorityChannel.Move, new AuthorityRequest { Holder = AuthorityHolder.Cinematic, Priority = 0 });
        a.HardReset();
        Assert.AreEqual(AuthorityHolder.Locomotion, a.MoveHolder);
        Assert.AreEqual(100, a.MovePriority);
        Assert.AreEqual(0, a.RecentChanges.Count);
    }

    // ─── Priority Table（W3） ────────────────────────────

    [Test]
    public void PriorityTable_LockedAction_ReturnsActionLocked()
    {
        var act = ScriptableObject.CreateInstance<ActionDataSO>();
        try
        {
            act.Category = ActionCategory.Offense;
            act.LockMoveDuringPlay = true;
            act.LockRotateDuringPlay = true;
            act.AuthorityPriorityOverride = -1;
            var p = AuthorityPriorityTable.ForAction(act, GameplayIntentKind.Skill_Entry_01);
            Assert.AreEqual(AuthorityPriorityTable.ActionLocked, p);
        }
        finally { Object.DestroyImmediate(act); }
    }

    [Test]
    public void PriorityTable_UnlockedAction_ReturnsActionUnlocked()
    {
        var act = ScriptableObject.CreateInstance<ActionDataSO>();
        try
        {
            act.Category = ActionCategory.Defensive;
            act.LockMoveDuringPlay = true;
            act.LockRotateDuringPlay = false; // 允许转
            act.AuthorityPriorityOverride = -1;
            var p = AuthorityPriorityTable.ForAction(act, GameplayIntentKind.Skill_Entry_01);
            Assert.AreEqual(AuthorityPriorityTable.ActionUnlocked, p);
        }
        finally { Object.DestroyImmediate(act); }
    }

    [Test]
    public void PriorityTable_RecoveryAction_ReturnsRecovery()
    {
        var act = ScriptableObject.CreateInstance<ActionDataSO>();
        try
        {
            act.IsLocomotionRecovery = true;
            act.AuthorityPriorityOverride = -1;
            var p = AuthorityPriorityTable.ForAction(act, GameplayIntentKind.Move);
            Assert.AreEqual(AuthorityPriorityTable.Recovery, p);
        }
        finally { Object.DestroyImmediate(act); }
    }

    [Test]
    public void PriorityTable_Override_TakesPrecedence()
    {
        var act = ScriptableObject.CreateInstance<ActionDataSO>();
        try
        {
            act.Category = ActionCategory.Offense;
            act.LockMoveDuringPlay = true;
            act.LockRotateDuringPlay = true;
            act.AuthorityPriorityOverride = 5;  // 覆盖
            var p = AuthorityPriorityTable.ForAction(act, GameplayIntentKind.Skill_Entry_01);
            Assert.AreEqual(5, p);
        }
        finally { Object.DestroyImmediate(act); }
    }

    [Test]
    public void PriorityTable_NullAction_ReturnsLocomotionDefault()
    {
        var p = AuthorityPriorityTable.ForAction(null, GameplayIntentKind.Move);
        Assert.AreEqual(AuthorityPriorityTable.LocomotionDefault, p);
    }

    [Test]
    public void PriorityTable_IsProactiveIntent_JumpAndSkill()
    {
        Assert.IsTrue(AuthorityPriorityTable.IsProactiveIntent(GameplayIntentKind.Jump));
        Assert.IsTrue(AuthorityPriorityTable.IsProactiveIntent(GameplayIntentKind.Skill_Entry_01));
        Assert.IsTrue(AuthorityPriorityTable.IsProactiveIntent(GameplayIntentKind.Skill_Entry_17));
        Assert.IsFalse(AuthorityPriorityTable.IsProactiveIntent(GameplayIntentKind.Move));
        Assert.IsFalse(AuthorityPriorityTable.IsProactiveIntent(GameplayIntentKind.None));
    }
}
