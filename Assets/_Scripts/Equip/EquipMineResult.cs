// TryMineMulti 결과 1건을 담는 값 타입
public struct EquipMineResult
{
    public Mine Mine;
    public ResourceDefinition YieldResource;
    public int YieldAmount;
    public bool Depleted;

    public EquipMineResult(Mine mine, ResourceDefinition yieldResource, int yieldAmount, bool depleted)
    {
        Mine = mine;
        YieldResource = yieldResource;
        YieldAmount = yieldAmount;
        Depleted = depleted;
    }
}
