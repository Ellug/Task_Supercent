using UnityEngine;

// 카메라 연출 1개의 데이터 — 이동/유지/복귀 시간과 커브 정의
[CreateAssetMenu(fileName = "CameraShot", menuName = "Game/Camera Shot")]
public class CameraShot : ScriptableObject
{
    [Tooltip("목적지까지 이동 시간")]
    [SerializeField, Min(0f)] public float travelDuration = 1f;

    [Tooltip("목적지에서 대기 시간 (이벤트 실행 전)")]
    [SerializeField, Min(0f)] public float holdDuration = 0.5f;

    [Tooltip("원래 위치로 복귀 시간")]
    [SerializeField, Min(0f)] public float returnDuration = 1f;

    [Tooltip("이동 보간 커브")]
    public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

}
