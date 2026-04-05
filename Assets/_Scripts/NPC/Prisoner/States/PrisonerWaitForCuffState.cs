public sealed class PrisonerWaitForCuffState : NpcState<Prisoner>
{
    // 수갑 대기 상태 생성
    public PrisonerWaitForCuffState(Prisoner npc) : base(npc) { }
    public override string Name => "WaitForCuff";
}
