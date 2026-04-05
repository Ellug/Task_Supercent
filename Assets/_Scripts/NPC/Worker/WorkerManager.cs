using System;
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
        ValidateReferencesOrThrow();
    }

    void OnEnable()
    {
        _buyWorkerZone.Completed += OnBuyWorkerZoneCompleted;
    }

    void Start()
    {
        if (_buyWorkerZone.IsCompleted)
            OnBuyWorkerZoneCompleted(_buyWorkerZone);
    }

    void OnDisable()
    {
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
        if (collectZone == null || submitZone == null)
            throw new InvalidOperationException("[WorkerManager] Route zones are required.");

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
            worker.Initialize(collectZone, submitZone);
            _workers.Add(worker);
        }

        _spawned = _workers.Count > 0;
    }

    private void ValidateReferencesOrThrow()
    {
        if (_workerPrefab == null)
            throw new InvalidOperationException("[WorkerManager] _workerPrefab is required.");

        if (_buyWorkerZoneObject == null || _buyWorkerZone == null)
            throw new InvalidOperationException("[WorkerManager] _buyWorkerZoneObject with InteractionZone is required.");

        if (_cuffFactoryObject == null || _cuffFactory == null)
            throw new InvalidOperationException("[WorkerManager] _cuffFactoryObject with CuffFactory is required.");

        if (_deskObject == null || _deskFacility == null)
            throw new InvalidOperationException("[WorkerManager] _deskObject with DeskFacility is required.");
    }

    private void ResolveRouteZones(out InteractionZone collectZone, out InteractionZone submitZone)
    {
        collectZone = _cuffFactory.CollectZone;
        submitZone = _deskFacility.BoundInputZone;
    }

    private static Quaternion GetUprightRotation(Quaternion source)
    {
        Vector3 euler = source.eulerAngles;
        return Quaternion.Euler(0f, euler.y, 0f);
    }
}
