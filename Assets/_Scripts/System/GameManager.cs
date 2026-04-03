using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    [SerializeField] private MoneyUI _moneyUI;
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private InteractionZoneFlowLibrary _interactionZoneFlowLibrary;

    void Start()
    {
        if (_resourceManager == null)
            _resourceManager = ResourceManager.Instance;

        BindMoneyUI();
        InteractionZoneFlowBootstrap.Apply(_interactionZoneFlowLibrary);
    }

    private void BindMoneyUI()
    {
        if (_moneyUI == null || _resourceManager == null) return;

        _moneyUI.Bind(_resourceManager);
    }

}
