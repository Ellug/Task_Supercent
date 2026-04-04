using UnityEngine;

// MinerManager가 할당한 Mine을 찾아 이동/채굴하는 NPC
// 채굴 산출물은 MinerManager를 통해 시설로 원격 제출
public class Miner : NPC
{
    [Header("Mining")]
    [SerializeField, Min(0.05f)] private float _mineInterval = 1f;
    [SerializeField, Min(1)] private int _mineDamage = 1;
    [SerializeField, Min(0f)] private float _mineArriveDistance = 0.8f;
    [SerializeField, Min(0f)] private float _mineExtraDistance = 0.5f;

    private MinerWaitState _waitState;
    private MinerMoveToMineState _moveToMineState;
    private MinerMineState _mineState;

    private MinerManager _manager;
    private Mine _targetMine;
    private bool _isWorking;

    protected override void BuildStates()
    {
        _waitState = new MinerWaitState(this);
        _moveToMineState = new MinerMoveToMineState(this);
        _mineState = new MinerMineState(this);
    }

    protected override void EnterInitialState()
    {
        ChangeState(_waitState);
    }

    public void Initialize(MinerManager manager)
    {
        _manager = manager;
        _isWorking = true;
        ChangeState(_waitState);
    }

    public void StartWork()
    {
        _isWorking = true;
        ChangeState(_waitState);
    }

    public void StopWork()
    {
        _isWorking = false;
        ClearTargetMine();
        ChangeState(_waitState);
    }

    void OnDestroy()
    {
        _manager?.UnregisterMiner(this);
    }

    internal bool IsWorking => _isWorking;
    internal float MineInterval => Mathf.Max(0.05f, _mineInterval);
    internal int MineDamage => Mathf.Max(1, _mineDamage);
    internal bool HasValidTargetMine => _targetMine != null && _targetMine.gameObject.activeInHierarchy;

    internal bool TryAcquireTargetMine()
    {
        if (_manager == null)
            return false;

        if (!_manager.TryAssignMine(this, transform.position, out Mine mine) || mine == null)
            return false;

        _targetMine = mine;
        return true;
    }

    internal void ClearTargetMine()
    {
        _manager?.ReleaseMine(this);
        _targetMine = null;
    }

    internal bool MoveToTargetMine()
    {
        if (!HasValidTargetMine)
            return false;

        float stopDistance = MineStopDistance;
        return MoveToPoint(_targetMine.transform.position, stopDistance);
    }

    internal bool IsAtTargetMine()
    {
        if (!HasValidTargetMine)
            return false;

        Vector3 toTarget = _targetMine.transform.position - transform.position;
        toTarget.y = 0f;
        return toTarget.magnitude <= MineStopDistance;
    }

    internal void MineTarget()
    {
        if (!HasValidTargetMine)
        {
            ClearTargetMine();
            return;
        }

        if (!_targetMine.TryMine(MineDamage, out ResourceData yieldResource, out int yieldAmount))
            return;

        _manager?.OnMinerDepletedMine(this, _targetMine, yieldResource, yieldAmount);
        _targetMine = null;
    }

    internal void EnterWait() => ChangeState(_waitState);
    internal void EnterMoveToMine() => ChangeState(_moveToMineState);
    internal void EnterMine() => ChangeState(_mineState);

    private float MineStopDistance => Mathf.Max(ArriveDistance, _mineArriveDistance) + Mathf.Max(0f, _mineExtraDistance);
}
