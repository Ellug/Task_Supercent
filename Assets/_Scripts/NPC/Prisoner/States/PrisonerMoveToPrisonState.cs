// 감옥 지점 도착 시 ArrivedAtPrisonPoint 이벤트 발생
public sealed class PrisonerMoveToPrisonState : NpcState<Prisoner>
{
    public PrisonerMoveToPrisonState(Prisoner npc) : base(npc) { }
    public override string Name => "MoveToPrison";

    public override void Tick(float deltaTime)
    {
        if (!Npc.MoveToPrisonPoint())
            return;

        Npc.RaiseArrivedAtPrisonPoint();
    }
}
