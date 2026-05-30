/// <summary>
/// 战斗帧上下文快照（0-GC struct）— 由 Player 单点 Build，Route / Graph / Condition 只读。
/// </summary>
public struct CombatContextSnapshot
{
    public bool IsAirborne;
    public MoveDirection8 MoveDirection;
    /// <summary>当前 Stage 内是否已确认命中（派生窗等）；Stage 进入时由 SkillEntryService 清零。</summary>
    public bool HitConfirmedThisStage;
    public float SnapshotTime;

    public bool MatchesMoveDirection(MoveDirection8 required)
    {
        if (required == MoveDirection8.None)
        {
            return true;
        }

        return MoveDirection == required;
    }
}
