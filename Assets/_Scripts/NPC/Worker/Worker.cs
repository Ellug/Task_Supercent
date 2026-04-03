using System;
using UnityEngine;

// 수집 지점 → 제출 지점을 반복 순환하며 쇠고랑을 운반하는 NPC
// 수집·제출 실행은 이벤트로 외부(NpcManager 등)에 위임
public class Worker : NPC
{
    [Header("Points")]
    [SerializeField] private Transform _waitPoint;
    [SerializeField] private Transform _collectPoint;
    [SerializeField] private Transform _submitPoint;

    [Header("Carry")]
    [SerializeField, Min(1)] private int _targetCarryAmount = 3;

    public int CarriedAmount { get; private set; }

    public event Action<Worker, int> CollectRequested;
    public event Action<Worker, int> SubmitRequested;

    private WorkerWaitState _waitState;
    private WorkerMoveToCollectState _moveToCollectState;
    private WorkerCollectState _collectState;
    private WorkerMoveToSubmitState _moveToSubmitState;
    private WorkerSubmitState _submitState;

    protected override void BuildStates()
    {
        _waitState = new WorkerWaitState(this);
        _moveToCollectState = new WorkerMoveToCollectState(this);
        _collectState = new WorkerCollectState(this);
        _moveToSubmitState = new WorkerMoveToSubmitState(this);
        _submitState = new WorkerSubmitState(this);
    }

    protected override void EnterInitialState()
    {
        ChangeState(_waitState);
    }

    public void SetWaitPoint(Transform point)
    {
        _waitPoint = point;
    }

    public void SetCollectPoint(Transform point)
    {
        _collectPoint = point;
    }

    public void SetSubmitPoint(Transform point)
    {
        _submitPoint = point;
    }

    public void StartWork()
    {
        ChangeState(_moveToCollectState);
    }

    public void StopWork()
    {
        ChangeState(_waitState);
    }

    // 외부에서 수집 완료를 통보 — 목표량 달성 시 제출 이동으로 전환
    public void OnCollected(int amount)
    {
        CarriedAmount = Mathf.Clamp(CarriedAmount + Mathf.Max(0, amount), 0, _targetCarryAmount);
        if (CarriedAmount >= _targetCarryAmount)
            ChangeState(_moveToSubmitState);
    }

    // 외부에서 제출 완료를 통보 — 소진 시 수집 이동으로 전환
    public void OnSubmitted(int amount)
    {
        CarriedAmount = Mathf.Max(0, CarriedAmount - Mathf.Max(0, amount));
        if (CarriedAmount <= 0)
            ChangeState(_moveToCollectState);
    }

    // 아래 internal 멤버는 외부 상태 클래스(WorkerXxxState)에서만 사용
    internal bool MoveToWaitPoint() => MoveToPoint(WaitPosition);
    internal bool MoveToCollectPoint() => MoveToPoint(CollectPosition);
    internal bool MoveToSubmitPoint() => MoveToPoint(SubmitPosition);

    internal int GetCollectNeedAmount() => Mathf.Max(0, TargetCarryAmount - CarriedAmount);

    internal void RequestCollect(int amount)
    {
        CollectRequested?.Invoke(this, Mathf.Max(0, amount));
    }

    internal void RequestSubmit()
    {
        SubmitRequested?.Invoke(this, CarriedAmount);
    }

    internal void EnterWait() => ChangeState(_waitState);
    internal void EnterMoveToCollect() => ChangeState(_moveToCollectState);
    internal void EnterCollect() => ChangeState(_collectState);
    internal void EnterMoveToSubmit() => ChangeState(_moveToSubmitState);
    internal void EnterSubmit() => ChangeState(_submitState);

    private int TargetCarryAmount => Mathf.Max(1, _targetCarryAmount);
    private Vector3 WaitPosition => _waitPoint != null ? _waitPoint.position : transform.position;
    private Vector3 CollectPosition => _collectPoint != null ? _collectPoint.position : transform.position;
    private Vector3 SubmitPosition => _submitPoint != null ? _submitPoint.position : transform.position;
}
