using UnityEngine;

public enum ResourceCategory
{
    Generic = 0,
    Money = 1,
}

// 리소스 메타데이터 ScriptableObject — 아이콘·카테고리·월드 뷰 프리팹 정의
[CreateAssetMenu(menuName = "Game/Resource Definition", fileName = "ResourceDefinition")]
public class ResourceDefinition : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] private Sprite _icon;
    [SerializeField] private ResourceCategory _category = ResourceCategory.Generic;
    [SerializeField] private GameObject _worldViewPrefab;

    public string Id => _id;
    public Sprite Icon => _icon;
    public ResourceCategory Category => _category;
    public GameObject WorldViewPrefab => _worldViewPrefab;
    public bool IsMoney => _category == ResourceCategory.Money;
}
