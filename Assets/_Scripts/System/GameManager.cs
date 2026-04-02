using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    [SerializeField] private MoneyUI _moneyUI;
    [SerializeField] private ResourceManager _resourceManager;

    void Start()
    {
        if (_resourceManager == null)
            _resourceManager = ResourceManager.Instance;

        BindMoneyUI();
    }

    private void BindMoneyUI()
    {
        if (_moneyUI == null || _resourceManager == null) return;

        _moneyUI.Bind(_resourceManager);
    }

}
