public sealed class WorkerWaitState : NpcState<Worker>
{
    public WorkerWaitState(Worker npc) : base(npc) { }
    public override string Name => "Wait";

    public override void Tick(float deltaTime)
    {
        Npc.MoveToWaitPoint();
    }
}
