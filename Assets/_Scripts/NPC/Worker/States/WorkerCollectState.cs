// 수집 지점에서 최대 적재량까지 인터벌 수집
public sealed class WorkerCollectState : NpcState<Worker>
{
    public WorkerCollectState(Worker npc) : base(npc) { }
    public override string Name => "Collect";

    public override void Tick(float deltaTime)
    {
        if (!Npc.IsWorking)
        {
            Npc.EnterWait();
            return;
        }

        if (Npc.IsCarryFull)
        {
            Npc.EnterMoveToSubmit();
            return;
        }

        Npc.TryCollectTick();
    }
}
