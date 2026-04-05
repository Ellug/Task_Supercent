using System.Collections.Generic;
using UnityEngine;

// ZoneId 기준으로 씬의 InteractionZone 조회/보관
public sealed class ZoneRegistry
{
    private readonly Dictionary<InteractionZoneId, InteractionZone> _zoneById = new();

    public IEnumerable<InteractionZone> Zones => _zoneById.Values;

    // 비활성 포함 씬 전체 Zone 인덱싱
    public void BuildFromScene()
    {
        _zoneById.Clear();

        InteractionZone[] zones = Object.FindObjectsByType<InteractionZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            InteractionZone zone = zones[i];
            if (zone == null || zone.ZoneId == InteractionZoneId.None)
                continue;

            if (_zoneById.TryGetValue(zone.ZoneId, out InteractionZone duplicated))
            {
                Debug.LogWarning($"[ZoneRegistry] Duplicate zone id detected: {zone.ZoneId}. " +
                                 $"Existing={duplicated.gameObject.name}, New={zone.gameObject.name}");
                continue;
            }

            _zoneById.Add(zone.ZoneId, zone);
        }
    }

    // ZoneId로 Zone 조회
    public bool TryGetZone(InteractionZoneId zoneId, out InteractionZone zone, bool logWarning = true)
    {
        zone = null;

        if (zoneId == InteractionZoneId.None)
            return false;

        if (_zoneById.TryGetValue(zoneId, out zone))
            return true;

        if (logWarning)
            Debug.LogWarning($"[ZoneRegistry] Zone not found for id: {zoneId}");

        return false;
    }

    public void Clear()
    {
        _zoneById.Clear();
    }
}
