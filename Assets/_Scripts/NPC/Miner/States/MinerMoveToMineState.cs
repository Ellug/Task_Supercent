public sealed class MinerMoveToMineState : NpcState<Miner>
{
    public MinerMoveToMineState(Miner npc) : base(npc) { }
    public override string Name => "MoveToMine";

    public override void Tick(float deltaTime)
    {
        if (Npc.MoveToMinePoint())
            Npc.EnterMine();
    }
}
