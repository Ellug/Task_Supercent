using System.Collections.Generic;
using UnityEngine;

// 뷰 오브젝트 스폰/릴리즈 및 매 프레임 위치 갱신 담당
// 설정: PlayerCarryConfig / sway 계산: PlayerCarrySwayCalculator
[DisallowMultipleComponent]
[RequireComponent(typeof(ResourceStack))]
public class PlayerCarryVisualizer : MonoBehaviour
{
    [SerializeField] private ResourceStack _resourceStack;
    [SerializeField] private List<CarryBinding> _carryBindings = new();
    [SerializeField, Min(0f)] private float _moneyBackOffsetWhenOreExists = 0.45f;

    [Header("Sway")]
    [SerializeField, Min(0f)] private float _swayStrength = 0.004f;
    [SerializeField, Min(0f)] private float _swaySmoothing = 8f;
    [SerializeField] private float _swayCurve = 2.1f;
    [SerializeField, Min(0f)] private float _bounceStrength = 0.008f;
    [SerializeField, Min(0f)] private float _bounceDamping = 6f;

    private readonly Dictionary<ResourceData, List<GameObject>> _spawnedViewsByResource = new();
    private readonly List<ResourceData> _resourceKeyBuffer = new();

    private PlayerCarryConfig _config;
    private PlayerCarrySwayCalculator _sway;
    private PlayerController _playerController;

    void Awake()
    {
        _playerController = GetComponent<PlayerController>();

        _config = new PlayerCarryConfig();
        _config.Build(_carryBindings, _resourceStack);

        _sway = new PlayerCarrySwayCalculator(
            _swayStrength, _swaySmoothing, _swayCurve, _bounceStrength, _bounceDamping);

        SyncAllCarryVisuals();
    }

    void OnEnable()
    {
        _sway?.Reset();
        _resourceStack.Changed += OnCarryChanged;
        SyncAllCarryVisuals();
    }

    void OnDisable()
    {
        _resourceStack.Changed -= OnCarryChanged;
    }

    void LateUpdate()
    {
        bool isMoving = _playerController != null && _playerController.LastMoveInput.sqrMagnitude > 0.0001f;
        _sway.Tick(Time.deltaTime, isMoving, transform.forward);
        UpdateAllVisualPositions();
    }

    void OnDestroy()
    {
        foreach (var pair in _spawnedViewsByResource)
            PooledViewBridge.ReleaseAll(pair.Value);

        _spawnedViewsByResource.Clear();
    }

    // ResourceStack.Changed 콜백 — 변경된 자원만 동기화
    private void OnCarryChanged(ResourceData resource, int count, int capacity)
    {
        SyncResourceVisuals(resource, count);

        ResourceData ore = _config.OreResource;
        ResourceData money = _config.MoneyResource;
        if (ore != null && money != null && resource == ore)
            SyncResourceVisuals(money, _resourceStack.GetCount(money));
    }

    // 등록된 모든 자원 뷰를 현재 스택 수량에 맞춰 동기화
    // 바인딩에서 제거된 자원의 잔여 뷰도 정리
    private void SyncAllCarryVisuals()
    {
        var bindings = _config.BindingByResource;

        foreach (var pair in bindings)
            SyncResourceVisuals(pair.Key, _resourceStack.GetCount(pair.Key));

        _resourceKeyBuffer.Clear();
        foreach (var pair in _spawnedViewsByResource)
        {
            if (!bindings.ContainsKey(pair.Key))
                _resourceKeyBuffer.Add(pair.Key);
        }

        for (int i = 0; i < _resourceKeyBuffer.Count; i++)
        {
            ResourceData resource = _resourceKeyBuffer[i];
            PooledViewBridge.ReleaseAll(_spawnedViewsByResource[resource]);
            _spawnedViewsByResource.Remove(resource);
        }
    }

    // 단일 자원의 뷰 개수를 count에 맞추고 위치 배치
    private void SyncResourceVisuals(ResourceData resource, int count)
    {
        if (!_spawnedViewsByResource.TryGetValue(resource, out List<GameObject> views))
        {
            views = new List<GameObject>();
            _spawnedViewsByResource[resource] = views;
        }

        if (!_config.BindingByResource.TryGetValue(resource, out CarryBinding binding))
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
            GameObject view = PooledViewBridge.Spawn(resource.WorldViewPrefab, root.position, root.rotation, root, false);
            views.Add(view);
        }

        PlaceResourceViews(resource, binding);
    }

    // 등록된 모든 자원 뷰 위치를 sway 포함해 갱신 (LateUpdate에서 매 프레임 호출)
    private void UpdateAllVisualPositions()
    {
        foreach (var pair in _config.BindingByResource)
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
        Quaternion stackRotation = PlayerCarrySwayCalculator.GetHorizontalRotation(root);

        for (int i = 0; i < views.Count; i++)
        {
            Vector3 layerSway = _sway.GetLayerSway(binding.VerticalSpacing, i);

            GameObject view = views[i];
            Vector3 pos = TryGetOverrideWorldPosition(resource, binding, i, out Vector3 overridePosition)
                ? overridePosition
                : root.TransformPoint(binding.LocalOffset + Vector3.up * (binding.VerticalSpacing * i));

            view.transform.SetPositionAndRotation(pos + layerSway, stackRotation);
        }
    }

    // Money 뷰 위치를 Ore 스택 루트 기준으로 오버라이드
    // Ore가 있으면 _moneyBackOffsetWhenOreExists만큼 뒤로 밀어 겹침 방지
    private bool TryGetOverrideWorldPosition(ResourceData resource, CarryBinding binding, int index, out Vector3 worldPosition)
    {
        worldPosition = default;

        ResourceData ore = _config.OreResource;
        ResourceData money = _config.MoneyResource;

        if (ore == null || money == null)
            return false;

        if (resource != money)
            return false;

        if (!_config.BindingByResource.TryGetValue(ore, out CarryBinding oreBinding))
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

        if (_resourceStack.GetCount(ore) > 0)
            basePosition += axisBack * Mathf.Max(0f, _moneyBackOffsetWhenOreExists);

        worldPosition = basePosition + (Vector3.up * (binding.VerticalSpacing * index));
        return true;
    }
}
