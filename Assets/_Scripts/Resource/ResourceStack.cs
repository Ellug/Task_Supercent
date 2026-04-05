using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceStack : MonoBehaviour
{
    [Serializable]
    public struct Slot
    {
        public ResourceData Resource;
        public Transform Anchor;
        public Vector3 LocalOffset;
        public int Capacity;
        public float VerticalSpacing;
    }

    [SerializeField] private List<Slot> _slots = new();

    private readonly Dictionary<ResourceData, Slot> _slotByResource = new();
    private readonly Dictionary<ResourceData, int> _countByResource = new();

    public event Action<ResourceData, int, int> Changed;

    void Awake()
    {
        Rebuild();
    }

    // 여러 슬롯을 코드로 설정하고 딕셔너리 재구성
    public void ConfigureSlots(IReadOnlyList<Slot> slots)
    {
        _slots.Clear();
        if (slots != null)
        {
            for (int i = 0; i < slots.Count; i++)
                _slots.Add(slots[i]);
        }

        Rebuild();
    }

    // _slots 기준으로 내부 딕셔너리 초기화
    public void Rebuild()
    {
        _slotByResource.Clear();
        _countByResource.Clear();

        for (int i = 0; i < _slots.Count; i++)
        {
            Slot slot = _slots[i];
            if (slot.Resource == null)
                continue;

            if (slot.Capacity < 0)
                slot.Capacity = 0;

            if (slot.VerticalSpacing < 0f)
                slot.VerticalSpacing = 0f;

            if (slot.Anchor == null)
                slot.Anchor = transform;

            _slotByResource[slot.Resource] = slot;
            _countByResource[slot.Resource] = 0;
        }
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

        return _slotByResource.TryGetValue(resource, out Slot slot) ? slot.Capacity : 0;
    }

    // 조건에 맞는 첫 번째 자원 반환 — 없으면 false
    public bool TryGetFirstResource(System.Predicate<ResourceData> match, out ResourceData resource)
    {
        if (match != null)
        {
            foreach (var pair in _slotByResource)
            {
                ResourceData candidate = pair.Key;
                if (candidate != null && match(candidate))
                {
                    resource = candidate;
                    return true;
                }
            }
        }

        resource = null;
        return false;
    }

    // 남은 여유 공간 반환
    public int GetRemaining(ResourceData resource)
    {
        int capacity = GetCapacity(resource);
        int count = GetCount(resource);
        return Mathf.Max(0, capacity - count);
    }

    // 여유 공간만큼 추가 — 실제 추가된 양을 added로 반환
    public bool TryAdd(ResourceData resource, int amount, out int added)
    {
        added = 0;

        if (resource == null || amount <= 0)
            return false;

        if (!_slotByResource.TryGetValue(resource, out Slot slot))
            return false;

        int current = GetCount(resource);
        int free = Mathf.Max(0, slot.Capacity - current);
        if (free == 0)
            return false;

        added = Mathf.Min(amount, free);
        _countByResource[resource] = current + added;
        Changed?.Invoke(resource, _countByResource[resource], slot.Capacity);
        return added > 0;
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
        return removed > 0;
    }

    // resource의 특정 적층 index(layer)에 해당하는 월드 좌표 반환
    public bool TryGetWorldPosition(ResourceData resource, int layer, out Vector3 worldPosition)
    {
        worldPosition = default;

        if (resource == null || layer < 0)
            return false;

        if (!_slotByResource.TryGetValue(resource, out Slot slot))
            return false;

        Vector3 local = slot.LocalOffset + (Vector3.up * (slot.VerticalSpacing * layer));
        worldPosition = slot.Anchor.TransformPoint(local);
        return true;
    }
}
