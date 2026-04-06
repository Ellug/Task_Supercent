using System;
using UnityEngine;

// Queue/Receive/Prison 포인트 이동을 상태로 처리하는 죄수 NPC
// 수령 지점·감옥 지점 도착 시점은 이벤트로 외부 매니저에 통보
public class Prisoner : NPC
{
    [Header("Points")]
    [SerializeField] private Transform _queuePoint;
    [SerializeField] private Transform _receiveCuffPoint;
    [SerializeField] private Transform _prisonPoint;

    public bool HasCuff { get; private set; }

    public event Action<Prisoner> ArrivedAtReceivePoint;
    public event Action<Prisoner> ArrivedAtPrisonPoint;

    private PrisonerMoveToQueueState _moveToQueueState;
    private PrisonerMoveToReceiveState _moveToReceiveState;
    private PrisonerWaitForCuffState _waitForCuffState;
    private PrisonerMoveToPrisonState _moveToPrisonState;
    private PrisonerInPrisonState _inPrisonState;
    private bool _externalStateRequested;

    protected override void BuildStates()
    {
        _moveToQueueState = new PrisonerMoveToQueueState(this);
        _moveToReceiveState = new PrisonerMoveToReceiveState(this);
        _waitForCuffState = new PrisonerWaitForCuffState(this);
        _moveToPrisonState = new PrisonerMoveToPrisonState(this);
        _inPrisonState = new PrisonerInPrisonState(this);
    }

    protected override void EnterInitialState()
    {
        if (_externalStateRequested)
            return;

        ChangeState(_moveToQueueState);
    }

    // 대기열 포인트 설정
    public void SetQueuePoint(Transform point)
    {
        _queuePoint = point;
    }

    // 수령 포인트 설정
    public void SetReceivePoint(Transform point)
    {
        _receiveCuffPoint = point;
    }

    // 감옥 포인트 설정
    public void SetPrisonPoint(Transform point)
    {
        _prisonPoint = point;
    }

    // 대기열로 이동 (Cuff 초기화)
    public void MoveToQueue()
    {
        _externalStateRequested = true;
        HasCuff = false;
        ChangeState(_moveToQueueState);
    }

    // 수령 지점으로 이동
    public void MoveToReceive()
    {
        _externalStateRequested = true;
        ChangeState(_moveToReceiveState);
    }

    // 감옥으로 이동 (HasCuff = true)
    public void MoveToPrison()
    {
        _externalStateRequested = true;
        HasCuff = true;
        ChangeState(_moveToPrisonState);
    }

    // 아래 internal 멤버는 외부 상태 클래스(PrisonerXxxState)에서만 사용
    internal bool MoveToQueuePoint() => MoveToPoint(QueuePosition);
    internal bool MoveToReceivePoint() => MoveToPoint(ReceivePosition);
    // 슬롯 도착 시 _prisonPoint의 rotation으로 전환 (그리드 오브젝트 +Z 방향으로 정렬)
    internal bool MoveToPrisonPoint() => MoveToPoint(PrisonPosition, arrivalRotation: _prisonPoint != null ? _prisonPoint.rotation : null);

    internal void EnterWaitForCuff() => ChangeState(_waitForCuffState);
    internal void EnterInPrison() => ChangeState(_inPrisonState);

    internal void RaiseArrivedAtReceivePoint() => ArrivedAtReceivePoint?.Invoke(this);
    internal void RaiseArrivedAtPrisonPoint() => ArrivedAtPrisonPoint?.Invoke(this);

    private Vector3 QueuePosition => _queuePoint != null ? _queuePoint.position : transform.position;
    private Vector3 ReceivePosition => _receiveCuffPoint != null ? _receiveCuffPoint.position : transform.position;
    private Vector3 PrisonPosition => _prisonPoint != null ? _prisonPoint.position : transform.position;
}
