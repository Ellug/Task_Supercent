using UnityEngine;

// 인터랙션 존 설정을 담는 ScriptableObject — Zone에 런타임에 ApplyLibrary로 적용 가능
[CreateAssetMenu(menuName = "Game/Interaction/Zone Library", fileName = "InteractionZoneLibrary")]
public class InteractionZoneLibrary : ScriptableObject
{
    [Header("Zone")]
    [SerializeField] private InteractionZoneType _type = InteractionZoneType.PurchaseEquip;
    [SerializeField] private bool _zoneEnabled = true;
    [SerializeField] private bool _completeOnce = true;
    [SerializeField, Min(0f)] private float _stopSpeedThreshold = 0.05f;

    [Header("Common Resource")]
    [SerializeField] private ResourceData _resource;
    [SerializeField, Min(1)] private int _amountPerTick = 1;
    [SerializeField, Min(0)] private int _completeAmount = 1;
    [SerializeField, Min(0)] private int _initialStoredAmount;

    [Header("Purchase")]
    [SerializeField] private EquipData _purchaseEquip;
    [SerializeField] private int _priceOverride = -1;  // -1이면 PurchaseEquip.Price 사용

    public InteractionZoneType Type => _type;
    public bool ZoneEnabled => _zoneEnabled;
    public bool CompleteOnce => _completeOnce;
    public float StopSpeedThreshold => Mathf.Max(0f, _stopSpeedThreshold);
    public ResourceData Resource => _resource;
    public int AmountPerTick => Mathf.Max(1, _amountPerTick);
    public int CompleteAmount => Mathf.Max(0, _completeAmount);
    public int InitialStoredAmount => Mathf.Max(0, _initialStoredAmount);
    public EquipData PurchaseEquip => _purchaseEquip;
    public int PriceOverride => _priceOverride;
}
