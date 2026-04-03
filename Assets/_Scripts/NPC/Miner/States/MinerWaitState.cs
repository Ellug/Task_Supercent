public sealed class MinerWaitState : NpcState<Miner>
{
    public MinerWaitState(Miner npc) : base(npc) { }
    public override string Name => "Wait";

    public override void Tick(float deltaTime)
    {
        Npc.MoveToWaitPoint();
    }
}
