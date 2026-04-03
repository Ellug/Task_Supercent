using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System;

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
    [SerializeField, Min(0f)] private float _tickInterval = 0.2f;
    [SerializeField] private bool _requirePlayerIdle = true;
    [SerializeField, Min(0f)] private float _stopSpeedThreshold = 0.05f;

    [Header("Common Resource")]
    [SerializeField] private ResourceData _resource;
    [SerializeField, Min(1)] private int _amountPerTick = 1;
    [SerializeField, Min(0)] private int _completeAmount = 1;
    [SerializeField, Min(0)] private int _storedAmount;

    [Header("Purchase")]
    [SerializeField] private EquipData _purchaseEquip;
    [SerializeField] private int _priceOverride = -1;

    [Header("World UI")]
    [SerializeField] private TMP_Text _amountText;
    [SerializeField] private Image _iconImage;

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
    public ResourceData Resource => _resource;
    public int AmountPerTick => _amountPerTick;

    public event Action<InteractionZone> Started;
    public event Action<InteractionZone> Completed;
    public event Action<IInteractionActor, ResourceData, int> ResourceSubmitted;
    public event Action<IInteractionActor, ResourceData, int> ResourceCollected;

    void Awake()
    {
        Collider zoneCollider = GetComponent<Collider>();
        if (!zoneCollider.isTrigger)
            zoneCollider.isTrigger = true;

        if (_applyLibraryOnAwake && _library != null)
            ApplyLibrary(_library, true);

        EnsureRuntimeState();
        UpdateWorldUI();
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

        bool requireIdle = _type == InteractionZoneType.PurchaseEquip && _requirePlayerIdle;
        if (requireIdle && !_actorInZone.IsInteractionReady(_stopSpeedThreshold))
            return;

        if (Time.time < _nextTickTime)
            return;

        _nextTickTime = Time.time + GetEffectiveTickInterval(_actorInZone);
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

        UpdateWorldUI();
    }

    // 타입 변경 후 진행 상태 초기화
    public void SetZoneType(InteractionZoneType type)
    {
        _type = type;
        _runtime.ResetProgress(_storedAmount);
        _nextTickTime = 0f;
        UpdateWorldUI();
    }

    // 완료 상태 설정 — completeOnce이면 존도 비활성화
    public void SetCompleted(bool completed)
    {
        _runtime.SetCompleted(completed);
        if (_completeOnce && _runtime.Completed)
            _zoneEnabled = false;

        UpdateWorldUI();
    }

    // 보관 수량 증감
    public void AddStoredAmount(int amount)
    {
        _runtime.AddStored(amount);
        SyncStoredAmountField();
        UpdateWorldUI();
    }

    // 진행 상태 초기화
    public void ResetProgress()
    {
        EnsureRuntimeState();
        _runtime.ResetProgress(_storedAmount);
        _nextTickTime = 0f;
        _actorInZone = null;
        UpdateWorldUI();
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
        _tickInterval = library.TickInterval;
        _requirePlayerIdle = library.RequireActorIdle;
        _stopSpeedThreshold = library.StopSpeedThreshold;
        _resource = library.Resource;
        _amountPerTick = library.AmountPerTick;
        _completeAmount = library.CompleteAmount;
        _storedAmount = library.InitialStoredAmount;
        _purchaseEquip = library.PurchaseEquip;
        _priceOverride = library.PriceOverride;

        if (resetProgress)
        {
            EnsureRuntimeState();
            _runtime.ResetProgress(_storedAmount);
        }

        UpdateWorldUI();
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
        _completeAmount = Mathf.Max(0, requiredAmount);
        _storedAmount = 0;
        _purchaseEquip = purchaseEquip;
        _priceOverride = Mathf.Max(1, requiredAmount);
        _completeOnce = completeOnce;
        _zoneEnabled = zoneEnabled;

        if (resetProgress)
            ResetProgress();

        UpdateWorldUI();
    }

    // 타입에 맞는 인터랙션 실행 — 최초 성공 시 Started 이벤트, 완료 조건 충족 시 CompleteZone
    private void TryProcessInteraction(IInteractionActor actor)
    {
        int submittedAmount = 0;
        int collectedAmount = 0;
        bool success = InteractionZoneActionProcessor.TryProcess(
            _type,
            actor,
            _runtime,
            _resource,
            GetEffectiveAmountPerTick(actor),
            GetPurchaseRequiredAmount(),
            _purchaseEquip,
            out int movedAmount);

        if (!success)
            return;

        if (movedAmount > 0)
        {
            if (_type == InteractionZoneType.SubmitResource)
                submittedAmount = movedAmount;
            else if (_type == InteractionZoneType.CollectResource)
                collectedAmount = movedAmount;
        }

        SyncStoredAmountField();

        if (!_runtime.Started)
        {
            _runtime.MarkStarted();
            Started?.Invoke(this);
        }

        if (submittedAmount > 0)
            ResourceSubmitted?.Invoke(actor, _resource, submittedAmount);

        if (collectedAmount > 0)
            ResourceCollected?.Invoke(actor, _resource, collectedAmount);

        if (ShouldComplete(actor))
            CompleteZone();

        UpdateWorldUI();
    }

    private float GetEffectiveTickInterval(IInteractionActor actor)
    {
        if (actor != null)
        {
            if (_type == InteractionZoneType.SubmitResource)
                return actor.SubmitTickInterval;

            if (_type == InteractionZoneType.CollectResource)
                return actor.CollectTickInterval;
        }

        return Mathf.Max(0f, _tickInterval);
    }

    private int GetEffectiveAmountPerTick(IInteractionActor actor)
    {
        if (actor != null)
        {
            if (_type == InteractionZoneType.SubmitResource)
                return actor.SubmitAmountPerTick;

            if (_type == InteractionZoneType.CollectResource)
                return actor.CollectAmountPerTick;
        }

        return Mathf.Max(1, _amountPerTick);
    }

    // 타입별 완료 조건 판단
    private bool ShouldComplete(IInteractionActor actor)
    {
        if (!_completeOnce)
            return false;

        switch (_type)
        {
            case InteractionZoneType.PurchaseEquip:
            {
                if (_runtime.StoredAmount < GetPurchaseRequiredAmount())
                    return false;

                if (_purchaseEquip == null)
                    return true;

                return actor != null && actor.HasEquipOrBetter(_purchaseEquip);
            }
            case InteractionZoneType.SubmitResource:
                return _completeAmount > 0 && _runtime.ProcessedAmount >= _completeAmount;
            case InteractionZoneType.CollectResource:
                if (_completeAmount > 0)
                    return _runtime.ProcessedAmount >= _completeAmount;

                return _runtime.StoredAmount <= 0;
            default:
                return false;
        }
    }

    // priceOverride → purchaseEquip.Price → completeAmount 순으로 구매 필요 금액 결정
    private int GetPurchaseRequiredAmount()
    {
        int price = _priceOverride >= 0 ? _priceOverride : (_purchaseEquip != null ? _purchaseEquip.Price : 0);
        if (price > 0)
            return price;

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
        UpdateWorldUI();
    }

    // 타입·상태에 맞게 월드 UI 텍스트·아이콘 갱신
    private void UpdateWorldUI()
    {
        if (_amountText != null)
            _amountText.text = InteractionZoneUIPresenter.BuildAmountText(
                _type,
                _runtime.StoredAmount,
                _runtime.ProcessedAmount,
                _completeAmount,
                GetPurchaseRequiredAmount());

        if (_iconImage != null)
            _iconImage.sprite = InteractionZoneUIPresenter.ResolveIconSprite(_type, _resource, _purchaseEquip);
    }

    // _runtime이 null이면 새로 생성 후 초기화
    private void EnsureRuntimeState()
    {
        if (_runtime == null)
            _runtime = new InteractionZoneRuntimeState();

        _runtime.ResetProgress(_storedAmount);
    }

    // 직렬화 필드 _storedAmount를 런타임 상태와 동기화
    private void SyncStoredAmountField()
    {
        _storedAmount = _runtime.StoredAmount;
    }
}

