using System;
using System.Collections.Generic;
using UnityEngine;

// 자원 수량 관리 전용 — 추가/제거/조회 및 변경 이벤트 발행
public class ResourceStack : MonoBehaviour
{
    private readonly Dictionary<ResourceData, int> _countByResource = new();
    private readonly Dictionary<ResourceData, int> _capacityByResource = new();

    public event Action<ResourceData, int, int> Changed;

    // 자원별 슬롯(용량) 등록
    public void RegisterSlot(ResourceData resource, int capacity)
    {
        if (resource == null)
            return;

        int clampedCapacity = Mathf.Max(0, capacity);

        if (_countByResource.TryGetValue(resource, out int currentCount))
        {
            _capacityByResource[resource] = clampedCapacity;
            _countByResource[resource] = Mathf.Min(currentCount, clampedCapacity);
            return;
        }

        _countByResource[resource] = 0;
        _capacityByResource[resource] = clampedCapacity;
    }

    // 현재 적재 수량 반환
    public int GetCount(ResourceData resource)
    {
        if (resource == null)
            return 0;

        return _countByResource.TryGetValue(resource, out int count) ? count : 0;
    }

    // 슬롯 최대 용량 반환
    public int GetCapacity(ResourceData resource)
    {
        if (resource == null)
            return 0;

        return _capacityByResource.TryGetValue(resource, out int cap) ? cap : 0;
    }

    // 남은 여유 공간 반환
    public int GetRemaining(ResourceData resource)
    {
        return Mathf.Max(0, GetCapacity(resource) - GetCount(resource));
    }

    // 조건에 맞는 첫 번째 자원 반환
    public bool TryGetFirstResource(Predicate<ResourceData> match, out ResourceData resource)
    {
        if (match != null)
        {
            foreach (var pair in _countByResource)
            {
                if (pair.Key != null && match(pair.Key))
                {
                    resource = pair.Key;
                    return true;
                }
            }
        }

        resource = null;
        return false;
    }

    // 여유 공간만큼 추가 — 실제 추가된 양을 added로 반환
    public bool TryAdd(ResourceData resource, int amount, out int added)
    {
        added = 0;

        if (resource == null || amount <= 0)
            return false;

        if (!_capacityByResource.TryGetValue(resource, out int capacity))
            return false;

        int current = GetCount(resource);
        int free = Mathf.Max(0, capacity - current);
        if (free == 0)
            return false;

        added = Mathf.Min(amount, free);
        _countByResource[resource] = current + added;
        Changed?.Invoke(resource, _countByResource[resource], capacity);
        return true;
    }

    // 보유량 내에서 차감 — 실제 제거된 양을 removed로 반환
    public bool TryRemove(ResourceData resource, int amount, out int removed)
    {
        removed = 0;

        if (resource == null || amount <= 0)
            return false;

        if (!_countByResource.TryGetValue(resource, out int current) || current <= 0)
            return false;

        removed = Mathf.Min(amount, current);
        _countByResource[resource] = current - removed;
        Changed?.Invoke(resource, _countByResource[resource], GetCapacity(resource));
        return true;
    }
}
