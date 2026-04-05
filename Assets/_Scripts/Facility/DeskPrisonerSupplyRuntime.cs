using UnityEngine;

// Desk의 Prisoner Cuff 지급 조건 런타임
// - 입력 버퍼에서 지급량 차감
// - Prisoner별 지급 카운트 관리
public sealed class DeskPrisonerSupplyRuntime
{
    private readonly FacilityStackViewRuntime _inputViews;
    private readonly int _maxCuffPerPrisoner;

    private Prisoner _supplyTarget;
    private int _currentCuff;

    public DeskPrisonerSupplyRuntime(FacilityStackViewRuntime inputViews, int maxCuffPerPrisoner)
    {
        _inputViews = inputViews;
        _maxCuffPerPrisoner = Mathf.Max(1, maxCuffPerPrisoner);
    }

    public int BufferedCount => _inputViews.Count;
    public int CurrentCuff => _currentCuff;

    public bool CanConsume(ResourceData resource)
    {
        return _inputViews.MatchesResource(resource);
    }

    public void AddBuffered(int amount)
    {
        _inputViews.Add(amount);
    }

    public bool TrySupplyToPrisoner(Prisoner prisoner, int amountPerTick, out int supplied, out int currentCuff)
    {
        supplied = 0;
        currentCuff = 0;

        if (prisoner == null)
            return false;

        if (_supplyTarget != prisoner)
        {
            _supplyTarget = prisoner;
            _currentCuff = 0;
        }

        if (_currentCuff >= _maxCuffPerPrisoner)
        {
            currentCuff = _currentCuff;
            return false;
        }

        int need = _maxCuffPerPrisoner - _currentCuff;
        int request = Mathf.Min(Mathf.Max(1, amountPerTick), need);
        supplied = _inputViews.Remove(request);

        _currentCuff += supplied;
        currentCuff = _currentCuff;
        return supplied > 0;
    }

    public int GetPrisonerCuff(Prisoner prisoner)
    {
        if (prisoner == null || _supplyTarget != prisoner)
            return 0;

        return _currentCuff;
    }

    public bool IsPrisonerFilled(Prisoner prisoner)
    {
        return _supplyTarget == prisoner && _currentCuff >= _maxCuffPerPrisoner;
    }

    public void ResetPrisonerCuff(Prisoner prisoner)
    {
        if (prisoner == null)
            return;

        _supplyTarget = prisoner;
        _currentCuff = 0;
    }

    public void RemovePrisonerCuff(Prisoner prisoner)
    {
        if (prisoner == null || _supplyTarget != prisoner)
            return;

        _supplyTarget = null;
        _currentCuff = 0;
    }

    public void Dispose()
    {
        _inputViews.Dispose();
        _supplyTarget = null;
        _currentCuff = 0;
    }
}
