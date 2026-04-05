using UnityEngine;

// 존 용량 제한/클램프/락 처리 런타임
public sealed class FacilityZoneCapacityRuntime
{
    private readonly InteractionZone _zone;
    private readonly int _maxCapacity;
    private readonly bool _lockZoneWhenFull;

    private bool _lockedByCapacity;
    private bool _zoneWasEnabledBeforeLock;

    public FacilityZoneCapacityRuntime(InteractionZone zone, int maxCapacity, bool lockZoneWhenFull)
    {
        _zone = zone;
        _maxCapacity = Mathf.Max(1, maxCapacity);
        _lockZoneWhenFull = lockZoneWhenFull;
    }

    public bool IsFull => _zone != null && _zone.StoredAmount >= _maxCapacity;
    public int Remaining => _zone == null ? 0 : Mathf.Max(0, _maxCapacity - _zone.StoredAmount);

    public void Tick()
    {
        ClampOverflow();
        UpdateZoneLock();
    }

    public int TryAdd(int amount)
    {
        if (_zone == null)
            return 0;

        int addAmount = Mathf.Min(Mathf.Max(0, amount), Remaining);
        if (addAmount <= 0)
            return 0;

        _zone.AddStoredAmount(addAmount);
        return addAmount;
    }

    public void Dispose()
    {
        if (!_lockZoneWhenFull)
            return;

        if (!_lockedByCapacity)
            return;

        if (_zone != null && _zoneWasEnabledBeforeLock)
            _zone.SetZoneEnabled(true);

        _lockedByCapacity = false;
        _zoneWasEnabledBeforeLock = false;
    }

    private void ClampOverflow()
    {
        if (_zone == null)
            return;

        int overflow = _zone.StoredAmount - _maxCapacity;
        if (overflow > 0)
            _zone.AddStoredAmount(-overflow);
    }

    private void UpdateZoneLock()
    {
        if (!_lockZoneWhenFull || _zone == null)
            return;

        if (IsFull)
        {
            if (_lockedByCapacity)
                return;

            _zoneWasEnabledBeforeLock = _zone.IsZoneEnabled;
            _zone.SetZoneEnabled(false);
            _lockedByCapacity = true;
            return;
        }

        if (!_lockedByCapacity)
            return;

        if (_zoneWasEnabledBeforeLock)
            _zone.SetZoneEnabled(true);

        _lockedByCapacity = false;
        _zoneWasEnabledBeforeLock = false;
    }
}
