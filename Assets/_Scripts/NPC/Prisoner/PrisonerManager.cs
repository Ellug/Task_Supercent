using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Prisoner 생성·공급·이동·완료를 총괄 매니저
// ReceiveSlot(수령 대기) / QueueSlot(대기열) 두 슬롯을 유지하며
// Receive 출발 시 QueueSlot을 즉시 승격하고 새 QueueSlot을 스폰
// Ent에서는 대기열을 유지하고, 내부 진입은 1명씩 처리
[DisallowMultipleComponent]
public class PrisonerManager : MonoBehaviour
{
    [Serializable]
    private sealed class QueueRuntime
    {
        public Prisoner Prisoner;
        public bool ReadyForSupply;     // 수령 지점 도착 완료 → 공급 시작 가능

        public void ResetState()
        {
            ReadyForSupply = false;
        }
    }

    [Header("References")]
    [SerializeField] private Prisoner _prisonerPrefab;
    [SerializeField] private DeskFacility _deskFacility;
    [SerializeField] private JailFacility _jailFacility;
    [SerializeField] private Transform _spawnPoint;
    [FormerlySerializedAs("_queuePoint1")]
    [SerializeField] private Transform _receivePoint;      // 수령 슬롯 대기 위치
    [FormerlySerializedAs("_queuePoint2")]
    [SerializeField] private Transform _queuePoint;        // 대기열 슬롯 대기 위치
    [SerializeField] private Transform _prisonEntrancePoint; // JailFacility 없을 때 폴백
    [SerializeField] private Transform _prisonInsidePoint;   // JailFacility 없을 때 폴백
    [SerializeField] private Transform _npcRoot;

    [Header("Rule")]
    [SerializeField, Min(0.01f)] private float _supplyInterval = 0.05f;
    [SerializeField, Min(1)] private int _supplyAmountPerTick = 1;

    private QueueRuntime _receiveSlot;
    private QueueRuntime _queueSlot;
    private readonly HashSet<Prisoner> _movingToEntrance = new();
    private readonly Queue<Prisoner> _entranceQueue = new();
    private readonly HashSet<Prisoner> _entranceQueued = new();
    private Prisoner _movingInside;
    private float _nextSupplyTime;

    void Start()
    {
        EnsureQueueFilled();
    }

    void Update()
    {
        EnsureQueueFilled();
        ProcessReceivePrisoner();
        TrySupplyReceivePrisoner();
        ProcessEntranceQueue();
    }

    void OnDestroy()
    {
        ReleaseSlot(_receiveSlot);
        ReleaseSlot(_queueSlot);

        if (_movingInside != null)
            ReleaseManagedPrisoner(_movingInside);

        if (_movingToEntrance.Count > 0)
        {
            List<Prisoner> moving = new(_movingToEntrance);
            for (int i = 0; i < moving.Count; i++)
                ReleaseManagedPrisoner(moving[i]);
        }

        if (_entranceQueue.Count > 0)
        {
            List<Prisoner> waiting = new(_entranceQueue);
            for (int i = 0; i < waiting.Count; i++)
                ReleaseManagedPrisoner(waiting[i]);
        }

        _receiveSlot = null;
        _queueSlot = null;
        _movingToEntrance.Clear();
        _entranceQueue.Clear();
        _entranceQueued.Clear();
        _movingInside = null;
    }

    // ReceiveSlot·QueueSlot이 비어있으면 스폰 또는 승격으로 채움
    private void EnsureQueueFilled()
    {
        if (_receiveSlot == null)
        {
            if (_queueSlot != null)
                PromoteQueueToReceive();
            else
                _receiveSlot = SpawnReceiveSlot();
        }

        if (_queueSlot == null)
            _queueSlot = SpawnQueueSlot();
    }

    // 쇠고랑 4개 지급 완료 시 즉시 Ent로 출발시키고
    // Receive 슬롯을 비워 다음 죄수가 들어오게 함
    private void ProcessReceivePrisoner()
    {
        if (_receiveSlot == null)
            return;

        if (_deskFacility == null)
            return;

        if (!CanDispatchReceivePrisonerToPrison())
            return;

        if (!_deskFacility.IsPrisonerCuffFilled(_receiveSlot.Prisoner))
            return;

        Prisoner departing = _receiveSlot.Prisoner;
        _receiveSlot = null;

        if (departing == null)
            return;

        _deskFacility.TryAddMoneyRewardForPrisonerPass();

        if (_deskFacility != null)
            _deskFacility.RemovePrisonerCuff(departing);

        _movingToEntrance.Add(departing);
        departing.SetPrisonPoint(ResolvePrisonEntrancePoint());
        departing.MoveToPrison();

        EnsureQueueFilled();
    }

