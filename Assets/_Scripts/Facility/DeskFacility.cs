using System;
using System.Collections.Generic;
using UnityEngine;

// Desk 시설: Submit Zone으로 들어온 Cuff를 입력 스택으로 적재
// Prisoner 지급은 Prisoner별 int 카운트로만 관리
public class DeskFacility : FacilityBase
{
    [Header("Cuff Buffer")]
    [SerializeField] private Transform _submitStackRoot;
    [SerializeField] private ResourceData _cuffResource;
    [SerializeField, Min(1)] private int _maxCuffPerPrisoner = 4;
    [SerializeField, Min(0f)] private float _submitLayerSpacing = 0.22f;
    [SerializeField] private Vector3 _submitLocalOffset = new(0f, 0.08f, 0f);

    [Header("Money Output")]
    [SerializeField] private InteractionZone _collectZone;
    [SerializeField] private Transform _collectStackRoot;
    [SerializeField] private ResourceData _moneyResource;
    [SerializeField, Min(1)] private int _rewardMoneyPerPrisoner = 10;
    [SerializeField, Min(1)] private int _collectColumns = 2;
    [SerializeField, Min(1)] private int _collectRows = 3;
    [SerializeField, Min(0f)] private float _collectColumnSpacing = 0.2f;
    [SerializeField, Min(0f)] private float _collectRowSpacing = 0.3f;
    [SerializeField, Min(0f)] private float _collectLayerSpacing = 0.13f;
    [SerializeField] private Vector3 _collectLocalOffset = new(0f, 0.08f, 0f);
    [SerializeField] private Vector3 _moneyViewEulerAngles = new(0f, 45f, 0f);

    private readonly List<GameObject> _cuffViews = new();
    private readonly List<GameObject> _moneyViews = new();
    private Prisoner _supplyTarget;
    private int _curCuff;
    private BoxCollider _collectZoneCollider;

    public int BufferedCuffCount => _cuffViews.Count;
    public int MaxCuffPerPrisoner => Mathf.Max(1, _maxCuffPerPrisoner);
    public int CurCuff => _curCuff;
    public InteractionZone CollectZone => _collectZone;

    protected override void Awake()
    {
        base.Awake();
        ValidateBindingsOrThrow();
        _collectZoneCollider = _collectZone.GetComponent<BoxCollider>();
        if (_collectZoneCollider == null)
            throw new InvalidOperationException("[DeskFacility] _collectZone requires BoxCollider.");
    }

    void LateUpdate()
    {
        SyncMoneyVisuals();
    }

    void OnDestroy()
    {
        PooledViewBridge.ReleaseAll(_cuffViews);
        PooledViewBridge.ReleaseAll(_moneyViews);
        _supplyTarget = null;
        _curCuff = 0;
        _collectZoneCollider = null;
    }

    protected override bool CanConsume(ResourceData resource)
    {
        if (!base.CanConsume(resource))
            return false;

        if (resource != _cuffResource)
            return false;

        return _cuffResource.WorldViewPrefab != null;
    }

    protected override int GetRemainingCapacity(ResourceData resource)
    {
        return int.MaxValue;
    }

    protected override void OnConsumed(ResourceData resource, int amount)
    {
        GameObject cuffPrefab = _cuffResource.WorldViewPrefab;

        for (int i = 0; i < amount; i++)
            SpawnCuffView(cuffPrefab, _cuffViews.Count);
    }

    // 지정 Prisoner에게 Desk 버퍼 Cuff를 지급
    // Prisoner 쪽은 int만 갱신하고, Desk 스택 뷰는 풀 반환으로 제거
    public bool TrySupplyCuffToPrisoner(Prisoner prisoner, int amountPerTick, out int supplied, out int currentCuff)
    {
        supplied = 0;
        currentCuff = 0;
        if (prisoner == null)
            return false;

        if (_supplyTarget != prisoner)
        {
            _supplyTarget = prisoner;
            _curCuff = 0;
        }

        int current = _curCuff;
        int max = MaxCuffPerPrisoner;
        if (current >= max)
        {
            currentCuff = current;
            return false;
        }

        int need = max - current;
        int targetAmount = Mathf.Min(Mathf.Max(1, amountPerTick), need, _cuffViews.Count);
        for (int i = 0; i < targetAmount; i++)
        {
            int lastIndex = _cuffViews.Count - 1;
            PooledViewBridge.Release(_cuffViews[lastIndex]);
            _cuffViews.RemoveAt(lastIndex);
            supplied++;
        }

        _curCuff = current + supplied;
        currentCuff = _curCuff;
        return supplied > 0;
    }

    public int GetPrisonerCuff(Prisoner prisoner)
    {
        if (prisoner == null || _supplyTarget != prisoner)
            return 0;

        return _curCuff;
    }

    public bool IsPrisonerCuffFilled(Prisoner prisoner)
    {
        return _supplyTarget == prisoner && _curCuff >= MaxCuffPerPrisoner;
    }

