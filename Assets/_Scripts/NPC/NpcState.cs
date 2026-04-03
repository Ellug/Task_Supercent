// 특정 NPC 타입에 종속된 상태의 제네릭 베이스
// Enter/Tick/Exit는 필요한 것만 서브클래스에서 override
public abstract class NpcState<TNpc> : INpcState where TNpc : NPC
{
    protected readonly TNpc Npc;

    protected NpcState(TNpc npc)
    {
        Npc = npc;
    }

    public abstract string Name { get; }

    public virtual void Enter() { }
    public virtual void Tick(float deltaTime) { }
    public virtual void Exit() { }
}
