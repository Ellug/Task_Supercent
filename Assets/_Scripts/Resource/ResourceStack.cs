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

    // 단일 슬롯을 코드로 설정하고 딕셔너리 재구성
    public void ConfigureSingleSlot(ResourceData resource, Transform anchor, int capacity, float verticalSpacing, Vector3 localOffset)
    {
        _slots.Clear();
        _slots.Add(new Slot
        {
            Resource = resource,
            Anchor = anchor,
            LocalOffset = localOffset,
            Capacity = Mathf.Max(1, capacity),
            VerticalSpacing = Mathf.Max(0f, verticalSpacing)
        });

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

    // 이 스택에서 target 스택으로 resource를 이동 — 수용 초과분은 반환
    public bool TryMoveTo(ResourceStack target, ResourceData resource, int amount, out int moved)
    {
        moved = 0;

        if (target == null || resource == null || amount <= 0)
            return false;

        if (!TryRemove(resource, amount, out int removed))
            return false;

        target.TryAdd(resource, removed, out int accepted);
        int rejected = removed - accepted;

        if (rejected > 0)
            TryAdd(resource, rejected, out _);

        moved = accepted;
        return moved > 0;
    }

    // 다음 적재 위치의 월드 좌표 반환 — 슬롯 앵커 + 수직 적층 오프셋 계산
    public bool TryGetNextWorldPosition(ResourceData resource, out Vector3 worldPosition)
    {
        worldPosition = default;

        if (resource == null || !_slotByResource.TryGetValue(resource, out Slot slot))
            return false;

        if (slot.Anchor == null)
            return false;

        int layer = GetCount(resource);
        Vector3 local = slot.LocalOffset + (Vector3.up * (slot.VerticalSpacing * layer));
        worldPosition = slot.Anchor.TransformPoint(local);
        return true;
    }
}

