using UnityEngine;

public static class InteractionZoneUI
{
    // 타입별 수량 표시 문자열 반환
    public static string BuildAmountText(InteractionZoneType type, int storedAmount, int processedAmount, int completeAmount, int purchaseRequiredAmount)
    {
        switch (type)
        {
            case InteractionZoneType.BuyEquip:
            case InteractionZoneType.BuyNpc:
            case InteractionZoneType.ExpandJail:
                return Mathf.Max(0, Mathf.Max(1, purchaseRequiredAmount) - storedAmount).ToString();
            case InteractionZoneType.Submit:
                if (completeAmount > 0)
                    return Mathf.Max(0, completeAmount - processedAmount).ToString();

                return storedAmount.ToString();
            case InteractionZoneType.Collect:
                if (completeAmount > 0)
                    return Mathf.Max(0, completeAmount - processedAmount).ToString();

                return storedAmount.ToString();
            default:
                return string.Empty;
        }
    }

    // 타입별 아이콘 스프라이트 반환
    public static Sprite ResolveIconSprite(InteractionZoneType type, ResourceData resource, EquipData purchaseEquip, Sprite displayIcon)
    {
        // 장비 구매는 EquipData 아이콘 우선 (단계 전환 시 자동 갱신)
        if (type == InteractionZoneType.BuyEquip && purchaseEquip != null)
            return purchaseEquip.Icon;

        // 존 데이터 표시 아이콘
        if (displayIcon != null)
            return displayIcon;

        switch (type)
        {
            case InteractionZoneType.Submit:
            case InteractionZoneType.Collect:
                return resource != null ? resource.Icon : null;
            case InteractionZoneType.BuyEquip:
            case InteractionZoneType.BuyNpc:
            case InteractionZoneType.ExpandJail:
                return resource != null ? resource.Icon : null;
            default:
                return null;
        }
    }
}
