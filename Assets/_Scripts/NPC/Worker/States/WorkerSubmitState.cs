// 제출 지점 도착 시 SubmitRequested 이벤트 발생
// 보유량이 없으면 수집 이동으로 전환
public sealed class WorkerSubmitState : NpcState<Worker>
{
    public WorkerSubmitState(Worker npc) : base(npc) { }
    public override string Name => "Submit";

    public override void Enter()
    {
        if (Npc.CarriedAmount <= 0)
        {
            Npc.EnterMoveToCollect();
            return;
        }

        Npc.RequestSubmit();
    }
}
