using System;
using System.Collections.Generic;
using UnityEngine;

// 씬 시작 시 각 존의 초기 활성 여부와 진행 상태 초기화 방식을 정의
[Serializable]
public struct InteractionZoneInitialStateData
{
    [SerializeField] private InteractionZoneId _zoneId;
    [SerializeField] private bool _zoneEnabled;
    [SerializeField] private bool _resetProgress;

    public InteractionZoneId ZoneId => _zoneId;
    public bool ZoneEnabled => _zoneEnabled;
    public bool ResetProgress => _resetProgress;
}

// 특정 존의 이벤트 발생 시 다른 존에 적용할 전이 한 건을 정의
[Serializable]
public struct InteractionZoneTransitionData
{
    [SerializeField] private InteractionZoneId _sourceZoneId;
    [SerializeField] private InteractionZoneFlowTrigger _trigger;
    [SerializeField] private InteractionZoneId _targetZoneId;
    [SerializeField] private InteractionZoneTransitionOperation _operation;
    [SerializeField] private bool _boolValue;
    [SerializeField] private InteractionZoneType _typeValue;

    public InteractionZoneId SourceZoneId => _sourceZoneId;
    public InteractionZoneFlowTrigger Trigger => _trigger;
    public InteractionZoneId TargetZoneId => _targetZoneId;
    public InteractionZoneTransitionOperation Operation => _operation;
    public bool BoolValue => _boolValue;
    public InteractionZoneType TypeValue => _typeValue;
}

// 특정 존 완료 시 대상 존의 구매 단계를 다음 장비로 업그레이드하는 규칙 정의
[Serializable]
public struct InteractionZonePurchaseUpgradeData
{
    [SerializeField] private InteractionZoneId _triggerZoneId;  // 이 존이 완료되면 규칙 실행
    [SerializeField] private InteractionZoneId _targetZoneId;  // 업그레이드할 구매 존
    [SerializeField] private string _equipId;                  // 다음 단계 장비 ID
    [SerializeField, Min(1)] private int _requiredAmount;
    [SerializeField] private bool _zoneEnabledAfterUpgrade;
    [SerializeField] private bool _completeOnce;

    public InteractionZoneId TriggerZoneId => _triggerZoneId;
    public InteractionZoneId TargetZoneId => _targetZoneId;
    public string EquipId => _equipId;
    public int RequiredAmount => Mathf.Max(1, _requiredAmount);
    public bool ZoneEnabledAfterUpgrade => _zoneEnabledAfterUpgrade;
    public bool CompleteOnce => _completeOnce;
}

// 씬 전체 진행 규칙 데이터를 보관하는 SO
// StageProgressManager.Initialize()에 전달해 씬 흐름 초기화에 사용
[CreateAssetMenu(menuName = "Game/Interaction/Flow Library", fileName = "InteractionZoneFlowLibrary")]
public class InteractionZoneFlowLibrary : ScriptableObject
{
    [SerializeField] private List<InteractionZoneInitialStateData> _initialStates = new();
    [SerializeField] private List<InteractionZoneTransitionData> _transitions = new();
    [SerializeField] private List<InteractionZonePurchaseUpgradeData> _purchaseUpgrades = new();

    public IReadOnlyList<InteractionZoneInitialStateData> InitialStates => _initialStates;
    public IReadOnlyList<InteractionZoneTransitionData> Transitions => _transitions;
    public IReadOnlyList<InteractionZonePurchaseUpgradeData> PurchaseUpgrades => _purchaseUpgrades;
}
