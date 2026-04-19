/// <summary>
/// 死亡状态（终态）。停止移动，禁用输入。
/// 复活由外部系统通过事件或直接调用状态机切换实现。
/// </summary>
public class PlayerDeadState : PlayerState
{
    protected override void OnEnter(Player player)
    {
        player.GameplayTags.Clear();
        player.GameplayTags.Add((ulong)StateTag.Dead);
        player.IntentBuffer.Clear();

        player.StopMove();
        if (player.InputReader != null)
        {
            // 不能整图 DisableAllInput：否则 Party* 也被关掉，阵亡后无法换人。
            player.InputReader.DisableGameplayExceptPartySwitch();
        }
    }

    protected override void OnExit(Player player)
    {
        player.GameplayTags.Remove((ulong)StateTag.Dead);
        if (player.InputReader != null)
            player.InputReader.EnableInput();
    }

    protected override void OnLogicUpdate(Player player)
    {
        player.StopMove();
        player.ApplyMotor();
    }
}
