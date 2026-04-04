using System.Collections.Generic;
using UnityEngine;

// BuyMiner 완료 시 Miner를 스폰하고 Mine 타겟 클레임을 관리
// 같은 Mine을 여러 Miner가 동시에 채굴하지 않도록 1:1로 할당
[DisallowMultipleComponent]
public class MinerManager : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private GameObject _buyMinerZoneObject;

    [Header("Spawn")]
    [SerializeField] private Miner _minerPrefab;
    [SerializeField] private Transform _npcRoot;
    [SerializeField, Min(1)] private int _spawnCount = 3;
    [SerializeField] private float _spawnZOffset = -2f;
    [SerializeField, Min(0f)] private float _spawnSpacing = 1.5f;

    [Header("Submit")]
    [SerializeField] private GameObject _cuffFactoryObject;

    private readonly List<Miner> _miners = new();
    private readonly Dictionary<Miner, Mine> _mineByMiner = new();
    private readonly Dictionary<Mine, Miner> _minerByMine = new();
    private readonly List<Mine> _activeMines = new();
    private bool _spawned;
    private InteractionZone _buyMinerZone;
    private CuffFactory _cuffFactory;

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        ResolveReferences();

        if (_buyMinerZone != null)
            _buyMinerZone.Completed += OnBuyMinerZoneCompleted;
    }

    void Start()
    {
        if (_buyMinerZone != null && _buyMinerZone.IsCompleted)
            OnBuyMinerZoneCompleted(_buyMinerZone);
    }

    void OnDisable()
    {
        if (_buyMinerZone != null)
            _buyMinerZone.Completed -= OnBuyMinerZoneCompleted;
    }

    private void ResolveReferences()
    {
        if (_buyMinerZoneObject != null)
            _buyMinerZoneObject.TryGetComponent(out _buyMinerZone);

        if (_cuffFactoryObject != null)
            _cuffFactoryObject.TryGetComponent(out _cuffFactory);

        if (_buyMinerZone == null)
            Debug.LogWarning("[MinerManager] Buy Miner Zone object is missing or has no InteractionZone.");

        if (_cuffFactory == null)
            Debug.LogWarning("[MinerManager] CuffFactory object is missing or has no CuffFactory component.");
    }

    // miner 위치 기준으로 가장 가까운 비할당 Mine을 1개 할당
    public bool TryAssignMine(Miner miner, Vector3 minerPosition, out Mine mine)
    {
        mine = null;
        if (miner == null)
            return false;

        ReleaseMine(miner);

        ResourceManager resourceManager = ResourceManager.Instance;
        if (resourceManager == null)
            return false;

        resourceManager.GetActiveMines(_activeMines);

        float bestDistanceSqr = float.MaxValue;
        for (int i = 0; i < _activeMines.Count; i++)
        {
            Mine candidate = _activeMines[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
                continue;

            if (_minerByMine.ContainsKey(candidate))
                continue;

            Vector3 toMine = candidate.transform.position - minerPosition;
            toMine.y = 0f;
            float distanceSqr = toMine.sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            mine = candidate;
        }

        if (mine == null)
            return false;

        _mineByMiner[miner] = mine;
        _minerByMine[mine] = miner;
        return true;
    }

    public void ReleaseMine(Miner miner)
    {
        if (miner == null)
            return;

        if (!_mineByMiner.TryGetValue(miner, out Mine mine))
            return;

        _mineByMiner.Remove(miner);
        if (mine != null && _minerByMine.TryGetValue(mine, out Miner owner) && owner == miner)
            _minerByMine.Remove(mine);
    }

    public void OnMinerDepletedMine(Miner miner, Mine mine, ResourceData yieldResource, int yieldAmount)
    {
        if (miner != null)
            ReleaseMine(miner);

        if (_cuffFactory == null)
            return;

        _cuffFactory.SubmitOreFromRemote(yieldResource, yieldAmount);
    }

    public void UnregisterMiner(Miner miner)
    {
        if (miner == null)
            return;

        ReleaseMine(miner);
        _miners.Remove(miner);
    }

    private void OnBuyMinerZoneCompleted(InteractionZone zone)
    {
        SpawnMiners();

        if (zone == null)
            return;

        zone.SetZoneEnabled(false);
        zone.gameObject.SetActive(false);
    }

    private void SpawnMiners()
    {
        if (_spawned)
            return;

        if (_minerPrefab == null || _buyMinerZone == null)
        {
            Debug.LogWarning("[MinerManager] Miner prefab or Buy Miner Zone is missing.");
            return;
        }

        Vector3 spawnBase = GetSpawnBasePosition();
        Quaternion spawnRotation = GetUprightRotation(_buyMinerZone.transform.rotation);
        Transform root = _npcRoot != null ? _npcRoot : transform;

        int count = Mathf.Max(1, _spawnCount);
        float center = (count - 1) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            float xOffset = (i - center) * _spawnSpacing;
            Vector3 spawnPosition = spawnBase + new Vector3(xOffset, 0f, 0f);
            spawnPosition.y = 1f;
            Miner miner = Instantiate(_minerPrefab, spawnPosition, spawnRotation, root);
            if (miner == null)
                continue;

            _miners.Add(miner);
            miner.Initialize(this);
        }

        _spawned = _miners.Count > 0;
    }

    private Vector3 GetSpawnBasePosition()
    {
        Vector3 position;
        if (_buyMinerZone != null)
            position = _buyMinerZone.transform.position + new Vector3(0f, 0f, _spawnZOffset);
        else
            position = transform.position;

        position.y = 1f;
        return position;
    }

    private static Quaternion GetUprightRotation(Quaternion source)
    {
        Vector3 euler = source.eulerAngles;
        return Quaternion.Euler(0f, euler.y, 0f);
    }
}
