using System;
using UnityEngine;

public class EquipBase : MonoBehaviour
{
    private const int MineHitSfxId = 3;
    private const string PickaxeIdPrefix = "pickaxe";

    [SerializeField] private Transform _equipAnchor;
    [SerializeField] private Transform _pickaxeAnchor;
    [SerializeField] private EquipLevelLibrary _levelLibrary;
    [SerializeField] private int _startLevel = 0;
    [SerializeField] private int _mineDamage = 1;

    private int _currentLevel;
    private EquipData _currentEquip;
    private float _nextMineTime;

    private GameObject _currentViewInstance;
    private EquipPresentationBase _presentation;

    public int CurrentLevel => _currentLevel;
    public EquipData CurrentEquip => _currentEquip;

    public event Action<int, EquipData> LevelChanged;
    public event Action Mined;

    public EquipData GetDataById(string id)
    {
        if (_levelLibrary == null)
            return null;

        return _levelLibrary.GetById(id);
    }

    void Awake()
    {
        if (_equipAnchor == null)
            _equipAnchor = transform;

        SetLevel(_startLevel, true);
    }

    void OnDestroy()
    {
        RemoveCurrentView();
    }

    // id로 장비를 찾아 레벨 업그레이드 시도
    public bool TryAcquireById(string id)
    {
        if (_levelLibrary == null)
            return false;

        EquipData equip = _levelLibrary.GetById(id);
        return TryAcquire(equip);
    }

    // equip의 레벨을 라이브러리에서 조회해 SetLevel 호출
    public bool TryAcquire(EquipData equip)
    {
        if (_levelLibrary == null || equip == null)
            return false;

        if (!_levelLibrary.TryGetLevel(equip, out int targetLevel))
            return false;

        return SetLevel(targetLevel, false);
    }

    // equip이 현재 장비 레벨 이하인지 확인
    public bool HasEquipOrBetter(EquipData equip)
    {
        if (_levelLibrary == null || equip == null)
            return false;

        if (!_levelLibrary.TryGetLevel(equip, out int targetLevel))
            return false;

        return _currentLevel >= targetLevel;
    }

    // 레벨 변경 후 뷰 교체 및 LevelChanged 발생 — allowSameLevel=false면 현재 이하 레벨은 무시
    public bool SetLevel(int level, bool allowSameLevel)
    {
        if (!allowSameLevel && level <= _currentLevel)
            return false;

        int nextLevel = Mathf.Max(0, level);
        EquipData nextEquip = _levelLibrary != null ? _levelLibrary.GetEquipForLevel(nextLevel) : null;

        if (_currentLevel == nextLevel && _currentEquip == nextEquip)
            return true;

        RemoveCurrentView();

        _currentLevel = nextLevel;
        _currentEquip = nextEquip;
        _nextMineTime = 0f;
        SetMiningRangeVisual(false);

        LevelChanged?.Invoke(_currentLevel, _currentEquip);
        return true;
    }

    // 쿨타임·사거리 체크 후 단일 광산 채굴
    public bool TryMine(Mine mine, out ResourceData yieldResource, out int yieldAmount, out bool depleted)
    {
        yieldResource = null;
        yieldAmount = 0;
        depleted = false;

        if (_currentEquip == null || mine == null)
            return false;

        float now = Time.time;
        if (now < _nextMineTime)
            return false;

        if (!IsInRange(mine))
            return false;

        _nextMineTime = now + _currentEquip.MineInterval;
        _presentation?.PlayMineAction(mine.transform.position);
        if (IsPickaxeEquip(_currentEquip))
        {
            AudioManager.TryPlayWorldSFX(MineHitSfxId, transform.position);
            Mined?.Invoke();
        }

        depleted = mine.TryMine(_mineDamage, out yieldResource, out yieldAmount);
        if (depleted)
            _presentation?.PlayMineDepleted(mine.transform.position);

        return true;
    }

    private static bool IsPickaxeEquip(EquipData equip)
    {
        return equip != null &&
               !string.IsNullOrEmpty(equip.Id) &&
               equip.Id.StartsWith(PickaxeIdPrefix, StringComparison.OrdinalIgnoreCase);
    }

    // mine이 현재 장비 사거리 안에 있는지 확인
    private bool IsInRange(Mine mine)
    {
        float range = _currentEquip.MineRange;
        float rangeSqr = range * range;
        return (mine.transform.position - transform.position).sqrMagnitude <= rangeSqr;
    }

    // 채굴 범위에 Mine이 있을 때 장비 프리팹 표시
    public void SetMiningRangeVisual(bool visible)
    {
        if (!visible)
        {
            if (_currentViewInstance != null && _currentViewInstance.activeSelf)
                _currentViewInstance.SetActive(false);

            return;
        }

        EnsureCurrentView();
        if (_currentViewInstance != null && !_currentViewInstance.activeSelf)
            _currentViewInstance.SetActive(true);
    }

    // 현재 레벨 장비의 뷰 인스턴스를 필요할 때 생성
    private void EnsureCurrentView()
    {
        if (_currentEquip == null || _currentViewInstance != null)
            return;

        GameObject viewPrefab = _currentEquip.PlayerViewPrefab;
        if (viewPrefab == null)
            return;

        Transform anchor = (IsPickaxeEquip(_currentEquip) && _pickaxeAnchor != null) ? _pickaxeAnchor : _equipAnchor;
        _currentViewInstance = Instantiate(viewPrefab, anchor);
        _currentViewInstance.transform.localPosition = Vector3.zero;
        _currentViewInstance.transform.localRotation = IsPickaxeEquip(_currentEquip)
            ? Quaternion.Euler(90f, 0f, 0f)
            : Quaternion.identity;

        // 부모 스케일에 관계없이 월드 스케일 1,1,1 유지
        Vector3 parentLossyScale = anchor.lossyScale;
        _currentViewInstance.transform.localScale = new Vector3(
            parentLossyScale.x == 0f ? 1f : 1f / parentLossyScale.x,
            parentLossyScale.y == 0f ? 1f : 1f / parentLossyScale.y,
            parentLossyScale.z == 0f ? 1f : 1f / parentLossyScale.z);

        _presentation = _currentViewInstance.GetComponentInChildren<EquipPresentationBase>(true);
        _presentation?.OnEquipped(_equipAnchor, _currentEquip);
        _currentViewInstance.SetActive(false);
    }

    // Presentation 해제 후 뷰 인스턴스 파괴
    private void RemoveCurrentView()
    {
        _presentation?.OnUnequipped();
        _presentation = null;

        if (_currentViewInstance != null)
            Destroy(_currentViewInstance);

        _currentViewInstance = null;
    }
}

