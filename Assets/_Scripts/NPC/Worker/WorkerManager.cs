using System.Collections.Generic;
using UnityEngine;

// BuyWorker 완료 시 Worker를 스폰해 Cuff Collect -> Desk Submit 운반 루프를 시작
[DisallowMultipleComponent]
public class WorkerManager : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private GameObject _buyWorkerZoneObject;

    [Header("Facility")]
    [SerializeField] private GameObject _cuffFactoryObject;
    [SerializeField] private GameObject _deskObject;

    [Header("Spawn")]
    [SerializeField] private Worker _workerPrefab;
    [SerializeField] private Transform _npcRoot;
    [SerializeField, Min(1)] private int _spawnCount = 1;
    [SerializeField, Min(0f)] private float _spawnSpacing = 1.5f;

    private readonly List<Worker> _workers = new();
    private bool _spawned;
    private InteractionZone _buyWorkerZone;
    private CuffFactory _cuffFactory;
    private DeskFacility _deskFacility;

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        ResolveReferences();

        if (_buyWorkerZone != null)
            _buyWorkerZone.Completed += OnBuyWorkerZoneCompleted;
    }

    void Start()
    {
        if (_buyWorkerZone != null && _buyWorkerZone.IsCompleted)
            OnBuyWorkerZoneCompleted(_buyWorkerZone);
    }

    void OnDisable()
    {
        if (_buyWorkerZone != null)
            _buyWorkerZone.Completed -= OnBuyWorkerZoneCompleted;
    }

    private void ResolveReferences()
    {
        if (_buyWorkerZoneObject != null)
            _buyWorkerZoneObject.TryGetComponent(out _buyWorkerZone);

        if (_cuffFactoryObject != null)
            _cuffFactoryObject.TryGetComponent(out _cuffFactory);

        if (_deskObject != null)
            _deskObject.TryGetComponent(out _deskFacility);

        if (_buyWorkerZone == null)
            Debug.LogWarning("[WorkerManager] Buy Worker Zone object is missing or has no InteractionZone.");

        if (_cuffFactory == null)
            Debug.LogWarning("[WorkerManager] CuffFactory object is missing or has no CuffFactory component.");

        if (_deskFacility == null)
            Debug.LogWarning("[WorkerManager] Desk object is missing or has no DeskFacility component.");
    }

    private void OnBuyWorkerZoneCompleted(InteractionZone zone)
    {
        SpawnWorkers();

        if (zone == null)
            return;

        zone.SetZoneEnabled(false);
        zone.gameObject.SetActive(false);
    }

    private void SpawnWorkers()
    {
        if (_spawned)
            return;

        ResolveRouteZones(out InteractionZone collectZone, out InteractionZone submitZone);

        if (_workerPrefab == null || _buyWorkerZone == null || collectZone == null || submitZone == null)
        {
            Debug.LogWarning("[WorkerManager] Worker prefab or route references are missing.");
            return;
        }

        Transform root = _npcRoot != null ? _npcRoot : transform;
        Vector3 basePosition = _buyWorkerZone.transform.position;
        basePosition.y = 1f;
        Quaternion rotation = GetUprightRotation(_buyWorkerZone.transform.rotation);

        int count = Mathf.Max(1, _spawnCount);
        float center = (count - 1) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            float xOffset = (i - center) * _spawnSpacing;
            Vector3 spawnPosition = basePosition + new Vector3(xOffset, 0f, 0f);
            spawnPosition.y = 1f;
            Worker worker = Instantiate(_workerPrefab, spawnPosition, rotation, root);
            if (worker == null)
                continue;

            worker.Initialize(collectZone, submitZone);
            _workers.Add(worker);
        }

        _spawned = _workers.Count > 0;
    }

    private void ResolveRouteZones(out InteractionZone collectZone, out InteractionZone submitZone)
    {
        collectZone = _cuffFactory != null ? _cuffFactory.CollectZone : null;
        submitZone = _deskFacility != null ? _deskFacility.BoundInputZone : null;
    }

    private static Quaternion GetUprightRotation(Quaternion source)
    {
        Vector3 euler = source.eulerAngles;
        return Quaternion.Euler(0f, euler.y, 0f);
    }
}
