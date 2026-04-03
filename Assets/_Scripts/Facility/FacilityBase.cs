using UnityEngine;

// 시설 공통 베이스 — InputZone의 자원을 인터벌마다 소비해 처리
// 소비 조건/용량/처리 결과는 서브클래스에서 구현
public abstract class FacilityBase : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InteractionZone _inputZone;
    [SerializeField, Min(0f)] private float _consumeInterval = 0.02f;
    [SerializeField, Min(1)] private int _consumeAmountPerTick = 5;

    private float _nextConsumeTime;

    protected InteractionZone InputZone => _inputZone;
    protected bool HasInputZone => _inputZone != null;

    // 런타임에서 InputZone을 코드로 연결할 때 사용
    protected void BindInputZone(InteractionZone inputZone)
    {
        _inputZone = inputZone;
    }

    protected virtual void Awake() {}

    void Update()
    {
        if (_inputZone == null)
            return;

        if (Time.time < _nextConsumeTime)
            return;

        _nextConsumeTime = Time.time + Mathf.Max(0f, _consumeInterval);
        TryConsumeInput();
    }

    // InputZone에서 가능한 만큼 자원을 꺼내 OnConsumed로 전달
    private void TryConsumeInput()
    {
        ResourceData resource = _inputZone.Resource;
        if (!CanConsume(resource))
            return;

        int available = _inputZone.StoredAmount;
        if (available <= 0)
            return;

        int remainingCapacity = GetRemainingCapacity(resource);
        if (remainingCapacity <= 0)
            return;

        int consumeAmount = Mathf.Min(available, remainingCapacity, Mathf.Max(1, _consumeAmountPerTick));
        if (consumeAmount <= 0)
            return;

        _inputZone.AddStoredAmount(-consumeAmount);
        OnConsumed(resource, consumeAmount);
    }

    // 해당 자원을 소비할 수 있는지 판단 — 서브클래스에서 조건 추가 가능
    protected virtual bool CanConsume(ResourceData resource)
    {
        return resource != null;
    }

    // 서브클래스에서 현재 수용 가능한 잔여 용량 반환
    protected abstract int GetRemainingCapacity(ResourceData resource);

    // 서브클래스에서 실제 소비 처리 구현
    protected abstract void OnConsumed(ResourceData resource, int amount);
}
