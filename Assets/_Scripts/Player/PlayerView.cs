using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EquipBase))]
public class PlayerView : MonoBehaviour
{
    [SerializeField] private Rigidbody _rigidbody;
    [SerializeField] private Animator _animator;

    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private EquipBase _equip;

    // XZ 평면 기준 현재 이동 속도
    public float PlanarSpeed
    {
        get
        {
            Vector3 velocity = _rigidbody.velocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }
    }

    void Awake()
    {
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
            
        _equip = GetComponent<EquipBase>();
    }

    void OnEnable()
    {
        if (_equip != null)
            _equip.Mined += OnMined;
    }

    void OnDisable()
    {
        if (_equip != null)
            _equip.Mined -= OnMined;
    }

    private void OnMined()
    {
        if (_animator != null)
            _animator.SetTrigger(AttackHash);
    }

    // 이동 방향으로 즉시 회전 후 Rigidbody 속도로 이동 적용
    public void ApplyMove(Vector2 moveInput, float moveSpeed)
    {
        Vector3 moveDirection = new(moveInput.x, 0f, moveInput.y);
        bool isMoving = moveDirection.sqrMagnitude > 0.0001f;

        // 이동 방향 즉시 회전
        if (isMoving)
            transform.rotation = Quaternion.LookRotation(moveDirection.normalized, Vector3.up);

        // 물리 이동은 Rigidbody 속도로 처리 (모서리 사이 뚫기 현상 방지)
        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = _rigidbody.velocity.y;
        _rigidbody.velocity = velocity;

        if (_animator != null)
            _animator.SetBool(IsMovingHash, isMoving);
    }

    // XZ 속도를 0으로 즉시 정지
    public void StopMove()
    {
        Vector3 velocity = _rigidbody.velocity;
        velocity.x = 0f;
        velocity.z = 0f;
        _rigidbody.velocity = velocity;
    }
}
