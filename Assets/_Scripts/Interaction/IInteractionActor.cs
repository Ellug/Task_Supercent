// 인터랙션 존과 상호작용하는 액터의 공통 인터페이스
public interface IInteractionActor
{
    ResourceStack CarryStack { get; }
    bool IsInteractionReady(float stopSpeedThreshold);
    float SubmitTickInterval { get; }
    int SubmitAmountPerTick { get; }
    float CollectTickInterval { get; }
    int CollectAmountPerTick { get; }
    bool TryAcquireEquip(EquipData equip);
    bool HasEquipOrBetter(EquipData equip);
}
