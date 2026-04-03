using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ResourceStack))]
public class PlayerCarryVisualizer : MonoBehaviour
{
    [Serializable]
    private class CarryBinding
    {
        public ResourceData Resource = null;
        public GameObject ViewPrefab = null;
        public Transform StackRoot = null;
        public int Capacity = 1;
        public float VerticalSpacing = 0f;
        public Vector3 LocalOffset = Vector3.zero;
    }

    [SerializeField] private ResourceStack _resourceStack;
    [SerializeField] private List<CarryBinding> _carryBindings = new();

    private readonly Dictionary<ResourceData, CarryBinding> _bindingByResource = new();
    private readonly Dictionary<ResourceData, List<GameObject>> _spawnedViewsByResource = new();
    private readonly List<ResourceStack.Slot> _slotBuffer = new();
    private readonly List<ResourceData> _resourceKeyBuffer = new();

    void Awake()
    {
        RebuildCarryBindings();
        ConfigureCarrySlots();
        SyncAllCarryVisuals();
    }

    void OnEnable()
    {
        _resourceStack.Changed += OnCarryChanged;
        SyncAllCarryVisuals();
    }

    void OnDisable()
    {
        _resourceStack.Changed -= OnCarryChanged;
    }

    // 스폰된 뷰 오브젝트 전체 반환
    void OnDestroy()
    {
        foreach (var pair in _spawnedViewsByResource)
            PooledViewBridge.ReleaseAll(pair.Value);

        _spawnedViewsByResource.Clear();
    }

    // Inspector 바인딩 목록을 딕셔너리로 재구성
    private void RebuildCarryBindings()
    {
        _bindingByResource.Clear();

        for (int i = 0; i < _carryBindings.Count; i++)
            RegisterBinding(_carryBindings[i]);
    }

    // Resource, ViewPrefab, StackRoot 모두 설정된 항목만 등록
    private void RegisterBinding(CarryBinding source)
    {
        if (source.Resource == null || source.ViewPrefab == null || source.StackRoot == null) return;

        CarryBinding binding = source;
        _bindingByResource[binding.Resource] = binding;
    }

    // 바인딩 기준으로 ResourceStack 슬롯 구성
    private void ConfigureCarrySlots()
    {
        _slotBuffer.Clear();
        foreach (var pair in _bindingByResource)
        {
            CarryBinding binding = pair.Value;
            _slotBuffer.Add(new ResourceStack.Slot
            {
                Resource = binding.Resource,
                Anchor = binding.StackRoot,
                LocalOffset = binding.LocalOffset,
                Capacity = Mathf.Max(1, binding.Capacity),
                VerticalSpacing = Mathf.Max(0f, binding.VerticalSpacing)
            });
        }

        _resourceStack.ConfigureSlots(_slotBuffer);
    }

    // ResourceStack.Changed 콜백 — 변경된 자원만 동기화
    private void OnCarryChanged(ResourceData resource, int count, int capacity)
    {
        SyncResourceVisuals(resource, count);
    }

    // 등록된 모든 자원 뷰를 현재 스택 수량에 맞춰 동기화
    // 바인딩에서 제거된 자원의 잔여 뷰도 정리
    private void SyncAllCarryVisuals()
    {
        foreach (var pair in _bindingByResource)
        {
            ResourceData resource = pair.Key;
            SyncResourceVisuals(resource, _resourceStack.GetCount(resource));
        }

        _resourceKeyBuffer.Clear();
        foreach (var pair in _spawnedViewsByResource)
        {
            if (_bindingByResource.ContainsKey(pair.Key))
                continue;

            _resourceKeyBuffer.Add(pair.Key);
        }

        for (int i = 0; i < _resourceKeyBuffer.Count; i++)
        {
            ResourceData resource = _resourceKeyBuffer[i];
            PooledViewBridge.ReleaseAll(_spawnedViewsByResource[resource]);
            _spawnedViewsByResource.Remove(resource);
        }
    }

    // 단일 자원의 뷰 개수를 count에 맞추고 월드 위치 재배치
    private void SyncResourceVisuals(ResourceData resource, int count)
    {
        if (!_spawnedViewsByResource.TryGetValue(resource, out List<GameObject> views))
        {
            views = new List<GameObject>();
            _spawnedViewsByResource[resource] = views;
        }

        if (!_bindingByResource.TryGetValue(resource, out CarryBinding binding))
        {
            PooledViewBridge.ReleaseAll(views);
            return;
        }

        int targetCount = Mathf.Max(0, count);

        while (views.Count > targetCount)
        {
            int lastIndex = views.Count - 1;
            PooledViewBridge.Release(views[lastIndex]);
            views.RemoveAt(lastIndex);
        }

        Transform root = binding.StackRoot;
        while (views.Count < targetCount)
        {
            GameObject view = PooledViewBridge.Spawn(binding.ViewPrefab, root.position, root.rotation, root, false);
            views.Add(view);
        }

        for (int i = 0; i < views.Count; i++)
        {
            GameObject view = views[i];

            if (_resourceStack.TryGetWorldPosition(resource, i, out Vector3 worldPosition))
            {
                view.transform.position = worldPosition;
                view.transform.rotation = Quaternion.identity;
                continue;
            }

            view.transform.SetParent(root, false);
            view.transform.localPosition = binding.LocalOffset + (Vector3.up * (binding.VerticalSpacing * i));
            view.transform.localRotation = Quaternion.identity;
        }
    }
}
