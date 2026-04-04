using UnityEngine;

public enum InteractionZoneType
{
    PurchaseEquip = 0,
    SubmitResource = 1,
    CollectResource = 2,
}

public enum InteractionZoneId
{
    None = 0,
    CollectMoney = 1,
    BuyEquip = 2,
    BuyMiner = 3,
    BuyWorker = 4,
    SubmitCuffFactory = 5,
    SubmitDesk = 6,
    CollectCuff = 7,
    BuyJail = 8,
}

public enum InteractionZoneFlowTrigger
{
    OnFirstInteraction = 0,
    OnCompleted = 1,
    OnFirstResourceAcquired = 2,
    OnJailBecameFull = 3,
}

public enum InteractionZoneTransitionOperation
{
    SetZoneEnabled = 0,
    SetGameObjectActive = 1,
    SetCompleted = 2,
    ChangeType = 3,
    ApplyLibrary = 4,
}


