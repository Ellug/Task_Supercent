using System;
using UnityEngine;

public class Mine : MonoBehaviour, IPoolable
{
    [Header("Mine")]
    [SerializeField] private ResourceDefinition _yieldResource;
    [SerializeField] private int _yieldAmount = 1;
    [SerializeField] private int _maxHp = 2;

    private int _currentHp;
    private bool _isDepleted;

    public Vector2Int GridCell { get; private set; }
    public int CurrentHp => _currentHp;

    public event Action<Mine, ResourceDefinition, int> Depleted;

    void Awake()
    {
        ResetMine();
    }

    // 이 광산이 속한 그리드 셀 좌표 바인딩
    public void BindCell(Vector2Int cell)
    {
        GridCell = cell;
    }

    // dmg 내구도를 깎고, 0이 되면 채굴 성공 후 Depleted 발생
    public bool TryMine(int dmg, out ResourceDefinition yieldResource, out int yieldAmount)
    {
        yieldResource = null;
        yieldAmount = 0;

        if (_isDepleted || dmg <= 0)
            return false;

        _currentHp = Mathf.Max(0, _currentHp - dmg);
        if (_currentHp > 0)
            return false;

        _isDepleted = true;
        yieldResource = _yieldResource;
        yieldAmount = Mathf.Max(1, _yieldAmount);
        Depleted?.Invoke(this, yieldResource, yieldAmount);
        return true;
    }

    // 내구도와 고갈 상태를 초기값으로 되돌림
    public void ResetMine()
    {
        _currentHp = Mathf.Max(1, _maxHp);
        _isDepleted = false;
    }

    public void OnSpawned()
    {
        ResetMine();
    }

    public void OnDespawned()
    {
    }
}
