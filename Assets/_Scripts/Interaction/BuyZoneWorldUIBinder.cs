using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(InteractionZone))]
[AddComponentMenu("Game/Interaction/Buy Zone World UI Binder")]
public class BuyZoneWorldUIBinder : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private InteractionZone _zone;

    [Header("Inject To UI (Optional)")]
    [SerializeField] private TMP_Text _amountText;
    [SerializeField] private Image _iconImage;

    [Header("Rule")]
    [SerializeField] private bool _buyZoneOnly = true;

    void Awake()
    {
        if (_zone == null)
            _zone = GetComponent<InteractionZone>();
    }

    void OnEnable()
    {
        _zone.StateChanged += OnZoneStateChanged;

        Refresh();
    }

    void OnDisable()
    {
        _zone.StateChanged -= OnZoneStateChanged;
    }

    private void OnZoneStateChanged(InteractionZone zone)
    {
        Refresh();
    }

    // Zone 상태 기준으로 수량 텍스트·아이콘 갱신
    private void Refresh()
    {
        if (_zone == null)
            return;

        if (_buyZoneOnly && !InteractionZoneActionController.IsBuyAction(_zone.Type))
            return;

        if (_amountText != null)
        {
            _amountText.text = InteractionZoneUI.BuildAmountText(
                _zone.Type,
                _zone.StoredAmount,
                _zone.ProcessedAmount,
                _zone.CompleteAmount,
                _zone.PurchaseRequiredAmount);
        }

        if (_iconImage != null)
            _iconImage.sprite = InteractionZoneUI.ResolveIconSprite(_zone.Type, _zone.Resource, _zone.PurchaseEquip, _zone.DisplayIcon);
    }
}
