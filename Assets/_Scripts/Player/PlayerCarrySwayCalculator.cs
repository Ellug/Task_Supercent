using UnityEngine;

// 이동 관성 sway 및 정지 bounce 계산, 각 층의 월드 위치 계산 담당
public class PlayerCarrySwayCalculator
{
    private readonly float _swayStrength;
    private readonly float _swaySmoothing;
    private readonly float _swayCurve;
    private readonly float _bounceStrength;
    private readonly float _bounceDamping;

    private bool _wasMoving;
    private Vector3 _swayOffset;
    private Vector3 _bounceOffset;

    public PlayerCarrySwayCalculator(
        float swayStrength,
        float swaySmoothing,
        float swayCurve,
        float bounceStrength,
        float bounceDamping)
    {
        _swayStrength = swayStrength;
        _swaySmoothing = swaySmoothing;
        _swayCurve = swayCurve;
        _bounceStrength = bounceStrength;
        _bounceDamping = bounceDamping;
    }

    public void Reset()
    {
        _wasMoving = false;
        _swayOffset = Vector3.zero;
        _bounceOffset = Vector3.zero;
    }

    // 인풋 기반 sway + 정지 순간 bounce 갱신 (LateUpdate에서 호출)
    public void Tick(float dt, bool isMoving, Vector3 forward)
    {
        if (dt <= 0f)
            return;

        Vector3 flatForward = forward;
        flatForward.y = 0f;
        bool hasForward = flatForward.sqrMagnitude > 0.0001f;

        Vector3 targetSway = Vector3.zero;
        if (isMoving && hasForward)
            targetSway = -flatForward.normalized * _swayStrength;

        _swayOffset = Vector3.Lerp(_swayOffset, targetSway, dt * _swaySmoothing);

        if (_wasMoving && !isMoving && hasForward)
            _bounceOffset = flatForward.normalized * _bounceStrength;

        _wasMoving = isMoving;

        _bounceOffset = Vector3.Lerp(_bounceOffset, Vector3.zero, dt * _bounceDamping);
    }

    // 층 높이(VerticalSpacing * layerIndex)를 지수 커브에 넣어 sway 오프셋 반환
    // spacing이 다른 자원도 같은 물리적 높이면 동일한 sway를 가짐
    public Vector3 GetLayerSway(float verticalSpacing, int layerIndex)
    {
        float heightT = verticalSpacing * layerIndex;
        float curve = heightT > 0f ? Mathf.Pow(heightT, _swayCurve) : 0f;
        return (_swayOffset + _bounceOffset) * curve;
    }

    // StackRoot의 forward를 수평으로 정규화한 rotation 반환
    public static Quaternion GetHorizontalRotation(Transform root)
    {
        if (root == null)
            return Quaternion.identity;

        Vector3 forward = root.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }
}
