public sealed class WorkerMoveToCollectState : NpcState<Worker>
{
    public WorkerMoveToCollectState(Worker npc) : base(npc) { }
    public override string Name => "MoveToCollect";

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
