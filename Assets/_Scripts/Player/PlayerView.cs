using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PlayerView : MonoBehaviour
{
    [SerializeField] private Rigidbody _rigidbody;

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
        if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
    }

    // 이동 방향으로 즉시 회전 후 Rigidbody 속도로 이동 적용
    public void ApplyMove(Vector2 moveInput, float moveSpeed)
    {
        Vector3 moveDirection = new(moveInput.x, 0f, moveInput.y);

        // 이동 방향 즉시 회전
        if (moveDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(moveDirection.normalized, Vector3.up);

        // 물리 이동은 Rigidbody 속도로 처리 (모서리 사이 뚫기 현상 방지)
        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = _rigidbody.velocity.y;
        _rigidbody.velocity = velocity;
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
