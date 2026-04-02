using System;
using System.Collections.Generic;
using UnityEngine;

public class EquipController : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Transform _equipAnchor;
    [SerializeField] private EquipBase _startEquip;
    [SerializeField] private List<EquipBase> _equipList = new();

    private EquipBase _currentEquip;

    public EquipBase CurrentEquip => _currentEquip;

    public event Action<EquipBase> EquipChanged;

    void Awake()
    {
        if (_equipAnchor == null)
            _equipAnchor = transform;

        for (int i = 0; i < _equipList.Count; i++)
        {
            if (_equipList[i] != null)
                _equipList[i].gameObject.SetActive(false);
        }

        if (_startEquip != null)
            Equip(_startEquip);
    }

    // 현재 장비를 해제하고 nextEquip으로 교체 후 EquipChanged 발생
    public void Equip(EquipBase nextEquip)
    {
        if (_currentEquip == nextEquip)
            return;

        if (_currentEquip != null)
        {
            _currentEquip.OnUnequipped();
            _currentEquip.gameObject.SetActive(false);
        }

        _currentEquip = nextEquip;

        if (_currentEquip != null)
        {
            _currentEquip.gameObject.SetActive(true);
            _currentEquip.OnEquipped(_equipAnchor);
        }

        EquipChanged?.Invoke(_currentEquip);
    }

    // 현재 장비로 mine 채굴 시도 — EquipBase.TryExecuteMine에 위임
    public bool TryMine(Mine mine, out ResourceBase yieldPrefab, out int yieldAmount, out bool depleted)
    {
        yieldPrefab = null;
        yieldAmount = 0;
        depleted = false;

        if (_currentEquip == null)
            return false;

        return _currentEquip.TryExecuteMine(mine, transform.position, Time.time, out yieldPrefab, out yieldAmount, out depleted);
    }
}
