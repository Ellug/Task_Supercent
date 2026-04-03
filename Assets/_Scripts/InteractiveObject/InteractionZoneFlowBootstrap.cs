using System.Collections.Generic;
using UnityEngine;

// FlowLibrary SO 데이터를 읽어 씬의 InteractionZone들에 초기 상태·전이·구매 업그레이드 규칙을 일괄 적용하는 정적 부트스트래퍼
// Apply() 한 번 호출로 씬 전체 존 흐름을 세팅
public static class InteractionZoneFlowBootstrap
{
    // FlowLibrary의 전이 데이터를 런타임에서 바로 실행할 수 있도록 미리 resolve해 둔 구조체
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

    private static readonly Dictionary<InteractionZoneId, InteractionZone> _zoneById = new();
    private static readonly Dictionary<InteractionZoneId, List<ResolvedTransition>> _onFirstTransitionsBySource = new();
    private static readonly Dictionary<InteractionZoneId, List<ResolvedTransition>> _onCompletedTransitionsBySource = new();
    private static readonly Dictionary<InteractionZoneId, List<int>> _purchaseRuleIndicesByTrigger = new();
    private static readonly List<InteractionZonePurchaseUpgradeData> _purchaseRules = new();
    private static readonly HashSet<int> _appliedPurchaseRuleIndices = new();
    private static readonly List<InteractionZone> _subscribedZones = new();

    // 전체 흐름 초기화 진입점 — 기존 바인딩 해제 후 flowLibrary 기준으로 재구성
    public static void Apply(InteractionZoneFlowLibrary flowLibrary)
    {
        if (flowLibrary == null)
        {
            Debug.LogWarning("[InteractionZoneFlowBootstrap] Flow library is null.");
            return;
        }

        ClearRuntimeBindings();
        BuildZoneIndex();
        ApplyInitialStates(flowLibrary.InitialStates);
        BuildTransitions(flowLibrary.Transitions);
        BuildPurchaseUpgradeRules(flowLibrary.PurchaseUpgrades);
        BindZoneEvents();
    }

    // 씬의 모든 InteractionZone을 ZoneId 기준으로 딕셔너리에 인덱싱
    private static void BuildZoneIndex()
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
                Debug.LogWarning($"[InteractionZoneFlowBootstrap] Duplicate zone id detected: {zone.ZoneId}. " +
                                 $"Existing={duplicated.gameObject.name}, New={zone.gameObject.name}");
                continue;
            }

            _zoneById.Add(zone.ZoneId, zone);
        }
    }

    // 각 존의 초기 활성 여부·진행 상태를 데이터 기준으로 설정
    private static void ApplyInitialStates(IReadOnlyList<InteractionZoneInitialStateData> states)
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

    // 전이 데이터를 source 존 ID 기준으로 분류해 딕셔너리에 저장
    private static void BuildTransitions(IReadOnlyList<InteractionZoneTransitionData> transitions)
    {
        _onFirstTransitionsBySource.Clear();
        _onCompletedTransitionsBySource.Clear();

        if (transitions == null)
            return;

        for (int i = 0; i < transitions.Count; i++)
        {
            InteractionZoneTransitionData data = transitions[i];
            if (!TryGetZone(data.SourceZoneId, out InteractionZone sourceZone))
                continue;

            if (!TryGetZone(data.TargetZoneId, out InteractionZone targetZone))
                continue;

            ResolvedTransition transition = new(
                targetZone,
                data.Operation,
                data.BoolValue,
                data.TypeValue,
                data.LibraryValue);

            if (data.Trigger == InteractionZoneFlowTrigger.OnFirstInteraction)
                AddTransition(_onFirstTransitionsBySource, sourceZone.ZoneId, transition);
            else
                AddTransition(_onCompletedTransitionsBySource, sourceZone.ZoneId, transition);
        }
    }

    private static void AddTransition(
        Dictionary<InteractionZoneId, List<ResolvedTransition>> map,
        InteractionZoneId sourceZoneId,
        ResolvedTransition transition)
    {
        if (!map.TryGetValue(sourceZoneId, out List<ResolvedTransition> list))
        {
            list = new List<ResolvedTransition>();
            map.Add(sourceZoneId, list);
        }

        list.Add(transition);
    }

    // 구매 업그레이드 규칙을 트리거 존 ID 기준으로 인덱싱
    private static void BuildPurchaseUpgradeRules(IReadOnlyList<InteractionZonePurchaseUpgradeData> purchaseRules)
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

    // 전이·구매 업그레이드가 걸린 존에만 이벤트 구독
    private static void BindZoneEvents()
    {
        HashSet<InteractionZoneId> sourceIds = new();

        foreach (var pair in _onFirstTransitionsBySource)
            sourceIds.Add(pair.Key);

        foreach (var pair in _onCompletedTransitionsBySource)
            sourceIds.Add(pair.Key);

        foreach (var pair in _purchaseRuleIndicesByTrigger)
            sourceIds.Add(pair.Key);

        foreach (InteractionZoneId sourceId in sourceIds)
        {
            if (!TryGetZone(sourceId, out InteractionZone zone))
                continue;

            zone.Started += OnZoneStarted;
            zone.Completed += OnZoneCompleted;
            _subscribedZones.Add(zone);
        }
    }

    // 존 최초 상호작용 시 OnFirstInteraction 전이 실행
    private static void OnZoneStarted(InteractionZone startedZone)
    {
        if (startedZone == null)
            return;

        ApplyTransitions(_onFirstTransitionsBySource, startedZone.ZoneId);
    }

    // 존 완료 시 OnCompleted 전이 및 구매 업그레이드 규칙 실행
    private static void OnZoneCompleted(InteractionZone completedZone)
    {
        if (completedZone == null)
            return;

        ApplyTransitions(_onCompletedTransitionsBySource, completedZone.ZoneId);
        ApplyPurchaseUpgrades(completedZone.ZoneId);
    }

    private static void ApplyTransitions(
        Dictionary<InteractionZoneId, List<ResolvedTransition>> map,
        InteractionZoneId sourceZoneId)
    {
        if (!map.TryGetValue(sourceZoneId, out List<ResolvedTransition> transitions))
            return;

        for (int i = 0; i < transitions.Count; i++)
            ApplyTransition(transitions[i]);
    }

    private static void ApplyTransition(ResolvedTransition transition)
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

    // 트리거 존 완료 시 연결된 구매 업그레이드 규칙을 대상 존에 적용 (1회만)
    private static void ApplyPurchaseUpgrades(InteractionZoneId triggerZoneId)
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
                Debug.LogWarning($"[InteractionZoneFlowBootstrap] Equip not found for id: {rule.EquipId}");
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

    // 씬의 EquipBase 컴포넌트들을 순회해 equipId에 해당하는 EquipData 반환
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

    private static bool TryGetZone(InteractionZoneId zoneId, out InteractionZone zone)
    {
        zone = null;

        if (zoneId == InteractionZoneId.None)
            return false;

        if (_zoneById.TryGetValue(zoneId, out zone))
            return true;

        Debug.LogWarning($"[InteractionZoneFlowBootstrap] Zone not found for id: {zoneId}");
        return false;
    }

    // 이벤트 구독 해제 및 모든 런타임 데이터 초기화
    private static void ClearRuntimeBindings()
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
        _onFirstTransitionsBySource.Clear();
        _onCompletedTransitionsBySource.Clear();
        _purchaseRuleIndicesByTrigger.Clear();
        _purchaseRules.Clear();
        _appliedPurchaseRuleIndices.Clear();
    }
}
