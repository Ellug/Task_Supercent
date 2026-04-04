using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class MoneyUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _moneyText;
    [SerializeField] private string _format = "{0}";
    [SerializeField] private PlayerController _player;
    [SerializeField] private ResourceData _moneyResource;

    private ResourceStack _boundStack;

    void Awake()
    {
        if (_moneyText == null)
            _moneyText = GetComponentInChildren<TMP_Text>();

        BindPlayerIfNeeded();
        RefreshText(GetCurrentMoneyAmount());
    }

    void OnEnable()
    {
        BindPlayerIfNeeded();
        RefreshText(GetCurrentMoneyAmount());
    }

    void OnDisable()
    {
        UnbindStack();
    }

    void OnDestroy()
    {
        UnbindStack();
    }

    public void Bind(PlayerController player, ResourceData moneyResource = null)
    {
        _player = player;
        if (moneyResource != null)
            _moneyResource = moneyResource;

        BindPlayerIfNeeded();
        RefreshText(GetCurrentMoneyAmount());
    }

    private void BindPlayerIfNeeded()
    {
        if (_player == null)
            _player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Exclude);

        ResourceStack nextStack = _player != null ? _player.CarryStack : null;
        if (_boundStack == nextStack)
            return;

        UnbindStack();
        _boundStack = nextStack;
        if (_boundStack != null)
        {
            _boundStack.Changed += OnCarryStackChanged;
            if (_moneyResource == null)
                _boundStack.TryGetFirstResource(IsMoneyResource, out _moneyResource);
        }
    }

    private void UnbindStack()
    {
        if (_boundStack == null)
            return;

        _boundStack.Changed -= OnCarryStackChanged;
        _boundStack = null;
    }

    private void OnCarryStackChanged(ResourceData resource, int count, int capacity)
    {
        if (_moneyResource == null)
        {
            if (resource != null && resource.IsMoney)
            {
                _moneyResource = resource;
                RefreshText(count);
            }

            return;
        }

        if (resource == _moneyResource)
            RefreshText(count);
    }

    private int GetCurrentMoneyAmount()
    {
        if (_boundStack == null || _moneyResource == null)
            return 0;

        return _boundStack.GetCount(_moneyResource);
    }

    private static bool IsMoneyResource(ResourceData resource)
    {
        return resource != null && resource.IsMoney;
    }

    private void RefreshText(int amount)
    {
        if (_moneyText == null)
            return;

        _moneyText.text = string.Format(_format, amount);
    }

}
