using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 뷰 오브젝트 스폰/릴리즈 및 매 프레임 위치 갱신 담당
// 설정: PlayerCarryConfig / sway 계산: PlayerCarrySwayCalculator
[DisallowMultipleComponent]
[RequireComponent(typeof(ResourceStack))]
public class PlayerCarryVisualizer : MonoBehaviour
{
    private const float MinTransferMoveSpeed = 26f;
    private const float TransferRotationFollowSpeed = 20f;

    [SerializeField] private ResourceStack _resourceStack;
    [SerializeField] private List<CarryBinding> _carryBindings = new();
    [SerializeField, Min(0f)] private float _moneyBackOffsetWhenOreExists = 0.45f;

    [Header("Sway")]
    [SerializeField, Min(0f)] private float _swayStrength = 0.004f;
    [SerializeField, Min(0f)] private float _swaySmoothing = 8f;
    [SerializeField] private float _swayCurve = 2.1f;
    [SerializeField, Min(0f)] private float _bounceStrength = 0.008f;
    [SerializeField, Min(0f)] private float _bounceDamping = 6f;

    [Header("Transfer Visual")]
    [SerializeField, Min(0.1f)] private float _transferMoveSpeed = 28f;
    [SerializeField, Min(0f)] private float _transferArcHeight = 0.22f;
    [SerializeField, Min(0f)] private float _transferScatter = 0.03f;
    [SerializeField] private Vector3 _transferSourceOffset = new(0f, 0.1f, 0f);

    [Header("Stack Bounce")]
    [SerializeField, Min(0f)] private float _stackBounceDuration = 0.2f;
    [SerializeField] private float _stackBounceScale = 1.1f;

    private sealed class TransferFlight
    {
        public GameObject View;
        public Vector3 Start;
        public Vector3 End;
        public float Duration;
        public float Elapsed;
        public Quaternion StartRotation;
        public Quaternion EndRotation;
        public bool FollowEnd;
        public ResourceData Resource;
        public CarryBinding Binding;
        public int SlotIndex;
    }

