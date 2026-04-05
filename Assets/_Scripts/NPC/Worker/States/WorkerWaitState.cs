public sealed class WorkerWaitState : NpcState<Worker>
{
    // 대기 상태 생성
    public WorkerWaitState(Worker npc) : base(npc) { }
    public override string Name => "Wait";

    // 작업 대기 처리
    public override void Tick(float deltaTime)
    {
        if (Npc.IsWorking)
        {
            Npc.EnterMoveToCollect();
            return;
        }

        Npc.MoveToWaitPoint();
    }
}
