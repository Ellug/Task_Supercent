using UnityEngine;

// InteractionZone 실제 동작 규칙 컨트롤러
public static class InteractionZoneActionController
{
    public static bool IsBuyAction(InteractionZoneType type)
    {
        return type == InteractionZoneType.BuyEquip ||
               type == InteractionZoneType.BuyNpc ||
               type == InteractionZoneType.ExpandJail;
    }

    // 타입별 틱 간격 반환
    public static float GetTickInterval(InteractionZoneType type, IInteractionActor actor)
    {
        if (type == InteractionZoneType.Collect)
            return actor.CollectTickInterval;

        return actor.SubmitTickInterval;
    }

    // 타입별 틱당 처리량 반환
    public static int GetAmountPerTick(InteractionZoneType type, IInteractionActor actor, int fallbackAmountPerTick)
    {
        if (actor != null)
        {
            if (type == InteractionZoneType.Collect)
                return actor.CollectAmountPerTick;

            return actor.SubmitAmountPerTick;
        }

        return Mathf.Max(1, fallbackAmountPerTick);
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
            case InteractionZoneType.BuyEquip:
            {
                if (runtimeState.StoredAmount < purchaseRequiredAmount)
                    return false;

                if (purchaseEquip == null)
                    return true;

                return actor != null && actor.HasEquipOrBetter(purchaseEquip);
            }
            case InteractionZoneType.BuyNpc:
            case InteractionZoneType.ExpandJail:
                return runtimeState.StoredAmount >= purchaseRequiredAmount;
            case InteractionZoneType.Submit:
                return completeAmount > 0 && runtimeState.ProcessedAmount >= completeAmount;
            case InteractionZoneType.Collect:
                if (completeAmount > 0)
                    return runtimeState.ProcessedAmount >= completeAmount;

                return runtimeState.StoredAmount <= 0;
            default:
                return false;
        }
    }

    // 타입에 따라 구매/제출/수집 중 해당 처리 실행
    public static bool TryProcess(
        InteractionZoneType type,
        IInteractionActor actor,
        InteractionZoneRuntimeState state,
        ResourceData resource,
        int amountPerTick,
        int purchaseRequiredAmount,
        EquipData purchaseEquip,
        out int movedAmount)
    {
        movedAmount = 0;

        if (actor == null || state == null)
            return false;

        ResourceStack carryStack = actor.CarryStack;

        switch (type)
        {
            case InteractionZoneType.BuyEquip:
                return ExecutePurchase(actor, carryStack, state, resource, amountPerTick, purchaseRequiredAmount, purchaseEquip, out movedAmount);
            case InteractionZoneType.BuyNpc:
            case InteractionZoneType.ExpandJail:
                return ExecutePurchase(actor, carryStack, state, resource, amountPerTick, purchaseRequiredAmount, null, out movedAmount);
            case InteractionZoneType.Submit:
                if (carryStack == null || resource == null)
                    return false;
                return ExecuteSubmit(carryStack, state, resource, amountPerTick, out movedAmount);
            case InteractionZoneType.Collect:
                if (carryStack == null || resource == null)
                    return false;
                return ExecuteCollect(carryStack, state, resource, amountPerTick, out movedAmount);
            default:
                return false;
        }
    }

    // 비용을 틱마다 적립하고, 목표 금액 달성 후 장비 획득
    private static bool ExecutePurchase(
        IInteractionActor actor,
        ResourceStack carryStack,
        InteractionZoneRuntimeState state,
        ResourceData costResource,
        int amountPerTick,
        int requiredAmount,
        EquipData purchaseEquip,
        out int paidAmount)
    {
        paidAmount = 0;
        int clampedRequired = Mathf.Max(1, requiredAmount);

        if (state.StoredAmount < clampedRequired)
        {
            int remaining = clampedRequired - state.StoredAmount;
            int tickAmount = Mathf.Min(Mathf.Max(1, amountPerTick), remaining);
            if (!TryDepositCost(carryStack, costResource, tickAmount, out int paidThisTick) || paidThisTick <= 0)
                return false;

            state.AddStoredAndProcessed(paidThisTick);
            paidAmount += paidThisTick;
        }

        if (state.StoredAmount < clampedRequired)
            return true;

        if (purchaseEquip == null)
            return true;

        if (actor.HasEquipOrBetter(purchaseEquip))
            return true;

        return actor.TryAcquireEquip(purchaseEquip);
    }

    // 액터 캐리 스택에서 amountPerTick만큼 꺼내 state에 적립
    private static bool ExecuteSubmit(
        ResourceStack carryStack,
        InteractionZoneRuntimeState state,
        ResourceData resource,
        int amountPerTick,
        out int movedAmount)
    {
        movedAmount = 0;

        int amount = Mathf.Max(1, amountPerTick);
        if (!carryStack.TryRemove(resource, amount, out int removed) || removed <= 0)
            return false;

        state.AddStoredAndProcessed(removed);
        movedAmount = removed;
        return true;
    }

    // state에서 amountPerTick만큼 꺼내 액터 캐리 스택에 추가
    // Money 리소스는 한 번에 전부 수거
    private static bool ExecuteCollect(
        ResourceStack carryStack,
        InteractionZoneRuntimeState state,
        ResourceData resource,
        int amountPerTick,
        out int movedAmount)
    {
        movedAmount = 0;

        if (state.StoredAmount <= 0)
            return false;

        int amount = resource != null && resource.IsMoney
            ? state.StoredAmount
            : Mathf.Min(Mathf.Max(1, amountPerTick), state.StoredAmount);

        if (!carryStack.TryAdd(resource, amount, out int added) || added <= 0)
            return false;

        state.AddStored(-added);
        state.AddProcessed(added);
        movedAmount = added;
        return true;
    }

    // costResource를 캐리 스택에서 차감
    private static bool TryDepositCost(ResourceStack carryStack, ResourceData costResource, int amount, out int paidAmount)
    {
        paidAmount = 0;
        int clampedAmount = Mathf.Max(1, amount);

        if (costResource == null || carryStack == null)
            return false;

        return carryStack.TryRemove(costResource, clampedAmount, out paidAmount);
    }
}

