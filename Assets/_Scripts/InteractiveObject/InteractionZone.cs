using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

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
    [SerializeField] private EquipDefinition _purchaseEquip;
    [SerializeField] private int _priceOverride = -1;

    [Header("Transitions")]
    [SerializeField] private List<InteractionZoneTransition> _onFirstInteraction = new();
    [SerializeField] private List<InteractionZoneTransition> _onCompleted = new();

    [Header("World UI")]
    [SerializeField] private TMP_Text _amountText;
    [SerializeField] private Image _iconImage;

    [Header("Runtime")]
    [SerializeField] private InteractionZoneRuntimeState _runtime = new();

    private IInteractionActor _actorInZone;
    private float _nextTickTime;

    public InteractionZoneType Type => _type;
    public bool IsZoneEnabled => _zoneEnabled;
    public bool IsCompleted => _runtime != null && _runtime.Completed;
    public int StoredAmount => _runtime != null ? _runtime.StoredAmount : _storedAmount;

    void Awake()
    {
        Collider zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null && !zoneCollider.isTrigger)
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

        if (_requirePlayerIdle && !_actorInZone.IsInteractionReady(_stopSpeedThreshold))
            return;

        if (Time.time < _nextTickTime)
            return;

        _nextTickTime = Time.time + _tickInterval;
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

    // 타입에 맞는 인터랙션 실행 — 최초 성공 시 onFirstInteraction, 완료 조건 충족 시 CompleteZone
    private void TryProcessInteraction(IInteractionActor actor)
    {
        if (actor == null)
            return;

        bool success = InteractionZoneActionProcessor.TryProcess(
            _type,
            actor,
            _runtime,
            _resource,
            _amountPerTick,
            GetPurchaseRequiredAmount(),
            _purchaseEquip);

        if (!success)
            return;

        SyncStoredAmountField();

        if (!_runtime.Started)
        {
            _runtime.MarkStarted();
            ApplyTransitions(_onFirstInteraction);
        }

        if (ShouldComplete(actor))
            CompleteZone();

        UpdateWorldUI();
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

    // 완료 플래그 설정 후 onCompleted 트랜지션 실행
    private void CompleteZone()
    {
        _runtime.SetCompleted(true);
        if (_completeOnce)
            _zoneEnabled = false;

        ApplyTransitions(_onCompleted);
        UpdateWorldUI();
    }

    // 트랜지션 목록을 순서대로 대상 존에 적용
    private void ApplyTransitions(List<InteractionZoneTransition> transitions)
    {
        for (int i = 0; i < transitions.Count; i++)
        {
            InteractionZoneTransition transition = transitions[i];
            InteractionZone target = transition.Target;
            if (target == null)
                continue;

            switch (transition.Operation)
            {
                case InteractionZoneTransitionOperation.SetZoneEnabled:
                    target.SetZoneEnabled(transition.BoolValue);
                    break;
                case InteractionZoneTransitionOperation.SetGameObjectActive:
                    target.gameObject.SetActive(transition.BoolValue);
                    break;
                case InteractionZoneTransitionOperation.SetCompleted:
                    target.SetCompleted(transition.BoolValue);
                    break;
                case InteractionZoneTransitionOperation.ChangeType:
                    target.SetZoneType(transition.TypeValue);
                    break;
                case InteractionZoneTransitionOperation.ApplyLibrary:
                    target.ApplyLibrary(transition.LibraryValue, true);
                    break;
            }
        }
    }

    // 타입·상태에 맞게 월드 UI 텍스트·아이콘 갱신
    private void UpdateWorldUI()
    {
        if (_runtime == null)
            return;

        if (_amountText != null)
            _amountText.text = InteractionZoneUIPresenter.BuildAmountText(
                _type,
                _runtime.StoredAmount,
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

