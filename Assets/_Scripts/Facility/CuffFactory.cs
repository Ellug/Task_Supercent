using System;
using UnityEngine;

// 광석(Ore)을 소비해 쇠고랑(Cuff)을 생산하는 시설
// InputZone에 쌓인 광석을 버퍼에 적재하고, 인터벌마다 1개씩 쇠고랑으로 변환해 CollectZone에 적재
public class CuffFactory : FacilityBase
{
    private const int ProduceConsumeAmountPerCycle = 1;
    private const float ProduceIntervalSeconds = 0.5f;

    [Header("Capacity")]
    [SerializeField, Min(1)] private int _submitMaxCapacity = 50;
    [SerializeField, Min(1)] private int _collectMaxCapacity = 50;

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
    [SerializeField, Min(0f)] private float _collectColumnSpacing = 0.45f;
    [SerializeField, Min(0f)] private float _collectLayerSpacing = 0.2f;
    [SerializeField] private Vector3 _collectLocalOffset = new(0f, 0.1f, 0f);
    [SerializeField, Min(1)] private int _collectColumns = 1;

    [Header("Cuff Rail")]
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private Transform _pathway;
    [SerializeField] private Transform _endpoint;
    [SerializeField, Min(0.1f)] private float _railMoveSpeed = 3f;

    [Header("Max Label")]
    [SerializeField] private GameObject _maxImagePrefab;
    [SerializeField] private Canvas _labelCanvas;
    [SerializeField] private Vector3 _submitMaxLabelOffset = new(0f, 2f, 0f);
    [SerializeField] private Vector3 _collectMaxLabelOffset = new(0f, 2f, 0f);

    private FacilityStackViewRuntime _oreInputViews;
    private FacilityZoneOutputRuntime _cuffOutputRuntime;
    private FacilityTimedProductionRuntime _productionRuntime;
    private CuffRailRuntime _railRuntime;
    private FacilityZoneCapacityRuntime _submitZoneCapacityRuntime;
    private FacilityZoneCapacityRuntime _collectZoneCapacityRuntime;
    private FacilityMaxLabelRuntime _submitMaxLabelRuntime;
    private FacilityMaxLabelRuntime _collectMaxLabelRuntime;

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

        RegisterStackBounce(_oreInputViews);
        RegisterStackBounce(cuffOutputViews);

        _cuffOutputRuntime = new FacilityZoneOutputRuntime(_collectZone, cuffOutputViews);
        _productionRuntime = new FacilityTimedProductionRuntime(ProduceIntervalSeconds);
        _submitZoneCapacityRuntime = new FacilityZoneCapacityRuntime(InputZone, _submitMaxCapacity, false);
        _collectZoneCapacityRuntime = new FacilityZoneCapacityRuntime(_collectZone, _collectMaxCapacity, false);

        if (_spawnPoint != null && _pathway != null && _endpoint != null)
        {
            _railRuntime = new CuffRailRuntime(
                this,
                _spawnPoint,
                _pathway,
                _endpoint,
                _cuffResource,
                _railMoveSpeed);
        }

        RectTransform labelParent = _labelCanvas != null ? _labelCanvas.transform as RectTransform : null;
        _submitMaxLabelRuntime = new FacilityMaxLabelRuntime(_maxImagePrefab, labelParent, _labelCanvas, InputZone.transform, _submitMaxLabelOffset);
        _collectMaxLabelRuntime = new FacilityMaxLabelRuntime(_maxImagePrefab, labelParent, _labelCanvas, _collectZone.transform, _collectMaxLabelOffset);
    }

    void LateUpdate()
    {
        _submitZoneCapacityRuntime.Tick();
        _collectZoneCapacityRuntime.Tick();

        bool submitFull = _submitZoneCapacityRuntime.IsFull;
        bool collectFull = _collectZoneCapacityRuntime.IsFull;

        _submitMaxLabelRuntime.SetVisible(submitFull);
        _collectMaxLabelRuntime.SetVisible(collectFull);

        Camera cam = Camera.main;
        _submitMaxLabelRuntime.Tick(cam);
        _collectMaxLabelRuntime.Tick(cam);

        _cuffOutputRuntime.SyncVisuals();

        // Collect가 MAX면 생산 중단
        if (collectFull)
            return;

        // 레일이 없으면 기존처럼 바로 CollectZone에 적재
        if (_railRuntime == null)
        {
            _productionRuntime.TryConvert(_oreInputViews, _cuffOutputRuntime, ProduceConsumeAmountPerCycle, 1);
            return;
        }

        // 레일이 있으면 생산 시 뷰를 레일에 올리고, endpoint 도달 시 CollectZone 적재
        if (_productionRuntime.TryConsume(_oreInputViews, ProduceConsumeAmountPerCycle))
        {
            TryPlayCuffSpawnSfx();
            _railRuntime.Launch(() => _cuffOutputRuntime.Add(ProduceConsumeAmountPerCycle));
        }
    }

    // 스폰된 뷰 오브젝트 전체 반환
    void OnDestroy()
    {
        _oreInputViews?.Dispose();
        _cuffOutputRuntime?.Dispose();
        _submitZoneCapacityRuntime?.Dispose();
        _collectZoneCapacityRuntime?.Dispose();
        _submitMaxLabelRuntime?.Dispose();
        _collectMaxLabelRuntime?.Dispose();
        _oreInputViews = null;
        _cuffOutputRuntime = null;
        _productionRuntime = null;
        _submitZoneCapacityRuntime = null;
        _collectZoneCapacityRuntime = null;
        _submitMaxLabelRuntime = null;
        _collectMaxLabelRuntime = null;
    }

    // 광석 자원만 소비 가능하며, 뷰 프리팹이 있어야 함
    protected override bool CanConsume(ResourceData resource)
    {
        return base.CanConsume(resource) && _oreInputViews.MatchesResource(resource);
    }

    // Submit Zone의 남은 용량 기준으로 소비 — Collect 상태와 무관하게 Ore는 계속 받음
    protected override int GetRemainingCapacity(ResourceData resource)
    {
        return _submitZoneCapacityRuntime != null ? _submitZoneCapacityRuntime.Remaining : int.MaxValue;
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

        if (_submitZoneCapacityRuntime == null)
            return 0;

        int added = _submitZoneCapacityRuntime.TryAdd(amount);
        if (added > 0)
            _oreInputViews.Add(added);

        return added;
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

    // 스폰 포인트가 화면 안일 때만 생산 SFX 재생
    private void TryPlayCuffSpawnSfx()
    {
        if (_spawnPoint == null)
            return;

        Vector3 spawnPosition = _spawnPoint.position;
        if (!AudioManager.IsInMainCameraView(spawnPosition))
            return;

        AudioManager.TryPlayWorldSFX(1, spawnPosition);
    }
}
