using UnityEngine;

// InteractionZone 출력 적재 + 뷰 동기화 공통 런타임
public sealed class FacilityZoneOutputRuntime
{
    private readonly InteractionZone _zone;
    private readonly FacilityStackViewRuntime _views;

    public FacilityZoneOutputRuntime(InteractionZone zone, FacilityStackViewRuntime views)
    {
        _zone = zone;
        _views = views;
    }

    public void Add(int amount)
    {
        int addAmount = Mathf.Max(0, amount);
        if (addAmount <= 0)
            return;

        _zone.AddStoredAmount(addAmount);
        SyncVisuals();
    }

    public void SyncVisuals()
    {
        _views.SyncToCount(Mathf.Max(0, _zone.StoredAmount));
    }

    public void Dispose()
    {
        _views.Dispose();
    }
}
