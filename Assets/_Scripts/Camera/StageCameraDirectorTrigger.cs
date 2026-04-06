using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// StageProgressManager 이벤트와 CameraDirector를 연결하는 어댑터
// 특정 Zone의 Started/Completed 시점에 카메라 연출을 실행
[DisallowMultipleComponent]
public class StageCameraDirectorTrigger : MonoBehaviour
{
    [Serializable]
    public sealed class Entry
    {
        [Tooltip("연출을 트리거할 Zone")]
        public InteractionZoneId zoneId;

        [Tooltip("OnStarted: Zone 첫 상호작용 시 / OnCompleted: Zone 완료 시")]
        public ZoneTriggerEvent triggerEvent;

        [Tooltip("카메라가 이동할 목적지 Transform (위치+회전 모두 사용)")]
        public Transform target;

        [Tooltip("연출 데이터")]
        public CameraShot shot;

        [Tooltip("연출 시작 전 지연")]
        [Min(0f)] public float startDelay;

        [Tooltip("카메라 도착 후 이벤트 실행 전 지연")]
        [Min(0f)] public float arrivalDelay;

        [Tooltip("이벤트 실행 후 복귀 전 대기")]
        [Min(0f)] public float postArrivalDelay;

        [Tooltip("연출 시작 즉시 숨길 Zone (옵션)")]
        public bool hideZoneOnStart;
        public InteractionZoneId zoneToHideOnStart;

        [Tooltip("도착 이벤트 시 표시할 Zone (옵션)")]
        public bool showZoneOnArrival;
        public InteractionZoneId zoneToShowOnArrival;

        [Tooltip("도착 이벤트 시 No Cell 버블 노출")]
        public bool showNoCellBubbleOnArrival;
        public string noCellText;

        [Tooltip("복귀 직전 No Cell 버블 숨김")]
        public bool hideNoCellBubbleBeforeReturn;

        [Tooltip("연출 시작 직전에 실행")]
        public UnityEvent onBeforePlay;

        [Tooltip("카메라 도착 후 실행")]
        public UnityEvent onArrival;

        [Tooltip("복귀 직전에 실행")]
        public UnityEvent onBeforeReturn;
    }

    public enum ZoneTriggerEvent
    {
        OnStarted = 0,
        OnCompleted = 1,
        OnJailBecameFull = 2,
    }

    [SerializeField] private StageProgressManager _stageProgressManager;
    [SerializeField] private CameraDirector _cameraDirector;
    [SerializeField] private PrisonerManager _prisonerManager;
    [SerializeField] private List<Entry> _entries = new();

    // OnStarted 트리거는 1회만 실행되도록 추적
    private readonly HashSet<InteractionZoneId> _firedOnStarted = new();
    private bool _jailFullTriggered;
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

    private void OnZoneStarted(InteractionZone zone)
    {
        if (zone == null)
            return;

        // OnStarted는 최초 1회만
        if (!_firedOnStarted.Add(zone.ZoneId))
            return;

        Fire(zone.ZoneId, ZoneTriggerEvent.OnStarted);
    }

    private void OnZoneCompleted(InteractionZone zone)
    {
        if (zone == null)
            return;

        Fire(zone.ZoneId, ZoneTriggerEvent.OnCompleted);
    }

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

    private bool IsEntryMatched(Entry entry, InteractionZoneId zoneId, ZoneTriggerEvent evt)
    {
        if (entry == null || entry.triggerEvent != evt)
            return false;

        if (evt == ZoneTriggerEvent.OnJailBecameFull)
            return true;

        return entry.zoneId == zoneId;
    }

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

        _cameraDirector.Play(entry.shot, entry.target, () => RunArrival(entry));
    }

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

    private void SetZoneVisible(InteractionZoneId zoneId, bool visible)
    {
        if (_stageProgressManager == null || zoneId == InteractionZoneId.None)
            return;

        if (!_stageProgressManager.TryGetZone(zoneId, out InteractionZone zone) || zone == null)
            return;

        zone.SetZoneEnabled(visible);
        zone.gameObject.SetActive(visible);
    }

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