    public void ResetPrisonerCuff(Prisoner prisoner)
    {
        if (prisoner == null)
            return;

        _supplyTarget = prisoner;
        _curCuff = 0;
    }

    public void RemovePrisonerCuff(Prisoner prisoner)
    {
        if (prisoner == null || _supplyTarget != prisoner)
            return;

        _supplyTarget = null;
        _curCuff = 0;
    }

    public bool TryAddMoneyRewardForPrisonerPass()
    {
        int rewardAmount = Mathf.Max(1, _rewardMoneyPerPrisoner);
        _collectZone.AddStoredAmount(rewardAmount);
        SyncMoneyVisuals();
        return true;
    }

    private void SpawnCuffView(GameObject cuffPrefab, int index)
    {
        Vector3 position = GetStackWorldPosition(index);
        GameObject view = PooledViewBridge.Spawn(cuffPrefab, position, Quaternion.identity, transform, true);
        _cuffViews.Add(view);
    }

    // 1열 기준 y축 적층
    private Vector3 GetStackWorldPosition(int index)
    {
        return FacilityStackUtility.GetColumnLayerWorldPosition(
            _submitStackRoot,
            index,
            1,
            0f,
            _submitLayerSpacing,
            _submitLocalOffset,
            false);
    }

    private void SyncMoneyVisuals()
    {
        FacilityStackUtility.CleanupMissing(_moneyViews);

        GameObject moneyPrefab = _moneyResource.WorldViewPrefab;
        if (moneyPrefab == null)
        {
            PooledViewBridge.ReleaseAll(_moneyViews);
            return;
        }

        int targetCount = Mathf.Max(0, _collectZone.StoredAmount);
        while (_moneyViews.Count > targetCount)
        {
            int lastIndex = _moneyViews.Count - 1;
            PooledViewBridge.Release(_moneyViews[lastIndex]);
            _moneyViews.RemoveAt(lastIndex);
        }

        while (_moneyViews.Count < targetCount)
            SpawnMoneyView(moneyPrefab, _moneyViews.Count);

        int columns = Mathf.Max(1, _collectColumns);
        int rows = Mathf.Max(1, _collectRows);
        Quaternion moneyRotation = Quaternion.Euler(_moneyViewEulerAngles);

        for (int i = 0; i < _moneyViews.Count; i++)
        {
            GameObject view = _moneyViews[i];
            if (view == null)
                continue;

            view.transform.position = GetGridStackWorldPosition(
                _collectStackRoot,
                i,
                columns,
                rows,
                _collectColumnSpacing,
                _collectRowSpacing,
                _collectLayerSpacing,
                _collectLocalOffset);
            view.transform.rotation = moneyRotation;
        }
    }

    private void SpawnMoneyView(GameObject moneyPrefab, int index)
    {
        Quaternion moneyRotation = Quaternion.Euler(_moneyViewEulerAngles);
        Vector3 position = GetGridStackWorldPosition(
            _collectStackRoot,
            index,
            Mathf.Max(1, _collectColumns),
            Mathf.Max(1, _collectRows),
            _collectColumnSpacing,
            _collectRowSpacing,
            _collectLayerSpacing,
            _collectLocalOffset);

        GameObject view = PooledViewBridge.Spawn(moneyPrefab, position, moneyRotation, transform, true);
        if (view == null)
            return;

        _moneyViews.Add(view);
    }

    private Vector3 GetGridStackWorldPosition(
        Transform root,
        int index,
        int columns,
        int rows,
        float columnSpacing,
        float rowSpacing,
        float layerSpacing,
        Vector3 localOffset)
    {
        if (FacilityStackUtility.TryGetAreaGridLayerWorldPosition(
            _collectZoneCollider,
            index,
            columns,
            rows,
            layerSpacing,
            localOffset,
            out Vector3 areaPosition))
        {
            return areaPosition;
        }

        return FacilityStackUtility.GetGridLayerWorldPosition(
            root,
            index,
            columns,
            rows,
            columnSpacing,
            rowSpacing,
            layerSpacing,
            localOffset);
    }

    private void ValidateBindingsOrThrow()
    {
        if (InputZone == null)
            throw new InvalidOperationException("[DeskFacility] _inputZone is required.");

        if (_collectZone == null)
            throw new InvalidOperationException("[DeskFacility] _collectZone is required.");

        if (_submitStackRoot == null)
            throw new InvalidOperationException("[DeskFacility] _submitStackRoot is required.");

        if (_collectStackRoot == null)
            throw new InvalidOperationException("[DeskFacility] _collectStackRoot is required.");

        if (_cuffResource == null)
            throw new InvalidOperationException("[DeskFacility] _cuffResource is required.");

        if (_moneyResource == null)
            throw new InvalidOperationException("[DeskFacility] _moneyResource is required.");

        if (_cuffResource.WorldViewPrefab == null)
            throw new InvalidOperationException("[DeskFacility] _cuffResource.WorldViewPrefab is required.");

        if (_moneyResource.WorldViewPrefab == null)
            throw new InvalidOperationException("[DeskFacility] _moneyResource.WorldViewPrefab is required.");
    }
}
