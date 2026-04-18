/// <summary>
/// 死亡状态（终态）。停止移动，禁用输入。
/// 复活由外部系统通过事件或直接调用状态机切换实现。
/// </summary>
public class PlayerDeadState : PlayerState
{
    private PlayerController _controller;

    protected override void OnEnter(Player player)
    {
        player.GameplayTags.Clear();
        player.GameplayTags.Add((ulong)StateTag.Dead);
        player.IntentBuffer.Clear();

        player.StopMove();
        // Shared InputReader stays enabled for the party — only disable this pawn's controller.
        _controller = player.GetComponent<PlayerController>();
        if (_controller != null)
        {
            _controller.enabled = false;
        }
    }

    protected override void OnExit(Player player)
    {
        player.GameplayTags.Remove((ulong)StateTag.Dead);
        if (_controller != null)
        {
            _controller.enabled = true;
        }

        _controller = null;
    }

    protected override void OnLogicUpdate(Player player)
    {
        player.StopMove();
        player.ApplyMotor();
    }
}
