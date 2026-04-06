using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// StageProgressManager 이벤트와 CameraDirector를 연결하는 어댑터
// 특정 Zone의 Started/Completed/JailFull 시점에 카메라 연출을 실행하고,
// 도착·복귀 각 단계에서 Zone 표시·이벤트·버블을 순서대로 처리
[DisallowMultipleComponent]
public class StageCameraDirectorTrigger : MonoBehaviour
{
    [Serializable]
    public sealed class Entry
    {
        [Tooltip("연출을 트리거할 Zone")]
        public InteractionZoneId zoneId;

        [Tooltip("OnStarted: Zone 첫 상호작용 시 / OnCompleted: Zone 완료 시 / OnJailBecameFull: 감옥이 가득 찼을 때")]
        public ZoneTriggerEvent triggerEvent;

        [Tooltip("카메라가 이동할 목적지 Transform (위치+회전 모두 사용)")]
        public Transform target;

        [Tooltip("연출 데이터 (이동·홀드·복귀 시간 및 커브)")]
        public CameraShot shot;

        [Tooltip("연출 시작 전 지연")]
        [Min(0f)] public float startDelay;

        [Tooltip("카메라 도착 후 Zone·이벤트 실행 전 추가 지연")]
        [Min(0f)] public float arrivalDelay;

        [Tooltip("도착 이벤트 실행 후 복귀 시작 전 대기")]
        [Min(0f)] public float postArrivalDelay;

        [Tooltip("연출 시작 즉시 숨길 Zone (옵션)")]
        public bool hideZoneOnStart;
        public InteractionZoneId zoneToHideOnStart;

        [Tooltip("카메라 도착 시 표시할 Zone (옵션)")]
        public bool showZoneOnArrival;
        public InteractionZoneId zoneToShowOnArrival;

        [Tooltip("카메라 도착 시 No Cell 말풍선 노출")]
        public bool showNoCellBubbleOnArrival;
        public string noCellText;

        [Tooltip("복귀 시작 직전 No Cell 말풍선 숨김")]
        public bool hideNoCellBubbleBeforeReturn;

        [Tooltip("연출 시작 직전에 실행")]
        public UnityEvent onBeforePlay;

        [Tooltip("카메라 도착 후 실행 (arrivalDelay 이후)")]
        public UnityEvent onArrival;

        [Tooltip("카메라 복귀 시작 직전에 실행")]
        public UnityEvent onBeforeReturn;

        [Tooltip("카메라 복귀 완료 후 추가 대기")]
        [Min(0f)] public float afterReturnDelay;

        [Tooltip("카메라 복귀 완료 후 실행 (afterReturnDelay 이후)")]
        public UnityEvent onAfterReturn;
    }

    public enum ZoneTriggerEvent
    {
        OnStarted = 0,       // Zone 최초 상호작용 시 (1회만)
        OnCompleted = 1,     // Zone 완료 시
        OnJailBecameFull = 2,// 감옥이 꽉 차서 빈 Cell이 없을 때 (빈 셀 생기면 리셋)
    }

    [SerializeField] private StageProgressManager _stageProgressManager;
    [SerializeField] private CameraDirector _cameraDirector;
    [SerializeField] private PrisonerManager _prisonerManager;
    [SerializeField] private List<Entry> _entries = new();

    // OnStarted 트리거는 Zone당 1회만 실행되도록 추적
    private readonly HashSet<InteractionZoneId> _firedOnStarted = new();
    // 감옥 꽉 참 트리거 중복 방지 (빈 셀 생기면 리셋)
    private bool _jailFullTriggered;
    // HideNoCellBubble 시 특정 Prisoner 대상으로 숨기기 위한 참조
    private Prisoner _noCellPrisoner;

    void OnEnable()
    {
        if (_stageProgressManager == null)
            return;

        _stageProgressManager.ZoneStarted += OnZoneStarted;
        _stageProgressManager.ZoneCompleted += OnZoneCompleted;
        _stageProgressManager.JailStateEvaluated += OnJailStateEvaluated;
    }

    void OnDisable()
    {
        if (_stageProgressManager != null)
        {
            _stageProgressManager.ZoneStarted -= OnZoneStarted;
            _stageProgressManager.ZoneCompleted -= OnZoneCompleted;
            _stageProgressManager.JailStateEvaluated -= OnJailStateEvaluated;
        }

        HideNoCellBubble();
    }

    // Zone 최초 상호작용 시 — zoneId당 1회만 Fire
    private void OnZoneStarted(InteractionZone zone)
    {
        if (zone == null)
            return;

        if (!_firedOnStarted.Add(zone.ZoneId))
            return;

        Fire(zone.ZoneId, ZoneTriggerEvent.OnStarted);
    }

    // Zone 완료 시
    private void OnZoneCompleted(InteractionZone zone)
    {
        if (zone == null)
            return;

        Fire(zone.ZoneId, ZoneTriggerEvent.OnCompleted);
    }

