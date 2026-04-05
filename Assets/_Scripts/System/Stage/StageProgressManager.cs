using System.Collections.Generic;
using UnityEngine;

// 스테이지 진행 오케스트레이터
// - Zone 흐름 실행
// - 구매 업그레이드 적용
// - 외부 상태(자원/Jail) 연결
[DisallowMultipleComponent]
public class StageProgressManager : MonoBehaviour
{
    [Header("Stage Data")]
    [SerializeField] private InteractionZoneFlowLibrary _flowLibrary;

    [Header("External Sources")]
    [SerializeField] private ResourceStack _playerCarryStack;
    [SerializeField] private JailFacility _jailFacility;
    [SerializeField] private EquipLevelLibrary _equipLevelLibrary;
    [SerializeField, Min(1)] private int _jailUpgradeCapacity = 40;

    private readonly ZoneRegistry _zoneRegistry = new();
    private readonly ZoneFlowController _zoneFlowController = new();
    private readonly ZonePurchaseUpgradeService _purchaseUpgradeService = new();
    private readonly StageStateMonitor _stageStateMonitor = new();
    private readonly List<InteractionZone> _subscribedZones = new();
    private readonly List<ResourceData> _resourceRuleKeys = new();

    private bool _jailCapacityUpgradeApplied;

    void OnDestroy()
    {
        ClearRuntimeBindings();
    }

    // flowLibrary 기준으로 전체 진행 규칙 초기화
    public void Initialize(InteractionZoneFlowLibrary flowLibrary = null)
    {
        if (flowLibrary != null)
            _flowLibrary = flowLibrary;

        if (_flowLibrary == null)
        {
            Debug.LogWarning("[StageProgressManager] Flow library is null.");
            return;
        }

        ClearRuntimeBindings();

        _zoneRegistry.BuildFromScene();
        _zoneFlowController.Initialize(_flowLibrary.InitialStates, _flowLibrary.Transitions, _zoneRegistry);
        _purchaseUpgradeService.Initialize(_flowLibrary.PurchaseUpgrades, _zoneRegistry, _equipLevelLibrary);

        BindZoneEvents();
        BindStageStateMonitor();
    }

    // 모든 Zone Started/Completed 이벤트 구독
    private void BindZoneEvents()
    {
        foreach (InteractionZone zone in _zoneRegistry.Zones)
        {
            if (zone == null)
                continue;

            zone.Started += OnZoneStarted;
            zone.Completed += OnZoneCompleted;
            _subscribedZones.Add(zone);
        }
    }

    // 외부 상태 이벤트 감시 연결
    private void BindStageStateMonitor()
    {
        _zoneFlowController.FillFirstResourceKeys(_resourceRuleKeys);

        _stageStateMonitor.Bind(
            _playerCarryStack,
            _jailFacility,
            _resourceRuleKeys,
            _zoneFlowController.HasJailFullRules,
            OnFirstResourceAcquired,
            OnJailStateEvaluated);
    }

    // Zone Started 처리
    private void OnZoneStarted(InteractionZone zone)
    {
        if (zone == null)
            return;

        _zoneFlowController.OnZoneStarted(zone.ZoneId);
    }

    // Zone Completed 처리
    private void OnZoneCompleted(InteractionZone zone)
    {
        if (zone == null)
            return;

        _zoneFlowController.OnZoneCompleted(zone.ZoneId);
        _purchaseUpgradeService.ApplyForTrigger(zone.ZoneId);
        ApplyZoneCompleteSideEffects(zone);
    }

    // 최초 자원 획득 트리거 처리
    private void OnFirstResourceAcquired(ResourceData resource)
    {
        _zoneFlowController.OnFirstResourceAcquired(resource);
    }

    // Jail open/close 상태 트리거 처리
    private void OnJailStateEvaluated(bool isJailOpen)
    {
        _zoneFlowController.EvaluateJailState(isJailOpen);
    }

    // Zone 완료 후처리
    private void ApplyZoneCompleteSideEffects(InteractionZone zone)
    {
        if (!zone.IsCompleted)
            return;

        if (zone.ZoneId == InteractionZoneId.BuyEquip)
            zone.gameObject.SetActive(false);

        if (zone.ZoneId == InteractionZoneId.BuyJail)
        {
            TryApplyJailCapacityUpgrade();
            zone.gameObject.SetActive(false);
        }
    }

    // 런타임 바인딩 해제 및 상태 초기화
    private void ClearRuntimeBindings()
    {
        for (int i = 0; i < _subscribedZones.Count; i++)
        {
            InteractionZone zone = _subscribedZones[i];
            if (zone == null)
                continue;

            zone.Started -= OnZoneStarted;
            zone.Completed -= OnZoneCompleted;
        }

        _subscribedZones.Clear();
        _resourceRuleKeys.Clear();
        _stageStateMonitor.Unbind();
        _zoneFlowController.Clear();
        _purchaseUpgradeService.Clear();
        _zoneRegistry.Clear();
        _jailCapacityUpgradeApplied = false;
    }

    // BuyJail 완료 시 JailFacility 최대 수용량 업그레이드
    private void TryApplyJailCapacityUpgrade()
    {
        if (_jailCapacityUpgradeApplied)
            return;

        if (_jailFacility == null)
            return;

        int before = _jailFacility.MaxCapacity;
        bool changed = _jailFacility.SetMaxCapacity(_jailUpgradeCapacity);
        _jailCapacityUpgradeApplied = true;

        if (!changed)
            return;

        Debug.Log($"[StageProgressManager] BuyJail completed: max capacity {before} -> {_jailFacility.MaxCapacity}");
        // TODO: Expand jail area size/layout after Jail upgrade is purchased.
    }
}
