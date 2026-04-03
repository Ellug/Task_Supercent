using UnityEngine;

// 장비 메타데이터 ScriptableObject — 가격·사거리·쿨타임·동시 채굴 수 정의
[CreateAssetMenu(menuName = "Game/Equip Data", fileName = "EquipData")]
public class EquipData : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] private Sprite _icon;
    [SerializeField] private GameObject _playerViewPrefab;
    [SerializeField] private int _price;
    [SerializeField] private float _mineRange = 2f;
    [SerializeField] private float _mineInterval = 0.6f;
    [SerializeField] private int _simultaneousMineCount = 1;

    public string Id => _id;
    public Sprite Icon => _icon;
    public GameObject PlayerViewPrefab => _playerViewPrefab;
    public int Price => Mathf.Max(0, _price);
    public float MineRange => Mathf.Max(0f, _mineRange);
    public float MineInterval => Mathf.Max(0f, _mineInterval);
    public int SimultaneousMineCount => Mathf.Max(1, _simultaneousMineCount);
}
