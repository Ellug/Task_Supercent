public sealed class PrisonerMoveToQueueState : NpcState<Prisoner>
{
    // 대기열 이동 상태 생성
    public PrisonerMoveToQueueState(Prisoner npc) : base(npc) { }
    public override string Name => "MoveToQueue";

    // 대기열 이동 처리
    public override void Tick(float deltaTime)
    {
        if (Npc.MoveToQueuePoint())
            Npc.EnterWaitForCuff();
    }
}
