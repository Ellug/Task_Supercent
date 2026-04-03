using UnityEngine;

// 수집 지점 도착 시 필요 수량 계산 후 CollectRequested 이벤트 발생
// 이미 목표량을 채웠으면 바로 제출 이동으로 전환
public sealed class WorkerCollectState : NpcState<Worker>
{
    public WorkerCollectState(Worker npc) : base(npc) { }
    public override string Name => "Collect";

    public override void Enter()
    {
        int needed = Npc.GetCollectNeedAmount();
        if (needed <= 0)
        {
            Npc.EnterMoveToSubmit();
            return;
        }

        Npc.RequestCollect(needed);
    }
}
