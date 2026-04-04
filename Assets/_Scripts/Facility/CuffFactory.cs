using System;
using System.Collections.Generic;
using UnityEngine;

// 광석(Ore)을 소비해 쇠고랑(Cuff)을 생산하는 시설
// InputZone에 쌓인 광석을 버퍼에 적재하고, 인터벌마다 1개씩 쇠고랑으로 변환해 CollectZone에 적재
public class CuffFactory : FacilityBase
{
    [Header("Ore Buffer")]
    [SerializeField] private Transform _oreStackRoot;
    [SerializeField] private ResourceData _oreResource;
    [SerializeField, Min(0f)] private float _oreColumnSpacing = 0.75f;
    [SerializeField, Min(0f)] private float _oreLayerSpacing = 0.35f;
    [SerializeField] private Vector3 _oreLocalOffset = new(0f, 0.2f, 0f);

    [Header("Cuff Output")]
    [SerializeField] private InteractionZone _collectZone;
    [SerializeField] private ResourceData _cuffResource;
    [SerializeField] private Transform _collectStackRoot;
    [SerializeField, Min(0.01f)] private float _produceInterval = 0.15f;
    [SerializeField, Min(0f)] private float _collectColumnSpacing = 0.45f;
    [SerializeField, Min(0f)] private float _collectLayerSpacing = 0.2f;
    [SerializeField] private Vector3 _collectLocalOffset = new(0f, 0.1f, 0f);
    [SerializeField, Min(1)] private int _collectColumns = 1;

    private readonly List<GameObject> _oreViews = new();
    private readonly List<GameObject> _cuffViews = new();
    private float _nextProduceTime;

    public InteractionZone CollectZone => _collectZone;

    protected override void Awake()
    {
        base.Awake();
        ValidateBindingsOrThrow();
    }

    void LateUpdate()
    {
        SyncCollectVisuals();
        TryProduce();
    }

    // 스폰된 뷰 오브젝트 전체 반환
    void OnDestroy()
    {
        PooledViewBridge.ReleaseAll(_oreViews);
        PooledViewBridge.ReleaseAll(_cuffViews);
    }

    // 광석 자원만 소비 가능하며, 뷰 프리팹이 있어야 함
    protected override bool CanConsume(ResourceData resource)
    {
        return base.CanConsume(resource) && resource == _oreResource;
    }

    // 현재 버퍼에 적재 가능한 광석 수량 반환
    protected override int GetRemainingCapacity(ResourceData resource)
    {
        FacilityStackUtility.CleanupMissing(_oreViews);
        return int.MaxValue;
    }

    // InputZone에서 광석을 받으면 뷰 스폰
    protected override void OnConsumed(ResourceData resource, int amount)
    {
        FacilityStackUtility.CleanupMissing(_oreViews);
        GameObject orePrefab = _oreResource.WorldViewPrefab;

        for (int i = 0; i < amount; i++)
            SpawnOreView(orePrefab, _oreViews.Count);
    }

    // Miner가 채굴 산출 Ore를 직접 SubmitZone으로 적재
    public int SubmitOreFromRemote(ResourceData resource, int amount)
    {
        if (amount <= 0)
            return 0;

        if (resource != null && resource != _oreResource)
            return 0;

        int submitAmount = Mathf.Max(1, amount);
        InputZone.AddStoredAmount(submitAmount);
        return submitAmount;
    }

    // 인터벌마다 광석 1개 소비 → 쇠고랑 1개 생산
    private void TryProduce()
    {
        if (Time.time < _nextProduceTime)
            return;

        _nextProduceTime = Time.time + Mathf.Max(0.01f, _produceInterval);

        if (_oreViews.Count <= 0)
            return;

        ConsumeOneOreView();
        _collectZone.AddStoredAmount(1);
        SyncCollectVisuals();
    }

    // CollectZone의 StoredAmount에 맞춰 쇠고랑 뷰 개수 및 위치 동기화
    private void SyncCollectVisuals()
    {
        FacilityStackUtility.CleanupMissing(_cuffViews);
        GameObject cuffPrefab = _cuffResource.WorldViewPrefab;

        int targetCount = Mathf.Max(0, _collectZone.StoredAmount);
        while (_cuffViews.Count > targetCount)
        {
            int lastIndex = _cuffViews.Count - 1;
            PooledViewBridge.Release(_cuffViews[lastIndex]);
            _cuffViews.RemoveAt(lastIndex);
        }

        while (_cuffViews.Count < targetCount)
            SpawnCuffView(cuffPrefab, _cuffViews.Count);

        for (int i = 0; i < _cuffViews.Count; i++)
        {
            GameObject view = _cuffViews[i];
            if (view == null)
                continue;

            view.transform.position = FacilityStackUtility.GetColumnLayerWorldPosition(
                _collectStackRoot,
                i,
                Mathf.Max(1, _collectColumns),
                _collectColumnSpacing,
                _collectLayerSpacing,
                _collectLocalOffset,
                false);

            view.transform.rotation = Quaternion.identity;
        }
    }

    // 광석 버퍼 맨 위 뷰 1개 반환
    private void ConsumeOneOreView()
    {
        FacilityStackUtility.CleanupMissing(_oreViews);
        if (_oreViews.Count <= 0)
            return;

        int lastIndex = _oreViews.Count - 1;
        PooledViewBridge.Release(_oreViews[lastIndex]);
        _oreViews.RemoveAt(lastIndex);
    }

    private void SpawnOreView(GameObject orePrefab, int index)
    {
        Vector3 position = FacilityStackUtility.GetColumnLayerWorldPosition(
            _oreStackRoot,
            index,
            2,
            _oreColumnSpacing,
            _oreLayerSpacing,
            _oreLocalOffset,
            true);
        GameObject view = PooledViewBridge.Spawn(orePrefab, position, Quaternion.identity, transform, true);
        if (view == null)
            return;

        _oreViews.Add(view);
    }

    private void SpawnCuffView(GameObject cuffPrefab, int index)
    {
        Vector3 position = FacilityStackUtility.GetColumnLayerWorldPosition(
            _collectStackRoot,
            index,
            Mathf.Max(1, _collectColumns),
            _collectColumnSpacing,
            _collectLayerSpacing,
            _collectLocalOffset,
            false);

        GameObject view = PooledViewBridge.Spawn(cuffPrefab, position, Quaternion.identity, transform, true);
        if (view == null)
            return;

        _cuffViews.Add(view);
    }

    private void ValidateResourceBindingsOrThrow()
    {
        if (_oreStackRoot == null)
            throw new InvalidOperationException("[CuffFactory] _oreStackRoot is required.");

        if (_collectStackRoot == null)
            throw new InvalidOperationException("[CuffFactory] _collectStackRoot is required.");

        if (_oreResource == null)
            throw new InvalidOperationException("[CuffFactory] _oreResource is required.");

        if (_cuffResource == null)
            throw new InvalidOperationException("[CuffFactory] _cuffResource is required.");

        if (_oreResource.WorldViewPrefab == null)
            throw new InvalidOperationException("[CuffFactory] _oreResource.WorldViewPrefab is required.");

        if (_cuffResource.WorldViewPrefab == null)
            throw new InvalidOperationException("[CuffFactory] _cuffResource.WorldViewPrefab is required.");
    }

    private void ValidateBindingsOrThrow()
    {
        if (InputZone == null)
            throw new InvalidOperationException("[CuffFactory] _inputZone is required.");

        if (_collectZone == null)
            throw new InvalidOperationException("[CuffFactory] _collectZone is required.");

        ValidateResourceBindingsOrThrow();
    }
}
