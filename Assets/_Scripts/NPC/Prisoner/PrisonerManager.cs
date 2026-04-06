using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Prisoner 이동 흐름:
// Spawn → Queue 대기 → Receive(수령) → Ent Grid 대기 → Jail 입구 → Jail Grid 슬롯 → InPrison
[DisallowMultipleComponent]
public class PrisonerManager : MonoBehaviour
{
    [System.Serializable]
    private sealed class QueueRuntime
    {
        public Prisoner Prisoner;
        public bool ReadyForSupply;

        public void ResetState() { ReadyForSupply = false; }
    }

    [Header("References")]
    [SerializeField] private Prisoner _prisonerPrefab;
    [SerializeField] private DeskFacility _deskFacility;
    [SerializeField] private JailFacility _jailFacility;
    [SerializeField] private Transform _spawnPoint;
    [FormerlySerializedAs("_queuePoint1")]
    [SerializeField] private Transform _receivePoint;        // 수령 대기 위치
    [FormerlySerializedAs("_queuePoint2")]
    [SerializeField] private Transform _queuePoint;          // Queue 대기 위치
    [SerializeField] private NpcGridArea _entGridArea;       // 수령 후 Jail 입장 전 대기 그리드
    [SerializeField] private Transform _prisonEntrancePoint; // JailFacility 없을 때 폴백
    [SerializeField] private Transform _npcRoot;

    [Header("Rule")]
    [SerializeField, Min(0.01f)] private float _supplyInterval = 0.05f;
    [SerializeField, Min(1)] private int _supplyAmountPerTick = 1;

    private QueueRuntime _receiveSlot;  // 현재 수령 중
    private QueueRuntime _queueSlot;    // Queue에서 대기 중

    // 수령 완료 후 Ent Grid에서 대기 중
    private readonly UniquePrisonerQueue _entWaitQueue = new();

    // Ent Grid 슬롯 점유 추적 (Prisoner → slotIndex)
    private readonly Dictionary<Prisoner, int> _entSlots = new();

    // Jail 입구로 이동 중 (1명 제한)
    private Prisoner _movingToJailEntrance;

    // Jail Grid 슬롯으로 이동 중
    private readonly HashSet<Prisoner> _movingToJailSlot = new();

    private float _nextSupplyTime;

    void Start()
    {
        EnsureQueueFilled();
    }

    void Update()
    {
        EnsureQueueFilled();
        TrySupplyReceivePrisoner();
        RefreshReceivePrisonerBubble();
        ProcessReceivePrisoner();
        ProcessEntWaitQueue();
    }

