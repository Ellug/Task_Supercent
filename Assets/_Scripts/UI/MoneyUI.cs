using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class MoneyUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _moneyText;
    [SerializeField] private string _format = "{0}";

    private ResourceManager _boundManager;

    void Awake()
    {
        if (_moneyText == null)
            _moneyText = GetComponentInChildren<TMP_Text>();

        RefreshText(0);
    }

    void OnDestroy()
    {
        Unbind();
    }

    public void Bind(ResourceManager manager)
    {
        if (manager == null)
            return;

        if (_boundManager == manager)
        {
            RefreshFromManager();
            return;
        }

        Unbind();

        _boundManager = manager;
        _boundManager.MoneyChanged += OnMoneyChanged;

        RefreshFromManager();
    }

    public void Unbind()
    {
        if (_boundManager == null)
            return;

        _boundManager.MoneyChanged -= OnMoneyChanged;
        _boundManager = null;
    }

    private void OnMoneyChanged(int money)
    {
        RefreshText(money);
    }

    private void RefreshFromManager()
    {
        if (_boundManager == null)
            return;

        RefreshText(_boundManager.PlayerMoney);
    }

    private void RefreshText(int amount)
    {
        if (_moneyText == null)
            return;

        _moneyText.text = string.Format(_format, amount);
    }

}
