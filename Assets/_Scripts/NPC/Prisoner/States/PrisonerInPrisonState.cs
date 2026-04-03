public sealed class PrisonerInPrisonState : NpcState<Prisoner>
{
    public PrisonerInPrisonState(Prisoner npc) : base(npc) { }
    public override string Name => "InPrison";
}
