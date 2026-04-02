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
        EquipDefinition purchaseEquip)
    {
        if (actor == null || state == null)
            return false;

        switch (type)
        {
            case InteractionZoneType.PurchaseEquip:
                return ExecutePurchase(actor, state, resource, amountPerTick, purchaseRequiredAmount, purchaseEquip);
            case InteractionZoneType.SubmitResource:
                return ExecuteSubmit(actor, state, resource, amountPerTick);
            case InteractionZoneType.CollectResource:
                return ExecuteCollect(actor, state, resource, amountPerTick);
            default:
                return false;
        }
    }

    // 비용을 틱마다 적립하고, 목표 금액 달성 후 장비 획득
    private static bool ExecutePurchase(
        IInteractionActor actor,
        InteractionZoneRuntimeState state,
        ResourceData costResource,
        int amountPerTick,
        int requiredAmount,
        EquipDefinition purchaseEquip)
    {
        int clampedRequired = Mathf.Max(1, requiredAmount);

        if (state.StoredAmount < clampedRequired)
        {
            int remaining = clampedRequired - state.StoredAmount;
            int tickAmount = Mathf.Min(Mathf.Max(1, amountPerTick), remaining);
            if (!TryDepositCost(actor, costResource, tickAmount, out int paidAmount) || paidAmount <= 0)
                return false;

            state.AddStoredAndProcessed(paidAmount);
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
        IInteractionActor actor,
        InteractionZoneRuntimeState state,
        ResourceData resource,
        int amountPerTick)
    {
        ResourceStack carryStack = actor.CarryStack;
        if (carryStack == null || resource == null)
            return false;

        int amount = Mathf.Max(1, amountPerTick);
        if (!carryStack.TryRemove(resource, amount, out int removed) || removed <= 0)
            return false;

        state.AddStoredAndProcessed(removed);
        return true;
    }

    // state에서 amountPerTick만큼 꺼내 액터 캐리 스택에 추가
    private static bool ExecuteCollect(
        IInteractionActor actor,
        InteractionZoneRuntimeState state,
        ResourceData resource,
        int amountPerTick)
    {
        ResourceStack carryStack = actor.CarryStack;
        if (carryStack == null || resource == null)
            return false;

        if (state.StoredAmount <= 0)
            return false;

        int amount = Mathf.Min(Mathf.Max(1, amountPerTick), state.StoredAmount);
        if (!carryStack.TryAdd(resource, amount, out int added) || added <= 0)
            return false;

        state.AddStored(-added);
        state.AddProcessed(added);
        return true;
    }

    // costResource가 있으면 캐리 스택에서, 없으면 소지금에서 비용 차감
    private static bool TryDepositCost(IInteractionActor actor, ResourceData costResource, int amount, out int paidAmount)
    {
        paidAmount = 0;
        int clampedAmount = Mathf.Max(1, amount);

        ResourceStack carryStack = actor.CarryStack;
        if (costResource != null && carryStack != null)
            return carryStack.TryRemove(costResource, clampedAmount, out paidAmount);

        ResourceManager resourceManager = ResourceManager.Instance;
        if (resourceManager == null)
            return false;

        if (!resourceManager.TrySpendMoney(clampedAmount))
            return false;

        paidAmount = clampedAmount;
        return true;
    }
}

