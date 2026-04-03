// NPC 상태 머신의 각 상태가 구현해야 할 인터페이스
public interface INpcState
{
    string Name { get; }
    void Enter();
    void Tick(float deltaTime);
    void Exit();
}
