using System.Collections;
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
    [SerializeField] private Transform _insidePoint;

    [Header("Objects")]
    [SerializeField] private Transform _door;
    [SerializeField] private GameObject _level2Wall;

    [Header("Capacity")]
    [SerializeField, Min(1)] private int _maxCapacity = 20;
    [SerializeField, Min(0)] private int _currentCount;
    [SerializeField, Min(1)] private int _upgradeCapacity = 40;

    [Header("Door Animation")]
    [SerializeField, Min(0.01f)] private float _doorSpeed = 4f;

    private Prisoner _entryOwner;
    private Coroutine _doorCoroutine;

    public Transform EntrancePoint => _entrancePoint != null ? _entrancePoint : transform;
    public Transform InsidePoint => _insidePoint != null ? _insidePoint : EntrancePoint;
    public int MaxCapacity => Mathf.Max(1, _maxCapacity);
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

    // 내부 도착 시 수용 확정 (count + 1)
    public bool CommitEnter(Prisoner prisoner)
    {
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

        _currentCount = Mathf.Min(MaxCapacity, _currentCount + 1);
        if (_entryOwner == prisoner)
            _entryOwner = null;

        NotifyStateChanged();
        LogState("Enter");
        return true;
    }

    public void CancelEntrance(Prisoner prisoner)
    {
        if (_entryOwner == prisoner)
            _entryOwner = null;
    }

    // 필요 시 외부에서 초기화/재설정
    public void ResetState(int currentCount = 0)
    {
        _currentCount = Mathf.Clamp(currentCount, 0, MaxCapacity);
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

    // _upgradeCapacity로 최대 수용량 업그레이드
    public bool Upgrade()
    {
        bool upgraded = SetMaxCapacity(_upgradeCapacity);
        if (upgraded && _level2Wall != null)
            _level2Wall.SetActive(false);
        return upgraded;
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke(this);
        UpdateDoor();
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
