using UnityEngine;

public static class InteractionZoneActionProcessor
{
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
            case InteractionZoneType.PurchaseEquip:
                return ExecutePurchase(actor, carryStack, state, resource, amountPerTick, purchaseRequiredAmount, purchaseEquip, out movedAmount);
            case InteractionZoneType.SubmitResource:
                if (carryStack == null || resource == null)
                    return false;
                return ExecuteSubmit(carryStack, state, resource, amountPerTick, out movedAmount);
            case InteractionZoneType.CollectResource:
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

        int amount = Mathf.Min(Mathf.Max(1, amountPerTick), state.StoredAmount);
        if (!carryStack.TryAdd(resource, amount, out int added) || added <= 0)
            return false;

        state.AddStored(-added);
        state.AddProcessed(added);
        movedAmount = added;
        return true;
    }

    // costResource가 있으면 캐리 스택에서, 없으면 소지금에서 비용 차감
    private static bool TryDepositCost(ResourceStack carryStack, ResourceData costResource, int amount, out int paidAmount)
    {
        paidAmount = 0;
        int clampedAmount = Mathf.Max(1, amount);

        if (costResource != null)
            return carryStack != null && carryStack.TryRemove(costResource, clampedAmount, out paidAmount);

        ResourceManager resourceManager = ResourceManager.Instance;
        if (resourceManager == null)
            return false;

        if (!resourceManager.TrySpendMoney(clampedAmount))
            return false;

        paidAmount = clampedAmount;
        return true;
    }
}

