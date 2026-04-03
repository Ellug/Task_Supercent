using UnityEngine;

// NPC 공통 베이스 — 이동·상태 머신 처리를 담당
// 구체적인 상태 구성과 초기 상태 진입은 서브클래스에서 구현
[DisallowMultipleComponent]
public abstract class NPC : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float _moveSpeed = 3.5f;
    [SerializeField, Min(0f)] private float _arriveDistance = 0.05f;
    [SerializeField] private bool _rotateToMoveDirection = true;
    [SerializeField] private string _currentState;

    private readonly NpcStateMachine _stateMachine = new();

    protected float MoveSpeed => Mathf.Max(0f, _moveSpeed);
    protected float ArriveDistance => Mathf.Max(0f, _arriveDistance);
    protected bool RotateToMoveDirection => _rotateToMoveDirection;
    protected NpcStateMachine StateMachine => _stateMachine;

    // 상태 인스턴스 생성 (Awake 시점)
    protected abstract void BuildStates();
    // 초기 상태 진입 (Start 시점 — 씬 전체 초기화 이후)
    protected abstract void EnterInitialState();

    protected virtual void Awake()
    {
        BuildStates();
    }

    protected virtual void Start()
    {
        EnterInitialState();
    }

    protected virtual void Update()
    {
        _stateMachine.Tick(Time.deltaTime);
        _currentState = _stateMachine.CurrentStateName;
    }

    // protected internal — 서브클래스 및 같은 어셈블리의 외부 상태 클래스에서 호출 가능
    protected internal void ChangeState(INpcState state)
    {
        _stateMachine.ChangeState(state);
    }

    // targetPosition을 향해 이동 — stopDistance 이내 도착 시 true 반환
    protected internal bool MoveToPoint(Vector3 targetPosition, float customArriveDistance = -1f)
    {
        float stopDistance = customArriveDistance >= 0f ? customArriveDistance : ArriveDistance;

        Vector3 current = transform.position;
        Vector3 toTarget = targetPosition - current;
        toTarget.y = 0f;

        float remaining = toTarget.magnitude;
        if (remaining <= stopDistance)
            return true;

        Vector3 direction = toTarget / remaining;
        float step = MoveSpeed * Time.deltaTime;
        transform.position = current + (direction * Mathf.Min(step, remaining));

        if (RotateToMoveDirection)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        return remaining <= stopDistance;
    }
}
