public sealed class MinerMoveToMineState : NpcState<Miner>
{
    public MinerMoveToMineState(Miner npc) : base(npc) { }
    public override string Name => "MoveToMine";

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
