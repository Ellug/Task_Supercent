using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : Singleton<ResourceManager>
{
    [Header("Mine Setup")]
    [SerializeField] private MineArea _mineArea;
    [SerializeField] private Mine _minePrefab;
    [SerializeField] private Transform _mineRoot;

    [Header("Player Resource")]
    [SerializeField, Min(0)] private int _playerMoney;

    private readonly Dictionary<Vector2Int, Mine> _mineByCell = new();
    private readonly Dictionary<Vector2Int, Coroutine> _respawnByCell = new();

    public int PlayerMoney => _playerMoney;

    public event Action<int> MoneyChanged;
    public event Action<ResourceData, int, Vector3> ResourceYielded;

    // MineArea 기반으로 전체 셀에 광산 초기 스폰
    protected override void OnSingletonAwake()
    {
        if (_mineArea == null)
            throw new InvalidOperationException("[ResourceManager] _mineArea is required.");

        if (_minePrefab == null)
            throw new InvalidOperationException("[ResourceManager] _minePrefab is required.");

        if (_mineRoot == null)
            _mineRoot = transform;

        SpawnAllCellMines();
        NotifyMoneyChanged();
    }

    // 이벤트 구독 해제 및 리스폰 코루틴 정리
    void OnDestroy()
    {
        foreach (Mine mine in _mineByCell.Values)
        {
            if (mine != null)
                mine.Depleted -= OnMineDepleted;
        }

        _mineByCell.Clear();

        foreach (var routine in _respawnByCell.Values)
        {
            if (routine != null)
                StopCoroutine(routine);
        }

        _respawnByCell.Clear();
    }

    // 셀에 광산을 스폰 — 이미 존재하면 재활성화, 없으면 새로 생성
    public bool TrySpawnMine(Vector2Int cell)
    {
        if (_mineArea == null || _minePrefab == null)
            return false;

        if (!_mineArea.IsInside(cell))
            return false;

        Vector3 spawnPosition = _mineArea.GetCellCenterWorld(cell);
        if (_mineByCell.TryGetValue(cell, out Mine existingMine) && existingMine != null)
        {
            existingMine.transform.position = spawnPosition;
            existingMine.BindCell(cell);
            existingMine.ResetMine();
            existingMine.gameObject.SetActive(true);
            return true;
        }

        Mine mine = Instantiate(_minePrefab, spawnPosition, Quaternion.identity, _mineRoot);
        if (mine == null)
            return false;

        mine.BindCell(cell);
        mine.Depleted -= OnMineDepleted;
        mine.Depleted += OnMineDepleted;

        _mineByCell[cell] = mine;
        return true;
    }

    // origin 전방(foward) range 내 활성 광산 중 가장 가까운 1개 반환
    public bool TryGetMineInFront(Vector3 origin, Vector3 forward, float range, float minForwardDot, out Mine mine)
    {
        mine = null;

        float rangeSqr = Mathf.Max(0f, range) * Mathf.Max(0f, range);
        float bestDistanceSqr = float.MaxValue;

        Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.forward;

        flatForward.Normalize();
        float forwardDotThreshold = Mathf.Clamp(minForwardDot, -1f, 1f);

        foreach (Mine candidate in _mineByCell.Values)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
                continue;

            Vector3 toMine = candidate.transform.position - origin;
            toMine.y = 0f;

            float distanceSqr = toMine.sqrMagnitude;
            if (distanceSqr > rangeSqr)
                continue;

            if (distanceSqr > 0.0001f)
            {
                float dot = Vector3.Dot(flatForward, toMine.normalized);
                if (dot < forwardDotThreshold)
                    continue;
            }

            if (distanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            mine = candidate;
        }

        return mine != null;
    }

    // 현재 활성화된 Mine 목록을 results에 채움
    public void GetActiveMines(List<Mine> results)
    {
        if (results == null)
            return;

        results.Clear();

        foreach (Mine mine in _mineByCell.Values)
        {
            if (mine == null || !mine.gameObject.activeInHierarchy)
                continue;

            results.Add(mine);
        }
    }

    // 플레이어 소지금 증가 후 MoneyChanged 발생
    public void AddMoney(int amount)
    {
        if (amount <= 0)
            return;

        _playerMoney += amount;
        NotifyMoneyChanged();
    }

    // 잔액이 충분하면 차감 후 true 반환, 부족하면 false
    public bool TrySpendMoney(int amount)
    {
        if (amount <= 0)
            return true;

        if (_playerMoney < amount)
            return false;

        _playerMoney -= amount;
        NotifyMoneyChanged();
        return true;
    }

    // 플레이어 소지금을 지정값으로 직접 설정
    public void SetMoney(int amount)
    {
        _playerMoney = Mathf.Max(0, amount);
        NotifyMoneyChanged();
    }

    // MineArea 전체 셀에 광산 일괄 스폰
    private void SpawnAllCellMines()
    {
        foreach (Vector2Int cell in _mineArea.EnumerateAllCells())
            TrySpawnMine(cell);
    }

    // 광산 소진 시 ResourceYielded 발생, Money면 소지금 추가, 리스폰 예약
    private void OnMineDepleted(Mine mine, ResourceData yieldResource, int yieldAmount)
    {
        if (mine == null)
            return;

        Vector2Int cell = mine.GridCell;
        if (_mineArea == null || !_mineArea.IsInside(cell))
            return;

        ResourceYielded?.Invoke(yieldResource, yieldAmount, mine.transform.position);

        if (yieldResource != null && yieldResource.IsMoney)
            AddMoney(yieldAmount);

        mine.gameObject.SetActive(false);

        if (_respawnByCell.TryGetValue(cell, out Coroutine existingRoutine) && existingRoutine != null)
            StopCoroutine(existingRoutine);

        _respawnByCell[cell] = StartCoroutine(CoRespawnMine(cell));
    }

    // RespawnSeconds 대기 후 해당 셀에 광산 재스폰
    private IEnumerator CoRespawnMine(Vector2Int cell)
    {
        float delay = _mineArea != null ? _mineArea.RespawnSeconds : 0f;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        _respawnByCell.Remove(cell);
        TrySpawnMine(cell);
    }

    // MoneyChanged 이벤트에 현재 소지금 전달
    private void NotifyMoneyChanged()
    {
        MoneyChanged?.Invoke(_playerMoney);
    }
}
