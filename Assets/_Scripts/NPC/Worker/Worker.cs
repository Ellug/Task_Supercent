using System.Collections.Generic;
using UnityEngine;

// Cuff Collect → Desk Submit을 왕복하며 자원을 운반하는 NPC
public class Worker : NPC
{
    [Header("Points")]
    [SerializeField] private Transform _waitPoint;

    [Header("Zone")]
    [SerializeField] private InteractionZone _collectZone;
    [SerializeField] private InteractionZone _submitZone;

    [Header("Carry")]
    [SerializeField, Min(1)] private int _targetCarryAmount = 10;
    [SerializeField] private Transform _carryRoot;
    [SerializeField] private Vector3 _carryLocalOffset = new(0f, 0.6f, 0.9f);
    [SerializeField, Min(0f)] private float _carryLayerSpacing = 0.18f;

    [Header("Transfer")]
    [SerializeField, Min(0.01f)] private float _collectInterval = 0.05f;
    [SerializeField, Min(1)] private int _collectAmountPerTick = 1;
    [SerializeField, Min(0.01f)] private float _submitInterval = 0.05f;
    [SerializeField, Min(1)] private int _submitAmountPerTick = 1;

    public int CarriedAmount { get; private set; }

    private WorkerWaitState _waitState;
    private WorkerMoveToCollectState _moveToCollectState;
    private WorkerCollectState _collectState;
    private WorkerMoveToSubmitState _moveToSubmitState;
    private WorkerSubmitState _submitState;
    private readonly List<GameObject> _carryViews = new();
    private float _nextCollectTime;
    private float _nextSubmitTime;
    private bool _isWorking;

    protected override void BuildStates()
    {
        _waitState = new WorkerWaitState(this);
        _moveToCollectState = new WorkerMoveToCollectState(this);
        _collectState = new WorkerCollectState(this);
        _moveToSubmitState = new WorkerMoveToSubmitState(this);
        _submitState = new WorkerSubmitState(this);
    }

    protected override void EnterInitialState()
    {
        if (_isWorking)
            ChangeState(_moveToCollectState);
        else
            ChangeState(_waitState);
    }

    public void Initialize(InteractionZone collectZone, InteractionZone submitZone)
    {
        _collectZone = collectZone;
        _submitZone = submitZone;
        _isWorking = true;
        ChangeState(_moveToCollectState);
    }

    // 대기 위치 설정
    public void SetWaitPoint(Transform point)
    {
        _waitPoint = point;
    }

    // 수집 존 변경
    public void SetCollectZone(InteractionZone zone)
    {
        _collectZone = zone;
    }

    // 제출 존 변경
    public void SetSubmitZone(InteractionZone zone)
    {
        _submitZone = zone;
    }

    // 운반 루프 재개
    public void StartWork()
    {
        _isWorking = true;
        ChangeState(_moveToCollectState);
    }

    // 운반 중단
    public void StopWork()
    {
        _isWorking = false;
        ChangeState(_waitState);
    }

    void LateUpdate()
    {
        RefreshCarryViewTransforms();
    }

    void OnDestroy()
    {
        PooledViewBridge.ReleaseAll(_carryViews);
    }

    internal bool IsWorking => _isWorking;
    internal bool IsCarryFull => CarriedAmount >= TargetCarryAmount;
    internal bool IsCarryEmpty => CarriedAmount <= 0;

    internal bool MoveToWaitPoint() => MoveToPoint(WaitPosition);
    internal bool MoveToCollectPoint() => MoveToPoint(CollectPosition);
    internal bool MoveToSubmitPoint() => MoveToPoint(SubmitPosition);

    // 수집 인터벌마다 CollectZone에서 적재량 한도까지 수집
    internal bool TryCollectTick()
    {
        if (_collectZone == null || _collectZone.Resource == null)
            return false;

        if (Time.time < _nextCollectTime)
            return false;

        if (CarriedAmount >= TargetCarryAmount)
            return false;

        int available = _collectZone.StoredAmount;
        if (available <= 0)
            return false;

        int remaining = TargetCarryAmount - CarriedAmount;
        int amount = Mathf.Min(available, remaining, Mathf.Max(1, _collectAmountPerTick));
        if (amount <= 0)
            return false;

        _nextCollectTime = Time.time + Mathf.Max(0.01f, _collectInterval);
        _collectZone.AddStoredAmount(-amount);
        AddCarry(amount);
        return true;
    }

