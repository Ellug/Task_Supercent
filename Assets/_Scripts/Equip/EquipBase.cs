using System;
using System.Collections.Generic;
using UnityEngine;

public class EquipBase : MonoBehaviour
{
    [SerializeField] private Transform _equipAnchor;
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

        depleted = mine.TryMine(_mineDamage, out yieldResource, out yieldAmount);
        if (depleted)
            _presentation?.PlayMineDepleted(mine.transform.position);

        return true;
    }

    // 쿨타임·사거리 체크 후 SimultaneousMineCount 한도로 복수 광산 채굴 — 채굴 성공 수 반환
    public int TryMineMulti(IReadOnlyList<Mine> mines, List<EquipMineResult> resultBuffer = null)
    {
        if (_currentEquip == null || mines == null || mines.Count == 0)
            return 0;

        float now = Time.time;
        if (now < _nextMineTime)
            return 0;

        int mineCount = 0;
        int maxMineCount = _currentEquip.SimultaneousMineCount;

        for (int i = 0; i < mines.Count && mineCount < maxMineCount; i++)
        {
            Mine mine = mines[i];
            if (mine == null || !mine.gameObject.activeInHierarchy)
                continue;

            if (!IsInRange(mine))
                continue;

            _presentation?.PlayMineAction(mine.transform.position);

            bool depleted = mine.TryMine(_mineDamage, out ResourceData yieldResource, out int yieldAmount);
            if (depleted)
                _presentation?.PlayMineDepleted(mine.transform.position);

            resultBuffer?.Add(new EquipMineResult(mine, yieldResource, yieldAmount, depleted));
            mineCount++;
        }

        if (mineCount > 0)
            _nextMineTime = now + _currentEquip.MineInterval;

        return mineCount;
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

        _currentViewInstance = Instantiate(viewPrefab, _equipAnchor);
        _currentViewInstance.transform.localPosition = Vector3.zero;
        _currentViewInstance.transform.localRotation = Quaternion.identity;
        _currentViewInstance.transform.localScale = Vector3.one;

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

