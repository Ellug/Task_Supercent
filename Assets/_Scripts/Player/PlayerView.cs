using UnityEngine;

[DisallowMultipleComponent]
public class PlayerView : MonoBehaviour
{
    public void ApplyMove(Vector2 moveInput, float moveSpeed, float fixedY, float fixedDeltaTime)
    {
        Vector3 moveDirection = new(moveInput.x, 0f, moveInput.y);

        // 이동 방향 즉시 회전
        if (moveDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(moveDirection.normalized, Vector3.up);

        // 고정 프레임 기준 즉시 이동
        Vector3 delta = moveDirection * (moveSpeed * fixedDeltaTime);
        Vector3 nextPosition = transform.position + delta;
        // 이동 중 Y값 고정
        nextPosition.y = fixedY;
        transform.position = nextPosition;
    }
}
