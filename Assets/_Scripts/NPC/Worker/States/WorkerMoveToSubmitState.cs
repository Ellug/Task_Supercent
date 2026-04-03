public sealed class WorkerMoveToSubmitState : NpcState<Worker>
{
    public WorkerMoveToSubmitState(Worker npc) : base(npc) { }
    public override string Name => "MoveToSubmit";

    public override void Tick(float deltaTime)
    {
        if (Npc.MoveToSubmitPoint())
            Npc.EnterSubmit();
    }
}
