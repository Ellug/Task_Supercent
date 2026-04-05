using UnityEngine;

// MAX 라벨 1회 생성 + 활성/비활성 런타임
public sealed class FacilityMaxLabelRuntime
{
    private readonly GameObject _instance;

    public FacilityMaxLabelRuntime(GameObject prefab, Transform parent, Vector3 localOffset)
    {
        if (prefab == null || parent == null)
            return;

        _instance = Object.Instantiate(prefab, parent);
        _instance.transform.localPosition = localOffset;
        _instance.transform.localRotation = Quaternion.identity;
        _instance.transform.localScale = Vector3.one;
        _instance.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        if (_instance == null)
            return;

        if (_instance.activeSelf == visible)
            return;

        _instance.SetActive(visible);
    }

    public void Dispose()
    {
        if (_instance != null)
            Object.Destroy(_instance);
    }
}
