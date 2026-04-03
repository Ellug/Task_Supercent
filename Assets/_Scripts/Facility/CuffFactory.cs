using System.Collections.Generic;
using UnityEngine;

// 광석(Ore)을 소비해 쇠고랑(Cuff)을 생산하는 시설
// InputZone에 쌓인 광석을 버퍼에 적재하고, 인터벌마다 1개씩 쇠고랑으로 변환해 CollectZone에 적재
public class CuffFactory : FacilityBase
{
    [Header("Zone Binding")]
    [SerializeField] private InteractionZone _embeddedInputZone;
    [SerializeField] private InteractionZone _embeddedCollectZone;

    [Header("Ore Buffer")]
    [SerializeField] private Transform _oreStackRoot;
    [SerializeField] private ResourceData _oreResource;
    [SerializeField, Min(1)] private int _maxBufferedOre = 999;
    [SerializeField, Min(0f)] private float _oreColumnSpacing = 0.75f;
    [SerializeField, Min(0f)] private float _oreLayerSpacing = 0.35f;
    [SerializeField] private Vector3 _oreLocalOffset = new(0f, 0.2f, 0f);

    [Header("Cuff Output")]
    [SerializeField] private InteractionZone _collectZone;
    [SerializeField] private ResourceData _cuffResource;
    [SerializeField] private Transform _collectStackRoot;
    [SerializeField, Min(0.01f)] private float _produceInterval = 0.15f;
    [SerializeField, Min(1)] private int _maxCollectStored = 240;
    [SerializeField, Min(0f)] private float _collectColumnSpacing = 0.45f;
    [SerializeField, Min(0f)] private float _collectLayerSpacing = 0.2f;
    [SerializeField] private Vector3 _collectLocalOffset = new(0f, 0.1f, 0f);
    [SerializeField, Min(1)] private int _collectColumns = 1;

    private readonly List<GameObject> _oreViews = new();
    private readonly List<GameObject> _cuffViews = new();
    private float _nextProduceTime;

    protected override void Awake()
    {
        base.Awake();

        // Inspector에 InputZone이 없을 때 embedded 존으로 대체
        if (!HasInputZone && _embeddedInputZone != null)
            BindInputZone(_embeddedInputZone);

        if (_collectZone == null && _embeddedCollectZone != null)
            _collectZone = _embeddedCollectZone;

        if (_oreStackRoot == null)
            _oreStackRoot = transform;
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
        if (!base.CanConsume(resource))
            return false;

        ResourceData targetOre = ResolveOreResource();
        if (targetOre == null || resource != targetOre)
            return false;

        return ResolveOrePrefab() != null;
    }

    // 현재 버퍼에 적재 가능한 광석 수량 반환
    protected override int GetRemainingCapacity(ResourceData resource)
    {
        FacilityStackUtility.CleanupMissing(_oreViews);
        return Mathf.Max(0, _maxBufferedOre - _oreViews.Count);
    }

    // InputZone에서 광석을 받으면 뷰 스폰
    protected override void OnConsumed(ResourceData resource, int amount)
    {
        FacilityStackUtility.CleanupMissing(_oreViews);

        GameObject orePrefab = ResolveOrePrefab();
        if (orePrefab == null)
            return;

        for (int i = 0; i < amount; i++)
        {
            if (_oreViews.Count >= _maxBufferedOre)
                break;

            SpawnOreView(orePrefab, _oreViews.Count);
        }
    }

    // 인터벌마다 광석 1개 소비 → 쇠고랑 1개 생산
    private void TryProduce()
    {
        if (Time.time < _nextProduceTime)
            return;

        _nextProduceTime = Time.time + Mathf.Max(0.01f, _produceInterval);

        if (_collectZone == null || ResolveCuffPrefab() == null)
            return;

        if (_oreViews.Count <= 0)
            return;

        // CollectZone 적재 상한 도달 시 생산 중단
        if (_collectZone.StoredAmount >= Mathf.Max(1, _maxCollectStored))
            return;

        ConsumeOneOreView();
        _collectZone.AddStoredAmount(1);
        SyncCollectVisuals();
    }

    // CollectZone의 StoredAmount에 맞춰 쇠고랑 뷰 개수 및 위치 동기화
    private void SyncCollectVisuals()
    {
        if (_collectZone == null)
            return;

        FacilityStackUtility.CleanupMissing(_cuffViews);

        GameObject cuffPrefab = ResolveCuffPrefab();
        if (cuffPrefab == null)
        {
            PooledViewBridge.ReleaseAll(_cuffViews);
            return;
        }

        int targetCount = Mathf.Clamp(_collectZone.StoredAmount, 0, Mathf.Max(1, _maxCollectStored));
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
                ResolveCollectStackRoot(),
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
        Transform root = _oreStackRoot != null ? _oreStackRoot : transform;
        Vector3 position = FacilityStackUtility.GetColumnLayerWorldPosition(
            root,
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
            ResolveCollectStackRoot(),
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

    // _collectStackRoot → _collectZone → self 순으로 수집 스택 루트 결정
    private Transform ResolveCollectStackRoot()
    {
        if (_collectStackRoot != null)
            return _collectStackRoot;

        if (_collectZone != null)
            return _collectZone.transform;

        return transform;
    }

    // _oreResource → InputZone.Resource 순으로 광석 데이터 결정
    private ResourceData ResolveOreResource()
    {
        if (_oreResource != null)
            return _oreResource;

        if (InputZone != null)
            return InputZone.Resource;

        return null;
    }

    private GameObject ResolveOrePrefab()
    {
        ResourceData resource = ResolveOreResource();
        return resource != null ? resource.WorldViewPrefab : null;
    }

    // _cuffResource → _collectZone.Resource 순으로 쇠고랑 데이터 결정
    private ResourceData ResolveCuffResource()
    {
        if (_cuffResource != null)
            return _cuffResource;

        if (_collectZone != null)
            return _collectZone.Resource;

        return null;
    }

    private GameObject ResolveCuffPrefab()
    {
        ResourceData resource = ResolveCuffResource();
        return resource != null ? resource.WorldViewPrefab : null;
    }

}
