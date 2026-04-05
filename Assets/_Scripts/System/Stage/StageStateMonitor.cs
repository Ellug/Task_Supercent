using System;
using UnityEngine;

// Jail 상태 이벤트 감시 및 콜백 전달
public sealed class StageStateMonitor
{
    private JailFacility _jailFacility;
    private Action<bool> _onJailStateEvaluated;

    // 상태 감시 시작
    public void Bind(
        JailFacility jailFacility,
        bool monitorJailState,
        Action<bool> onJailStateEvaluated)
    {
        Unbind();

        _jailFacility = jailFacility;
        _onJailStateEvaluated = onJailStateEvaluated;

        BindJailState(monitorJailState);
    }

    // 상태 감시 종료
    public void Unbind()
    {
        if (_jailFacility != null)
            _jailFacility.StateChanged -= OnJailStateChanged;

        _jailFacility = null;
        _onJailStateEvaluated = null;
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

    // Jail 변경 시 상태 재평가
    private void OnJailStateChanged(JailFacility jail)
    {
        if (jail == null)
            return;

        _onJailStateEvaluated?.Invoke(jail.IsOpen);
    }
}
