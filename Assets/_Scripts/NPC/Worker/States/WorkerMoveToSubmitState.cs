public sealed class WorkerMoveToSubmitState : NpcState<Worker>
{
    // 제출 이동 상태 생성
    public WorkerMoveToSubmitState(Worker npc) : base(npc) { }
    public override string Name => "MoveToSubmit";

    // 제출 지점 이동 처리
    public override void Tick(float deltaTime)
    {
        if (!Npc.IsWorking)
        {
            Npc.EnterWait();
            return;
        }

        if (Npc.MoveToSubmitPoint())
            Npc.EnterSubmit();
    }
}
