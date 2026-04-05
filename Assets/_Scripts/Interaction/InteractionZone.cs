using System;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class InteractionZone : MonoBehaviour
{
    [Header("Library")]
    [FormerlySerializedAs("_definition")]
    [FormerlySerializedAs("_data")]
    [SerializeField] private InteractionZoneLibrary _library;
    [FormerlySerializedAs("_applyDefinitionOnAwake")]
    [FormerlySerializedAs("_applyDataOnAwake")]
    [SerializeField] private bool _applyLibraryOnAwake = true;

    [Header("Identity")]
    [SerializeField] private InteractionZoneId _zoneId = InteractionZoneId.None;

    [Header("Zone")]
    [SerializeField] private InteractionZoneType _type = InteractionZoneType.PurchaseEquip;
    [SerializeField] private bool _zoneEnabled = true;
    [SerializeField] private bool _completeOnce = true;
    [SerializeField, Min(0f)] private float _stopSpeedThreshold = 0.05f;

    [Header("Common Resource")]
    [SerializeField] private ResourceData _resource;
    [SerializeField, Min(1)] private int _amountPerTick = 1;
    [SerializeField, Min(0)] private int _completeAmount = 1;
    [SerializeField, Min(0)] private int _storedAmount;

    [Header("Purchase")]
    [SerializeField] private EquipData _purchaseEquip;
    [SerializeField] private int _priceOverride = -1;

    [Header("Runtime")]
    [SerializeField] private InteractionZoneRuntimeState _runtime = new();

    private IInteractionActor _actorInZone;
    private float _nextTickTime;

    public InteractionZoneId ZoneId => _zoneId;
    public InteractionZoneType Type => _type;
    public InteractionZoneLibrary Library => _library;
    public bool IsZoneEnabled => _zoneEnabled;
    public bool IsCompleted => _runtime.Completed;
    public int StoredAmount => _runtime.StoredAmount;
    public int ProcessedAmount => _runtime.ProcessedAmount;
    public ResourceData Resource => _resource;
    public EquipData PurchaseEquip => _purchaseEquip;
    public int AmountPerTick => _amountPerTick;
    public int CompleteAmount => _completeAmount;
    public int PurchaseRequiredAmount => GetPurchaseRequiredAmount();

    public event Action<InteractionZone> Started;
    public event Action<InteractionZone> Completed;
    public event Action<InteractionZone> StateChanged;

    void Awake()
    {
        Collider zoneCollider = GetComponent<Collider>();
        if (!zoneCollider.isTrigger)
            zoneCollider.isTrigger = true;

        if (_applyLibraryOnAwake && _library != null)
        {
            ApplyLibrary(_library, true);
            return;
        }

        _runtime.ResetProgress(_storedAmount);
        NotifyStateChanged();
    }

    // 틱 간격마다 액터 상태 체크 후 인터랙션 처리
    void Update()
    {
        if (!_zoneEnabled)
            return;

        if (_actorInZone == null)
            return;

        if (_completeOnce && _runtime.Completed)
            return;

        if (_type == InteractionZoneType.PurchaseEquip && !_actorInZone.IsInteractionReady(_stopSpeedThreshold))
            return;

        if (Time.time < _nextTickTime)
            return;

        _nextTickTime = Time.time + InteractionZoneRuleController.GetTickInterval(_type, _actorInZone);
        TryProcessInteraction(_actorInZone);
    }

    // 존에 입장한 액터 등록
    void OnTriggerEnter(Collider other)
    {
        if (!InteractionActorResolver.TryResolve(other, out IInteractionActor actor))
            return;

        _actorInZone = actor;
    }

    // 존을 벗어난 액터 해제
    void OnTriggerExit(Collider other)
    {
        if (!InteractionActorResolver.TryResolve(other, out IInteractionActor actor))
            return;

        if (ReferenceEquals(_actorInZone, actor))
            _actorInZone = null;
    }

    // 존 활성/비활성 전환 — 비활성 시 액터 참조 해제
    public void SetZoneEnabled(bool enabled)
    {
        _zoneEnabled = enabled;
        if (!_zoneEnabled)
            _actorInZone = null;

        NotifyStateChanged();
    }

    // 타입 변경 후 진행 상태 초기화
    public void SetZoneType(InteractionZoneType type)
    {
        _type = type;
        _runtime.ResetProgress(_storedAmount);
        _nextTickTime = 0f;
        NotifyStateChanged();
    }

    // 완료 상태 설정 — completeOnce이면 존도 비활성화
    public void SetCompleted(bool completed)
    {
        _runtime.SetCompleted(completed);
        if (_completeOnce && _runtime.Completed)
            _zoneEnabled = false;

        NotifyStateChanged();
    }

    // 보관 수량 증감
    public void AddStoredAmount(int amount)
    {
        _runtime.AddStored(amount);
        SyncStoredAmountField();
        NotifyStateChanged();
    }

    // 진행 상태 초기화
    public void ResetProgress()
    {
        _runtime.ResetProgress(_storedAmount);
        _nextTickTime = 0f;
        _actorInZone = null;
        NotifyStateChanged();
    }

    // library 값을 이 존에 덮어쓰고, resetProgress이면 런타임 상태 초기화
    public void ApplyLibrary(InteractionZoneLibrary library, bool resetProgress)
    {
        if (library == null)
            return;

        _library = library;
        _type = library.Type;
        _zoneEnabled = library.ZoneEnabled;
        _completeOnce = library.CompleteOnce;
        _stopSpeedThreshold = library.StopSpeedThreshold;
        _resource = library.Resource;
        _amountPerTick = library.AmountPerTick;
        _completeAmount = library.CompleteAmount;
        _storedAmount = library.InitialStoredAmount;
        _purchaseEquip = library.PurchaseEquip;
        _priceOverride = library.PriceOverride;

        if (resetProgress)
        {
            _runtime.ResetProgress(_storedAmount);
        }

        NotifyStateChanged();
    }

    // 구매 존 상태를 코드에서 직접 재설정
    public void ConfigurePurchaseStep(
        ResourceData costResource,
        int amountPerTick,
        int requiredAmount,
        EquipData purchaseEquip,
        bool completeOnce,
        bool zoneEnabled,
        bool resetProgress)
    {
        _type = InteractionZoneType.PurchaseEquip;
        _resource = costResource;
        _amountPerTick = Mathf.Max(1, amountPerTick);
        _completeAmount = 0;
        _storedAmount = 0;
        _purchaseEquip = purchaseEquip;
        _priceOverride = Mathf.Max(1, requiredAmount);
        _completeOnce = completeOnce;
        _zoneEnabled = zoneEnabled;

        if (resetProgress)
            ResetProgress();
        else
            NotifyStateChanged();
    }

    // 타입에 맞는 인터랙션 실행 — 최초 성공 시 Started 이벤트, 완료 조건 충족 시 CompleteZone
    private void TryProcessInteraction(IInteractionActor actor)
    {
        bool success = InteractionZoneActionProcessor.TryProcess(
            _type,
            actor,
            _runtime,
            _resource,
            InteractionZoneRuleController.GetAmountPerTick(_type, actor, _amountPerTick),
            GetPurchaseRequiredAmount(),
            _purchaseEquip,
            out _);

        if (!success)
            return;

        SyncStoredAmountField();

        if (!_runtime.Started)
        {
            _runtime.MarkStarted();
            Started?.Invoke(this);
        }

        if (InteractionZoneRuleController.ShouldComplete(
            _completeOnce,
            _type,
            _runtime,
            GetPurchaseRequiredAmount(),
            _purchaseEquip,
            _completeAmount,
            actor))
        {
            CompleteZone();
        }
        else
            NotifyStateChanged();
    }

    // priceOverride → completeAmount 순으로 구매 필요 금액 결정
    private int GetPurchaseRequiredAmount()
    {
        if (_priceOverride >= 0)
            return _priceOverride;

        if (_completeAmount > 0)
            return _completeAmount;

        return 1;
    }

    // 완료 플래그 설정 후 Completed 이벤트 발생
    private void CompleteZone()
    {
        _runtime.SetCompleted(true);
        if (_completeOnce)
            _zoneEnabled = false;

        Completed?.Invoke(this);
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke(this);
    }

    // 직렬화 필드 _storedAmount를 런타임 상태와 동기화
    private void SyncStoredAmountField()
    {
        _storedAmount = _runtime.StoredAmount;
    }
}
