public sealed class MinerMoveToMineState : NpcState<Miner>
{
    // 이동 상태 생성
    public MinerMoveToMineState(Miner npc) : base(npc) { }
    public override string Name => "MoveToMine";

    // 타겟 광맥 이동 처리
    public override void Tick(float deltaTime)
    {
        if (!Npc.IsWorking)
        {
            Npc.ClearTargetMine();
            Npc.EnterWait();
            return;
        }

        if (!Npc.HasValidTargetMine)
        {
            Npc.ClearTargetMine();
            Npc.EnterWait();
            return;
        }

        if (Npc.MoveToTargetMine())
            Npc.EnterMine();
    }
}
