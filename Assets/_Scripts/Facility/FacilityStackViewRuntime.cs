using System;
using System.Collections.Generic;
using UnityEngine;

// 시설 스택 뷰 공통 런타임
// - 리소스별 뷰 스폰/반납
// - 컬럼/그리드 배치
public sealed class FacilityStackViewRuntime
{
    private readonly Transform _stackRoot;
    private readonly Transform _spawnRoot;
    private readonly ResourceData _resource;
    private readonly bool _useGrid;
    private readonly int _columns;
    private readonly int _rows;
    private readonly float _columnSpacing;
    private readonly float _rowSpacing;
    private readonly float _layerSpacing;
    private readonly Vector3 _localOffset;
    private readonly bool _useTopY;
    private readonly BoxCollider _areaCollider;
    private readonly Quaternion _rotation;

    private readonly List<GameObject> _views = new();
    private Action<GameObject> _onViewSpawned;

    public FacilityStackViewRuntime(
        Transform stackRoot,
        Transform spawnRoot,
        ResourceData resource,
        bool useGrid,
        int columns,
        int rows,
        float columnSpacing,
        float rowSpacing,
        float layerSpacing,
        Vector3 localOffset,
        bool useTopY,
        BoxCollider areaCollider,
        Vector3 viewEulerAngles)
    {
        _stackRoot = stackRoot;
        _spawnRoot = spawnRoot;
        _resource = resource;
        _useGrid = useGrid;
        _columns = Mathf.Max(1, columns);
        _rows = Mathf.Max(1, rows);
        _columnSpacing = Mathf.Max(0f, columnSpacing);
        _rowSpacing = Mathf.Max(0f, rowSpacing);
        _layerSpacing = Mathf.Max(0f, layerSpacing);
        _localOffset = localOffset;
        _useTopY = useTopY;
        _areaCollider = areaCollider;
        _rotation = Quaternion.Euler(viewEulerAngles);
    }

    public int Count => _views.Count;

    public bool MatchesResource(ResourceData resource)
    {
        return resource == _resource && _resource != null && _resource.WorldViewPrefab != null;
    }

    public void Add(int amount)
    {
        int addAmount = Mathf.Max(0, amount);
        if (addAmount <= 0)
            return;

        GameObject prefab = _resource.WorldViewPrefab;
        for (int i = 0; i < addAmount; i++)
            SpawnView(prefab, _views.Count);
    }

    public int Remove(int amount)
    {
        FacilityStackUtility.CleanupMissing(_views);
        int removeAmount = Mathf.Min(Mathf.Max(0, amount), _views.Count);
        for (int i = 0; i < removeAmount; i++)
        {
            int lastIndex = _views.Count - 1;
            PooledViewBridge.Release(_views[lastIndex]);
            _views.RemoveAt(lastIndex);
        }

        return removeAmount;
    }

    public void SyncToCount(int targetCount)
    {
        FacilityStackUtility.CleanupMissing(_views);

        int target = Mathf.Max(0, targetCount);
        while (_views.Count > target)
        {
            int lastIndex = _views.Count - 1;
            PooledViewBridge.Release(_views[lastIndex]);
            _views.RemoveAt(lastIndex);
        }

        GameObject prefab = _resource.WorldViewPrefab;
        while (_views.Count < target)
            SpawnView(prefab, _views.Count);

        RefreshTransforms();
    }

    public void RefreshTransforms()
    {
        FacilityStackUtility.CleanupMissing(_views);
        for (int i = 0; i < _views.Count; i++)
        {
            GameObject view = _views[i];
            if (view == null)
                continue;

            view.transform.position = GetWorldPosition(i);
            view.transform.rotation = _rotation;
        }
    }

    public void Dispose()
    {
        PooledViewBridge.ReleaseAll(_views);
        _views.Clear();
    }

    public void SetOnViewSpawned(Action<GameObject> callback)
    {
        _onViewSpawned = callback;
    }

    private void SpawnView(GameObject prefab, int index)
    {
        Vector3 position = GetWorldPosition(index);
        GameObject view = PooledViewBridge.Spawn(prefab, position, _rotation, _spawnRoot);
        _views.Add(view);
        _onViewSpawned?.Invoke(view);
    }

    private Vector3 GetWorldPosition(int index)
    {
        if (_useGrid)
        {
            if (FacilityStackUtility.TryGetAreaGridLayerWorldPosition(
                _areaCollider,
                index,
                _columns,
                _rows,
                _layerSpacing,
                _localOffset,
                out Vector3 areaPosition))
            {
                return areaPosition;
            }

            return FacilityStackUtility.GetGridLayerWorldPosition(
                _stackRoot,
                index,
                _columns,
                _rows,
                _columnSpacing,
                _rowSpacing,
                _layerSpacing,
                _localOffset);
        }

        return FacilityStackUtility.GetColumnLayerWorldPosition(
            _stackRoot,
            index,
            _columns,
            _columnSpacing,
            _layerSpacing,
            _localOffset,
            _useTopY);
    }
}
