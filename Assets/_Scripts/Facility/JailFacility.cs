using System.Collections;
using TMPro;
using UnityEngine;

// 감옥 수용 상태 관리
// - 최대 수용: _maxCapacity
// - 현재 수용: _currentCount
// - 20/20 미만 OPEN, 20/20이면 CLOSE
// - 입구->내부 이동은 한 번에 1명만 승인
[DisallowMultipleComponent]
public class JailFacility : MonoBehaviour
{
    [Header("Points")]
    [SerializeField] private Transform _entrancePoint;

    [Header("Grid Areas")]
    [SerializeField] private NpcGridArea _gridArea1;   // 레벨 1 수용 영역
    [SerializeField] private NpcGridArea _gridArea2;   // 레벨 2 수용 영역 (업그레이드 후 개방)

    [Header("Objects")]
    [SerializeField] private Transform _door;
    [SerializeField] private GameObject _level2Wall;

    [Header("Capacity")]
    [SerializeField, Min(1)] private int _maxCapacity = 20;
    [SerializeField, Min(0)] private int _currentCount;

    [Header("UI")]
    [SerializeField] private TMP_Text _capacityText;

    [Header("Door Animation")]
    [SerializeField, Min(0.01f)] private float _doorSpeed = 4f;

    private Prisoner _entryOwner;
    private Coroutine _doorCoroutine;

    // 슬롯 인덱스 추적 (Prisoner → (gridIndex 0/1, slotIndex))
    private readonly System.Collections.Generic.Dictionary<Prisoner, (int grid, int slot)> _prisonerSlots = new();
    private bool _upgraded;

    public Transform EntrancePoint => _entrancePoint != null ? _entrancePoint : transform;
    public int MaxCapacity => TotalGridCapacity > 0 ? TotalGridCapacity : Mathf.Max(1, _maxCapacity);
    private int TotalGridCapacity => (_gridArea1 != null ? _gridArea1.Capacity : 0) + (_upgraded && _gridArea2 != null ? _gridArea2.Capacity : 0);
    public int CurrentCount => _currentCount;
    public bool IsOpen => _currentCount < MaxCapacity;
    public event System.Action<JailFacility> StateChanged;

    void Awake()
    {
        _currentCount = 0;
        _entryOwner = null;
        NotifyStateChanged();
        LogState("Init");
    }

    void OnDisable()
    {
        if (_doorCoroutine != null)
        {
            StopCoroutine(_doorCoroutine);
            _doorCoroutine = null;
        }
    }

    // 입구에서 내부 이동할 1명 예약
    public bool TryAcquireEntrance(Prisoner prisoner)
    {
        if (prisoner == null)
            return false;

        if (!IsOpen)
            return false;

        if (_entryOwner != null && _entryOwner != prisoner)
            return false;

        _entryOwner = prisoner;
        return true;
    }

    // 내부 도착 시 슬롯 배정 및 수용 확정, 배정된 슬롯 위치를 slotPosition으로 반환
    public bool CommitEnter(Prisoner prisoner, out Vector3 slotPosition)
    {
        slotPosition = Vector3.zero;

        if (prisoner == null)
            return false;

        if (_entryOwner != null && _entryOwner != prisoner)
            return false;

        if (!IsOpen)
        {
            if (_entryOwner == prisoner)
                _entryOwner = null;

            LogState("Blocked");
            return false;
        }

        // 그리드 슬롯 배정 — 1영역 먼저, 가득 차면 2영역
        if (_gridArea1 != null && _gridArea1.ClaimSlot(out int slotIndex1, out Vector3 worldPos1, out _))
        {
            _prisonerSlots[prisoner] = (0, slotIndex1);
            slotPosition = worldPos1;
        }
        else if (_upgraded && _gridArea2 != null && _gridArea2.ClaimSlot(out int slotIndex2, out Vector3 worldPos2, out _))
        {
            _prisonerSlots[prisoner] = (1, slotIndex2);
            slotPosition = worldPos2;
        }
        else
        {
            slotPosition = EntrancePoint.position;
        }

        _currentCount = Mathf.Min(MaxCapacity, _currentCount + 1);
        if (_entryOwner == prisoner)
            _entryOwner = null;

        NotifyStateChanged();
        LogState("Enter");
        return true;
    }

    // 감옥에서 죄수 제거 및 슬롯 반환
    public void RemovePrisoner(Prisoner prisoner)
    {
        if (prisoner == null)
            return;

        if (_prisonerSlots.TryGetValue(prisoner, out var entry))
        {
            if (entry.grid == 0) _gridArea1?.ReleaseSlot(entry.slot);
            else _gridArea2?.ReleaseSlot(entry.slot);
            _prisonerSlots.Remove(prisoner);
        }

        _currentCount = Mathf.Max(0, _currentCount - 1);
        NotifyStateChanged();
    }

    public void CancelEntrance(Prisoner prisoner)
    {
        if (_entryOwner == prisoner)
            _entryOwner = null;
    }

    // 필요 시 외부에서 초기화/재설정
    public void ResetState(int currentCount = 0)
    {
        foreach (var kv in _prisonerSlots)
        {
            if (kv.Value.grid == 0) _gridArea1?.ReleaseSlot(kv.Value.slot);
            else _gridArea2?.ReleaseSlot(kv.Value.slot);
        }

        _prisonerSlots.Clear();
        _currentCount = 0;
        _entryOwner = null;
        NotifyStateChanged();
        LogState("Reset");
    }

    public bool SetMaxCapacity(int maxCapacity)
    {
        int nextCapacity = Mathf.Max(1, maxCapacity);
        if (_maxCapacity == nextCapacity)
            return false;

        _maxCapacity = nextCapacity;
        _currentCount = Mathf.Clamp(_currentCount, 0, MaxCapacity);
        NotifyStateChanged();
        LogState("CapacityChanged");
        return true;
    }

    // 레벨 2 영역 개방 및 벽 제거
    public bool Upgrade()
    {
        if (_upgraded)
            return false;

        _upgraded = true;
        if (_level2Wall != null)
            _level2Wall.SetActive(false);

        NotifyStateChanged();
        LogState("Upgraded");
        return true;
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke(this);
        UpdateDoor();
        UpdateCapacityText();
    }

    private void UpdateCapacityText()
    {
        if (_capacityText != null)
        {
            _capacityText.text = $"{_currentCount}/{MaxCapacity}";
            _capacityText.color = _currentCount < MaxCapacity ? Color.white : Color.red;
        }
    }

    private void UpdateDoor()
    {
        if (_door == null)
            return;

        float targetY = IsOpen ? -2f : 1f;

        if (_doorCoroutine != null)
            StopCoroutine(_doorCoroutine);

        _doorCoroutine = StartCoroutine(MoveDoor(targetY));
    }

    private IEnumerator MoveDoor(float targetY)
    {
        while (true)
        {
            Vector3 pos = _door.localPosition;
            pos.y = Mathf.MoveTowards(pos.y, targetY, _doorSpeed * Time.deltaTime);
            _door.localPosition = pos;

            if (Mathf.Approximately(pos.y, targetY))
                break;

            yield return null;
        }

        _doorCoroutine = null;
    }

    private void LogState(string reason)
    {
        Debug.Log($"[JailFacility] {reason} {_currentCount}/{MaxCapacity} ({(IsOpen ? "OPEN" : "CLOSE")})");
    }
}
