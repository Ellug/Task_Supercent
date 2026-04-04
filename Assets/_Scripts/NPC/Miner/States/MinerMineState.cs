using UnityEngine;

// 타겟 Mine을 인터벌마다 1 대미지씩 채굴
public sealed class MinerMineState : NpcState<Miner>
{
    private float _nextMineTime;

    public MinerMineState(Miner npc) : base(npc) { }
    public override string Name => "Mine";

    public override void Enter()
    {
        _nextMineTime = Time.time + Npc.MineInterval;
    }

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

        if (!Npc.IsAtTargetMine())
        {
            Npc.EnterMoveToMine();
            return;
        }

        if (Time.time < _nextMineTime)
            return;

        _nextMineTime = Time.time + Npc.MineInterval;
        Npc.MineTarget();

        if (!Npc.HasValidTargetMine)
            Npc.EnterWait();
    }
}
