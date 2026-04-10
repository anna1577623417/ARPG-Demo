/// <summary>
/// 死亡状态（终态）。停止移动，禁用输入。
/// 复活由外部系统通过事件或直接调用状态机切换实现。
/// </summary>
public class PlayerDeadState : PlayerState
{
    protected override void OnEnter(Player player)
    {
        player.StopMove();
        if (player.InputReader != null)
            player.InputReader.DisableAllInput();
    }

    protected override void OnExit(Player player)
    {
        if (player.InputReader != null)
            player.InputReader.EnableInput();
    }

    protected override void OnLogicUpdate(Player player)
    {
        player.StopMove();
        player.ApplyMotor();
    }
}