    // 감옥 상태 변화 시 — 빈 셀이 없을 때 1회 Fire, 빈 셀 생기면 트리거 리셋
    private void OnJailStateEvaluated(bool isJailOpen)
    {
        if (isJailOpen)
        {
            _jailFullTriggered = false;
            return;
        }

        if (_jailFullTriggered)
            return;

        _jailFullTriggered = true;
        Fire(InteractionZoneId.None, ZoneTriggerEvent.OnJailBecameFull);
    }

    // 조건에 맞는 Entry를 모두 찾아 코루틴 실행
    private void Fire(InteractionZoneId zoneId, ZoneTriggerEvent evt)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            Entry entry = _entries[i];
            if (!IsEntryMatched(entry, zoneId, evt))
                continue;

            if (entry.shot == null || entry.target == null)
                continue;

            StartCoroutine(PlayEntry(entry));
        }
    }

    // Entry의 triggerEvent·zoneId가 현재 이벤트와 일치하는지 확인
    private bool IsEntryMatched(Entry entry, InteractionZoneId zoneId, ZoneTriggerEvent evt)
    {
        if (entry == null || entry.triggerEvent != evt)
            return false;

        // OnJailBecameFull은 zoneId 무관
        if (evt == ZoneTriggerEvent.OnJailBecameFull)
            return true;

        return entry.zoneId == zoneId;
    }

    // startDelay → Zone 숨김 → onBeforePlay → CameraDirector.Play
    private IEnumerator PlayEntry(Entry entry)
    {
        if (entry == null)
            yield break;

        if (entry.startDelay > 0f)
            yield return new WaitForSeconds(entry.startDelay);

        if (entry.hideZoneOnStart && entry.zoneToHideOnStart != InteractionZoneId.None)
            SetZoneVisible(entry.zoneToHideOnStart, false);

        entry.onBeforePlay?.Invoke();

        if (_cameraDirector == null || entry.shot == null || entry.target == null)
            yield break;

        _cameraDirector.Play(entry.shot, entry.target, () => RunArrival(entry), () => RunAfterReturn(entry));
    }

    // 카메라 도착 후 실행
    // arrivalDelay → Zone 표시 → No Cell 버블 → onArrival → postArrivalDelay → 버블 숨김 → onBeforeReturn
    private IEnumerator RunArrival(Entry entry)
    {
        if (entry == null)
            yield break;

        if (entry.arrivalDelay > 0f)
            yield return new WaitForSeconds(entry.arrivalDelay);

        if (entry.showZoneOnArrival && entry.zoneToShowOnArrival != InteractionZoneId.None)
            SetZoneVisible(entry.zoneToShowOnArrival, true);

        if (entry.showNoCellBubbleOnArrival)
            ShowNoCellBubble(entry.noCellText);

        entry.onArrival?.Invoke();

        if (entry.postArrivalDelay > 0f)
            yield return new WaitForSeconds(entry.postArrivalDelay);

        if (entry.hideNoCellBubbleBeforeReturn)
            HideNoCellBubble();

        entry.onBeforeReturn?.Invoke();
    }

    // 카메라 복귀 완료 후 실행
    // afterReturnDelay → onAfterReturn
    private IEnumerator RunAfterReturn(Entry entry)
    {
        if (entry == null)
            yield break;

        if (entry.afterReturnDelay > 0f)
            yield return new WaitForSeconds(entry.afterReturnDelay);

        entry.onAfterReturn?.Invoke();
    }

    // Zone을 활성/비활성 처리 (SetZoneEnabled + SetActive 동시 적용)
    private void SetZoneVisible(InteractionZoneId zoneId, bool visible)
    {
        if (_stageProgressManager == null || zoneId == InteractionZoneId.None)
            return;

        if (!_stageProgressManager.TryGetZone(zoneId, out InteractionZone zone) || zone == null)
            return;

        zone.SetZoneEnabled(visible);
        zone.gameObject.SetActive(visible);
    }

    // 가이드 Prisoner 머리 위에 No Cell 말풍선 표시
    private void ShowNoCellBubble(string text)
    {
        if (_prisonerManager == null || PrisonerReceiveBubbleUI.Instance == null)
            return;

        if (!_prisonerManager.TryGetGuidePrisoner(out Prisoner prisoner) || prisoner == null)
            return;

        _noCellPrisoner = prisoner;
        string message = string.IsNullOrWhiteSpace(text) ? "No Cell!" : text;
        PrisonerReceiveBubbleUI.Instance.ShowMessageFor(prisoner, message);
    }

    // No Cell 말풍선 숨김 — 대상 Prisoner가 있으면 해당 대상만, 없으면 전체 숨김
    private void HideNoCellBubble()
    {
        if (PrisonerReceiveBubbleUI.Instance == null)
            return;

        if (_noCellPrisoner != null)
            PrisonerReceiveBubbleUI.Instance.HideChatFor(_noCellPrisoner);
        else
            PrisonerReceiveBubbleUI.Instance.HideChat();

        _noCellPrisoner = null;
    }
}