    // 제출 인터벌마다 보유분을 SubmitZone에 전달
    internal bool TrySubmitTick()
    {
        if (_submitZone == null || _submitZone.Resource == null)
            return false;

        if (Time.time < _nextSubmitTime)
            return false;

        if (CarriedAmount <= 0)
            return false;

        int amount = Mathf.Min(CarriedAmount, Mathf.Max(1, _submitAmountPerTick));
        if (amount <= 0)
            return false;

        _nextSubmitTime = Time.time + Mathf.Max(0.01f, _submitInterval);
        _submitZone.AddStoredAmount(amount);
        RemoveCarry(amount);
        return true;
    }

    internal void EnterWait() => ChangeState(_waitState);
    internal void EnterMoveToCollect() => ChangeState(_moveToCollectState);
    internal void EnterCollect() => ChangeState(_collectState);
    internal void EnterMoveToSubmit() => ChangeState(_moveToSubmitState);
    internal void EnterSubmit() => ChangeState(_submitState);

    // 적재량 증가 및 뷰 스폰
    private void AddCarry(int amount)
    {
        int addAmount = Mathf.Max(0, amount);
        if (addAmount <= 0)
            return;

        CarriedAmount = Mathf.Min(TargetCarryAmount, CarriedAmount + addAmount);

        GameObject carryPrefab = ResolveCarryPrefab();
        if (carryPrefab == null)
            return;

        while (_carryViews.Count < CarriedAmount)
        {
            GameObject view = PooledViewBridge.Spawn(carryPrefab, transform.position, Quaternion.identity, ResolveCarryRoot());
            if (view == null)
                break;

            _carryViews.Add(view);
        }

        RefreshCarryViewTransforms();
    }

    // 적재량 감소 및 뷰 반환
    private void RemoveCarry(int amount)
    {
        int removeAmount = Mathf.Max(0, amount);
        if (removeAmount <= 0)
            return;

        CarriedAmount = Mathf.Max(0, CarriedAmount - removeAmount);

        while (_carryViews.Count > CarriedAmount)
        {
            int lastIndex = _carryViews.Count - 1;
            PooledViewBridge.Release(_carryViews[lastIndex]);
            _carryViews.RemoveAt(lastIndex);
        }

        RefreshCarryViewTransforms();
    }

    // 모든 운반 뷰 위치를 _carryRoot 기준으로 재배치
    private void RefreshCarryViewTransforms()
    {
        Transform root = ResolveCarryRoot();
        for (int i = 0; i < _carryViews.Count; i++)
        {
            GameObject view = _carryViews[i];
            if (view == null)
                continue;

            Vector3 localPosition = _carryLocalOffset + (Vector3.up * (_carryLayerSpacing * i));
            view.transform.SetParent(root, false);
            view.transform.localPosition = localPosition;
            view.transform.localRotation = Quaternion.identity;
        }
    }

    // _carryRoot 없으면 자신의 transform 반환
    private Transform ResolveCarryRoot()
    {
        if (_carryRoot != null)
            return _carryRoot;

        return transform;
    }

    // CollectZone 자원의 WorldViewPrefab 반환
    private GameObject ResolveCarryPrefab()
    {
        ResourceData resource = _collectZone != null ? _collectZone.Resource : null;
        return resource != null ? resource.WorldViewPrefab : null;
    }

    private int TargetCarryAmount => Mathf.Max(1, _targetCarryAmount);
    private Vector3 WaitPosition => _waitPoint != null ? _waitPoint.position : transform.position;
    private Vector3 CollectPosition => _collectZone != null ? _collectZone.transform.position : transform.position;
    private Vector3 SubmitPosition => _submitZone != null ? _submitZone.transform.position : transform.position;
}
