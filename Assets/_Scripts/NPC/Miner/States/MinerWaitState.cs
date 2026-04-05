public sealed class MinerWaitState : NpcState<Miner>
{
    // 대기 상태 생성
    public MinerWaitState(Miner npc) : base(npc) { }
    public override string Name => "Wait";

    // 대기 중 타겟 확보 처리
    public override void Tick(float deltaTime)
    {
        if (!Npc.IsWorking)
            return;

        if (!Npc.TryAcquireTargetMine())
            return;

        Npc.EnterMoveToMine();
    }
}
