// InteractionZone의 틱/완료 판정 규칙 컨트롤러
public static class InteractionZoneRuleController
{
    // 타입별 틱 간격 반환
    public static float GetTickInterval(InteractionZoneType type, IInteractionActor actor)
    {
        if (type == InteractionZoneType.CollectResource)
            return actor.CollectTickInterval;

        return actor.SubmitTickInterval;
    }

    // 타입별 틱당 처리량 반환
    public static int GetAmountPerTick(InteractionZoneType type, IInteractionActor actor, int fallbackAmountPerTick)
    {
        if (actor != null)
        {
            if (type == InteractionZoneType.CollectResource)
                return actor.CollectAmountPerTick;

            return actor.SubmitAmountPerTick;
        }

        return UnityEngine.Mathf.Max(1, fallbackAmountPerTick);
    }

    // 타입별 완료 조건 판단
    public static bool ShouldComplete(
        bool completeOnce,
        InteractionZoneType type,
        InteractionZoneRuntimeState runtimeState,
        int purchaseRequiredAmount,
        EquipData purchaseEquip,
        int completeAmount,
        IInteractionActor actor)
    {
        if (!completeOnce)
            return false;

        switch (type)
        {
            case InteractionZoneType.PurchaseEquip:
            {
                if (runtimeState.StoredAmount < purchaseRequiredAmount)
                    return false;

                if (purchaseEquip == null)
                    return true;

                return actor != null && actor.HasEquipOrBetter(purchaseEquip);
            }
            case InteractionZoneType.SubmitResource:
                return completeAmount > 0 && runtimeState.ProcessedAmount >= completeAmount;
            case InteractionZoneType.CollectResource:
                if (completeAmount > 0)
                    return runtimeState.ProcessedAmount >= completeAmount;

                return runtimeState.StoredAmount <= 0;
            default:
                return false;
        }
    }
}
