using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct InteractionZoneInitialStateData
{
    [SerializeField] private InteractionZoneId _zoneId;
    [SerializeField] private bool _zoneEnabled;
    [SerializeField] private bool _resetProgress;
    [SerializeField] private bool _applyLibraryBeforeReset;

    public InteractionZoneId ZoneId => _zoneId;
    public bool ZoneEnabled => _zoneEnabled;
    public bool ResetProgress => _resetProgress;
    public bool ApplyLibraryBeforeReset => _applyLibraryBeforeReset;
}

[Serializable]
public struct InteractionZoneTransitionData
{
    [SerializeField] private InteractionZoneId _sourceZoneId;
    [SerializeField] private InteractionZoneFlowTrigger _trigger;
    [SerializeField] private InteractionZoneId _targetZoneId;
    [SerializeField] private InteractionZoneTransitionOperation _operation;
    [SerializeField] private bool _boolValue;
    [SerializeField] private InteractionZoneType _typeValue;
    [SerializeField] private InteractionZoneLibrary _libraryValue;

    public InteractionZoneId SourceZoneId => _sourceZoneId;
    public InteractionZoneFlowTrigger Trigger => _trigger;
    public InteractionZoneId TargetZoneId => _targetZoneId;
    public InteractionZoneTransitionOperation Operation => _operation;
    public bool BoolValue => _boolValue;
    public InteractionZoneType TypeValue => _typeValue;
    public InteractionZoneLibrary LibraryValue => _libraryValue;
}

[Serializable]
public struct InteractionZonePurchaseUpgradeData
{
    [SerializeField] private InteractionZoneId _triggerZoneId;
    [SerializeField] private InteractionZoneId _targetZoneId;
    [SerializeField] private string _equipId;
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
