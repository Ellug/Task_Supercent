public sealed class PrisonerMoveToQueueState : NpcState<Prisoner>
{
    public PrisonerMoveToQueueState(Prisoner npc) : base(npc) { }
    public override string Name => "MoveToQueue";

    public override void Tick(float deltaTime)
    {
        if (Npc.MoveToQueuePoint())
            Npc.EnterWaitForCuff();
    }
}
