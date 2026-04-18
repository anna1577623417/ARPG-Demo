/// <summary>
/// 默认探索 / 战斗：将 Follow 与 LookAt 同步到当前在场角色的双锚点。
/// </summary>
public sealed class FreeLookCameraState : GameplayCameraStateBase
{
    private readonly ICameraGameplayContext _ctx;

    public FreeLookCameraState(ICameraGameplayContext ctx)
    {
        _ctx = ctx;
    }

    public override void Enter()
    {
        ApplyFollowLook(_ctx);
    }

    public static void ApplyFollowLook(ICameraGameplayContext ctx)
    {
        if (ctx?.ActionCamera == null || ctx.PlayerManager?.ActiveCharacter == null)
        {
            return;
        }

        var pc = ctx.PlayerManager.ActiveCharacter;
        var follow = pc.CameraFollowOrRoot;
        var look = pc.CameraLookAtOrFollow;
        if (follow != null)
        {
            ctx.ActionCamera.SetFollowAndLookAt(follow, look);
        }
    }
}
