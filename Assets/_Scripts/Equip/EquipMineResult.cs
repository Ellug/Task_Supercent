// TryMineMulti 결과 1건을 담는 값 타입
public struct EquipMineResult
{
    public Mine Mine;
    public ResourceData YieldResource;
    public int YieldAmount;
    public bool Depleted;

    public EquipMineResult(Mine mine, ResourceData yieldResource, int yieldAmount, bool depleted)
    {
        Mine = mine;
        YieldResource = yieldResource;
        YieldAmount = yieldAmount;
        Depleted = depleted;
    }
}

