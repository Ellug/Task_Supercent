using System.Collections.Generic;
using UnityEngine;

// PoolManager 유무에 관계없이 스폰/반환을 통일된 인터페이스로 처리하는 정적 유틸
// Pool 있으면 재사용, 없으면 Instantiate/Destroy로 폴백
public static class PooledViewBridge
{
    // Pool에서 꺼내거나 Instantiate로 생성 후 parent에 붙여 반환
    public static GameObject Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null,
        bool worldPositionStays = true)
    {
        if (prefab == null)
            return null;

        PoolManager pool = PoolManager.Instance;
        if (pool != null)
        {
            Transform pooledTransform = pool.Spawn(prefab.transform, position, rotation);
            if (pooledTransform != null)
            {
                GameObject pooledView = pooledTransform.gameObject;
                bool keepWorldPosition = parent == null || worldPositionStays;
                pooledView.transform.SetParent(parent, keepWorldPosition);

                return pooledView;
            }
        }

        return Object.Instantiate(prefab, position, rotation, parent);
    }

    // Pool 소속이면 Despawn, 아니면 Destroy
    public static void Release(GameObject view)
    {
        if (view == null)
            return;

        if (view.TryGetComponent(out PoolMember poolMember) && poolMember.Manager != null)
        {
            poolMember.Despawn();
            return;
        }

        Object.Destroy(view);
    }

    // 리스트 내 모든 뷰 반환 후 리스트 비움
    public static void ReleaseAll(List<GameObject> views)
    {
        if (views == null)
            return;

        for (int i = 0; i < views.Count; i++)
            Release(views[i]);

        views.Clear();
    }
}
