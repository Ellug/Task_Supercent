using System.Collections.Generic;
using UnityEngine;

// Desk 시설: Submit Zone으로 들어온 Cuff를 입력 스택으로 적재
// NPC 처리/돈 생산은 다음 단계에서 연결
public class DeskFacility : FacilityBase
{
    [Header("Zone Binding")]
    [SerializeField] private InteractionZone _embeddedInputZone;
    [SerializeField] private InteractionZone _embeddedCollectZone;

    [Header("Cuff Buffer")]
    [SerializeField] private Transform _submitStackRoot;
    [SerializeField] private ResourceData _cuffResource;
    [SerializeField, Min(1)] private int _maxBufferedCuff = 240;
    [SerializeField, Min(0f)] private float _submitLayerSpacing = 0.22f;
    [SerializeField] private Vector3 _submitLocalOffset = new(0f, 0.08f, 0f);

    [Header("Common")]
    [SerializeField] private bool _disableSpawnedColliders = true;

    private readonly List<GameObject> _cuffViews = new();

    public int BufferedCuffCount => _cuffViews.Count;
    public InteractionZone CollectZone => _embeddedCollectZone;

    protected override void Awake()
    {
        base.Awake();

        if (!HasInputZone && _embeddedInputZone != null)
            BindInputZone(_embeddedInputZone);

        if (_submitStackRoot == null)
            _submitStackRoot = transform;
    }

    void OnDestroy()
    {
        PooledViewBridge.ReleaseAll(_cuffViews);
    }

    protected override bool CanConsume(ResourceData resource)
    {
        if (!base.CanConsume(resource))
            return false;

        ResourceData cuff = ResolveCuffResource();
        if (cuff == null || resource != cuff)
            return false;

        return ResolveCuffPrefab() != null;
    }

    protected override int GetRemainingCapacity(ResourceData resource)
    {
        return Mathf.Max(0, _maxBufferedCuff - _cuffViews.Count);
    }

    protected override void OnConsumed(ResourceData resource, int amount)
    {
        GameObject cuffPrefab = ResolveCuffPrefab();
        if (cuffPrefab == null)
            return;

        for (int i = 0; i < amount; i++)
        {
            if (_cuffViews.Count >= _maxBufferedCuff)
                break;

            SpawnCuffView(cuffPrefab, _cuffViews.Count);
        }
    }

    // 다음 단계(NPC 처리)에서 호출: 입력 버퍼에서 Cuff를 소비
    public bool TryConsumeBufferedCuff(int amount, out int consumed)
    {
        consumed = 0;
        int targetAmount = Mathf.Min(Mathf.Max(1, amount), _cuffViews.Count);

        for (int i = 0; i < targetAmount; i++)
        {
            int lastIndex = _cuffViews.Count - 1;
            PooledViewBridge.Release(_cuffViews[lastIndex]);
            _cuffViews.RemoveAt(lastIndex);
            consumed++;
        }

        return consumed > 0;
    }

    private void SpawnCuffView(GameObject cuffPrefab, int index)
    {
        Vector3 position = GetStackWorldPosition(index);
        GameObject view = PooledViewBridge.Spawn(cuffPrefab, position, Quaternion.identity, transform, true);

        if (_disableSpawnedColliders)
            DisableAllColliders(view);

        _cuffViews.Add(view);
    }

    // 1열 기준 y축 적층
    private Vector3 GetStackWorldPosition(int index)
    {
        Transform root = _submitStackRoot != null ? _submitStackRoot : transform;
        float yOffset = index * Mathf.Max(0f, _submitLayerSpacing);

        Vector3 axisX = GetHorizontalAxis(root.right, Vector3.right);
        Vector3 axisZ = GetHorizontalAxis(root.forward, Vector3.forward);

        Vector3 position = root.position;
        position += axisX * _submitLocalOffset.x;
        position += axisZ * _submitLocalOffset.z;
        position += Vector3.up * _submitLocalOffset.y;

        return position + (Vector3.up * yOffset);
    }

    private ResourceData ResolveCuffResource()
    {
        if (_cuffResource != null)
            return _cuffResource;

        return InputZone != null ? InputZone.Resource : null;
    }

    private GameObject ResolveCuffPrefab()
    {
        ResourceData cuff = ResolveCuffResource();
        return cuff != null ? cuff.WorldViewPrefab : null;
    }

    private static Vector3 GetHorizontalAxis(Vector3 sourceAxis, Vector3 fallback)
    {
        Vector3 axis = sourceAxis;
        axis.y = 0f;

        if (axis.sqrMagnitude < 0.0001f)
            axis = fallback;

        return axis.normalized;
    }

    private static void DisableAllColliders(GameObject rootObject)
    {
        Collider[] colliders = rootObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }
}
