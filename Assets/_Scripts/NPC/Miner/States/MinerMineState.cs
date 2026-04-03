using UnityEngine;

// 채굴 지점 도착 후 인터벌마다 OreMined 이벤트 발생
// 채굴 비활성화 시 Wait 상태로 전환
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
        if (!Npc.IsMiningEnabled)
        {
            Npc.EnterWait();
            return;
        }

        if (Time.time < _nextMineTime)
            return;

        _nextMineTime = Time.time + Npc.MineInterval;
        Npc.RaiseOreMined();
    }
}
