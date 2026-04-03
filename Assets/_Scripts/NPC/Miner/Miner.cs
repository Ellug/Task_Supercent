using System;
using UnityEngine;

// 지정 채굴 지점으로 이동해 인터벌마다 광석을 채굴하는 NPC
// 채굴 결과는 OreMined 이벤트로 외부(NpcManager 등)에 전달
public class Miner : NPC
{
    [Header("Points")]
    [SerializeField] private Transform _waitPoint;
    [SerializeField] private Transform _minePoint;

    [Header("Mining")]
    [SerializeField, Min(0.05f)] private float _mineInterval = 1f;
    [SerializeField, Min(1)] private int _yieldAmount = 1;

    public event Action<Miner, int> OreMined;

    private MinerWaitState _waitState;
    private MinerMoveToMineState _moveToMineState;
    private MinerMineState _mineState;

    private bool _isMiningEnabled = true;

    protected override void BuildStates()
    {
        _waitState = new MinerWaitState(this);
        _moveToMineState = new MinerMoveToMineState(this);
        _mineState = new MinerMineState(this);
    }

    protected override void EnterInitialState()
    {
        if (_isMiningEnabled)
            ChangeState(_moveToMineState);
        else
            ChangeState(_waitState);
    }

    public void SetWaitPoint(Transform point)
    {
        _waitPoint = point;
    }

    public void SetMinePoint(Transform point)
    {
        _minePoint = point;
    }

    public void StartMining()
    {
        _isMiningEnabled = true;
        ChangeState(_moveToMineState);
    }

    public void StopMining()
    {
        _isMiningEnabled = false;
        ChangeState(_waitState);
    }

    // 아래 internal 멤버는 외부 상태 클래스(MinerXxxState)에서만 사용
    internal bool IsMiningEnabled => _isMiningEnabled;
    internal float MineInterval => Mathf.Max(0.05f, _mineInterval);
    internal int YieldAmount => Mathf.Max(1, _yieldAmount);

    internal bool MoveToWaitPoint() => MoveToPoint(WaitPosition);
    internal bool MoveToMinePoint() => MoveToPoint(MinePosition);

    internal void RaiseOreMined()
    {
        OreMined?.Invoke(this, YieldAmount);
    }

    internal void EnterWait() => ChangeState(_waitState);
    internal void EnterMoveToMine() => ChangeState(_moveToMineState);
    internal void EnterMine() => ChangeState(_mineState);

    private Vector3 WaitPosition => _waitPoint != null ? _waitPoint.position : transform.position;
    private Vector3 MinePosition => _minePoint != null ? _minePoint.position : transform.position;
}
