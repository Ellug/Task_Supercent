using System.Collections.Generic;
using UnityEngine;

// FlowLibrary의 초기 상태/전이 규칙 실행
public sealed class ZoneFlowController
{
    // TransitionData를 런타임 적용 가능한 형태로 변환한 값 타입
    private readonly struct ResolvedTransition
    {
        public readonly InteractionZone Target;
        public readonly InteractionZoneTransitionOperation Operation;
        public readonly bool BoolValue;
        public readonly InteractionZoneType TypeValue;

        public ResolvedTransition(
            InteractionZone target,
            InteractionZoneTransitionOperation operation,
            bool boolValue,
            InteractionZoneType typeValue)
        {
            Target = target;
            Operation = operation;
            BoolValue = boolValue;
            TypeValue = typeValue;
        }
    }

    private readonly Dictionary<InteractionZoneId, List<ResolvedTransition>> _onFirstTransitionsBySource = new();
    private readonly Dictionary<InteractionZoneId, List<ResolvedTransition>> _onCompletedTransitionsBySource = new();
    private readonly List<ResolvedTransition> _onJailBecameFullTransitions = new();
    private readonly HashSet<InteractionZoneId> _appliedCompletedTransitionSourceIds = new();

    private ZoneRegistry _zoneRegistry;
    private bool _jailFullTransitionsApplied;

    public bool HasJailFullRules => _onJailBecameFullTransitions.Count > 0;

    // FlowLibrary 데이터 초기화
    public void Initialize(
        IReadOnlyList<InteractionZoneInitialStateData> initialStates,
        IReadOnlyList<InteractionZoneTransitionData> transitions,
        ZoneRegistry zoneRegistry)
    {
        _zoneRegistry = zoneRegistry;
        Clear();
        ApplyInitialStates(initialStates);
        BuildTransitions(transitions);
    }

    // OnFirstInteraction 전이 적용
    public void OnZoneStarted(InteractionZoneId sourceZoneId)
    {
        ApplyTransitions(_onFirstTransitionsBySource, sourceZoneId);
    }

    // OnCompleted 전이 1회 적용
    public void OnZoneCompleted(InteractionZoneId sourceZoneId)
    {
        if (!_appliedCompletedTransitionSourceIds.Add(sourceZoneId))
            return;

        ApplyTransitions(_onCompletedTransitionsBySource, sourceZoneId);
    }

    // 감옥 상태 평가 (닫힘이면 Full 처리)
    public void EvaluateJailState(bool isJailOpen)
    {
        if (_onJailBecameFullTransitions.Count == 0)
            return;

        if (_jailFullTransitionsApplied)
            return;

        if (isJailOpen)
            return;

        _jailFullTransitionsApplied = true;
        ApplyTransitions(_onJailBecameFullTransitions);
    }

    // 런타임 데이터 초기화
    public void Clear()
    {
        _onFirstTransitionsBySource.Clear();
        _onCompletedTransitionsBySource.Clear();
        _onJailBecameFullTransitions.Clear();
        _appliedCompletedTransitionSourceIds.Clear();
        _jailFullTransitionsApplied = false;
    }

    // InitialStates 적용
    private void ApplyInitialStates(IReadOnlyList<InteractionZoneInitialStateData> states)
    {
        if (states == null)
            return;

        for (int i = 0; i < states.Count; i++)
        {
            InteractionZoneInitialStateData state = states[i];
            if (!_zoneRegistry.TryGetZone(state.ZoneId, out InteractionZone zone))
                continue;

            if (state.ResetProgress)
            {
                if (zone.Library != null)
                    zone.ApplyLibrary(zone.Library, true);
                else
                    zone.ResetProgress();
            }

            zone.SetCompleted(false);
            zone.SetZoneEnabled(state.ZoneEnabled);
        }
    }

    // TransitionData 분류/보관
    private void BuildTransitions(IReadOnlyList<InteractionZoneTransitionData> transitions)
    {
        if (transitions == null)
            return;

        for (int i = 0; i < transitions.Count; i++)
        {
            InteractionZoneTransitionData data = transitions[i];
            if (!_zoneRegistry.TryGetZone(data.TargetZoneId, out InteractionZone targetZone))
                continue;

            ResolvedTransition transition = new(
                targetZone,
                data.Operation,
                data.BoolValue,
                data.TypeValue);

            switch (data.Trigger)
            {
                case InteractionZoneFlowTrigger.OnFirstInteraction:
                    if (_zoneRegistry.TryGetZone(data.SourceZoneId, out InteractionZone firstSourceZone))
                        AddTransition(_onFirstTransitionsBySource, firstSourceZone.ZoneId, transition);
                    break;
                case InteractionZoneFlowTrigger.OnCompleted:
                    if (_zoneRegistry.TryGetZone(data.SourceZoneId, out InteractionZone completedSourceZone))
                        AddTransition(_onCompletedTransitionsBySource, completedSourceZone.ZoneId, transition);
                    break;
                case InteractionZoneFlowTrigger.OnJailBecameFull:
                    _onJailBecameFullTransitions.Add(transition);
                    break;
            }
        }
    }

    // Dictionary key에 전이 추가
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

    // ZoneId 기반 전이 적용
    private void ApplyTransitions(
        Dictionary<InteractionZoneId, List<ResolvedTransition>> map,
        InteractionZoneId sourceZoneId)
    {
        if (!map.TryGetValue(sourceZoneId, out List<ResolvedTransition> transitions))
            return;

        ApplyTransitions(transitions);
    }

    // 전이 리스트 일괄 적용
    private void ApplyTransitions(List<ResolvedTransition> transitions)
    {
        for (int i = 0; i < transitions.Count; i++)
            ApplyTransition(transitions[i]);
    }

    // Operation에 따라 대상 Zone 상태 반영
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
        }
    }
}
