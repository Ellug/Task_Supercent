using UnityEngine;

public abstract class EquipBase : MonoBehaviour
{
    [Header("Mine Data")]
    [SerializeField] private EquipType _equipType;
    [SerializeField] private int _mineDamage = 1;
    [SerializeField] private float _mineRange = 2f;
    [SerializeField] private float _mineInterval = 0.6f;

    [Header("Presentation")]
    [SerializeField] private EquipPresentationBase _presentation;

    private float _nextMineTime;

    public EquipType EquipType => _equipType;
    public int MineDamage => _mineDamage;
    public float MineRange => _mineRange;
    public float MineInterval => _mineInterval;

    // 장착 시 쿨타임 초기화 및 Presentation에 owner 전달
    public virtual void OnEquipped(Transform owner)
    {
        _nextMineTime = 0f;
        if (_presentation != null)
            _presentation.OnEquipped(owner, this);
    }

    // 해제 시 Presentation 비활성화
    public virtual void OnUnequipped()
    {
        if (_presentation != null)
            _presentation.OnUnequipped();
    }

    // 쿨타임·사거리 체크 후 mine을 채굴 — 소진되면 Depleted 연출도 재생
    public bool TryExecuteMine(Mine mine, Vector3 ownerPosition, float now, out ResourceBase yieldPrefab, out int yieldAmount, out bool depleted)
    {
        yieldPrefab = null;
        yieldAmount = 0;
        depleted = false;

        if (mine == null)
            return false;

        if (now < _nextMineTime)
            return false;

        float rangeSqr = _mineRange * _mineRange;
        if ((mine.transform.position - ownerPosition).sqrMagnitude > rangeSqr)
            return false;

        _nextMineTime = now + _mineInterval;

        if (_presentation != null)
            _presentation.PlayMineAction(mine.transform.position);

        depleted = mine.TryMine(_mineDamage, out yieldPrefab, out yieldAmount);

        if (depleted && _presentation != null)
            _presentation.PlayMineDepleted(mine.transform.position);

        return true;
    }
}
