using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }
    private Dictionary<GameObject, Queue<GameObject>> _poolMap = new();
    private Dictionary<GameObject, Transform> _rootMap = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 프리웜 : 최초 사전 생성
    public void Prewarm<T>(T prefab, int count) where T : Component
    {
        if (prefab == null || count <= 0) return;

        GameObject key = prefab.gameObject;
        CreateNewQueue(key);

        for (int i = 0; i < count; i++)
        {
            var obj = CreateNewObj(key);
            Despawn(obj);
        }
    }

    // 스폰 : 부족시 새로 생성 nor 디큐. 활성화 후 OnSpawned로 초기화.
    public T Spawn<T>(T prefab, Vector3 pos, Quaternion rot) where T : Component
    {
        if (prefab == null)
        {
            Debug.LogError("[PoolManager] Hey!! where is your prefab!?");
            return null;
        }

        GameObject key = prefab.gameObject;
        CreateNewQueue(key);

        GameObject obj = (_poolMap[key].Count > 0)
            ? _poolMap[key].Dequeue()
            : CreateNewObj(key);

        obj.transform.SetPositionAndRotation(pos, rot);
        obj.SetActive(true);

        if (obj.TryGetComponent<IPoolable>(out var poolable))
            poolable.OnSpawned();

        return obj.GetComponent<T>();
    }

    // 디스폰 : 비활성화 후 인큐
    public void Despawn(GameObject instance)
    {
        if (instance == null) return;

        if (instance.TryGetComponent<IPoolable>(out var poolable))
            poolable.OnDespawned();

        instance.SetActive(false);

        GameObject prefabKey = instance.GetComponent<PoolMember>().PrefabKey;
        if (_rootMap.TryGetValue(prefabKey, out Transform root) && root != null)
            instance.transform.SetParent(root, worldPositionStays: false);

        _poolMap[prefabKey].Enqueue(instance);
    }

    // 프리팹 전용 큐 추가
    private void CreateNewQueue(GameObject key)
    {
        if (!_poolMap.ContainsKey(key))
            _poolMap.Add(key, new Queue<GameObject>());

        if (!_rootMap.ContainsKey(key) || _rootMap[key] == null)
        {
            var root = new GameObject($"[{key.name}]");
            root.transform.SetParent(transform, false);
            _rootMap[key] = root.transform;
        }
    }

    // 풀에 새 오브젝트 추가
    private GameObject CreateNewObj(GameObject prefabKey)
    {
        var obj = Instantiate(prefabKey);
        obj.name = prefabKey.name;

        var member = obj.GetComponent<PoolMember>();
        if (member == null)
            member = obj.AddComponent<PoolMember>();

        member.Init(this, prefabKey);

        obj.SetActive(false);
        obj.transform.SetParent(_rootMap[prefabKey], worldPositionStays: false);
        return obj;
    }
}
