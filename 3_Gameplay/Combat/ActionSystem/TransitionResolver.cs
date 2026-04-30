/// <summary>
/// 标签仲裁器：只根据 <see cref="FrameContext.CurrentTags"/> 与意图上的标签约束判断是否“可被状态考虑”。
/// 真正的状态迁移仍由当前 <see cref="EntityState{T}"/> 决定（避免把规则写死在仲裁器里导致难以扩展）。
/// </summary>
public static class TransitionResolver
{
    /// <summary>
    /// 判断意图是否仍有效且标签层面允许交给当前状态处理。
    /// </summary>
    public static bool CanOfferIntent(in FrameContext ctx, in GameplayIntent intent)
    {
        return CanOfferIntent(in ctx, in intent, out _);
    }

    /// <summary>
    /// 与 <see cref="CanOfferIntent(in FrameContext, in GameplayIntent)"/> 相同，
    /// 但附带拒绝原因，供调试仲裁链路使用。
    /// </summary>
    public static bool CanOfferIntent(in FrameContext ctx, in GameplayIntent intent, out string rejectReason)
    {
        rejectReason = null;

        if (ctx.Time >= intent.ExpireTime)
        {
            rejectReason = "expired";
            return false;
        }

        var tags = ctx.CurrentTags.Value;

        if ((tags & intent.ForbiddenTags) != 0UL)
        {
            rejectReason = "hit forbidden tags";
            return false;
        }

        if ((tags & intent.RequiredAllTags) != intent.RequiredAllTags)
        {
            rejectReason = "missing required-all tags";
            return false;
        }

        if (intent.RequiredAnyTags != 0UL && (tags & intent.RequiredAnyTags) == 0UL)
        {
            rejectReason = "missing required-any tags";
            return false;
        }

        return true;
    }
}