    void OnDestroy()
    {
        ReleaseSlot(_receiveSlot);
        ReleaseSlot(_queueSlot);

        if (_movingToJailEntrance != null)
            ReleaseManagedPrisoner(_movingToJailEntrance);

        foreach (Prisoner p in _entWaitQueue.Snapshot())
            ReleaseManagedPrisoner(p);

        foreach (Prisoner p in new List<Prisoner>(_movingToJailSlot))
            ReleaseManagedPrisoner(p);

        _receiveSlot = null;
        _queueSlot = null;
        _entWaitQueue.Clear();
        _movingToJailSlot.Clear();
        _movingToJailEntrance = null;
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

    // 수령 완료 시 Ent Grid 슬롯 배정 후 이동
    private void ProcessReceivePrisoner()
    {
        if (_receiveSlot == null || _deskFacility == null)
            return;

        if (!CanReceivePrisoner)
            return;

        if (!_deskFacility.IsPrisonerCuffFilled(_receiveSlot.Prisoner))
            return;

        Prisoner departing = _receiveSlot.Prisoner;
        if (PrisonerReceiveBubbleUI.Instance != null)
            PrisonerReceiveBubbleUI.Instance.HideFor(departing);
        _receiveSlot = null;

        _deskFacility.TryAddMoneyRewardForPrisonerPass();
        _deskFacility.RemovePrisonerCuff(departing);

        MoveToEntGrid(departing);
        EnsureQueueFilled();
    }

    // 인터벌마다 Receive 슬롯 Prisoner에게 Cuff 지급
    private void TrySupplyReceivePrisoner()
    {
        if (_receiveSlot == null || !_receiveSlot.ReadyForSupply || _deskFacility == null)
            return;

        if (!CanReceivePrisoner)
            return;

        if (_deskFacility.IsPrisonerCuffFilled(_receiveSlot.Prisoner))
            return;

        if (Time.time < _nextSupplyTime)
            return;

        _nextSupplyTime = Time.time + Mathf.Max(0.01f, _supplyInterval);
        _deskFacility.TrySupplyCuffToPrisoner(_receiveSlot.Prisoner, _supplyAmountPerTick, out _, out _);
    }

    // Ent Grid 슬롯 배정 후 이동, 도착하면 _entWaitQueue에 추가됨
    private void MoveToEntGrid(Prisoner prisoner)
    {
        Vector3 dest;
        Quaternion rot;
        if (_entGridArea != null && _entGridArea.ClaimSlot(out int slotIndex, out Vector3 worldPos, out Quaternion worldRot))
        {
            _entSlots[prisoner] = slotIndex;
            dest = worldPos;
            rot = worldRot;
        }
        else
        {
            dest = ResolvePrisonEntrancePoint().position;
            rot = Quaternion.identity;
        }

        prisoner.SetPrisonPoint(CreateTempTransform(dest, rot));
        prisoner.MoveToPrison();
    }

    // Ent 대기 큐에서 1명씩 Jail 입구로 보냄
    private void ProcessEntWaitQueue()
    {
        if (_movingToJailEntrance != null)
            return;

        if (!IsJailOpen)
            return;

        while (_entWaitQueue.Count > 0)
        {
            if (!_entWaitQueue.TryPeek(out Prisoner next))
                return;

            if (_jailFacility != null && !_jailFacility.TryAcquireEntrance(next))
                return;

            if (!_entWaitQueue.TryDequeue(out Prisoner entering))
                return;

            ReleaseEntSlot(entering);
            _movingToJailEntrance = entering;
            entering.SetPrisonPoint(ResolvePrisonEntrancePoint());
            entering.MoveToPrison();
            return;
        }
    }

    private QueueRuntime SpawnReceiveSlot()
    {
        Prisoner prisoner = SpawnPrisoner();
        QueueRuntime slot = new QueueRuntime { Prisoner = prisoner };
        ConfigureAsReceiveSlot(slot);
        return slot;
    }

    private QueueRuntime SpawnQueueSlot()
    {
        Prisoner prisoner = SpawnPrisoner();
        QueueRuntime slot = new QueueRuntime { Prisoner = prisoner };
        ConfigureAsQueueSlot(slot);
        return slot;
    }

    private Prisoner SpawnPrisoner()
    {
        Transform root = _npcRoot != null ? _npcRoot : transform;
        Vector3 position = _spawnPoint != null ? _spawnPoint.position : transform.position;
        position.y = 1f;
        Quaternion rotation = _spawnPoint != null ? _spawnPoint.rotation : transform.rotation;

        Prisoner prisoner = Instantiate(_prisonerPrefab, position, rotation, root);
        prisoner.ArrivedAtReceivePoint += OnArrivedAtReceivePoint;
        prisoner.ArrivedAtPrisonPoint += OnArrivedAtPrisonPoint;
        return prisoner;
    }

    // Queue에서 Receive로 이동
    private void ConfigureAsReceiveSlot(QueueRuntime slot)
    {
        slot.ResetState();
        slot.Prisoner.SetReceivePoint(ResolveReceivePoint());

        if (_deskFacility != null)
            _deskFacility.ResetPrisonerCuff(slot.Prisoner);

        if (PrisonerReceiveBubbleUI.Instance != null)
            PrisonerReceiveBubbleUI.Instance.HideFor(slot.Prisoner);
        slot.Prisoner.MoveToReceive();
    }

    // Queue 위치로 이동 후 대기
    private void ConfigureAsQueueSlot(QueueRuntime slot)
    {
        slot.ResetState();
        if (PrisonerReceiveBubbleUI.Instance != null)
            PrisonerReceiveBubbleUI.Instance.HideFor(slot.Prisoner);
        slot.Prisoner.SetQueuePoint(ResolveQueuePoint());
        slot.Prisoner.SetReceivePoint(ResolveReceivePoint());
        slot.Prisoner.MoveToQueue();
    }

    private void OnArrivedAtReceivePoint(Prisoner prisoner)
    {
        if (_receiveSlot == null || _receiveSlot.Prisoner != prisoner)
            return;

        _receiveSlot.ReadyForSupply = true;

        int maxCuff = _deskFacility != null ? _deskFacility.MaxCuffPerPrisoner : 1;
        int currentCuff = _deskFacility != null ? _deskFacility.GetPrisonerCuff(prisoner) : 0;
        if (PrisonerReceiveBubbleUI.Instance != null)
            PrisonerReceiveBubbleUI.Instance.ShowFor(prisoner, currentCuff, maxCuff);
    }

    private void OnArrivedAtPrisonPoint(Prisoner prisoner)
    {
        if (prisoner == null)
            return;

        // Jail 입구 도착 → CommitEnter 후 Jail Grid 슬롯으로 이동
        if (_movingToJailEntrance == prisoner)
        {
            _movingToJailEntrance = null;

            if (_jailFacility != null && _jailFacility.CommitEnter(prisoner, out Vector3 slotPos))
            {
                _movingToJailSlot.Add(prisoner);
                prisoner.SetPrisonPoint(CreateTempTransform(slotPos));
                prisoner.MoveToPrison();
            }
            else
            {
                // 슬롯 배정 실패 — Ent 대기 큐로 복귀
                _entWaitQueue.Enqueue(prisoner);
            }

            ProcessEntWaitQueue();
            return;
        }

        // Jail Grid 슬롯 도착 → InPrison
        if (_movingToJailSlot.Remove(prisoner))
        {
            prisoner.EnterInPrison();
            ProcessEntWaitQueue();
            return;
        }

        // Ent Grid 슬롯 도착 → Ent 대기 큐에 추가
        prisoner.EnterInPrison();
        _entWaitQueue.Enqueue(prisoner);
        ProcessEntWaitQueue();
    }

    private void ReleaseEntSlot(Prisoner prisoner)
    {
        if (_entSlots.TryGetValue(prisoner, out int slotIndex))
        {
            if (_entGridArea != null)
                _entGridArea.ReleaseSlot(slotIndex);
            _entSlots.Remove(prisoner);
        }
    }

    private Transform CreateTempTransform(Vector3 worldPos, Quaternion worldRot = default)
    {
        Transform root = _npcRoot != null ? _npcRoot : transform;
        GameObject go = new("SlotPoint");
        go.transform.SetParent(root);
        go.transform.SetPositionAndRotation(worldPos, worldRot == default ? Quaternion.identity : worldRot);
        return go.transform;
    }

    private void ReleaseSlot(QueueRuntime slot)
    {
        if (slot == null || slot.Prisoner == null)
            return;

        ReleaseManagedPrisoner(slot.Prisoner);
    }

    private void ReleaseManagedPrisoner(Prisoner prisoner)
    {
        if (prisoner == null)
            return;

        if (PrisonerReceiveBubbleUI.Instance != null)
            PrisonerReceiveBubbleUI.Instance.HideFor(prisoner);

        _entWaitQueue.Remove(prisoner);
        _movingToJailSlot.Remove(prisoner);
        ReleaseEntSlot(prisoner);

        if (_movingToJailEntrance == prisoner)
            _movingToJailEntrance = null;

        if (_jailFacility != null)
            _jailFacility.CancelEntrance(prisoner);

        if (_deskFacility != null)
            _deskFacility.RemovePrisonerCuff(prisoner);

        prisoner.ArrivedAtReceivePoint -= OnArrivedAtReceivePoint;
        prisoner.ArrivedAtPrisonPoint -= OnArrivedAtPrisonPoint;
    }

    // QueueSlot → ReceiveSlot 승격, 새 QueueSlot 스폰
    private void PromoteQueueToReceive()
    {
        if (_queueSlot == null)
            return;

        _receiveSlot = _queueSlot;
        _queueSlot = null;

        ConfigureAsReceiveSlot(_receiveSlot);
        _queueSlot = SpawnQueueSlot();
    }

    private void RefreshReceivePrisonerBubble()
    {
        if (_receiveSlot == null || _receiveSlot.Prisoner == null)
            return;

        if (!_receiveSlot.ReadyForSupply)
        {
            if (PrisonerReceiveBubbleUI.Instance != null)
                PrisonerReceiveBubbleUI.Instance.HideFor(_receiveSlot.Prisoner);
            return;
        }

        if (_deskFacility == null)
        {
            if (PrisonerReceiveBubbleUI.Instance != null)
                PrisonerReceiveBubbleUI.Instance.UpdateFor(_receiveSlot.Prisoner, 0, 1);
            return;
        }

        int maxCuff = _deskFacility.MaxCuffPerPrisoner;
        int currentCuff = _deskFacility.GetPrisonerCuff(_receiveSlot.Prisoner);
        if (PrisonerReceiveBubbleUI.Instance != null)
            PrisonerReceiveBubbleUI.Instance.UpdateFor(_receiveSlot.Prisoner, currentCuff, maxCuff);
    }

    private Transform ResolveReceivePoint() =>
        _receivePoint != null ? _receivePoint : transform;

    private Transform ResolveQueuePoint() =>
        _queuePoint != null ? _queuePoint : ResolveReceivePoint();

    // 감옥 입구 포인트 — JailFacility > Inspector > self 폴백
    private Transform ResolvePrisonEntrancePoint()
    {
        if (_jailFacility != null && _jailFacility.EntrancePoint != null)
            return _jailFacility.EntrancePoint;

        if (_prisonEntrancePoint != null)
            return _prisonEntrancePoint;

        return transform;
    }

    private bool IsJailOpen => _jailFacility == null || _jailFacility.IsOpen;

    // Jail이 닫혀도 entGridArea 대기 인원이 3명 이하면 수령을 계속 허용
    private bool CanReceivePrisoner => IsJailOpen || _entSlots.Count <= 3;

    // 카메라/가이드 연출용으로 표시 가능한 Prisoner 1명 반환
    public bool TryGetGuidePrisoner(out Prisoner prisoner)
    {
        if (_entWaitQueue.TryPeek(out prisoner) && prisoner != null)
            return true;

        if (_movingToJailEntrance != null)
        {
            prisoner = _movingToJailEntrance;
            return true;
        }

        if (_receiveSlot != null && _receiveSlot.Prisoner != null)
        {
            prisoner = _receiveSlot.Prisoner;
            return true;
        }

        if (_queueSlot != null && _queueSlot.Prisoner != null)
        {
            prisoner = _queueSlot.Prisoner;
            return true;
        }

        foreach (Prisoner moving in _movingToJailSlot)
        {
            if (moving == null)
                continue;

            prisoner = moving;
            return true;
        }

        prisoner = null;
        return false;
    }
}
