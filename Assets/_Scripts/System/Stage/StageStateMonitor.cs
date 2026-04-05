using System;
using System.Collections.Generic;
using UnityEngine;

// 자원/Jail 상태 이벤트 감시 및 콜백 전달
public sealed class StageStateMonitor
{
    private readonly HashSet<ResourceData> _resourceTargets = new();

    private ResourceStack _playerCarryStack;
    private JailFacility _jailFacility;
    private Action<ResourceData> _onFirstResourceAcquired;
    private Action<bool> _onJailStateEvaluated;

    // 상태 감시 시작
    public void Bind(
        ResourceStack playerCarryStack,
        JailFacility jailFacility,
        IReadOnlyList<ResourceData> resourceTargets,
        bool monitorJailState,
        Action<ResourceData> onFirstResourceAcquired,
        Action<bool> onJailStateEvaluated)
    {
        Unbind();

        _playerCarryStack = playerCarryStack;
        _jailFacility = jailFacility;
        _onFirstResourceAcquired = onFirstResourceAcquired;
        _onJailStateEvaluated = onJailStateEvaluated;

        _resourceTargets.Clear();
        if (resourceTargets != null)
        {
            for (int i = 0; i < resourceTargets.Count; i++)
            {
                ResourceData resource = resourceTargets[i];
                if (resource != null)
                    _resourceTargets.Add(resource);
            }
        }

        BindResourceState();
        BindJailState(monitorJailState);
    }

    // 상태 감시 종료
    public void Unbind()
    {
        if (_playerCarryStack != null)
            _playerCarryStack.Changed -= OnCarryChanged;

        if (_jailFacility != null)
            _jailFacility.StateChanged -= OnJailStateChanged;

        _resourceTargets.Clear();
        _playerCarryStack = null;
        _jailFacility = null;
        _onFirstResourceAcquired = null;
        _onJailStateEvaluated = null;
    }

    // 자원 상태 구독 + 초기값 평가
    private void BindResourceState()
    {
        if (_resourceTargets.Count == 0)
            return;

        if (_playerCarryStack == null)
        {
            Debug.LogWarning("[StageStateMonitor] Player carry stack is missing for OnFirstResourceAcquired rules.");
            return;
        }

        _playerCarryStack.Changed += OnCarryChanged;
        EvaluateCarryStateAtStart();
    }

    // Jail 상태 구독 + 초기값 평가
    private void BindJailState(bool monitorJailState)
    {
        if (!monitorJailState)
            return;

        if (_jailFacility == null)
        {
            Debug.LogWarning("[StageStateMonitor] Jail facility is missing for OnJailBecameFull rules.");
            return;
        }

        _jailFacility.StateChanged += OnJailStateChanged;
        _onJailStateEvaluated?.Invoke(_jailFacility.IsOpen);
    }

    // 시작 시 이미 보유 중인 자원 평가
    private void EvaluateCarryStateAtStart()
    {
        foreach (ResourceData resource in _resourceTargets)
        {
            if (resource == null)
                continue;

            if (_playerCarryStack.GetCount(resource) <= 0)
                continue;

            _onFirstResourceAcquired?.Invoke(resource);
        }
    }

    // Carry 변경 시 자원 규칙 평가
    private void OnCarryChanged(ResourceData resource, int count, int capacity)
    {
        if (resource == null || count <= 0)
            return;

        if (!_resourceTargets.Contains(resource))
            return;

        _onFirstResourceAcquired?.Invoke(resource);
    }

    // Jail 변경 시 상태 재평가
    private void OnJailStateChanged(JailFacility jail)
    {
        if (jail == null)
            return;

        _onJailStateEvaluated?.Invoke(jail.IsOpen);
    }
}
