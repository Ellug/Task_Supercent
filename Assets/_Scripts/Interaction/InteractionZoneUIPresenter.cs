using UnityEngine;

public static class InteractionZoneUIPresenter
{
    // 타입별 수량 표시 문자열 반환
    public static string BuildAmountText(InteractionZoneType type, int storedAmount, int processedAmount, int completeAmount, int purchaseRequiredAmount)
    {
        switch (type)
        {
            case InteractionZoneType.PurchaseEquip:
                return Mathf.Max(0, Mathf.Max(1, purchaseRequiredAmount) - storedAmount).ToString();
            case InteractionZoneType.SubmitResource:
                if (completeAmount > 0)
                    return Mathf.Max(0, completeAmount - processedAmount).ToString();

                return storedAmount.ToString();
            case InteractionZoneType.CollectResource:
                if (completeAmount > 0)
                    return Mathf.Max(0, completeAmount - processedAmount).ToString();

                return storedAmount.ToString();
            default:
                return string.Empty;
        }
    }

    // 타입별 아이콘 스프라이트 반환
    public static Sprite ResolveIconSprite(InteractionZoneType type, ResourceData resource, EquipData purchaseEquip)
    {
        switch (type)
        {
            case InteractionZoneType.PurchaseEquip:
                return purchaseEquip != null ? purchaseEquip.Icon : null;
            case InteractionZoneType.SubmitResource:
            case InteractionZoneType.CollectResource:
                return resource != null ? resource.Icon : null;
            default:
                return null;
        }
    }
}
