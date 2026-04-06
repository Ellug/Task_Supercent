using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 시설 공통 베이스 — InputZone의 자원을 인터벌마다 소비해 처리
// 소비 조건/용량/처리 결과는 서브클래스에서 구현
public abstract class FacilityBase : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InteractionZone _inputZone;
    [SerializeField, Min(0f)] private float _consumeInterval = 0.02f;
    [SerializeField, Min(1)] private int _consumeAmountPerTick = 5;

    [Header("Stack Bounce")]
    [SerializeField, Min(0f)] private float _stackBounceDuration = 0.2f;
    [SerializeField] private float _stackBounceScale = 1.1f;

    private float _nextConsumeTime;
    private readonly Dictionary<GameObject, Coroutine> _bounceCoroutines = new();

    protected InteractionZone InputZone => _inputZone;
    public InteractionZone BoundInputZone => _inputZone;
    protected float ConsumeInterval => Mathf.Max(0f, _consumeInterval);
    protected int ConsumeAmountPerTick => Mathf.Max(1, _consumeAmountPerTick);

    // 기본 초기화 훅
    protected virtual void Awake() {}

    void Update()
    {
        if (!CanConsumeThisFrame())
            return;

        if (!TryPrepareConsume(out ResourceData resource, out int consumeAmount))
            return;

        _inputZone.AddStoredAmount(-consumeAmount);
        OnConsumed(resource, consumeAmount);
    }

    // 입력 존 존재 + 인터벌 충족 여부 검사
    private bool CanConsumeThisFrame()
    {
        if (_inputZone == null)
            return false;

        if (Time.time < _nextConsumeTime)
            return false;

        _nextConsumeTime = Time.time + ConsumeInterval;
        return true;
    }

    // 이번 틱에 실제 소비할 자원/수량 계산
    private bool TryPrepareConsume(out ResourceData resource, out int consumeAmount)
    {
        resource = _inputZone.Resource;
        consumeAmount = 0;

        if (!CanConsume(resource))
            return false;

        int available = _inputZone.StoredAmount;
        if (available <= 0)
            return false;

        int remainingCapacity = GetRemainingCapacity(resource);
        if (remainingCapacity <= 0)
            return false;

        consumeAmount = Mathf.Min(available, remainingCapacity, ConsumeAmountPerTick);
        return consumeAmount > 0;
    }

    // FacilityStackViewRuntime에 bounce 콜백 연결
    protected void RegisterStackBounce(FacilityStackViewRuntime runtime)
    {
        runtime?.SetOnViewSpawned(TriggerStackBounce);
    }

    private void TriggerStackBounce(GameObject view)
    {
        if (view == null || _stackBounceDuration <= 0f)
            return;

        if (_bounceCoroutines.TryGetValue(view, out Coroutine existing) && existing != null)
            StopCoroutine(existing);

        _bounceCoroutines[view] = StartCoroutine(BounceCoroutine(view));
    }

    private IEnumerator BounceCoroutine(GameObject view)
    {
        if (view == null)
            yield break;

        float half = _stackBounceDuration * 0.5f;
        float elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            if (view != null)
                view.transform.localScale = Vector3.one * Mathf.LerpUnclamped(1f, _stackBounceScale, elapsed / half);
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            if (view != null)
                view.transform.localScale = Vector3.one * Mathf.LerpUnclamped(_stackBounceScale, 1f, elapsed / half);
            yield return null;
        }

        if (view != null)
            view.transform.localScale = Vector3.one;

        _bounceCoroutines.Remove(view);
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
