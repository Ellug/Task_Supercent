// 수령 지점 도착 시 ArrivedAtReceivePoint 이벤트 발생 후 쇠고랑 대기
public sealed class PrisonerMoveToReceiveState : NpcState<Prisoner>
{
    public PrisonerMoveToReceiveState(Prisoner npc) : base(npc) { }
    public override string Name => "MoveToReceive";

    public override void Tick(float deltaTime)
    {
        if (!Npc.MoveToReceivePoint())
            return;

        Npc.RaiseArrivedAtReceivePoint();
        Npc.EnterWaitForCuff();
    }
}
