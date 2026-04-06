using System;
using UnityEngine;

// Desk 시설: Submit Cuff를 버퍼에 적재하고 Prisoner에게 지급
// Prisoner 통과 보상 Money는 CollectZone에 적재하고 시각화
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

    private DeskPrisonerSupplyRuntime _prisonerSupplyRuntime;
    private FacilityZoneOutputRuntime _moneyOutputRuntime;

    public int BufferedCuffCount => _prisonerSupplyRuntime != null ? _prisonerSupplyRuntime.BufferedCount : 0;
    public int MaxCuffPerPrisoner => Mathf.Max(1, _maxCuffPerPrisoner);
    public int CurCuff => _prisonerSupplyRuntime != null ? _prisonerSupplyRuntime.CurrentCuff : 0;
    public InteractionZone CollectZone => _collectZone;

    protected override void Awake()
    {
        base.Awake();
        ValidateBindingsOrThrow();

        BoxCollider collectZoneCollider = _collectZone.GetComponent<BoxCollider>();
        if (collectZoneCollider == null)
            throw new InvalidOperationException("[DeskFacility] _collectZone requires BoxCollider.");

        FacilityStackViewRuntime cuffInputViews = new(
            _submitStackRoot,
            transform,
            _cuffResource,
            false,
            1,
            1,
            0f,
            0f,
            _submitLayerSpacing,
            _submitLocalOffset,
            false,
            null,
            Vector3.zero);

        RegisterStackBounce(cuffInputViews);
        _prisonerSupplyRuntime = new DeskPrisonerSupplyRuntime(cuffInputViews, MaxCuffPerPrisoner);

        FacilityStackViewRuntime moneyOutputViews = new(
            _collectStackRoot,
            transform,
            _moneyResource,
            true,
            _collectColumns,
            _collectRows,
            _collectColumnSpacing,
            _collectRowSpacing,
            _collectLayerSpacing,
            _collectLocalOffset,
            false,
            collectZoneCollider,
            _moneyViewEulerAngles);

        RegisterStackBounce(moneyOutputViews);
        _moneyOutputRuntime = new FacilityZoneOutputRuntime(_collectZone, moneyOutputViews);
    }

    void LateUpdate()
    {
        _moneyOutputRuntime.SyncVisuals();
    }

    void OnDestroy()
    {
        _prisonerSupplyRuntime?.Dispose();
        _moneyOutputRuntime?.Dispose();
        _prisonerSupplyRuntime = null;
        _moneyOutputRuntime = null;
    }

    protected override bool CanConsume(ResourceData resource)
    {
        return base.CanConsume(resource) && _prisonerSupplyRuntime.CanConsume(resource);
    }

    protected override int GetRemainingCapacity(ResourceData resource)
    {
        return int.MaxValue;
    }

    protected override void OnConsumed(ResourceData resource, int amount)
    {
        _prisonerSupplyRuntime.AddBuffered(amount);
    }

    // 지정 Prisoner에게 Desk 버퍼 Cuff 지급
    public bool TrySupplyCuffToPrisoner(Prisoner prisoner, int amountPerTick, out int supplied, out int currentCuff)
    {
        return _prisonerSupplyRuntime.TrySupplyToPrisoner(prisoner, amountPerTick, out supplied, out currentCuff);
    }

    public int GetPrisonerCuff(Prisoner prisoner)
    {
        return _prisonerSupplyRuntime.GetPrisonerCuff(prisoner);
    }

    public bool IsPrisonerCuffFilled(Prisoner prisoner)
    {
        return _prisonerSupplyRuntime.IsPrisonerFilled(prisoner);
    }

    public void ResetPrisonerCuff(Prisoner prisoner)
    {
        _prisonerSupplyRuntime.ResetPrisonerCuff(prisoner);
    }

    public void RemovePrisonerCuff(Prisoner prisoner)
    {
        _prisonerSupplyRuntime.RemovePrisonerCuff(prisoner);
    }

    // 죄수 1명 통과 시 보상 Money 적재
    public bool TryAddMoneyRewardForPrisonerPass()
    {
        _moneyOutputRuntime.Add(_rewardMoneyPerPrisoner);
        return true;
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
