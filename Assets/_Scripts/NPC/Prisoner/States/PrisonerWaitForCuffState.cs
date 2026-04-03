public sealed class PrisonerWaitForCuffState : NpcState<Prisoner>
{
    public PrisonerWaitForCuffState(Prisoner npc) : base(npc) { }
    public override string Name => "WaitForCuff";
}
