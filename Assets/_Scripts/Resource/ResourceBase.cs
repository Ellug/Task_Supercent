using UnityEngine;

public abstract class ResourceBase : MonoBehaviour, IPoolable
{
    [SerializeField, Min(1)] private int _amount = 1;

    public int Amount => Mathf.Max(1, _amount);

    // amount를 1 이상으로 클램프해서 설정
    public virtual void SetAmount(int amount)
    {
        _amount = Mathf.Max(1, amount);
    }

    public virtual void OnSpawned()
    {
    }

    public virtual void OnDespawned()
    {
    }
}
