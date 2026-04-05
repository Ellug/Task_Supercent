public sealed class PrisonerInPrisonState : NpcState<Prisoner>
{
    // 감옥 상태 생성
    public PrisonerInPrisonState(Prisoner npc) : base(npc) { }
    public override string Name => "InPrison";
}
