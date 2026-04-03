public sealed class WorkerMoveToCollectState : NpcState<Worker>
{
    public WorkerMoveToCollectState(Worker npc) : base(npc) { }
    public override string Name => "MoveToCollect";

    public override void Tick(float deltaTime)
    {
        if (Npc.MoveToCollectPoint())
            Npc.EnterCollect();
    }
}
