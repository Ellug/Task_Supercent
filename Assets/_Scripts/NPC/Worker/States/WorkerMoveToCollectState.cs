public sealed class WorkerMoveToCollectState : NpcState<Worker>
{
    // 수집 이동 상태 생성
    public WorkerMoveToCollectState(Worker npc) : base(npc) { }
    public override string Name => "MoveToCollect";

    // 수집 지점 이동 처리
    public override void Tick(float deltaTime)
    {
        if (!Npc.IsWorking)
        {
            Npc.EnterWait();
            return;
        }

        if (Npc.MoveToCollectPoint())
            Npc.EnterCollect();
    }
}
