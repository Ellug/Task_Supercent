using UnityEngine;

// 입력 스택을 인터벌마다 소비해 출력 존으로 전환하는 공통 런타임
public sealed class FacilityTimedProductionRuntime
{
    private readonly float _interval;
    private float _nextTime;

    public FacilityTimedProductionRuntime(float interval)
    {
        _interval = Mathf.Max(0.01f, interval);
    }

    public bool TryConvert(
        FacilityStackViewRuntime inputViews,
        FacilityZoneOutputRuntime outputZone,
        int consumeAmountPerTick,
        int producePerConsume)
    {
        if (!TryConsume(inputViews, consumeAmountPerTick))
            return false;

        int produceAmount = Mathf.Max(1, consumeAmountPerTick) * Mathf.Max(1, producePerConsume);
        outputZone.Add(produceAmount);
        return true;
    }

    // 인터벌마다 입력 스택에서 소비만 수행 — 출력은 호출자가 처리
    public bool TryConsume(FacilityStackViewRuntime inputViews, int consumeAmountPerTick)
    {
        if (Time.time < _nextTime)
            return false;

        _nextTime = Time.time + _interval;

        int consumeAmount = Mathf.Max(1, consumeAmountPerTick);
        if (inputViews.Count < consumeAmount)
            return false;

        int removed = inputViews.Remove(consumeAmount);
        return removed > 0;
    }
}
