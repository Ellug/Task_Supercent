using System;
using UnityEngine;

public class Mine : MonoBehaviour, IPoolable
{
    [Header("Mine")]
    [SerializeField] private ResourceData _yieldResource;
    [SerializeField] private int _yieldAmount = 1;
    [SerializeField] private int _maxHp = 2;

    [Header("Break Debris FX")]
    [SerializeField] private GameObject _debrisPrefab;
    [SerializeField, Min(0)] private int _debrisMinCount = 7;
    [SerializeField, Min(0)] private int _debrisMaxCount = 12;
    [SerializeField, Min(0f)] private float _debrisSpawnRadius = 0.25f;
    [SerializeField] private Vector2 _debrisScaleRange = new(0.2f, 0.5f);
    [SerializeField] private Vector2 _debrisSpeedRange = new(2.5f, 5.5f);
    [SerializeField] private Vector2 _debrisLifetimeRange = new(0.35f, 0.65f);
    [SerializeField, Min(0f)] private float _debrisGravity = 14f;
    [SerializeField, Min(0f)] private float _debrisUpBias = 0.35f;

    private int _currentHp;
    private bool _isDepleted;

    public Vector2Int GridCell { get; private set; }
    public int CurrentHp => _currentHp;
    public ResourceData YieldResource => _yieldResource;

    public event Action<Mine, ResourceData, int> Depleted;

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
    public bool TryMine(int dmg, out ResourceData yieldResource, out int yieldAmount)
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
        PlayBreakDebrisFx();
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

    // 광산 파괴 시 파편 프리팹을 랜덤 개수/크기/방향으로 튕겨내는 연출
    private void PlayBreakDebrisFx()
    {
        if (_debrisPrefab == null)
            return;

        int minCount = Mathf.Min(_debrisMinCount, _debrisMaxCount);
        int maxCount = Mathf.Max(_debrisMinCount, _debrisMaxCount);
        int count = UnityEngine.Random.Range(minCount, maxCount + 1);
        if (count <= 0)
            return;

        float minScale = Mathf.Min(_debrisScaleRange.x, _debrisScaleRange.y);
        float maxScale = Mathf.Max(_debrisScaleRange.x, _debrisScaleRange.y);
        float minSpeed = Mathf.Min(_debrisSpeedRange.x, _debrisSpeedRange.y);
        float maxSpeed = Mathf.Max(_debrisSpeedRange.x, _debrisSpeedRange.y);
        float minLifetime = Mathf.Min(_debrisLifetimeRange.x, _debrisLifetimeRange.y);
        float maxLifetime = Mathf.Max(_debrisLifetimeRange.x, _debrisLifetimeRange.y);

        Vector3 origin = transform.position + (Vector3.up * 0.2f);
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnOffset = UnityEngine.Random.insideUnitSphere * Mathf.Max(0f, _debrisSpawnRadius);
            spawnOffset.y = Mathf.Abs(spawnOffset.y);
            Vector3 spawnPosition = origin + spawnOffset;

            GameObject debrisView = PooledViewBridge.Spawn(
                _debrisPrefab,
                spawnPosition,
                UnityEngine.Random.rotation,
                null,
                true);
            if (debrisView == null)
                continue;

            if (!debrisView.TryGetComponent(out MineDebrisPiece debris))
            {
                PooledViewBridge.Release(debrisView);
                continue;
            }

            Vector3 direction = UnityEngine.Random.onUnitSphere;
            direction.y = Mathf.Abs(direction.y) + Mathf.Max(0f, _debrisUpBias);
            direction.Normalize();

            float speed = UnityEngine.Random.Range(minSpeed, maxSpeed);
            float lifetime = UnityEngine.Random.Range(minLifetime, maxLifetime);
            float scale = UnityEngine.Random.Range(minScale, maxScale);
            debris.Play(direction * speed, lifetime, scale, _debrisGravity);
        }
    }
}