    // 인터벌마다 Desk 버퍼에서 Receive 슬롯 Prisoner에게 Cuff 지급
    private void TrySupplyReceivePrisoner()
    {
        if (_receiveSlot == null || !_receiveSlot.ReadyForSupply || _deskFacility == null)
            return;

        if (!CanDispatchReceivePrisonerToPrison())
            return;

        if (_deskFacility.IsPrisonerCuffFilled(_receiveSlot.Prisoner))
            return;

        if (Time.time < _nextSupplyTime)
            return;

        _nextSupplyTime = Time.time + Mathf.Max(0.01f, _supplyInterval);
        _deskFacility.TrySupplyCuffToPrisoner(_receiveSlot.Prisoner, _supplyAmountPerTick, out _, out _);

        ProcessEntranceQueue();
    }

    // Receive 슬롯용 Prisoner 스폰 및 초기화
    private QueueRuntime SpawnReceiveSlot()
    {
        Prisoner prisoner = SpawnPrisoner();
        QueueRuntime slot = new QueueRuntime { Prisoner = prisoner };
        ConfigureAsReceiveSlot(slot);
        return slot;
    }

    // Queue 슬롯용 Prisoner 스폰 및 초기화
    private QueueRuntime SpawnQueueSlot()
    {
        Prisoner prisoner = SpawnPrisoner();
        QueueRuntime slot = new QueueRuntime { Prisoner = prisoner };
        ConfigureAsQueueSlot(slot);
        return slot;
    }

    // _spawnPoint 위치에 Prisoner 인스턴스 생성 후 이벤트 구독
    private Prisoner SpawnPrisoner()
    {
        Transform root = _npcRoot != null ? _npcRoot : transform;
        Vector3 position = _spawnPoint != null ? _spawnPoint.position : transform.position;
        position.y = 1f;
        Quaternion rotation = _spawnPoint != null ? _spawnPoint.rotation : transform.rotation;

        Prisoner prisoner = Instantiate(_prisonerPrefab, position, rotation, root);
        prisoner.ArrivedAtReceivePoint += OnPrisonerArrivedAtReceivePoint;
        prisoner.ArrivedAtPrisonPoint += OnPrisonerArrivedAtPrisonPoint;
        return prisoner;
    }

    // 포인트 설정 후 Desk Cuff 초기화 → 수령 지점으로 이동
    private void ConfigureAsReceiveSlot(QueueRuntime slot)
    {
        slot.ResetState();
        slot.Prisoner.SetQueuePoint(ResolveQueuePoint());
        slot.Prisoner.SetReceivePoint(ResolveReceivePoint());
        slot.Prisoner.SetPrisonPoint(ResolvePrisonEntrancePoint());

        if (_deskFacility != null)
            _deskFacility.ResetPrisonerCuff(slot.Prisoner);

        slot.Prisoner.MoveToReceive();
    }

    // 포인트 설정 후 대기열로 이동
    private void ConfigureAsQueueSlot(QueueRuntime slot)
    {
        slot.ResetState();
        slot.Prisoner.SetQueuePoint(ResolveQueuePoint());
        slot.Prisoner.SetReceivePoint(ResolveReceivePoint());
        slot.Prisoner.SetPrisonPoint(ResolvePrisonEntrancePoint());
        slot.Prisoner.MoveToQueue();
    }

    private void OnPrisonerArrivedAtReceivePoint(Prisoner prisoner)
    {
        if (_receiveSlot == null || _receiveSlot.Prisoner != prisoner)
            return;

        _receiveSlot.ReadyForSupply = true;
    }

    // 감옥 입구/내부 도착 이벤트 처리
    private void OnPrisonerArrivedAtPrisonPoint(Prisoner prisoner)
    {
        if (prisoner == null)
            return;

        if (_movingInside == prisoner)
        {
            if (_jailFacility != null && !_jailFacility.CommitEnter(prisoner))
            {
                _movingInside = null;
                _movingToEntrance.Add(prisoner);
                prisoner.SetPrisonPoint(ResolvePrisonEntrancePoint());
                prisoner.MoveToPrison();
                return;
            }

            _movingInside = null;
            prisoner.EnterInPrison();
            ReleaseManagedPrisoner(prisoner);
            ProcessEntranceQueue();
            return;
        }

        if (_movingToEntrance.Remove(prisoner))
        {
            prisoner.EnterInPrison();
            EnqueueEntrance(prisoner);
            ProcessEntranceQueue();
        }
    }

