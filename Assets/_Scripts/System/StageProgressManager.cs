using System.Collections.Generic;
using UnityEngine;

// 스테이지 진행 규칙 일괄 관리자
// - Zone Started/Completed 기반 전이
// - 구매 업그레이드 단계 전환
// - 특정 자원 최초 획득 기반 전이
// - Jail 가득참(Full) 기반 전이
[DisallowMultipleComponent]
public class StageProgressManager : MonoBehaviour
{
    private readonly struct ResolvedTransition
    {
        public readonly InteractionZone Target;
        public readonly InteractionZoneTransitionOperation Operation;
        public readonly bool BoolValue;
        public readonly InteractionZoneType TypeValue;
        public readonly InteractionZoneLibrary LibraryValue;

        public ResolvedTransition(
            InteractionZone target,
            InteractionZoneTransitionOperation operation,
            bool boolValue,
            InteractionZoneType typeValue,
            InteractionZoneLibrary libraryValue)
        {
            Target = target;
            Operation = operation;
            BoolValue = boolValue;
            TypeValue = typeValue;
            LibraryValue = libraryValue;
        }
    }

    [Header("Stage Data")]
    [SerializeField] private InteractionZoneFlowLibrary _flowLibrary;

    [Header("External Sources")]
    [SerializeField] private PlayerController _player;
    [SerializeField] private ResourceStack _playerCarryStack;
    [SerializeField] private JailFacility _jailFacility;
    [SerializeField, Min(1)] private int _jailUpgradeCapacity = 40;

    private readonly Dictionary<InteractionZoneId, InteractionZone> _zoneById = new();
    private readonly Dictionary<InteractionZoneId, List<ResolvedTransition>> _onFirstTransitionsBySource = new();
    private readonly Dictionary<InteractionZoneId, List<ResolvedTransition>> _onCompletedTransitionsBySource = new();
    private readonly Dictionary<ResourceData, List<ResolvedTransition>> _onFirstResourceTransitionsByResource = new();
    private readonly List<ResolvedTransition> _onJailBecameFullTransitions = new();
    private readonly Dictionary<InteractionZoneId, List<int>> _purchaseRuleIndicesByTrigger = new();
    private readonly List<InteractionZonePurchaseUpgradeData> _purchaseRules = new();
    private readonly HashSet<int> _appliedPurchaseRuleIndices = new();
    private readonly HashSet<InteractionZoneId> _appliedCompletedTransitionSourceIds = new();
    private readonly HashSet<ResourceData> _appliedFirstResourceTransitionResources = new();
    private readonly List<InteractionZone> _subscribedZones = new();

