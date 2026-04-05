// 제출 지점에서 보유분을 모두 제출한 뒤 다시 수집 지점으로 이동
public sealed class WorkerSubmitState : NpcState<Worker>
{
    // 제출 상태 생성
    public WorkerSubmitState(Worker npc) : base(npc) { }
    public override string Name => "Submit";

    public override void Tick(float deltaTime)
    {
        if (!Npc.IsWorking)
        {
            Npc.EnterWait();
            return;
        }

        if (Npc.IsCarryEmpty)
        {
            Npc.EnterMoveToCollect();
            return;
        }

        Npc.TrySubmitTick();

        if (Npc.IsCarryEmpty)
            Npc.EnterMoveToCollect();
    }
}
