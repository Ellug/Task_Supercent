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
    [SerializeField] private ResourceData _oreResource;
    [SerializeField] private ResourceData _moneyResource;
    [SerializeField, Min(0f)] private float _moneyBackOffsetWhenOreExists = 0.45f;

    [Header("Sway")]
    [SerializeField, Min(0f)] private float _swayStrength = 0.004f;      // 이동 중 sway 목표 오프셋
    [SerializeField, Min(0f)] private float _swaySmoothing = 8f;          // sway 추종 속도
    [SerializeField, Min(0f)] private float _swayMaxOffset = 0.12f;       // 층당 최대 sway 오프셋
    [SerializeField] private float _swayCurve = 1.4f;                     // 지수 커브 (1=선형, >1 위로 갈수록 급격히 휨)
    [SerializeField, Min(0f)] private float _bounceStrength = 0.008f;     // 멈출 때 앞으로 튀는 세기
    [SerializeField, Min(0f)] private float _bounceDamping = 6;         // bounce 감쇠 속도

    private readonly Dictionary<ResourceData, CarryBinding> _bindingByResource = new();
    private readonly Dictionary<ResourceData, List<GameObject>> _spawnedViewsByResource = new();
    private readonly List<ResourceStack.Slot> _slotBuffer = new();
    private readonly List<ResourceData> _resourceKeyBuffer = new();
    private ResourceData _resolvedOreResource;
    private ResourceData _resolvedMoneyResource;

    private PlayerController _playerController;
    private bool _wasMoving;
    private Vector3 _swayOffset;    // 이동 중 관성 sway (위로 갈수록 지수적으로 증가)
    private Vector3 _bounceOffset;  // 멈추는 순간 앞으로 튕기는 오프셋 (독립 감쇠)

    void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        RebuildCarryBindings();
        ResolvePriorityResources();
        ConfigureCarrySlots();
        SyncAllCarryVisuals();
    }

    void OnEnable()
    {
        _wasMoving = false;
        _swayOffset = Vector3.zero;
        _bounceOffset = Vector3.zero;
        _resourceStack.Changed += OnCarryChanged;
        SyncAllCarryVisuals();
    }

    void OnDisable()
    {
        _resourceStack.Changed -= OnCarryChanged;
    }

    void LateUpdate()
    {
        UpdateSway();
        UpdateAllVisualPositions();
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

    // Money/Ore 오프셋 계산에 쓸 우선 자원 결정
    // Inspector 지정 > IsMoney 탐색 > id="ore" 탐색 > 첫 비-Money 자원 순으로 폴백
    private void ResolvePriorityResources()
    {
        _resolvedOreResource = _oreResource;
        _resolvedMoneyResource = _moneyResource;

        if (_resolvedMoneyResource == null)
        {
            for (int i = 0; i < _carryBindings.Count; i++)
            {
                CarryBinding binding = _carryBindings[i];
                if (binding.Resource == null || !binding.Resource.IsMoney)
                    continue;

                _resolvedMoneyResource = binding.Resource;
                break;
            }
        }

        if (_resolvedOreResource != null)
            return;

        for (int i = 0; i < _carryBindings.Count; i++)
        {
            CarryBinding binding = _carryBindings[i];
            ResourceData resource = binding.Resource;
            if (resource == null || resource == _resolvedMoneyResource)
                continue;

            if (string.Equals(resource.Id, "ore", StringComparison.OrdinalIgnoreCase))
            {
                _resolvedOreResource = resource;
                return;
            }
        }

        for (int i = 0; i < _carryBindings.Count; i++)
        {
            CarryBinding binding = _carryBindings[i];
            ResourceData resource = binding.Resource;
            if (resource == null || resource == _resolvedMoneyResource)
                continue;

            _resolvedOreResource = resource;
            return;
        }
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

        if (_resolvedOreResource != null && _resolvedMoneyResource != null && resource == _resolvedOreResource)
            SyncResourceVisuals(_resolvedMoneyResource, _resourceStack.GetCount(_resolvedMoneyResource));
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

        PlaceResourceViews(resource, binding);
    }

    // 등록된 모든 자원 뷰 위치를 sway 포함해 갱신 (LateUpdate에서 매 프레임 호출)
    private void UpdateAllVisualPositions()
    {
        foreach (var pair in _bindingByResource)
        {
            if (_spawnedViewsByResource.TryGetValue(pair.Key, out List<GameObject> views) && views.Count > 0)
                PlaceResourceViews(pair.Key, pair.Value);
        }
    }

    // 뷰 오브젝트들의 위치를 sway 오프셋을 층마다 누적 적용해서 배치
    private void PlaceResourceViews(ResourceData resource, CarryBinding binding)
    {
        if (!_spawnedViewsByResource.TryGetValue(resource, out List<GameObject> views))
            return;

        Transform root = binding.StackRoot;
        Quaternion stackRotation = GetHorizontalRotation(root);

        for (int i = 0; i < views.Count; i++)
        {
            // 실제 y 높이(VerticalSpacing * i)를 지수 커브에 넣어 sway 계산
            // → spacing이 다른 Ore/Money가 같은 높이면 동일한 sway를 가짐
            float heightT = binding.VerticalSpacing * i;
            float curve = heightT > 0f ? Mathf.Pow(heightT, _swayCurve) : 0f;
            Vector3 layerSway = (_swayOffset + _bounceOffset) * curve;

            GameObject view = views[i];
            if (TryGetOverrideWorldPosition(resource, binding, i, out Vector3 overridePosition))
            {
                view.transform.position = overridePosition + layerSway;
                view.transform.rotation = stackRotation;
                continue;
            }

            if (_resourceStack.TryGetWorldPosition(resource, i, out Vector3 worldPosition))
            {
                view.transform.position = worldPosition + layerSway;
                view.transform.rotation = stackRotation;
                continue;
            }

            view.transform.SetParent(root, false);
            view.transform.localPosition = binding.LocalOffset + (Vector3.up * (binding.VerticalSpacing * i));
            view.transform.localRotation = Quaternion.identity;
        }
    }

    // Money 뷰 위치를 Ore 스택 루트 기준으로 오버라이드
    // Ore가 있으면 _moneyBackOffsetWhenOreExists만큼 뒤로 밀어 겹침 방지
    private bool TryGetOverrideWorldPosition(ResourceData resource, CarryBinding binding, int index, out Vector3 worldPosition)
    {
        worldPosition = default;

        if (_resolvedOreResource == null || _resolvedMoneyResource == null)
            return false;

        if (resource != _resolvedMoneyResource)
            return false;

        if (!_bindingByResource.TryGetValue(_resolvedOreResource, out CarryBinding oreBinding))
            return false;

        Transform root = oreBinding.StackRoot != null ? oreBinding.StackRoot : binding.StackRoot;
        if (root == null)
            return false;

        Vector3 axisRight = FacilityStackUtility.GetHorizontalAxis(root.right, Vector3.right);
        Vector3 axisForward = FacilityStackUtility.GetHorizontalAxis(root.forward, Vector3.forward);
        Vector3 axisBack = -axisForward;

        Vector3 basePosition = root.position;
        basePosition += axisRight * oreBinding.LocalOffset.x;
        basePosition += axisForward * oreBinding.LocalOffset.z;
        basePosition += Vector3.up * oreBinding.LocalOffset.y;

        if (_resourceStack.GetCount(_resolvedOreResource) > 0)
            basePosition += axisBack * Mathf.Max(0f, _moneyBackOffsetWhenOreExists);

        worldPosition = basePosition + (Vector3.up * (binding.VerticalSpacing * index));
        return true;
    }

    // 인풋 기반 sway + 멈추는 순간 bounce 계산
    private void UpdateSway()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        bool isMoving = _playerController != null && _playerController.LastMoveInput.sqrMagnitude > 0.0001f;

        // 이동 중이면 플레이어 forward 반대 방향으로 sway, 정지면 0으로 복귀
        Vector3 targetSway = Vector3.zero;
        if (isMoving)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
                targetSway = -forward.normalized * _swayStrength;
        }

        _swayOffset = Vector3.Lerp(_swayOffset, targetSway, dt * _swaySmoothing);

        // 이동 → 정지 전환 순간 bounce 발동 (이전 프레임에 이동 중이었으면)
        if (_wasMoving && !isMoving)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
                _bounceOffset = forward.normalized * _bounceStrength;
        }

        _wasMoving = isMoving;

        // bounce는 독립적으로 0을 향해 감쇠
        _bounceOffset = Vector3.Lerp(_bounceOffset, Vector3.zero, dt * _bounceDamping);
    }

    // StackRoot의 forward를 수평으로 정규화한 rotation 반환
    // forward가 수직에 가까우면 world forward로 폴백
    private static Quaternion GetHorizontalRotation(Transform root)
    {
        if (root == null)
            return Quaternion.identity;

        Vector3 forward = root.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }
}