    private bool _jailFullTransitionsApplied;
    private bool _jailCapacityUpgradeApplied;
    void OnDestroy()
    {
        ClearRuntimeBindings();
    }

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
        ResolveExternalSources();
        BuildZoneIndex();
        ApplyInitialStates(_flowLibrary.InitialStates);
        BuildTransitions(_flowLibrary.Transitions);
        BuildPurchaseUpgradeRules(_flowLibrary.PurchaseUpgrades);
        BindZoneEvents();
        BindExternalEvents();
        EvaluateExternalStateAtStart();
    }

    private void ResolveExternalSources()
    {
        if (_playerCarryStack == null)
        {
            if (_player == null)
                _player = FindObjectOfType<PlayerController>();

            if (_player != null)
                _playerCarryStack = _player.CarryStack;
        }

        if (_jailFacility == null)
            _jailFacility = FindObjectOfType<JailFacility>();
    }

    private void BuildZoneIndex()
    {
        _zoneById.Clear();

        InteractionZone[] zones = Object.FindObjectsByType<InteractionZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            InteractionZone zone = zones[i];
            if (zone == null || zone.ZoneId == InteractionZoneId.None)
                continue;

            if (_zoneById.TryGetValue(zone.ZoneId, out InteractionZone duplicated))
            {
                Debug.LogWarning($"[StageProgressManager] Duplicate zone id detected: {zone.ZoneId}. " +
                                 $"Existing={duplicated.gameObject.name}, New={zone.gameObject.name}");
                continue;
            }

            _zoneById.Add(zone.ZoneId, zone);
        }
    }

    private void ApplyInitialStates(IReadOnlyList<InteractionZoneInitialStateData> states)
    {
        if (states == null)
            return;

        for (int i = 0; i < states.Count; i++)
        {
            InteractionZoneInitialStateData state = states[i];
            if (!TryGetZone(state.ZoneId, out InteractionZone zone))
                continue;

            if (state.ApplyLibraryBeforeReset && zone.Library != null)
                zone.ApplyLibrary(zone.Library, true);
            else if (state.ResetProgress)
                zone.ResetProgress();

            zone.SetCompleted(false);
            zone.SetZoneEnabled(state.ZoneEnabled);
        }
    }

    private void BuildTransitions(IReadOnlyList<InteractionZoneTransitionData> transitions)
    {
        _onFirstTransitionsBySource.Clear();
        _onCompletedTransitionsBySource.Clear();
        _onFirstResourceTransitionsByResource.Clear();
        _onJailBecameFullTransitions.Clear();
        _appliedFirstResourceTransitionResources.Clear();
        _jailFullTransitionsApplied = false;

        if (transitions == null)
            return;

        for (int i = 0; i < transitions.Count; i++)
        {
            InteractionZoneTransitionData data = transitions[i];
            if (!TryGetZone(data.TargetZoneId, out InteractionZone targetZone))
                continue;

            ResolvedTransition transition = new(
                targetZone,
                data.Operation,
                data.BoolValue,
                data.TypeValue,
                data.LibraryValue);

            switch (data.Trigger)
            {
                case InteractionZoneFlowTrigger.OnFirstInteraction:
                    if (TryGetZone(data.SourceZoneId, out InteractionZone firstSourceZone))
                        AddTransition(_onFirstTransitionsBySource, firstSourceZone.ZoneId, transition);
                    break;
                case InteractionZoneFlowTrigger.OnCompleted:
                    if (TryGetZone(data.SourceZoneId, out InteractionZone completedSourceZone))
                        AddTransition(_onCompletedTransitionsBySource, completedSourceZone.ZoneId, transition);
                    break;
                case InteractionZoneFlowTrigger.OnFirstResourceAcquired:
                {
                    ResourceData resource = data.ResourceValue;
                    if (resource == null)
                        break;

                    AddTransition(_onFirstResourceTransitionsByResource, resource, transition);
                    break;
                }
                case InteractionZoneFlowTrigger.OnJailBecameFull:
                    _onJailBecameFullTransitions.Add(transition);
                    break;
            }
        }
    }

    private static void AddTransition<T>(
        Dictionary<T, List<ResolvedTransition>> map,
        T key,
        ResolvedTransition transition)
    {
        if (!map.TryGetValue(key, out List<ResolvedTransition> list))
        {
            list = new List<ResolvedTransition>();
            map.Add(key, list);
        }

        list.Add(transition);
    }

    private void BuildPurchaseUpgradeRules(IReadOnlyList<InteractionZonePurchaseUpgradeData> purchaseRules)
    {
        _purchaseRules.Clear();
        _purchaseRuleIndicesByTrigger.Clear();
        _appliedPurchaseRuleIndices.Clear();

        if (purchaseRules == null || purchaseRules.Count == 0)
            return;

        for (int i = 0; i < purchaseRules.Count; i++)
        {
            InteractionZonePurchaseUpgradeData rule = purchaseRules[i];
            _purchaseRules.Add(rule);

            if (!_purchaseRuleIndicesByTrigger.TryGetValue(rule.TriggerZoneId, out List<int> indices))
            {
                indices = new List<int>();
                _purchaseRuleIndicesByTrigger.Add(rule.TriggerZoneId, indices);
            }

            indices.Add(i);
        }
    }

    private void BindZoneEvents()
    {
        foreach (InteractionZone zone in _zoneById.Values)
        {
            if (zone == null)
                continue;

            zone.Started += OnZoneStarted;
            zone.Completed += OnZoneCompleted;
            _subscribedZones.Add(zone);
        }
    }

    private void BindExternalEvents()
    {
        if (_onFirstResourceTransitionsByResource.Count > 0)
        {
            if (_playerCarryStack == null)
                Debug.LogWarning("[StageProgressManager] Player carry stack is missing for OnFirstResourceAcquired rules.");
            else
                _playerCarryStack.Changed += OnPlayerCarryChanged;
        }

        if (_onJailBecameFullTransitions.Count > 0)
        {
            if (_jailFacility == null)
                Debug.LogWarning("[StageProgressManager] Jail facility is missing for OnJailBecameFull rules.");
            else
                _jailFacility.StateChanged += OnJailStateChanged;
        }
    }

    private void EvaluateExternalStateAtStart()
    {
        EvaluateResourceRulesFromCurrentCarry();
        EvaluateJailFullRules();
    }

    private void EvaluateResourceRulesFromCurrentCarry()
    {
        if (_playerCarryStack == null || _onFirstResourceTransitionsByResource.Count == 0)
            return;

        List<ResourceData> resources = new(_onFirstResourceTransitionsByResource.Keys);
        for (int i = 0; i < resources.Count; i++)
        {
            ResourceData resource = resources[i];
            if (resource == null || _appliedFirstResourceTransitionResources.Contains(resource))
                continue;

            if (_playerCarryStack.GetCount(resource) <= 0)
                continue;

            _appliedFirstResourceTransitionResources.Add(resource);
            ApplyTransitions(_onFirstResourceTransitionsByResource, resource);
        }
    }

    private void EvaluateJailFullRules()
    {
        if (_jailFacility == null || _onJailBecameFullTransitions.Count == 0 || _jailFullTransitionsApplied)
            return;

        if (_jailFacility.IsOpen)
            return;

        _jailFullTransitionsApplied = true;
        ApplyTransitions(_onJailBecameFullTransitions);
    }

    private void OnZoneStarted(InteractionZone startedZone)
    {
        if (startedZone == null)
            return;

        ApplyTransitions(_onFirstTransitionsBySource, startedZone.ZoneId);
    }

    private void OnZoneCompleted(InteractionZone completedZone)
    {
        if (completedZone == null)
            return;

        if (_appliedCompletedTransitionSourceIds.Add(completedZone.ZoneId))
            ApplyTransitions(_onCompletedTransitionsBySource, completedZone.ZoneId);

        ApplyPurchaseUpgrades(completedZone.ZoneId);

        if (completedZone.ZoneId == InteractionZoneId.BuyEquip && completedZone.IsCompleted)
            completedZone.gameObject.SetActive(false);

        if (completedZone.ZoneId == InteractionZoneId.BuyJail && completedZone.IsCompleted)
        {
            TryApplyJailCapacityUpgrade();
            completedZone.gameObject.SetActive(false);
        }
    }

    private void OnPlayerCarryChanged(ResourceData resource, int count, int capacity)
    {
        if (resource == null || count <= 0)
            return;

        if (_appliedFirstResourceTransitionResources.Contains(resource))
            return;

        if (!_onFirstResourceTransitionsByResource.ContainsKey(resource))
            return;

        _appliedFirstResourceTransitionResources.Add(resource);
        ApplyTransitions(_onFirstResourceTransitionsByResource, resource);
    }

    private void OnJailStateChanged(JailFacility jail)
    {
        EvaluateJailFullRules();
    }

    private void ApplyTransitions(
        Dictionary<InteractionZoneId, List<ResolvedTransition>> map,
        InteractionZoneId sourceZoneId)
    {
        if (!map.TryGetValue(sourceZoneId, out List<ResolvedTransition> transitions))
            return;

        ApplyTransitions(transitions);
    }

    private void ApplyTransitions(
        Dictionary<ResourceData, List<ResolvedTransition>> map,
        ResourceData resource)
    {
        if (!map.TryGetValue(resource, out List<ResolvedTransition> transitions))
            return;

        ApplyTransitions(transitions);
    }

    private void ApplyTransitions(List<ResolvedTransition> transitions)
    {
        for (int i = 0; i < transitions.Count; i++)
            ApplyTransition(transitions[i]);
    }

    private void ApplyTransition(ResolvedTransition transition)
    {
        InteractionZone target = transition.Target;
        if (target == null)
            return;

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

    private void ApplyPurchaseUpgrades(InteractionZoneId triggerZoneId)
    {
        if (!_purchaseRuleIndicesByTrigger.TryGetValue(triggerZoneId, out List<int> ruleIndices))
            return;

        for (int i = 0; i < ruleIndices.Count; i++)
        {
            int ruleIndex = ruleIndices[i];
            if (_appliedPurchaseRuleIndices.Contains(ruleIndex))
                continue;

            InteractionZonePurchaseUpgradeData rule = _purchaseRules[ruleIndex];
            if (!TryGetZone(rule.TargetZoneId, out InteractionZone targetZone))
                continue;

            EquipData equip = ResolveEquip(rule.EquipId);
            if (equip == null)
            {
                Debug.LogWarning($"[StageProgressManager] Equip not found for id: {rule.EquipId}");
                continue;
            }

            targetZone.ConfigurePurchaseStep(
                targetZone.Resource,
                targetZone.AmountPerTick,
                rule.RequiredAmount,
                equip,
                rule.CompleteOnce,
                rule.ZoneEnabledAfterUpgrade,
                true);

            _appliedPurchaseRuleIndices.Add(ruleIndex);
        }
    }

    private static EquipData ResolveEquip(string equipId)
    {
        if (string.IsNullOrEmpty(equipId))
            return null;

        EquipBase[] equipBases = Object.FindObjectsByType<EquipBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < equipBases.Length; i++)
        {
            EquipBase equipBase = equipBases[i];
            if (equipBase == null)
                continue;

            EquipData equip = equipBase.GetDataById(equipId);
            if (equip != null)
                return equip;
        }

        return null;
    }

    private bool TryGetZone(InteractionZoneId zoneId, out InteractionZone zone)
    {
        zone = null;

        if (zoneId == InteractionZoneId.None)
            return false;

        if (_zoneById.TryGetValue(zoneId, out zone))
            return true;

        Debug.LogWarning($"[StageProgressManager] Zone not found for id: {zoneId}");
        return false;
    }

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

        if (_playerCarryStack != null)
            _playerCarryStack.Changed -= OnPlayerCarryChanged;

        if (_jailFacility != null)
            _jailFacility.StateChanged -= OnJailStateChanged;

        _zoneById.Clear();
        _onFirstTransitionsBySource.Clear();
        _onCompletedTransitionsBySource.Clear();
        _onFirstResourceTransitionsByResource.Clear();
        _onJailBecameFullTransitions.Clear();
        _purchaseRuleIndicesByTrigger.Clear();
        _purchaseRules.Clear();
        _appliedPurchaseRuleIndices.Clear();
        _appliedCompletedTransitionSourceIds.Clear();
        _appliedFirstResourceTransitionResources.Clear();
        _jailFullTransitionsApplied = false;
        _jailCapacityUpgradeApplied = false;
    }

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