    private readonly Dictionary<ResourceData, List<GameObject>> _spawnedViewsByResource = new();
    private readonly List<ResourceData> _resourceKeyBuffer = new();
    private readonly List<TransferFlight> _transferFlights = new();
    // 바운스 중인 뷰의 원래 스케일 추적 (바운스 완료 후 복원)
    private readonly Dictionary<GameObject, Coroutine> _bounceCoroutines = new();

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
        UpdateTransferFlights();
    }

    void OnDestroy()
    {
        foreach (var pair in _spawnedViewsByResource)
            PooledViewBridge.ReleaseAll(pair.Value);

        _spawnedViewsByResource.Clear();

        for (int i = 0; i < _transferFlights.Count; i++)
        {
            if (_transferFlights[i]?.View != null)
                PooledViewBridge.Release(_transferFlights[i].View);
        }

        _transferFlights.Clear();
    }

    // Ore 슬롯 capacity를 베이스 + bonus로 재등록
    public void ApplyOreCarryCapacityBonus(int bonus)
    {
        if (_carryBindings.Count == 0 || _carryBindings[0].Resource == null)
            return;

        CarryBinding oreBinding = _carryBindings[0];
        int newCapacity = Mathf.Max(1, oreBinding.Capacity + bonus);
        _resourceStack.RegisterSlot(oreBinding.Resource, newCapacity);
    }

    // 월드 위치에서 플레이어 적재 위치로 이동하는 연출
    public void PlayIncomingTransfer(ResourceData resource, Vector3 sourceWorldPosition, int amount)
    {
        if (!TryGetBinding(resource, out CarryBinding binding))
            return;

        Quaternion stackRotation = PlayerCarrySwayCalculator.GetHorizontalRotation(binding.StackRoot);
        int stackCount = Mathf.Max(0, _resourceStack.GetCount(resource));
        int count = Mathf.Max(1, amount);
        for (int i = 0; i < count; i++)
        {
            Vector3 start = sourceWorldPosition + _transferSourceOffset + GetTransferScatterOffset(i);
            int slotIndex = Mathf.Max(0, (stackCount - 1) - i);
            Vector3 end = GetStackSlotWorldPosition(resource, binding, slotIndex);
            SpawnTransfer(resource, start, end, stackRotation, stackRotation, true, resource, binding, slotIndex);
        }
    }

    // 플레이어 적재 위치에서 월드 목표 지점으로 이동하는 연출
    public void PlayOutgoingTransfer(ResourceData resource, Vector3 targetWorldPosition, int amount)
    {
        if (!TryGetBinding(resource, out CarryBinding binding))
            return;

        Quaternion stackRotation = PlayerCarrySwayCalculator.GetHorizontalRotation(binding.StackRoot);
        int stackCount = Mathf.Max(0, _resourceStack.GetCount(resource));
        int count = Mathf.Max(1, amount);
        for (int i = 0; i < count; i++)
        {
            int slotIndex = stackCount + i;
            Vector3 start = GetStackSlotWorldPosition(resource, binding, slotIndex);
            Vector3 end = targetWorldPosition + GetTransferScatterOffset(i);
            SpawnTransfer(resource, start, end, stackRotation, stackRotation, false, resource, binding, slotIndex);
        }
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
            GameObject view = PooledViewBridge.Spawn(resource.WorldViewPrefab, root.position, root.rotation, root);
            views.Add(view);
            TriggerStackBounce(view);
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

    private void SpawnTransfer(
        ResourceData resource,
        Vector3 start,
        Vector3 end,
        Quaternion startRotation,
        Quaternion endRotation,
        bool followEnd,
        ResourceData followResource,
        CarryBinding followBinding,
        int followSlotIndex)
    {
        if (resource == null || resource.WorldViewPrefab == null)
            return;

        GameObject view = PooledViewBridge.Spawn(resource.WorldViewPrefab, start, startRotation);
        if (view == null)
            return;

        float distance = Vector3.Distance(start, end);
        float duration = distance / Mathf.Max(MinTransferMoveSpeed, _transferMoveSpeed);
        _transferFlights.Add(new TransferFlight
        {
            View = view,
            Start = start,
            End = end,
            Duration = Mathf.Max(0.05f, duration),
            Elapsed = 0f,
            StartRotation = startRotation,
            EndRotation = endRotation,
            FollowEnd = followEnd,
            Resource = followResource,
            Binding = followBinding,
            SlotIndex = followSlotIndex,
        });
    }

    // 비행 중인 이펙트 뷰 위치 갱신
    private void UpdateTransferFlights()
    {
        for (int i = _transferFlights.Count - 1; i >= 0; i--)
        {
            TransferFlight flight = _transferFlights[i];
            if (flight == null || flight.View == null)
            {
                _transferFlights.RemoveAt(i);
                continue;
            }

            if (flight.FollowEnd && flight.Binding != null && flight.Resource != null)
                flight.End = GetStackSlotWorldPosition(flight.Resource, flight.Binding, flight.SlotIndex);

            flight.Elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(flight.Elapsed / flight.Duration);

            Vector3 previousPosition = flight.View.transform.position;
            Vector3 position = Vector3.Lerp(flight.Start, flight.End, t);
            position.y += Mathf.Sin(t * Mathf.PI) * _transferArcHeight;
            flight.View.transform.position = position;
            UpdateTransferRotation(flight, previousPosition, position, t);

            if (t < 1f)
                continue;

            PooledViewBridge.Release(flight.View);
            _transferFlights.RemoveAt(i);
        }
    }

    private void TriggerStackBounce(GameObject view)
    {
        if (view == null || _stackBounceDuration <= 0f)
            return;

        if (_bounceCoroutines.TryGetValue(view, out Coroutine existing) && existing != null)
            StopCoroutine(existing);

        _bounceCoroutines[view] = StartCoroutine(BounceCoroutine(view));
    }

    private IEnumerator BounceCoroutine(GameObject view)
    {
        if (view == null)
            yield break;

        float half = _stackBounceDuration * 0.5f;
        float elapsed = 0f;

        // 1→peak
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / half;
            if (view != null)
                view.transform.localScale = Vector3.one * Mathf.LerpUnclamped(1f, _stackBounceScale, t);
            yield return null;
        }

        elapsed = 0f;

        // peak→1
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / half;
            if (view != null)
                view.transform.localScale = Vector3.one * Mathf.LerpUnclamped(_stackBounceScale, 1f, t);
            yield return null;
        }

        if (view != null)
            view.transform.localScale = Vector3.one;

        _bounceCoroutines.Remove(view);
    }

    private bool TryGetBinding(ResourceData resource, out CarryBinding binding)
    {
        binding = null;
        if (resource == null || _config == null)
            return false;

        return _config.BindingByResource.TryGetValue(resource, out binding) && binding != null && binding.StackRoot != null;
    }

    private Vector3 GetStackSlotWorldPosition(ResourceData resource, CarryBinding binding, int slotIndex)
    {
        int safeIndex = Mathf.Max(0, slotIndex);
        Vector3 position = TryGetOverrideWorldPosition(resource, binding, safeIndex, out Vector3 overridePosition)
            ? overridePosition
            : binding.StackRoot.TransformPoint(binding.LocalOffset + Vector3.up * (binding.VerticalSpacing * safeIndex));

        if (_sway != null)
            position += _sway.GetLayerSway(binding.VerticalSpacing, safeIndex);

        return position;
    }

    private void UpdateTransferRotation(TransferFlight flight, Vector3 previousPosition, Vector3 currentPosition, float t)
    {
        Vector3 horizontalDirection = currentPosition - previousPosition;
        horizontalDirection.y = 0f;

        if (horizontalDirection.sqrMagnitude < 0.0001f)
        {
            horizontalDirection = flight.End - flight.Start;
            horizontalDirection.y = 0f;
        }

        Quaternion travelRotation = horizontalDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(horizontalDirection.normalized, Vector3.up)
            : flight.EndRotation;

        Quaternion stackRotation = Quaternion.Slerp(flight.StartRotation, flight.EndRotation, t);
        Quaternion targetRotation = Quaternion.Slerp(travelRotation, stackRotation, 0.35f);
        float rotationLerp = Mathf.Clamp01(Time.deltaTime * TransferRotationFollowSpeed);
        flight.View.transform.rotation = Quaternion.Slerp(flight.View.transform.rotation, targetRotation, rotationLerp);
    }

    private Vector3 GetTransferScatterOffset(int index)
    {
        if (_transferScatter <= 0f || index <= 0)
            return Vector3.zero;

        float angle = index * 2.3999631f;
        float radius = _transferScatter * (1f + (index % 3) * 0.2f);
        return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
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
