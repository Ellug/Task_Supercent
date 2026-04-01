using UnityEngine;

[DisallowMultipleComponent]
public class PlayerModel : MonoBehaviour
{
    [Header("Movement")]
    // 플레이어 기본 이동 속도
    [SerializeField, Min(0f)] private float _moveSpeed = 10f;

    public float MoveSpeed => _moveSpeed;

    public Vector2 ComposeMoveInput(Vector2 moveInput)
    {
        // 이동 입력 정규화
        return Vector2.ClampMagnitude(moveInput, 1f);
    }
}
