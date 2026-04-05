using System;
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
            throw new InvalidOperationException("[MoneyUI] _moneyText is required.");

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

    // 코드에서 플레이어·자원 교체 후 즉시 갱신
    public void Bind(PlayerController player, ResourceData moneyResource = null)
    {
        _player = player;
        if (moneyResource != null)
            _moneyResource = moneyResource;

        BindPlayerIfNeeded();
        RefreshText(GetCurrentMoneyAmount());
    }

    // 현재 _player의 CarryStack으로 이벤트 재바인딩
    private void BindPlayerIfNeeded()
    {
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

    // Changed 이벤트 구독 해제
    private void UnbindStack()
    {
        if (_boundStack == null)
            return;

        _boundStack.Changed -= OnCarryStackChanged;
        _boundStack = null;
    }

    // 스택 변경 콜백 — Money 자원일 때만 텍스트 갱신
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

    // 현재 Money 보유량 반환
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

    // _format에 amount 적용 후 텍스트 갱신
    private void RefreshText(int amount)
    {
        if (_moneyText == null)
            return;

        _moneyText.text = string.Format(_format, amount);
    }

}
