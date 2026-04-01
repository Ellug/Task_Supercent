using UnityEngine;

public class PoolMember : MonoBehaviour
{
    public PoolManager Manager { get; private set; }
    public GameObject PrefabKey { get; private set; }

    // 멤버 자신의 매니져와 프리팹키를 기억
    public void Init(PoolManager manager, GameObject prefabKey)
    {
        Manager = manager;
        PrefabKey = prefabKey;
    }

    public void Despawn()
    {
        if (Manager == null || PrefabKey == null)
        {
            Destroy(gameObject);
            return;
        }

        Manager.Despawn(gameObject);
    }
}