    // Ent 대기열에서 Jail 내부로 1명 이동
    private void ProcessEntranceQueue()
    {
        if (_movingInside != null)
            return;

        if (!IsJailOpen)
            return;

        while (_entranceQueue.Count > 0)
        {
            Prisoner next = _entranceQueue.Peek();
            if (next == null || !_entranceQueued.Contains(next))
            {
                _entranceQueue.Dequeue();
                continue;
            }

            if (_jailFacility != null && !_jailFacility.TryAcquireEntrance(next))
                return;

            _entranceQueue.Dequeue();
            _entranceQueued.Remove(next);
            _movingInside = next;
            next.SetPrisonPoint(ResolvePrisonInsidePoint());
            next.MoveToPrison();
            return;
        }
    }

    private void ReleaseSlot(QueueRuntime slot)
    {
        if (slot == null || slot.Prisoner == null)
            return;

        ReleaseManagedPrisoner(slot.Prisoner);
    }

    // 이벤트 구독 해제·추적 컬렉션 정리·감옥 예약 취소
    private void ReleaseManagedPrisoner(Prisoner prisoner)
    {
        if (prisoner == null)
            return;

        _movingToEntrance.Remove(prisoner);
        _entranceQueued.Remove(prisoner);

        if (_movingInside == prisoner)
            _movingInside = null;

        if (_jailFacility != null)
            _jailFacility.CancelEntrance(prisoner);

        if (_deskFacility != null)
            _deskFacility.RemovePrisonerCuff(prisoner);

        prisoner.ArrivedAtReceivePoint -= OnPrisonerArrivedAtReceivePoint;
        prisoner.ArrivedAtPrisonPoint -= OnPrisonerArrivedAtPrisonPoint;
    }

    // 중복 없이 입구 대기열에 추가
    private void EnqueueEntrance(Prisoner prisoner)
    {
        if (prisoner == null)
            return;

        if (_entranceQueued.Add(prisoner))
            _entranceQueue.Enqueue(prisoner);
    }

    // QueueSlot을 ReceiveSlot으로 승격하고 새 QueueSlot 스폰
    private void PromoteQueueToReceive()
    {
        if (_queueSlot == null)
            return;

        _receiveSlot = _queueSlot;
        _queueSlot = null;

        ConfigureAsReceiveSlot(_receiveSlot);
        _queueSlot = SpawnQueueSlot();
    }

    // 감옥이 닫혔거나, 이미 감옥으로 향하는 인원이 남은 수용량을 모두 점유한 경우
    // Receive 단계의 공급/출발을 멈춘다.
    private bool CanDispatchReceivePrisonerToPrison()
    {
        if (_jailFacility == null)
            return true;

        int reservedCount = _movingToEntrance.Count + _entranceQueue.Count + (_movingInside != null ? 1 : 0);
        return (_jailFacility.CurrentCount + reservedCount) < _jailFacility.MaxCapacity;
    }

    // 수령 포인트 — _receivePoint > _queuePoint > self 순 폴백
    private Transform ResolveReceivePoint()
    {
        if (_receivePoint != null)
            return _receivePoint;

        if (_queuePoint != null)
            return _queuePoint;

        return transform;
    }

    // 대기열 포인트 — _queuePoint > ResolveReceivePoint 폴백
    private Transform ResolveQueuePoint()
    {
        if (_queuePoint != null)
            return _queuePoint;

        return ResolveReceivePoint();
    }

    // 감옥 입구 포인트 — JailFacility > Inspector > self 폴백
    private Transform ResolvePrisonEntrancePoint()
    {
        if (_jailFacility != null && _jailFacility.EntrancePoint != null)
            return _jailFacility.EntrancePoint;

        if (_prisonEntrancePoint != null)
            return _prisonEntrancePoint;

        return transform;
    }

    // 감옥 내부 포인트 — JailFacility > Inspector > EntrancePoint 폴백
    private Transform ResolvePrisonInsidePoint()
    {
        if (_jailFacility != null && _jailFacility.InsidePoint != null)
            return _jailFacility.InsidePoint;

        if (_prisonInsidePoint != null)
            return _prisonInsidePoint;

        return ResolvePrisonEntrancePoint();
    }

    private bool IsJailOpen => _jailFacility == null || _jailFacility.IsOpen;
}
