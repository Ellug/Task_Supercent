using System;
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

    private FacilityStackViewRuntime _oreInputViews;
    private FacilityZoneOutputRuntime _cuffOutputRuntime;
    private FacilityTimedProductionRuntime _productionRuntime;

    public InteractionZone CollectZone => _collectZone;

    protected override void Awake()
    {
        base.Awake();
        ValidateBindingsOrThrow();

        _oreInputViews = new FacilityStackViewRuntime(
            _oreStackRoot,
            transform,
            _oreResource,
            false,
            2,
            1,
            _oreColumnSpacing,
            0f,
            _oreLayerSpacing,
            _oreLocalOffset,
            true,
            null,
            Vector3.zero);

        FacilityStackViewRuntime cuffOutputViews = new FacilityStackViewRuntime(
            _collectStackRoot,
            transform,
            _cuffResource,
            false,
            _collectColumns,
            1,
            _collectColumnSpacing,
            0f,
            _collectLayerSpacing,
            _collectLocalOffset,
            false,
            null,
            Vector3.zero);

        _cuffOutputRuntime = new FacilityZoneOutputRuntime(_collectZone, cuffOutputViews);
        _productionRuntime = new FacilityTimedProductionRuntime(_produceInterval);
    }

    void LateUpdate()
    {
        _cuffOutputRuntime.SyncVisuals();
        _productionRuntime.TryConvert(_oreInputViews, _cuffOutputRuntime, 1, 1);
    }

    // 스폰된 뷰 오브젝트 전체 반환
    void OnDestroy()
    {
        _oreInputViews?.Dispose();
        _cuffOutputRuntime?.Dispose();
        _oreInputViews = null;
        _cuffOutputRuntime = null;
        _productionRuntime = null;
    }

    // 광석 자원만 소비 가능하며, 뷰 프리팹이 있어야 함
    protected override bool CanConsume(ResourceData resource)
    {
        return base.CanConsume(resource) && _oreInputViews.MatchesResource(resource);
    }

    // 현재 버퍼에 적재 가능한 광석 수량 반환
    protected override int GetRemainingCapacity(ResourceData resource)
    {
        return int.MaxValue;
    }

    // InputZone에서 광석을 받으면 뷰 스폰
    protected override void OnConsumed(ResourceData resource, int amount)
    {
        _oreInputViews.Add(amount);
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
