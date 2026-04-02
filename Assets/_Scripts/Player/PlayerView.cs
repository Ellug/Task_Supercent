using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PlayerView : MonoBehaviour
{
    [SerializeField] private Rigidbody _rigidbody;

    void Awake()
    {
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();
    }

    public void ApplyMove(Vector2 moveInput, float moveSpeed)
    {
        Vector3 moveDirection = new(moveInput.x, 0f, moveInput.y);

        // 이동 방향 즉시 회전
        if (moveDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(moveDirection.normalized, Vector3.up);

        if (_rigidbody == null)
            return;

        // 물리 이동은 Rigidbody 속도로 처리
        Vector3 velocity = moveDirection * moveSpeed;
        velocity.y = _rigidbody.velocity.y;
        _rigidbody.velocity = velocity;
    }

    public void StopMove()
    {
        if (_rigidbody == null)
            return;

        Vector3 velocity = _rigidbody.velocity;
        velocity.x = 0f;
        velocity.z = 0f;
        _rigidbody.velocity = velocity;
    }
}
